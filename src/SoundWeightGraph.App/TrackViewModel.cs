using SoundWeightGraph.Core;

namespace SoundWeightGraph.App;

public sealed class TrackViewModel(AudioStreamInfo stream) : Observable
{
    public AudioStreamInfo Stream { get; } = stream;
    public string Name => Stream.DisplayName;
    public string Details => $"{Stream.Codec.ToUpperInvariant()} · {Stream.Channels}채널 · stream {Stream.StreamIndex}";
    public string MixHint { get; init; } = "";
    private bool enabled = true;
    private double weight = 1;
    private TrackKind kind;
    private string status = "미분석";
    public bool Touched { get; private set; }
    public event Action? SelectionChanged;
    public bool Enabled { get => enabled; set { if (Set(ref enabled, value)) SelectionWasChanged(); } }
    public double Weight
    {
        get => weight;
        set { if (Set(ref weight, Math.Clamp(Math.Round((double.IsFinite(value) ? value : 1) * 4) / 4, 0, 3))) SelectionWasChanged(); }
    }
    public TrackKind Kind { get => kind; set { if (Set(ref kind, value)) SelectionWasChanged(); } }
    public string Status { get => status; set => Set(ref status, value); }
    public TrackSelection Selection => new(Stream.StreamIndex, Enabled, Weight, Kind);
    private void SelectionWasChanged() { Touched = true; SelectionChanged?.Invoke(); }
    public void Restore(TrackSelection selection)
    { Enabled = selection.Enabled; Weight = selection.Weight; Kind = selection.Kind; Touched = true; }
}
