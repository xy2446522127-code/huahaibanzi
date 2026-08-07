using System;

internal sealed class InstallSwapResult
{
    public InstallSwapResult(bool backupCleanupPending)
    {
        BackupCleanupPending = backupCleanupPending;
    }

    public bool BackupCleanupPending { get; private set; }
}

// Keeps activation/rollback atomic. Cleanup after the commit point is deliberately
// outside the rollback path so a locked backup can never corrupt the active version.
internal static class InstallSwapTransaction
{
    public static InstallSwapResult Execute(
        string stagingRoot,
        string installRoot,
        string backupRoot,
        Func<string, bool> directoryExists,
        Action<string, string> moveDirectory,
        Func<string, bool> tryDeleteDirectory,
        Action activateCandidate)
    {
        if (directoryExists == null) throw new ArgumentNullException("directoryExists");
        if (moveDirectory == null) throw new ArgumentNullException("moveDirectory");
        if (tryDeleteDirectory == null) throw new ArgumentNullException("tryDeleteDirectory");
        if (activateCandidate == null) throw new ArgumentNullException("activateCandidate");

        bool previousMoved = false;
        bool candidateMoved = false;

        try
        {
            if (directoryExists(installRoot))
            {
                moveDirectory(installRoot, backupRoot);
                previousMoved = true;
            }

            moveDirectory(stagingRoot, installRoot);
            candidateMoved = true;
            activateCandidate();
        }
        catch (Exception activationError)
        {
            if (candidateMoved && directoryExists(installRoot))
            {
                bool candidateRemoved;
                try
                {
                    candidateRemoved = tryDeleteDirectory(installRoot) && !directoryExists(installRoot);
                }
                catch (Exception cleanupError)
                {
                    throw new InvalidOperationException(
                        "Installation rollback could not remove the candidate version; the candidate and complete old-version backup were preserved.",
                        new AggregateException(activationError, cleanupError));
                }

                if (!candidateRemoved)
                {
                    throw new InvalidOperationException(
                        "Installation rollback could not remove the candidate version; the candidate and complete old-version backup were preserved.",
                        activationError);
                }
            }

            if (previousMoved)
            {
                if (!directoryExists(backupRoot))
                {
                    throw new InvalidOperationException(
                        "Installation rollback could not find the complete old-version backup.",
                        activationError);
                }

                try
                {
                    moveDirectory(backupRoot, installRoot);
                }
                catch (Exception restoreError)
                {
                    throw new InvalidOperationException(
                        "Installation rollback could not restore the complete old version; the backup was preserved.",
                        new AggregateException(activationError, restoreError));
                }
            }

            throw;
        }

        // Commit point: owner marker, shortcuts and uninstall registration succeeded.
        // Backup cleanup is best-effort and can no longer trigger rollback.
        bool backupCleanupPending = false;
        if (previousMoved && directoryExists(backupRoot))
        {
            try
            {
                backupCleanupPending = !tryDeleteDirectory(backupRoot) || directoryExists(backupRoot);
            }
            catch
            {
                backupCleanupPending = true;
            }
        }

        return new InstallSwapResult(backupCleanupPending);
    }
}
