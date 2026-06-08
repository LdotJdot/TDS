using System;
using System.IO;
using System.Threading.Tasks;

namespace TDS.Screenshot;

internal static class ScreenshotFileSaver
{
    internal const string FileNamePrefix = "snapshot";

    internal static string ResolveDirectory(string? configured, string fallbackDirectory)
    {
        if (string.IsNullOrWhiteSpace(configured))
            return fallbackDirectory;
        return configured.Trim().TrimEnd('\\', '/');
    }

    internal static string BuildFileName()
        => $"{FileNamePrefix}_{DateTime.Now:yyyyMMdd_HHmmss_fff}.png";

    internal static string BuildUniquePath(string directory)
    {
        var stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");
        var path = Path.Combine(directory, $"{FileNamePrefix}_{stamp}.png");
        if (!File.Exists(path))
            return path;

        for (var i = 1; i < 1000; i++)
        {
            path = Path.Combine(directory, $"{FileNamePrefix}_{stamp}_{i}.png");
            if (!File.Exists(path))
                return path;
        }

        return Path.Combine(directory, $"{FileNamePrefix}_{stamp}_{Guid.NewGuid():N}.png");
    }

    internal static async Task<(bool Ok, string? Path, string? Error)> SavePngAsync(
        byte[] pngBytes, string? configuredDirectory, string fallbackDirectory)
    {
        var directory = ResolveDirectory(configuredDirectory, fallbackDirectory);
        try
        {
            if (!Directory.Exists(directory))
            {
                try
                {
                    Directory.CreateDirectory(directory);
                }
                catch (Exception ex)
                {
                    return (false, null, $"Failed to create directory: {directory}\r\n{ex.Message}");
                }
            }

            _ = Directory.GetFiles(directory);
            var path = BuildUniquePath(directory);
            await using var fs = new FileStream(
                path, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
            await fs.WriteAsync(pngBytes);
            await fs.FlushAsync();
            return (true, path, null);
        }
        catch (UnauthorizedAccessException ex)
        {
            return (false, null, $"Access denied: {directory}\r\n{ex.Message}");
        }
        catch (IOException ex) when (ex is not DirectoryNotFoundException)
        {
            return (false, null, $"Failed to write file under {directory}\r\n{ex.Message}");
        }
        catch (Exception ex)
        {
            return (false, null, ex.Message);
        }
    }
}
