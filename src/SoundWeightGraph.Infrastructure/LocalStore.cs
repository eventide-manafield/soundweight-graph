using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SoundWeightGraph.Core;

namespace SoundWeightGraph.Infrastructure;

public sealed record FeatureCache(string Key, string Version, TrackFeatures[] Features);
public sealed class LocalStore(string? root = null)
{
    private readonly string root = root ?? AppPaths.Root;
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = false };
    public string? LastWarning { get; private set; }
    public static string FileKey(string path)
    {
        var info = new FileInfo(path);
        if (!info.Exists) throw new FileNotFoundException("원본 파일이 없어요.");
        string source = $"{info.FullName.ToUpperInvariant()}|{info.Length}|{info.LastWriteTimeUtc.Ticks}|{AnalysisFormat.Version}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(source)));
    }
    public async Task<TrackFeatures[]?> LoadFeaturesAsync(string key, MediaInfo media, CancellationToken ct)
    {
        LastWarning = null;
        try
        {
            string path = Path.Combine(root, "cache", key + ".json");
            if (!File.Exists(path)) return null;
            if (new FileInfo(path).Length > 256_000_000) throw new InvalidDataException();
            await using var file = File.OpenRead(path);
            var cache = await JsonSerializer.DeserializeAsync<FeatureCache>(file, Json, ct);
            if (cache is null || cache.Key != key || cache.Version != AnalysisFormat.Version ||
                cache.Features is null || cache.Features.Any(f => f is null || !f.IsValid) ||
                !cache.Features.Select(x => x.StreamIndex).Order().SequenceEqual(media.AudioStreams.Select(x => x.StreamIndex).Order()))
                throw new InvalidDataException();
            return cache.Features;
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or JsonException or UnauthorizedAccessException or NullReferenceException)
        { LastWarning = "캐시를 읽지 못해서 다시 분석해요."; return null; }
    }
    public Task SaveFeaturesAsync(string key, TrackFeatures[] features, CancellationToken ct) =>
        WriteAsync(Path.Combine(root, "cache", key + ".json"), new FeatureCache(key, AnalysisFormat.Version, features), ct);
    public async Task<TrackSelection[]?> LoadSelectionsAsync(string key, CancellationToken ct)
    {
        try
        {
            string path = Path.Combine(root, "config", key + ".json");
            if (!File.Exists(path)) return null;
            await using var file = File.OpenRead(path);
            var selections = await JsonSerializer.DeserializeAsync<TrackSelection[]>(file, Json, ct);
            return selections is not null && selections.All(s => double.IsFinite(s.Weight) && s.Weight is >= 0 and <= 3) ? selections : null;
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        { return null; }
    }
    public Task SaveSelectionsAsync(string key, TrackSelection[] selections, CancellationToken ct) =>
        WriteAsync(Path.Combine(root, "config", key + ".json"), selections, ct);
    public void ClearCache()
    {
        string directory = Path.Combine(root, "cache");
        if (Directory.Exists(directory))
            foreach (string path in Directory.EnumerateFiles(directory, "*.json")) File.Delete(path);
    }
    private static async Task WriteAsync<T>(string path, T value, CancellationToken ct)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None, 65536, true))
                await JsonSerializer.SerializeAsync(stream, value, Json, ct);
            ct.ThrowIfCancellationRequested();
            File.Move(temp, path, true);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
}
