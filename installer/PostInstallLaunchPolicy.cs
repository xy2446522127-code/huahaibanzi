public static class PostInstallLaunchPolicy
{
    public static bool ShouldLaunch(bool noLaunch, bool restartRequired)
    {
        return !noLaunch && !restartRequired;
    }

    public static string ArgumentsFor(bool silent)
    {
        return silent ? "--background" : null;
    }
}
