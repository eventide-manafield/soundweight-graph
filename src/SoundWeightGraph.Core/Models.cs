namespace SoundWeightGraph.Core;

public static class AnalysisFormat
{
    public const int SampleRate = 16000;
    public const double FrameSeconds = 0.5;
    public const string Version = "features-1-pts16k-activity1";
    public const double SilenceDb = -65;
}

public enum TrackKind { Unknown, Independent, Mix }
public sealed record AudioStreamInfo(int StreamIndex, int AudioOrdinal, string Codec, int Channels,
    double StartTime, double Duration, string DisplayName);
public sealed record MediaInfo(string FilePath, double DurationSec, double TimelineOriginSec,
    AudioStreamInfo[] AudioStreams);
public sealed record TrackSelection(int StreamIndex, bool Enabled = true, double Weight = 1,
    TrackKind Kind = TrackKind.Unknown);
public sealed record TrackFeatures(int StreamIndex, double FrameDurationSec, double StartTimeSec,
    double[] Rms, double[] Peak, double[] DbFS, int[] SampleCounts, double[] Activity)
{
    public bool IsEmpty => Peak.Length == 0 || Peak.Max() < Math.Pow(10, AnalysisFormat.SilenceDb / 20);
    public double ActiveRatio => DbFS.Length == 0 ? 0 : (double)DbFS.Count(x => x > AnalysisFormat.SilenceDb) / DbFS.Length;
    public bool IsValid => FrameDurationSec == AnalysisFormat.FrameSeconds && StartTimeSec == 0 &&
        Rms.Length == Peak.Length && Rms.Length == DbFS.Length && Rms.Length == SampleCounts.Length &&
        Rms.Length == Activity.Length && Rms.All(x => double.IsFinite(x) && x >= 0) &&
        Peak.All(x => double.IsFinite(x) && x >= 0) && DbFS.All(double.IsFinite) &&
        Activity.All(x => double.IsFinite(x) && x is >= 0 and <= 1) &&
        SampleCounts.All(x => x is > 0 and <= 8000);
}
public sealed record ScoringOptions(double CoincidenceWindowSec = 3, double SmoothingSec = 1.5);
public sealed record ScoreSeries(double FrameDurationSec, double[] BaseScore, double[] Coincidence,
    double[] RawScore, double[] SmoothedScore)
{
    // Smoothed weighted shares of BaseScore, excluding the shared coincidence bonus.
    public IReadOnlyDictionary<int, double[]> TrackContributions { get; init; } = new Dictionary<int, double[]>();
    public bool HasTracks => RawScore.Length > 0;
    public static ScoreSeries Empty { get; } = new(AnalysisFormat.FrameSeconds, [], [], [], []);
}
public interface IMediaProbe { Task<MediaInfo> ProbeAsync(string path, CancellationToken ct); }
public interface IAudioFeatureExtractor
{
    Task<TrackFeatures> ExtractAsync(MediaInfo media, AudioStreamInfo stream,
        IProgress<double>? progress, CancellationToken ct);
}
public interface IScoreCalculator
{
    ScoreSeries Calculate(IReadOnlyList<TrackFeatures> features, IReadOnlyList<TrackSelection> selections,
        ScoringOptions options, CancellationToken ct = default);
}
