namespace SoundWeightGraph.Core;

/// <summary>Consumes bounded PCM chunks; only half-second summaries survive.</summary>
public sealed class FeatureAccumulator
{
    private readonly List<double> rms = [], peak = [];
    private readonly List<int> counts = [];
    private double sumSquares, max;
    private int count;
    public long Samples { get; private set; }

    public void Add(ReadOnlySpan<float> samples)
    {
        foreach (float value in samples)
        {
            if (!float.IsFinite(value)) throw new InvalidDataException("PCM에 유효하지 않은 값이 있어요.");
            double x = value;
            sumSquares += x * x;
            max = Math.Max(max, Math.Abs(x));
            count++;
            Samples++;
            if (count == 8000) Flush();
        }
    }
    private void Flush()
    {
        if (count == 0) return;
        rms.Add(Math.Sqrt(sumSquares / count));
        peak.Add(max); counts.Add(count);
        count = 0; sumSquares = max = 0;
    }
    public TrackFeatures Finish(int streamIndex, CancellationToken ct = default)
    {
        Flush();
        double[] levels = rms.Select(x => 20 * Math.Log10(Math.Max(x, 1e-12))).ToArray();
        return new(streamIndex, AnalysisFormat.FrameSeconds, 0, rms.ToArray(), peak.ToArray(),
            levels, counts.ToArray(), ActivityNormalizer.Calculate(levels, ct));
    }
}

public static class ActivityNormalizer
{
    public static double[] Calculate(double[] db, CancellationToken ct = default)
    {
        double[] activity = new double[db.Length], onset = new double[db.Length];
        var neighborhood = new double[121];
        for (int i = 0; i < db.Length; i++)
        {
            ct.ThrowIfCancellationRequested();
            if (!double.IsFinite(db[i])) throw new ArgumentException("Nonfinite dBFS");
            if (db[i] <= AnalysisFormat.SilenceDb) continue;
            int start = Math.Max(0, i - 60), end = Math.Min(db.Length - 1, i + 60);
            int size = end - start + 1;
            Array.Copy(db, start, neighborhood, 0, size);
            Array.Sort(neighborhood, 0, size);
            double baseline = neighborhood[size / 2];
            double deviation = Math.Clamp((db[i] - baseline - 3) / 18, 0, 1);
            double previous = i == 0 ? db[i] : db.Skip(Math.Max(0, i - 4)).Take(Math.Min(i, 4)).Average();
            onset[i] = Math.Clamp((db[i] - previous - 3) / 18, 0, 1);
            int bursts = 0;
            for (int j = Math.Max(0, i - 9); j <= i; j++)
                if (onset[j] > .25 && (j == 0 || onset[j - 1] <= .25)) bursts++;
            double gate = Math.Clamp((db[i] - AnalysisFormat.SilenceDb) / 15, 0, 1);
            activity[i] = Math.Clamp((.45 * deviation + .35 * onset[i] + .20 * Math.Min(1, bursts / 3.0)) * gate, 0, 1);
        }
        return activity;
    }
}
