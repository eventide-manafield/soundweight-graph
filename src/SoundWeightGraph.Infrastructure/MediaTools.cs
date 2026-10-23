using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using SoundWeightGraph.Core;

namespace SoundWeightGraph.Infrastructure;

public static class ProcessRunner
{
    public static Process Start(string executable, IEnumerable<string> arguments)
    {
        var info = new ProcessStartInfo(executable)
        {
            UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true
        };
        foreach (string argument in arguments) info.ArgumentList.Add(argument);
        return Process.Start(info) ?? throw new IOException("미디어 도구를 실행할 수 없어요.");
    }
    public static void Kill(Process p)
    {
        try { if (!p.HasExited) p.Kill(true); }
        catch (InvalidOperationException) { }
        catch (System.ComponentModel.Win32Exception) { }
    }
    public static async Task<string> ReadBoundedAsync(StreamReader reader, int limit, CancellationToken ct)
    {
        var text = new StringBuilder();
        char[] buffer = new char[4096];
        int n;
        while ((n = await reader.ReadAsync(buffer.AsMemory(), ct)) != 0)
            if (text.Length < limit) text.Append(buffer, 0, Math.Min(n, limit - text.Length));
        return text.ToString();
    }
}

public sealed class MediaProbe(ToolPaths tools) : IMediaProbe
{
    public async Task<MediaInfo> ProbeAsync(string path, CancellationToken ct)
    {
        if (!Path.GetExtension(path).Equals(".mp4", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("MP4 파일을 선택해 주세요.");
        path = Path.GetFullPath(path);
        using (File.OpenRead(path)) { }
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));
        using var p = ProcessRunner.Start(tools.FFprobe,
            ["-v", "error", "-protocol_whitelist", "file,pipe", "-f", "mov", "-show_format", "-show_streams", "-of", "json", path]);
        using var registration = timeout.Token.Register(() => ProcessRunner.Kill(p));
        Task<string> error = ProcessRunner.ReadBoundedAsync(p.StandardError, 8192, timeout.Token);
        try
        {
            string json = await ProcessRunner.ReadBoundedAsync(p.StandardOutput, 4_000_000, timeout.Token);
            await p.WaitForExitAsync(timeout.Token);
            _ = await error;
            if (p.ExitCode != 0) throw new InvalidDataException("MP4를 읽지 못했어요. 파일 손상 또는 지원하지 않는 형식일 수 있어요.");
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var streams = new List<AudioStreamInfo>();
            double origin = 0, duration = 0;
            if (root.TryGetProperty("format", out var format))
            { origin = Number(format, "start_time"); duration = Number(format, "duration"); }
            foreach (var s in root.GetProperty("streams").EnumerateArray())
            {
                if (Text(s, "codec_type") != "audio") continue;
                if (streams.Count >= 64) throw new InvalidDataException("오디오 트랙은 최대 64개까지 지원해요.");
                int ordinal = streams.Count + 1;
                string title = s.TryGetProperty("tags", out var tags) ? Text(tags, "title") : "";
                streams.Add(new(s.GetProperty("index").GetInt32(), ordinal, Text(s, "codec_name"),
                    s.TryGetProperty("channels", out var channels) ? channels.GetInt32() : 0,
                    Number(s, "start_time"), Number(s, "duration"),
                    $"오디오 {ordinal}" + (title.Length == 0 ? "" : $" · {title}")));
            }
            if (streams.Count == 0) throw new InvalidDataException("오디오 트랙이 없는 MP4예요.");
            duration = Math.Max(duration, streams.Max(s => s.StartTime - origin + s.Duration));
            if (duration > 7 * 86400) throw new InvalidDataException("7일을 넘는 녹화는 지원하지 않아요.");
            return new(path, Math.Max(0, duration), origin, streams.ToArray());
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        { throw new TimeoutException("파일 조사 시간이 초과됐어요."); }
        finally
        {
            ProcessRunner.Kill(p);
            await p.WaitForExitAsync(CancellationToken.None);
            try { await error; } catch (OperationCanceledException) { }
        }
    }
    private static double Number(JsonElement e, string key) => e.TryGetProperty(key, out var v) &&
        double.TryParse(v.ToString(), CultureInfo.InvariantCulture, out double n) && double.IsFinite(n) ? n : 0;
    private static string Text(JsonElement e, string key) => e.TryGetProperty(key, out var v) ? v.ToString() : "";
}

public sealed class AudioFeatureExtractor(ToolPaths tools) : IAudioFeatureExtractor
{
    public int DecodeCount { get; private set; }
    public async Task<TrackFeatures> ExtractAsync(MediaInfo media, AudioStreamInfo stream,
        IProgress<double>? progress, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        DecodeCount++;
        string origin = media.TimelineOriginSec.ToString("R", CultureInfo.InvariantCulture);
        // Preserve input timestamps, subtract common container origin, then insert/drop samples.
        // first_pts=0 pads initial offsets; async handles timestamp gaps during a stream.
        string filter = $"asetpts=PTS-({origin})/TB,aresample=16000:async=1:first_pts=0";
        using var p = ProcessRunner.Start(tools.FFmpeg,
            ["-nostdin", "-v", "error", "-xerror", "-copyts", "-protocol_whitelist", "file,pipe", "-f", "mov",
             "-i", media.FilePath, "-map", $"0:{stream.StreamIndex}", "-vn", "-sn", "-dn",
             "-af", filter, "-ac", "1", "-ar", "16000", "-f", "f32le", "pipe:1"]);
        using var registration = ct.Register(() => ProcessRunner.Kill(p));
        Task<string> stderr = ProcessRunner.ReadBoundedAsync(p.StandardError, 8192, CancellationToken.None);
        var accumulator = new FeatureAccumulator();
        byte[] buffer = new byte[65536 + 4];
        int carry = 0;
        var throttle = Stopwatch.StartNew();
        try
        {
            while (true)
            {
                using var stall = CancellationTokenSource.CreateLinkedTokenSource(ct);
                stall.CancelAfter(TimeSpan.FromSeconds(30));
                int read;
                try { read = await p.StandardOutput.BaseStream.ReadAsync(buffer.AsMemory(carry, 65536), stall.Token); }
                catch (OperationCanceledException) when (!ct.IsCancellationRequested)
                { throw new TimeoutException("오디오 디코딩 응답이 30초 동안 없어요."); }
                if (read == 0) break;
                int bytes = carry + read, whole = bytes / 4 * 4;
                Consume(accumulator, buffer, whole);
                carry = bytes - whole;
                if (carry > 0) Array.Copy(buffer, whole, buffer, 0, carry);
                if (accumulator.Samples > 7L * 86400 * AnalysisFormat.SampleRate)
                    throw new InvalidDataException("오디오가 최대 분석 길이를 초과했어요.");
                if (throttle.ElapsedMilliseconds >= 100)
                {
                    progress?.Report(media.DurationSec > 0 ? Math.Clamp(accumulator.Samples / 16000.0 / media.DurationSec, 0, 1) : 0);
                    throttle.Restart();
                }
            }
            await p.WaitForExitAsync(ct);
            _ = await stderr;
            ct.ThrowIfCancellationRequested();
            if (p.ExitCode != 0 || carry != 0 || accumulator.Samples == 0)
                throw new InvalidDataException("오디오 디코딩에 실패했어요. 지원하지 않는 코덱 또는 손상된 트랙일 수 있어요.");
            progress?.Report(1);
            return accumulator.Finish(stream.StreamIndex, ct);
        }
        finally { ProcessRunner.Kill(p); await p.WaitForExitAsync(CancellationToken.None); await stderr; }
    }
    private static void Consume(FeatureAccumulator accumulator, byte[] bytes, int length) =>
        accumulator.Add(MemoryMarshal.Cast<byte, float>(bytes.AsSpan(0, length)));
}
