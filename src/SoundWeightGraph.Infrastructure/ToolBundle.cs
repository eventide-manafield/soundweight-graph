using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;

namespace SoundWeightGraph.Infrastructure;

public sealed record ToolManifest(string Version, Dictionary<string, string> Hashes);
public sealed record ToolPaths(string FFmpeg, string FFprobe);
public static class AppPaths
{
    public static string Root => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SoundWeightGraph");
}

public sealed class ToolBundle(string? overrideDirectory = null)
{
    private static readonly SemaphoreSlim Gate = new(1);
    private static readonly Assembly Assembly = typeof(ToolBundle).Assembly;
    public static string Notices
    {
        get
        {
            using var stream = Assembly.GetManifestResourceStream("Notices");
            using var reader = new StreamReader(stream ?? throw new FileNotFoundException("오픈소스 고지 누락"));
            return reader.ReadToEnd();
        }
    }
    public async Task<ToolPaths> EnsureAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        // Explicit opt-in lets users run their own LGPL-compatible replacement tools.
        // Bundled tools still use the normal integrity-checked extraction below.
        string? custom = overrideDirectory ?? Environment.GetEnvironmentVariable("SOUNDWEIGHTGRAPH_FFMPEG_DIR");
        if (!string.IsNullOrWhiteSpace(custom))
        {
            if (!Path.IsPathFullyQualified(custom))
                throw new InvalidDataException("SOUNDWEIGHTGRAPH_FFMPEG_DIR에는 절대 경로를 지정해 주세요.");
            var paths = new ToolPaths(Path.Combine(custom, "ffmpeg.exe"), Path.Combine(custom, "ffprobe.exe"));
            if (!File.Exists(paths.FFmpeg) || !File.Exists(paths.FFprobe))
                throw new FileNotFoundException("지정한 미디어 도구 폴더에 ffmpeg.exe와 ffprobe.exe가 모두 필요해요.");
            return paths;
        }
        await Gate.WaitAsync(ct);
        try
        {
            await using var manifestStream = Assembly.GetManifestResourceStream("Tools.manifest.json")
                ?? throw new FileNotFoundException("미디어 도구 리소스가 없어요. scripts/prepare-tools.ps1을 실행한 뒤 빌드해 주세요.");
            var manifest = await JsonSerializer.DeserializeAsync<ToolManifest>(manifestStream, cancellationToken: ct)
                ?? throw new InvalidDataException("도구 manifest 오류");
            string directory = Path.Combine(AppPaths.Root, "tools", manifest.Version);
            Directory.CreateDirectory(directory);
            foreach (var (name, hash) in manifest.Hashes)
            {
                if (Path.GetFileName(name) != name) throw new InvalidDataException("도구 파일명 오류");
                string target = Path.Combine(directory, name);
                if (File.Exists(target) && await HashAsync(target, ct) == hash) continue;
                string temp = target + "." + Guid.NewGuid().ToString("N") + ".tmp";
                try
                {
                    await using (var source = Assembly.GetManifestResourceStream("Tools." + name)
                        ?? throw new FileNotFoundException(name))
                    await using (var output = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None, 65536, true))
                        await source.CopyToAsync(output, ct);
                    if (await HashAsync(temp, ct) != hash) throw new InvalidDataException("도구 무결성 검증 실패");
                    File.Move(temp, target, true);
                }
                finally { if (File.Exists(temp)) File.Delete(temp); }
            }
            return new(Path.Combine(directory, "ffmpeg.exe"), Path.Combine(directory, "ffprobe.exe"));
        }
        finally { Gate.Release(); }
    }
    private static async Task<string> HashAsync(string path, CancellationToken ct)
    {
        await using var stream = File.OpenRead(path);
        return Convert.ToHexString(await SHA256.HashDataAsync(stream, ct));
    }
}
