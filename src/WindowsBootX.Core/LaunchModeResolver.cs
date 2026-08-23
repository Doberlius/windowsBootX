namespace WindowsBootX.Core;

public static class LaunchModeResolver
{
    public static LaunchMode Resolve(string[] args)
    {
        foreach (var arg in args)
        {
            if (string.Equals(arg, "--play", StringComparison.OrdinalIgnoreCase))
            {
                return LaunchMode.Player;
            }
        }

        return LaunchMode.Configurator;
    }
}
