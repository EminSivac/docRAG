using DocRag.Shared;

namespace DocRag.Server.Ingestion;

public sealed class FileCrawler
{
    public IEnumerable<string> EnumerateFiles(IEnumerable<string> roots, ILogger? logger = null)
    {
        foreach (var root in roots)
        {
            if (!Directory.Exists(root))
            {
                logger?.LogWarning("Dokumentordner existiert nicht: {Root}", root);
                continue;
            }

            foreach (var file in EnumerateDirectory(root, logger))
            {
                yield return file;
            }
        }
    }

    private static IEnumerable<string> EnumerateDirectory(string directory, ILogger? logger)
    {
        IEnumerable<string> files = Array.Empty<string>();
        try
        {
            files = Directory.EnumerateFiles(directory)
                .Where(f => AppConfig.SupportedExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()));
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            logger?.LogWarning("Ordner konnte nicht gelesen werden, wird übersprungen: {Directory} ({Message})", directory, ex.Message);
        }

        foreach (var file in files)
        {
            yield return file;
        }

        IEnumerable<string> subDirectories = Array.Empty<string>();
        try
        {
            subDirectories = Directory.EnumerateDirectories(directory);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            logger?.LogWarning("Unterordner konnten nicht gelesen werden: {Directory} ({Message})", directory, ex.Message);
        }

        foreach (var subDirectory in subDirectories)
        {
            foreach (var file in EnumerateDirectory(subDirectory, logger))
            {
                yield return file;
            }
        }
    }
}
