using SoundWeightGraph.Core;
using SoundWeightGraph.Infrastructure;
using Xunit;

namespace SoundWeightGraph.Tests;

public class IntegrationTests
{
    private static string Project
    {
        get
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "SoundWeightGraph.sln"))) directory = directory.Parent;
            return directory?.FullName ?? throw new DirectoryNotFoundException();
        }
    }
    private static string Fixture(string name) => Path.Combine(Project, "artifacts", "fixtures", "한글 공백 & 특수", name);
    private static ToolPaths Paths => new(Path.Combine(Project, "third_party", "tools", "ffmpeg.exe"), Path.Combine(Project, "third_party", "tools", "ffprobe.exe"));
    [Fact] public async Task SixTracksUseActualStreamIndicesAndRecalculateWithoutDecoding()
    {
        var media = await new MediaProbe(Paths).ProbeAsync(Fixture("OBS_6tracks.mp4"), default);
        Assert.Equal(6, media.AudioStreams.Length); Assert.Equal(1, media.AudioStreams[0].StreamIndex);
        var extractor = new AudioFeatureExtractor(Paths); var features = new List<TrackFeatures>();
        foreach (var stream in media.AudioStreams) features.Add(await extractor.ExtractAsync(media, stream, null, default));
        Assert.All(features, f => Assert.True(f.IsValid));
        Assert.True(features[4].IsEmpty); Assert.False(features[1].IsEmpty);
        var selections = media.AudioStreams.Select(s => new TrackSelection(s.StreamIndex, s.AudioOrdinal is not 1 and not 5)).ToArray();
        var calculator = new ScoreCalculator();
        var score = calculator.Calculate(features, selections, new());
        Assert.True(score.RawScore.Max() > 20);
        selections[1] = selections[1] with { Weight = 2 };
        calculator.Calculate(features, selections, new());
        Assert.Equal(6, extractor.DecodeCount);
    }
    [Fact] public async Task OffsetIsPaddedOnCommonTimeline()
    {
        var media = await new MediaProbe(Paths).ProbeAsync(Fixture("offset_streams.mp4"), default);
        Assert.True(media.AudioStreams[1].StartTime > 2.8);
        var features = await new AudioFeatureExtractor(Paths).ExtractAsync(media, media.AudioStreams[1], null, default);
        Assert.All(features.Rms.Take(5), rms => Assert.True(rms < .0001, $"Expected initial silence, got {rms}"));
        Assert.True(features.Rms[6] > .1);
        Assert.InRange(features.Rms.Length, 39, 42);
    }
    [Fact] public async Task SilentAndSteadyAudioDoNotRunAway()
    {
        foreach (string name in new[] { "silent.mp4", "steady_background.mp4" })
        {
            var media = await new MediaProbe(Paths).ProbeAsync(Fixture(name), default);
            var f = await new AudioFeatureExtractor(Paths).ExtractAsync(media, media.AudioStreams[0], null, default);
            if (name == "silent.mp4") { Assert.True(f.IsEmpty); Assert.All(f.Activity, x => Assert.Equal(0, x)); }
            else Assert.True(f.Activity.Skip(3).Take(30).Average() < .05);
        }
    }
    [Theory]
    [InlineData("corrupt.mp4")]
    [InlineData("no_audio.mp4")]
    public async Task InvalidInputIsNotSilence(string name) =>
        await Assert.ThrowsAsync<InvalidDataException>(() => new MediaProbe(Paths).ProbeAsync(Fixture(name), default));
    [Fact] public async Task PreCancelledRequestDoesNotStartDecoder()
    {
        var media = await new MediaProbe(Paths).ProbeAsync(Fixture("OBS_6tracks.mp4"), default);
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new AudioFeatureExtractor(Paths).ExtractAsync(media, media.AudioStreams[0], null, cts.Token));
    }
    [Fact] public async Task CancellationDuringFiveHourDecodeReapsProcess()
    {
        var media = await new MediaProbe(Paths).ProbeAsync(Fixture("long_5h.mp4"), default);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var extractor = new AudioFeatureExtractor(Paths);
        bool receivedProgress = false;
        var progress = new ImmediateProgress(p => { if (p > 0) { receivedProgress = true; cts.Cancel(); } });
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => extractor.ExtractAsync(media, media.AudioStreams[0], progress, cts.Token));
        Assert.True(receivedProgress);
        Assert.DoesNotContain(System.Diagnostics.Process.GetProcessesByName("ffmpeg"), p =>
        {
            try { return p.MainModule?.FileName == Paths.FFmpeg; }
            catch (InvalidOperationException) { return false; }
        });
    }
    private sealed class ImmediateProgress(Action<double> report) : IProgress<double>
    { public void Report(double value) => report(value); }
    [Fact] public async Task CacheRoundtripCorruptionAndFileChange()
    {
        string directory = Path.Combine(Project, "artifacts", "cache-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string copy = Path.Combine(directory, "recording.mp4"); File.Copy(Fixture("one_audio.mp4"), copy);
        var media = await new MediaProbe(Paths).ProbeAsync(copy, default);
        string key = LocalStore.FileKey(copy); var store = new LocalStore(directory);
        var f = await new AudioFeatureExtractor(Paths).ExtractAsync(media, media.AudioStreams[0], null, default);
        await store.SaveFeaturesAsync(key, [f], default);
        var restored = await store.LoadFeaturesAsync(key, media, default);
        Assert.NotNull(restored); Assert.Equal(f.Rms, restored[0].Rms);
        await store.SaveFeaturesAsync(key, [f with { Activity = [] }], default);
        Assert.Null(await store.LoadFeaturesAsync(key, media, default));
        await store.SaveFeaturesAsync(key, [f], default);
        await store.SaveSelectionsAsync(key, [new(f.StreamIndex, false, 2.5)], default);
        Assert.False((await store.LoadSelectionsAsync(key, default))![0].Enabled);
        await File.WriteAllTextAsync(Path.Combine(directory, "cache", key + ".json"), "{");
        Assert.Null(await store.LoadFeaturesAsync(key, media, default)); Assert.NotNull(store.LastWarning);
        File.SetLastWriteTimeUtc(copy, DateTime.UtcNow.AddMinutes(1));
        Assert.NotEqual(key, LocalStore.FileKey(copy));
        store.ClearCache(); Assert.Empty(Directory.GetFiles(Path.Combine(directory, "cache")));
    }
    [Fact] public async Task EmbeddedToolsExtractAndReuse()
    {
        var bundle = new ToolBundle(); var paths = await bundle.EnsureAsync(default);
        Assert.True(File.Exists(paths.FFmpeg)); var timestamp = File.GetLastWriteTimeUtc(paths.FFmpeg);
        Assert.Equal(paths, await bundle.EnsureAsync(default));
        Assert.Equal(timestamp, File.GetLastWriteTimeUtc(paths.FFmpeg));
    }
    [Fact] public async Task ReplacementToolsAreUsedWithoutOverwritingThem()
    {
        string directory = Path.Combine(Project, "artifacts", "replacement-tools-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        // Test path selection only: arbitrary bytes must not be executed or overwritten here.
        foreach (string name in new[] { "ffmpeg.exe", "ffprobe.exe" })
            await File.WriteAllTextAsync(Path.Combine(directory, name), "user replacement");
        var paths = await new ToolBundle(directory).EnsureAsync(default);
        Assert.Equal(Path.Combine(directory, "ffmpeg.exe"), paths.FFmpeg);
        Assert.Equal("user replacement", await File.ReadAllTextAsync(paths.FFmpeg));
        Assert.Equal("user replacement", await File.ReadAllTextAsync(paths.FFprobe));
    }
    [Fact] public async Task IncompleteReplacementDoesNotSilentlyUseBundledTools()
    {
        string directory = Path.Combine(Project, "artifacts", "replacement-tools-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(Path.Combine(directory, "ffmpeg.exe"), "user replacement");
        await Assert.ThrowsAsync<FileNotFoundException>(() => new ToolBundle(directory).EnsureAsync(default));
        await Assert.ThrowsAsync<InvalidDataException>(() => new ToolBundle("relative-tools").EnsureAsync(default));
    }
}
