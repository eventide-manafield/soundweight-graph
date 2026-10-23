namespace SoundWeightGraph.Infrastructure;

public static class DiagnosticLog
{
    // Do not log filenames, paths, stderr, or audio content.
    public static void Error(string stage, Exception error)
    {
        try
        {
            string directory = Path.Combine(AppPaths.Root, "logs"); Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, "errors.log");
            if (File.Exists(path) && new FileInfo(path).Length > 1_000_000) File.Move(path, path + ".previous", true);
            File.AppendAllText(path, $"{DateTimeOffset.UtcNow:O}\t{stage}\t{error.GetType().Name}\n");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }
}
