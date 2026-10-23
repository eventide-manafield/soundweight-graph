using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace SoundWeightGraph.App;

public partial class App : Application
{
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var window = new MainWindow(); MainWindow = window; window.Show();
        if (e.Args.Length == 3 && e.Args[0] == "--smoke-test")
        {
            string report = Path.GetFullPath(e.Args[2]);
            try
            {
                if (window.Title != "SoundWeight Graph" || typeof(App).Assembly.GetName().Name != "SoundWeightGraph" ||
                    Path.GetFileName(SoundWeightGraph.Infrastructure.AppPaths.Root) != "SoundWeightGraph" ||
                    !SoundWeightGraph.Infrastructure.ToolBundle.Notices.Contains("Copyright 2026 eventide-manafield"))
                    throw new InvalidOperationException("Application branding or project notice is incorrect");
                Directory.CreateDirectory(Path.GetDirectoryName(report)!);
                await window.Model.OpenAsync(e.Args[1]);
                await window.Model.AnalyzeAsync();
                if (!window.Model.Series.HasTracks) throw new InvalidOperationException(window.Model.Warning + " " + window.Model.Status);
                int decodes = window.Model.DecodeCount;
                var track = window.Model.Tracks.First(t => t.Enabled);
                track.Weight = track.Weight == 2 ? 1.75 : 2; await window.Model.FlushCalculationAsync();
                if (window.Model.DecodeCount != decodes) throw new InvalidOperationException("Weight change decoded audio");
                var selected = window.Model.Tracks.Select(t => t.Enabled).ToArray();
                foreach (var t in window.Model.Tracks) t.Enabled = false;
                await window.Model.FlushCalculationAsync();
                if (window.Model.Series.HasTracks) throw new InvalidOperationException("All-off graph is not empty");
                for (int i = 0; i < selected.Length; i++) window.Model.Tracks[i].Enabled = selected[i];
                track.Weight = .25; track.Weight = 3; track.Weight = 1.5;
                await window.Model.FlushCalculationAsync();
                if (!window.Model.Series.HasTracks || window.Model.DecodeCount != decodes)
                    throw new InvalidOperationException("Toggle/rapid reweight failed");
                double expectedEnd = (window.Model.Series.RawScore.Length - 1) * .5;
                if (Math.Abs(window.RenderedSeriesEnd - expectedEnd) > .001)
                    throw new InvalidOperationException("Plot sample spacing is incorrect");
                await window.Model.OpenAsync(e.Args[1]); await window.Model.AnalyzeAsync();
                if (window.Model.DecodeCount != decodes) throw new InvalidOperationException("Cache reopen decoded media");
                window.VerifyTimelineNavigationForSmoke();
                window.Model.SelectedTime = "00:00:12.000";
                await Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
                await Task.Delay(600);
                window.UpdateLayout();
                var visual = (FrameworkElement)window.Content;
                var bitmap = new RenderTargetBitmap((int)visual.ActualWidth, (int)visual.ActualHeight, 96, 96, PixelFormats.Pbgra32);
                bitmap.Render(visual);
                var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
                using (var output = File.Create(Path.ChangeExtension(report, ".png"))) encoder.Save(output);
                int tracks = window.Model.Tracks.Count, frames = window.Model.Series.RawScore.Length;
                double maxScore = window.Model.Series.RawScore.Max();
                bool cancellationChecked = false, fileSwitchChecked = false;
                string longFile = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(e.Args[1]))!, "long_5h.mp4");
                if (File.Exists(longFile))
                {
                    await window.Model.OpenAsync(longFile);
                    Task analysis = window.Model.AnalyzeAsync();
                    await WaitForProgress(window.Model, analysis);
                    window.Model.Cancel(); await analysis;
                    cancellationChecked = window.Model.Status.Contains("취소");
                    if (!cancellationChecked) throw new InvalidOperationException("Active GUI cancellation failed");
                    Task again = window.Model.AnalyzeAsync();
                    await WaitForProgress(window.Model, again);
                    await window.Model.OpenAsync(e.Args[1]); await again;
                    await window.Model.AnalyzeAsync();
                    fileSwitchChecked = window.Model.Tracks.Count == tracks && window.Model.Series.HasTracks;
                    if (!fileSwitchChecked) throw new InvalidOperationException("Switch during decode failed");
                }
                await File.WriteAllTextAsync(report, JsonSerializer.Serialize(new
                {
                    Success = true, ProductName = window.Title, BrandingChecked = true,
                    RuntimeVersion = Environment.Version.ToString(), Tracks = tracks, Frames = frames,
                    InitialDecodes = decodes, RecalculationDecodes = 0, AllOffChecked = true,
                    CacheReopenChecked = true, PlotTimeScaleChecked = true,
                    HorizontalWheelZoomChecked = true, ShiftWheelPanChecked = true, FiveHourNavigationChecked = true,
                    ActiveCancellationChecked = cancellationChecked, FileSwitchDuringDecodeChecked = fileSwitchChecked,
                    MaxScore = maxScore, window.Model.Warning
                }, new JsonSerializerOptions { WriteIndented = true }));
                window.Close();
            }
            catch (Exception ex)
            { await File.WriteAllTextAsync(report, JsonSerializer.Serialize(new { Success = false, Error = ex.ToString() })); Shutdown(1); }
        }
        else if (e.Args.Length == 1) await window.Model.OpenAsync(e.Args[0]);
    }
    private static async Task WaitForProgress(MainViewModel model, Task work)
    {
        var timeout = System.Diagnostics.Stopwatch.StartNew();
        while (model.Progress <= 0 && !work.IsCompleted && timeout.Elapsed < TimeSpan.FromSeconds(10)) await Task.Delay(25);
        if (work.IsCompleted || model.Progress <= 0) throw new InvalidOperationException("No active decode for lifecycle test");
    }
}
