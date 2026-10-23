using System.Collections.ObjectModel;
using System.IO;
using SoundWeightGraph.Core;
using SoundWeightGraph.Infrastructure;

namespace SoundWeightGraph.App;

public sealed class MainViewModel : Observable
{
    private readonly ToolBundle bundle = new();
    private readonly LocalStore store = new();
    private readonly ScoreCalculator calculator = new();
    private CancellationTokenSource? operationCts, scoreCts;
    private Task operation = Task.CompletedTask, calculation = Task.CompletedTask;
    private long generation, scoreGeneration;
    private MediaInfo? media;
    private string? key;
    private TrackFeatures[] features = [];
    private ToolPaths? tools;
    private bool busy, isAnalyzing;
    private double progress;
    private string title = "녹화 속 반응이 모이는 순간을 찾아봐";
    private string subtitle = "MP4 파일을 선택하거나 이 창에 끌어다 놓아 주세요.";
    private string status = "파일을 기다리는 중";
    private string warning = "";
    private string cursor = "그래프 위에서 시각과 점수를 확인할 수 있어요.";
    private string selectedTime = "";
    public ObservableCollection<TrackViewModel> Tracks { get; } = [];
    public ScoreSeries Series { get; private set; } = ScoreSeries.Empty;
    public event Action<bool>? SeriesChanged;
    public int DecodeCount { get; private set; }
    public double Duration => Math.Max(media?.DurationSec ?? 0, features.Length == 0 ? 0 : features.Max(f => f.SampleCounts.Sum(n => (long)n) / 16000.0));
    public bool Busy { get => busy; private set { Set(ref busy, value); AnalyzeCommand.Refresh(); CancelCommand.Refresh(); ClearCacheCommand.Refresh(); } }
    public bool IsAnalyzing { get => isAnalyzing; private set => Set(ref isAnalyzing, value); }
    public double Progress { get => progress; private set => Set(ref progress, value); }
    public string Title { get => title; private set => Set(ref title, value); }
    public string Subtitle { get => subtitle; private set => Set(ref subtitle, value); }
    public string Status { get => status; private set => Set(ref status, value); }
    public string Warning { get => warning; private set => Set(ref warning, value); }
    public string Cursor { get => cursor; set => Set(ref cursor, value); }
    public string SelectedTime { get => selectedTime; set => Set(ref selectedTime, value); }
    public RelayCommand AnalyzeCommand { get; }
    public RelayCommand CancelCommand { get; }
    public RelayCommand ClearCacheCommand { get; }
    public MainViewModel()
    {
        AnalyzeCommand = new(() => _ = AnalyzeAsync(), () => !Busy && media is not null);
        CancelCommand = new(Cancel, () => Busy);
        ClearCacheCommand = new(() =>
        {
            try { store.ClearCache(); Status = "저장된 특징 캐시를 지웠어요. 현재 그래프는 유지돼요."; }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { Warning = "캐시를 지우지 못했어요. 접근 권한을 확인해 주세요."; }
        }, () => !Busy);
    }
    public Task OpenAsync(string path) => StartOperation(async (ct, gen) =>
    {
        media = null; key = null; features = []; Tracks.Clear(); SelectedTime = "";
        Series = ScoreSeries.Empty; SeriesChanged?.Invoke(true);
        Title = Path.GetFileName(path); Subtitle = "파일 조사 중"; Status = "내장 미디어 도구 확인 중";
        tools = await Task.Run(() => bundle.EnsureAsync(ct), ct);
        Status = "MP4 오디오 트랙 조사 중";
        var result = await new MediaProbe(tools).ProbeAsync(path, ct);
        ct.ThrowIfCancellationRequested();
        media = result; key = LocalStore.FileKey(path);
        var saved = await store.LoadSelectionsAsync(key, ct);
        ct.ThrowIfCancellationRequested();
        foreach (var stream in media.AudioStreams)
        {
            var track = new TrackViewModel(stream)
            { MixHint = stream.AudioOrdinal == 1 && media.AudioStreams.Length > 1 ? "믹스 가능성 있음 · 전체 믹스라면 사용 해제 권장" : "" };
            var selection = saved?.FirstOrDefault(s => s.StreamIndex == stream.StreamIndex);
            if (selection is not null) track.Restore(selection);
            track.SelectionChanged += QueueRecalculation;
            track.HighlightChanged += () => SeriesChanged?.Invoke(false);
            Tracks.Add(track);
        }
        Subtitle = $"{FormatTime(Duration)} · 오디오 {Tracks.Count}개 · 로컬 분석";
        Status = "트랙 설정을 확인한 뒤 ‘분석 시작’을 눌러 주세요.";
    });
    public Task AnalyzeAsync()
    {
        if (media is null || tools is null || Busy) return Task.CompletedTask;
        MediaInfo target = media; ToolPaths paths = tools; string targetKey = key!;
        return StartOperation(async (ct, gen) =>
        {
            Status = "캐시 확인 중";
            var cached = await Task.Run(() => store.LoadFeaturesAsync(targetKey, target, ct), ct);
            ct.ThrowIfCancellationRequested();
            if (store.LastWarning is not null) Warning = store.LastWarning;
            var collected = new List<TrackFeatures>();
            if (cached is not null) collected.AddRange(cached);
            else
            {
                var extractor = new AudioFeatureExtractor(paths);
                for (int i = 0; i < target.AudioStreams.Length; i++)
                {
                    int ordinal = i;
                    var track = Tracks[i];
                    Status = $"오디오 {i + 1}/{Tracks.Count} 분석 중"; track.Status = "분석 중";
                    var reporter = new Progress<double>(p =>
                    { if (gen == generation && !ct.IsCancellationRequested) Progress = 100 * (ordinal + p) / target.AudioStreams.Length; });
                    try
                    {
                        DecodeCount++;
                        var f = await Task.Run(() => extractor.ExtractAsync(target, track.Stream, reporter, ct), ct);
                        ct.ThrowIfCancellationRequested(); collected.Add(f); SetTrackResult(track, f);
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    { DiagnosticLog.Error("decode", ex); track.Status = "분석 실패 · 점수에서 제외"; Warning = ex.Message; }
                }
            }
            ct.ThrowIfCancellationRequested();
            if (LocalStore.FileKey(target.FilePath) != targetKey) throw new IOException("분석 중 원본 파일이 변경됐어요. 다시 열어 주세요.");
            features = collected.ToArray();
            foreach (var f in features) SetTrackResult(Tracks.First(t => t.Stream.StreamIndex == f.StreamIndex), f);
            if (cached is null && features.Length == Tracks.Count)
            {
                try { await Task.Run(() => store.SaveFeaturesAsync(targetKey, features, ct), ct); }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                { Warning = "캐시를 저장하지 못했지만 분석 결과는 사용할 수 있어요."; }
            }
            Progress = 100;
            Subtitle = $"{FormatTime(Duration)} · 오디오 {Tracks.Count}개 · 로컬 분석";
            Status = cached is not null ? "캐시에서 불러왔어요 · 디코딩 없이 재계산 가능" : $"분석 완료 {features.Length}/{Tracks.Count} · 가중치 변경은 재디코딩 없이 반영돼요";
            await RecalculateAsync(true);
        }, isAnalysis: true);
    }
    private static void SetTrackResult(TrackViewModel track, TrackFeatures f)
    {
        track.Status = f.IsEmpty ? "사실상 무음 · 비활성 권장" : $"완료 · 유효 신호 {f.ActiveRatio:P1}";
        if (f.IsEmpty && !track.Touched) track.Enabled = false;
    }
    private Task StartOperation(Func<CancellationToken, long, Task> action, bool isAnalysis = false)
    {
        operationCts?.Cancel(); scoreCts?.Cancel();
        var old = operation;
        var cts = new CancellationTokenSource(); operationCts = cts;
        long gen = ++generation;
        operation = RunAsync();
        return operation;
        async Task RunAsync()
        {
            await old;
            if (cts.IsCancellationRequested) { cts.Dispose(); return; }
            Busy = true; Progress = 0; Warning = ""; IsAnalyzing = isAnalysis;
            try { await action(cts.Token, gen); }
            catch (OperationCanceledException) { if (gen == generation) Status = "분석을 취소했어요. 다시 시작할 수 있어요."; }
            catch (Exception ex) { DiagnosticLog.Error("operation", ex); if (gen == generation) { Status = "작업을 완료하지 못했어요."; Warning = ex.Message; } }
            finally
            {
                if (gen == generation)
                {
                    foreach (var track in Tracks.Where(t => t.Status == "분석 중")) track.Status = "취소됨";
                    IsAnalyzing = false; Busy = false; operationCts = null;
                }
                cts.Dispose();
            }
        }
    }
    private void QueueRecalculation() { if (!Busy) calculation = RecalculateAsync(false); }
    private async Task RecalculateAsync(bool reset)
    {
        scoreCts?.Cancel();
        using var cts = new CancellationTokenSource(); scoreCts = cts;
        long gen = ++scoreGeneration, fileGen = generation;
        var snapshot = features; var selections = Tracks.Select(t => t.Selection).ToArray(); string? fileKey = key;
        try
        {
            await Task.Delay(reset ? 0 : 80, cts.Token);
            var result = await Task.Run(() => calculator.Calculate(snapshot, selections, new(), cts.Token), cts.Token);
            if (gen != scoreGeneration || fileGen != generation || cts.IsCancellationRequested) return;
            Series = result; SeriesChanged?.Invoke(reset);
            if (!result.HasTracks && snapshot.Length > 0) Status = "분석 대상 트랙 없음 · 트랙을 켜거나 가중치를 높여 주세요.";
            else if (!reset && result.HasTracks) Status = "점수 재계산 완료 · MP4 재디코딩 없음";
            if (selections.Any(s => s.Enabled && s.Weight > 0 && s.Kind == TrackKind.Mix))
                Warning = "전체 믹스 트랙이 포함돼 있어요. 개별 트랙과 소리가 중복 집계될 수 있어요.";
            if (fileKey is not null) await store.SaveSelectionsAsync(fileKey, selections, cts.Token);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { if (gen == scoreGeneration && fileGen == generation) Warning = $"재계산 또는 설정 저장 실패: {ex.Message}"; }
        finally { if (ReferenceEquals(scoreCts, cts)) scoreCts = null; }
    }
    public Task FlushCalculationAsync() => calculation;
    public void Cancel() { operationCts?.Cancel(); scoreCts?.Cancel(); }
    public async Task CloseAsync()
    {
        Cancel(); await operation; await calculation;
        if (key is not null)
        {
            try { await store.SaveSelectionsAsync(key, Tracks.Select(t => t.Selection).ToArray(), CancellationToken.None); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { DiagnosticLog.Error("save-on-close", ex); }
        }
    }
    public static string FormatTime(double seconds)
    {
        var t = TimeSpan.FromSeconds(Math.Max(0, seconds));
        return $"{(int)t.TotalHours:00}:{t.Minutes:00}:{t.Seconds:00}.{t.Milliseconds:000}";
    }
}
