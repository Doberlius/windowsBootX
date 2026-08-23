using WindowsBootX.Core;
using Xunit;

namespace WindowsBootX.Core.Tests;

public class LaunchModeResolverTests
{
    [Fact]
    public void Resolve_NoArgs_ReturnsConfigurator()
    {
        var result = LaunchModeResolver.Resolve(Array.Empty<string>());
        Assert.Equal(LaunchMode.Configurator, result);
    }

    [Fact]
    public void Resolve_PlayFlag_ReturnsPlayer()
    {
        var result = LaunchModeResolver.Resolve(new[] { "--play" });
        Assert.Equal(LaunchMode.Player, result);
    }

    [Fact]
    public void Resolve_PlayFlagCaseInsensitive_ReturnsPlayer()
    {
        var result = LaunchModeResolver.Resolve(new[] { "--PLAY" });
        Assert.Equal(LaunchMode.Player, result);
    }

    [Fact]
    public void Resolve_UnrelatedArgs_ReturnsConfigurator()
    {
        var result = LaunchModeResolver.Resolve(new[] { "--foo", "bar" });
        Assert.Equal(LaunchMode.Configurator, result);
    }
}
