public static class PostInstallLaunchPolicy
{
    public static string ArgumentsFor(bool silent)
    {
        return silent ? "--background" : null;
    }
}
