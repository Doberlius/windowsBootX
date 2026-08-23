namespace WindowsBootX.Core;

public static class ContentTypeResolver
{
    public static ContentType FromFilePath(string filePath)
    {
        var extension = Path.GetExtension(filePath).ToLowerInvariant();

        return extension switch
        {
            ".mp4" or ".wmv" or ".avi" or ".mov" or ".mkv" => ContentType.Video,
            ".gif" => ContentType.Gif,
            ".png" or ".jpg" or ".jpeg" or ".bmp" => ContentType.Image,
            _ => throw new NotSupportedException($"Unsupported content file extension: {extension}")
        };
    }
}
