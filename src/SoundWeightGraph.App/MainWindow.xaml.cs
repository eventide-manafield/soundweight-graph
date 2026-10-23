using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using SoundWeightGraph.Infrastructure;
using Microsoft.Win32;

namespace SoundWeightGraph.App;

public partial class MainWindow : Window
{
    public MainViewModel Model { get; } = new();
    private Point down;
    private bool closing;
    private ScottPlot.Plottables.Signal? totalSignal;
    private readonly Dictionary<int, ScottPlot.Plottables.Signal> highlightedSignals = [];
    public MainWindow()
    {
        InitializeComponent(); DataContext = Model;
        Chart.Plot.XLabel("Time"); Chart.Plot.YLabel("Score");
        Chart.Plot.Axes.Right.MinimumSize = 45;
        Chart.Plot.Axes.Rules.Add(new ScottPlot.AxisRules.LockedVertical(Chart.Plot.Axes.Left, 0, 100));
        Chart.UserInputProcessor.RemoveAll<ScottPlot.Interactivity.UserActionResponses.MouseWheelZoom>();
        Chart.UserInputProcessor.UserActionResponses.Add(new TimelineWheelResponse());
        Chart.UserInputProcessor.LeftClickDragPan(true, horizontal: true, vertical: false);
        Chart.UserInputProcessor.RightClickDragZoom(true, horizontal: true, vertical: false);
        Chart.Plot.Axes.Bottom.TickGenerator = new ScottPlot.TickGenerators.NumericAutomatic
        { LabelFormatter = seconds => MainViewModel.FormatTime(seconds).Split('.')[0] };
        Model.SeriesChanged += UpdatePlot;
        Chart.Plot.Axes.SetLimits(0, 60, 0, 100);
        Chart.Refresh();
    }
    private void UpdatePlot(bool reset)
    {
        var limits = Chart.Plot.Axes.GetLimits();
        Chart.Plot.Clear(); totalSignal = null; highlightedSignals.Clear();
        if (Model.Series.HasTracks)
        {
            // Signal uses per-pixel min/max rendering for dense, evenly-spaced samples.
            totalSignal = Chart.Plot.Add.Signal(Model.Series.SmoothedScore, Model.Series.FrameDurationSec);
            totalSignal.Color = ScottPlot.Color.FromHex("3470D8"); totalSignal.LineWidth = 1.8f;
            foreach (var track in Model.Tracks.Where(t => t.Highlighted && t.Enabled && t.Weight > 0))
            {
                if (!Model.Series.TrackContributions.TryGetValue(track.Stream.StreamIndex, out var values)) continue;
                var overlay = Chart.Plot.Add.Signal(values, Model.Series.FrameDurationSec);
                overlay.Color = ScottPlot.Color.FromHex(track.HighlightColor).WithAlpha(.6);
                overlay.LineWidth = 2.4f;
                highlightedSignals.Add(track.Stream.StreamIndex, overlay);
            }
        }
        if (reset) SetFullView();
        else Chart.Plot.Axes.SetLimits(limits.Left, limits.Right, 0, 100);
        Chart.Refresh();
    }
    private void SetFullView() => Chart.Plot.Axes.SetLimits(0, Math.Max(1, Model.Duration), 0, 100);
    internal double RenderedSeriesEnd => totalSignal?.GetAxisLimits().Right ?? 0;
    internal async Task VerifyTrackHighlightForSmokeAsync()
    {
        var tracks = Model.Tracks.Where(t => t.Enabled && t.Weight > 0 && Model.Series.TrackContributions.ContainsKey(t.Stream.StreamIndex)).Take(2).ToArray();
        if (tracks.Length != 2) throw new InvalidOperationException("Overlay smoke requires two analyzed tracks");
        var score = Model.Series;
        var raw = score.RawScore.ToArray();
        int decodes = Model.DecodeCount;
        Chart.Plot.Axes.SetLimits(3, 13, 0, 100);
        foreach (var track in tracks) track.Highlighted = true;
        var limits = Chart.Plot.Axes.GetLimits();
        if (highlightedSignals.Count != 2 || Chart.Plot.GetPlottables().Count() != 3 ||
            !ReferenceEquals(score, Model.Series) || !raw.SequenceEqual(Model.Series.RawScore) ||
            limits.Left != 3 || limits.Right != 13 || limits.Bottom != 0 || limits.Top != 100)
            throw new InvalidOperationException("Highlight changed scores, view, or wrong tracks");
        foreach (var track in tracks)
        {
            var overlay = highlightedSignals[track.Stream.StreamIndex];
            if (overlay.Color != ScottPlot.Color.FromHex(track.HighlightColor).WithAlpha(.6) ||
                Math.Abs(overlay.GetAxisLimits().Right - RenderedSeriesEnd) > .001)
                throw new InvalidOperationException("Overlay color/opacity or timing is incorrect");
        }
        tracks[0].Highlighted = false;
        if (highlightedSignals.Count != 1 || !highlightedSignals.ContainsKey(tracks[1].Stream.StreamIndex))
            throw new InvalidOperationException("Unchecking a track removed the wrong overlay");
        tracks[0].Highlighted = true;
        double weight = tracks[0].Weight;
        tracks[0].Weight = 0; await Model.FlushCalculationAsync();
        if (highlightedSignals.ContainsKey(tracks[0].Stream.StreamIndex)) throw new InvalidOperationException("Zero-weight overlay visible");
        tracks[0].Weight = weight; await Model.FlushCalculationAsync();
        tracks[0].Enabled = false; await Model.FlushCalculationAsync();
        if (highlightedSignals.ContainsKey(tracks[0].Stream.StreamIndex)) throw new InvalidOperationException("Disabled overlay visible");
        tracks[0].Enabled = true; await Model.FlushCalculationAsync();
        if (highlightedSignals.Count != 2 || Model.DecodeCount != decodes || !raw.SequenceEqual(Model.Series.RawScore))
            throw new InvalidOperationException("Restoring highlights changed decoding or final scores");
        foreach (var track in tracks) track.Highlighted = false;
        SetFullView(); Chart.Refresh();
    }
    internal void VerifyTimelineNavigationForSmoke()
    {
        // Exercise the installed input processor with a real five-hour Signal series.
        // This checks library dispatch and axis behavior, not physical OS mouse delivery.
        var processor = Chart.UserInputProcessor;
        try
        {
            Chart.Plot.Clear();
            double[] samples = Enumerable.Range(0, 36000).Select(i => 50 + 25 * Math.Sin(i / 30.0)).ToArray();
            Chart.Plot.Add.Signal(samples, .5);
            Chart.Plot.Add.Signal(samples.Select(x => x * .4).ToArray(), .5).Color = ScottPlot.Color.FromHex("D97706").WithAlpha(.6);
            Chart.Plot.Add.Signal(samples.Select(x => x * .3).ToArray(), .5).Color = ScottPlot.Color.FromHex("059669").WithAlpha(.6);
            Chart.Plot.Axes.SetLimits(0, 18000, 0, 100);
            Chart.Plot.RenderInMemory(900, 400);
            var rect = Chart.Plot.LastRender.DataRect;
            var pixel = new ScottPlot.Pixel(rect.Left + rect.Width * .65f, rect.Top + rect.Height * .5f);
            double anchor = Chart.Plot.GetCoordinates(pixel).X;
            processor.Process(new ScottPlot.Interactivity.UserActions.MouseWheelUp(pixel));
            Chart.Plot.RenderInMemory(900, 400);
            var zoomed = Chart.Plot.Axes.GetLimits();
            if (zoomed.Right - zoomed.Left >= 18000 || zoomed.Bottom != 0 || zoomed.Top != 100)
                throw new InvalidOperationException("Wheel must zoom time only");
            if (Math.Abs(Chart.Plot.GetCoordinates(pixel).X - anchor) > .01)
                throw new InvalidOperationException("Wheel zoom must preserve the pointer time");
            processor.KeyState.Add(ScottPlot.Interactivity.StandardKeys.Shift);
            processor.Process(new ScottPlot.Interactivity.UserActions.MouseWheelUp(pixel));
            Chart.Plot.RenderInMemory(900, 400);
            var panned = Chart.Plot.Axes.GetLimits();
            if (panned.Left >= zoomed.Left || Math.Abs((panned.Right - panned.Left) - (zoomed.Right - zoomed.Left)) > .01 || panned.Bottom != 0 || panned.Top != 100)
                throw new InvalidOperationException("Shift+wheel must pan left at fixed scale");
            processor.Process(new ScottPlot.Interactivity.UserActions.MouseWheelDown(pixel));
            Chart.Plot.RenderInMemory(900, 400);
            var returned = Chart.Plot.Axes.GetLimits();
            if (Math.Abs(returned.Left - zoomed.Left) > .01)
                throw new InvalidOperationException("Shift+wheel down must pan right");
            processor.KeyState.Remove(ScottPlot.Interactivity.StandardKeys.Shift);
            processor.Process(new ScottPlot.Interactivity.UserActions.MouseWheelDown(pixel));
            Chart.Plot.RenderInMemory(900, 400);
            var full = Chart.Plot.Axes.GetLimits();
            if (Math.Abs((full.Right - full.Left) - 18000) > .01 || full.Bottom != 0 || full.Top != 100 || samples.Length != 36000)
                throw new InvalidOperationException("Wheel zoom out changed sample resolution or score limits");
        }
        finally { processor.KeyState.Reset(); UpdatePlot(true); }
    }
    private void FitPlot(object sender, RoutedEventArgs e) { SetFullView(); Chart.Refresh(); }
    private async void ChooseFile(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Filter = "MP4 녹화 파일 (*.mp4)|*.mp4", CheckFileExists = true };
        if (dialog.ShowDialog(this) == true) await Model.OpenAsync(dialog.FileName);
    }
    private void FileDragOver(object sender, DragEventArgs e)
    { e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None; e.Handled = true; }
    private async void FileDropped(object sender, DragEventArgs e)
    { if (e.Data.GetData(DataFormats.FileDrop) is string[] { Length: > 0 } paths) await Model.OpenAsync(paths[0]); }
    private double TimeAt(MouseEventArgs e)
    {
        Point point = e.GetPosition(Chart); var dpi = VisualTreeHelper.GetDpi(Chart);
        return Math.Clamp(Chart.Plot.GetCoordinates(new ScottPlot.Pixel(point.X * dpi.DpiScaleX, point.Y * dpi.DpiScaleY)).X, 0, Model.Duration);
    }
    private void PlotMouseMove(object sender, MouseEventArgs e)
    {
        if (!Model.Series.HasTracks) return;
        double time = TimeAt(e);
        int i = Math.Clamp((int)(time / Model.Series.FrameDurationSec), 0, Model.Series.SmoothedScore.Length - 1);
        Model.Cursor = $"{MainViewModel.FormatTime(time)}  ·  {Model.Series.SmoothedScore[i]:F1}점";
    }
    private void PlotMouseDown(object sender, MouseButtonEventArgs e) => down = e.GetPosition(Chart);
    private void PlotMouseUp(object sender, MouseButtonEventArgs e)
    { if ((e.GetPosition(Chart) - down).Length < 4 && Model.Series.HasTracks) Model.SelectedTime = MainViewModel.FormatTime(TimeAt(e)); }
    private void CopyTime(object sender, RoutedEventArgs e)
    {
        if (Model.SelectedTime.Length == 0) return;
        try { Clipboard.SetText(Model.SelectedTime); }
        catch (System.Runtime.InteropServices.COMException) { MessageBox.Show(this, "클립보드를 사용 중이에요. 잠시 뒤 다시 시도해 주세요."); }
    }
    private void ShowNotices(object sender, RoutedEventArgs e)
    {
        new Window
        {
            Title = "오픈소스 고지 · SoundWeight Graph", Owner = this, Width = 850, Height = 650,
            Content = new System.Windows.Controls.TextBox
            {
                Text = ToolBundle.Notices, IsReadOnly = true, TextWrapping = TextWrapping.Wrap, Padding = new Thickness(18),
                VerticalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Auto
            }
        }.ShowDialog();
    }
    private async void WindowClosing(object? sender, CancelEventArgs e)
    {
        if (closing) return;
        e.Cancel = true; IsEnabled = false; await Model.CloseAsync(); closing = true;
        _ = Dispatcher.BeginInvoke(Close);
    }
}
