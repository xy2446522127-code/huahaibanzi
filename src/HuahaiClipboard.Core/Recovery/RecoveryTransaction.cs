namespace HuahaiClipboard.Core.Recovery;

public enum RecoveryTransactionState
{
    Activated,
    RolledBack
}

public sealed record RecoveryTransactionResult(
    RecoveryTransactionState State,
    string? PreservedPreviousRoot,
    string PreservedCandidateRoot);

/// <summary>
/// Activates a prepared recovery candidate only after it passes validation.
/// Both directories must be siblings so each activation step is a rename on
/// one volume; no live data is deleted by this transaction.
/// </summary>
public sealed class RecoveryTransaction
{
    public async Task<RecoveryTransactionResult> ApplyAsync(
        string candidateRoot,
        string destinationRoot,
        Func<string, Task> validateCandidateAsync,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(validateCandidateAsync);

        var candidate = NormalizeExistingDirectory(candidateRoot, nameof(candidateRoot));
        var destination = NormalizeDirectory(destinationRoot, nameof(destinationRoot));
        EnsureSeparateSiblingDirectories(candidate, destination);

        var parent = Directory.GetParent(destination)?.FullName
            ?? throw new ArgumentException("Destination must have a parent directory.", nameof(destinationRoot));
        Directory.CreateDirectory(parent);

        var previousRoot = CreatePreservedPath(parent, "previous");
        var failedCandidateRoot = CreatePreservedPath(parent, "candidate");
        var destinationExisted = Directory.Exists(destination);
        var previousMoved = false;
        var candidateActivated = false;

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (destinationExisted)
            {
                RejectReparsePoint(destination);
                Directory.Move(destination, previousRoot);
                previousMoved = true;
            }

            Directory.Move(candidate, destination);
            candidateActivated = true;
            await validateCandidateAsync(destination);
            cancellationToken.ThrowIfCancellationRequested();

            return new RecoveryTransactionResult(
                RecoveryTransactionState.Activated,
                previousMoved ? previousRoot : null,
                destination);
        }
        catch (OperationCanceledException)
        {
            RollBack(destination, previousRoot, failedCandidateRoot, previousMoved, candidateActivated);
            throw;
        }
        catch
        {
            RollBack(destination, previousRoot, failedCandidateRoot, previousMoved, candidateActivated);
            return new RecoveryTransactionResult(
                RecoveryTransactionState.RolledBack,
                previousMoved ? destination : null,
                candidateActivated ? failedCandidateRoot : candidate);
        }
    }

    private static void RollBack(
        string destination,
        string previousRoot,
        string failedCandidateRoot,
        bool previousMoved,
        bool candidateActivated)
    {
        try
        {
            if (candidateActivated && Directory.Exists(destination))
            {
                Directory.Move(destination, failedCandidateRoot);
            }

            if (previousMoved && Directory.Exists(previousRoot))
            {
                Directory.Move(previousRoot, destination);
            }
        }
        catch (Exception rollbackException)
        {
            throw new IOException("Recovery activation failed and rollback could not preserve the previous data.", rollbackException);
        }
    }

    private static string NormalizeExistingDirectory(string path, string parameterName)
    {
        var normalized = NormalizeDirectory(path, parameterName);
        if (!Directory.Exists(normalized)) throw new DirectoryNotFoundException(normalized);
        RejectReparsePoint(normalized);
        return normalized;
    }

    private static string NormalizeDirectory(string path, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Directory path is required.", parameterName);

        var fullPath = Path.GetFullPath(path);
        var root = Path.GetPathRoot(fullPath);
        if (string.Equals(fullPath, root, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("A filesystem root cannot be used for recovery.", parameterName);
        }

        return fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    private static void EnsureSeparateSiblingDirectories(string candidate, string destination)
    {
        if (string.Equals(candidate, destination, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Candidate and destination must be different directories.");
        }

        var candidateParent = Directory.GetParent(candidate)?.FullName;
        var destinationParent = Directory.GetParent(destination)?.FullName;
        if (!string.Equals(candidateParent, destinationParent, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Candidate and destination must be sibling directories on the same volume.");
        }
    }

    private static string CreatePreservedPath(string parent, string kind) =>
        Path.Combine(parent, $".{kind}-recovery-{Guid.NewGuid():N}");

    private static void RejectReparsePoint(string path)
    {
        if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
        {
            throw new IOException("Recovery transaction does not support linked directories.");
        }
    }
}
