namespace SoundWeightGraph.Core;

public sealed class ScoreCalculator : IScoreCalculator
{
    public ScoreSeries Calculate(IReadOnlyList<TrackFeatures> features, IReadOnlyList<TrackSelection> selections,
        ScoringOptions options, CancellationToken ct = default)
    {
        if (!double.IsFinite(options.CoincidenceWindowSec) || options.CoincidenceWindowSec is < 0 or > 60 ||
            !double.IsFinite(options.SmoothingSec) || options.SmoothingSec is < 0 or > 60)
            throw new ArgumentOutOfRangeException(nameof(options));
        if (selections.Select(x => x.StreamIndex).Distinct().Count() != selections.Count)
            throw new ArgumentException("Duplicate stream selection");
        var selected = selections.Where(s => s.Enabled && double.IsFinite(s.Weight) && s.Weight is > 0 and <= 3)
            .Join(features.Where(f => f.IsValid), s => s.StreamIndex, f => f.StreamIndex,
                (s, f) => (s.Weight, Feature: f)).ToArray();
        if (selected.Length == 0) return ScoreSeries.Empty;
        int length = selected.Max(x => x.Feature.Activity.Length);
        if (length == 0) return ScoreSeries.Empty;
        double total = selected.Sum(x => x.Weight), pairs = 0;
        double[] b = new double[length], c = new double[length], raw = new double[length];
        int radius = (int)Math.Round(options.CoincidenceWindowSec / AnalysisFormat.FrameSeconds);
        var dilated = selected.Select(x => WindowMax(x.Feature.Activity, radius, length)).ToArray();
        for (int i = 0; i < selected.Length; i++)
            for (int j = i + 1; j < selected.Length; j++) pairs += selected[i].Weight * selected[j].Weight;
        for (int t = 0; t < length; t++)
        {
            if ((t & 255) == 0) ct.ThrowIfCancellationRequested();
            for (int i = 0; i < selected.Length; i++)
            {
                var (w, f) = selected[i];
                b[t] += w * (t < f.Activity.Length ? f.Activity[t] : 0) / total;
                if (pairs > 0)
                    for (int j = i + 1; j < selected.Length; j++)
                        c[t] += w * selected[j].Weight * Math.Min(dilated[i][t], dilated[j][t]) / pairs;
            }
            raw[t] = 100 * Math.Clamp(b[t] + .25 * c[t] * (1 - b[t]), 0, 1);
            b[t] *= 100;
        }
        int smoothRadius = (int)Math.Floor(options.SmoothingSec / AnalysisFormat.FrameSeconds / 2);
        double[] prefix = new double[length + 1], smooth = new double[length];
        for (int i = 0; i < length; i++) prefix[i + 1] = prefix[i] + raw[i];
        for (int i = 0; i < length; i++)
        {
            int left = Math.Max(0, i - smoothRadius), right = Math.Min(length - 1, i + smoothRadius);
            smooth[i] = (prefix[right + 1] - prefix[left]) / (right - left + 1);
        }
        return new(AnalysisFormat.FrameSeconds, b, c, raw, smooth);
    }

    // Monotonic deque: O(frames), including the padded tail of shorter tracks.
    private static double[] WindowMax(double[] values, int radius, int length)
    {
        var result = new double[length];
        var deque = new int[length];
        int head = 0, tail = 0, next = 0;
        double At(int i) => i < values.Length ? values[i] : 0;
        for (int t = 0; t < length; t++)
        {
            while (next <= Math.Min(length - 1, t + radius))
            {
                while (tail > head && At(deque[tail - 1]) <= At(next)) tail--;
                deque[tail++] = next++;
            }
            while (tail > head && deque[head] < t - radius) head++;
            result[t] = tail > head ? At(deque[head]) : 0;
        }
        return result;
    }
}
