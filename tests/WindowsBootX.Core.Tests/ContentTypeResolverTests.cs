using WindowsBootX.Core;
using Xunit;

namespace WindowsBootX.Core.Tests;

public class ContentTypeResolverTests
{
    [Theory]
    [InlineData("clip.mp4", ContentType.Video)]
    [InlineData("clip.MOV", ContentType.Video)]
    [InlineData("clip.mkv", ContentType.Video)]
    [InlineData("clip.wmv", ContentType.Video)]
    [InlineData("clip.avi", ContentType.Video)]
    [InlineData("anim.gif", ContentType.Gif)]
    [InlineData("frame.png", ContentType.Image)]
    [InlineData("frame.jpg", ContentType.Image)]
    [InlineData("frame.jpeg", ContentType.Image)]
    [InlineData("frame.bmp", ContentType.Image)]
    public void FromFilePath_KnownExtensions_ReturnsExpectedType(string fileName, ContentType expected)
    {
        var result = ContentTypeResolver.FromFilePath(fileName);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void FromFilePath_UnknownExtension_Throws()
    {
        Assert.Throws<NotSupportedException>(() => ContentTypeResolver.FromFilePath("file.txt"));
    }
}
