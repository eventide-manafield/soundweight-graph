using SoundWeightGraph.Core;
using Xunit;

namespace SoundWeightGraph.Tests;

public class ScoreTests
{
    public static TrackFeatures Feature(int id, params double[] activity) => new(id, .5, 0,
        activity.Select(_ => .1).ToArray(), activity.Select(_ => .2).ToArray(),
        activity.Select(_ => -20.0).ToArray(), activity.Select(_ => 8000).ToArray(), activity);
    private readonly ScoreCalculator calculator = new();
    [Fact] public void WeightedExample()
    {
        var score = calculator.Calculate([Feature(1, .8), Feature(2, .9), Feature(3, .4)],
            [new(1, true, 2), new(2, true, 1.5), new(3, true, .5)], new());
        Assert.Equal(78.75, score.BaseScore[0], 10);
        Assert.InRange(score.RawScore[0], 78.75, 100);
    }
    [Fact] public void ContributionsAreWeightedSharesWithoutSharedBonus()
    {
        var score = calculator.Calculate([Feature(1, .8), Feature(2, .9), Feature(3, .4)],
            [new(1, true, 2), new(2, true, 1.5), new(3, true, .5)], new());
        Assert.Equal(40, score.TrackContributions[1][0], 10);
        Assert.Equal(33.75, score.TrackContributions[2][0], 10);
        Assert.Equal(5, score.TrackContributions[3][0], 10);
        Assert.Equal(score.BaseScore[0], score.TrackContributions.Values.Sum(v => v[0]), 10);
        Assert.True(score.SmoothedScore[0] > score.TrackContributions.Values.Sum(v => v[0]));
    }
    [Fact] public void ContributionsUseSameSmoothingAndExcludeUnavailableTracks()
    {
        var score = calculator.Calculate([Feature(1, 1), Feature(2, 0, 1, 0), Feature(3, 1, 1, 1)],
            [new(1), new(2), new(3, false), new(4), new(5, true, 0)], new());
        Assert.Equal(new[] { 1, 2 }, score.TrackContributions.Keys.Order());
        Assert.Equal(new[] { 25.0, 50.0 / 3, 0 }, score.TrackContributions[1]);
        Assert.Equal(new[] { 25.0, 50.0 / 3, 25.0 }, score.TrackContributions[2]);
        Assert.All(score.TrackContributions.Values, values => Assert.Equal(score.SmoothedScore.Length, values.Length));
        Assert.Empty(calculator.Calculate([Feature(1, 1)], [new(1, false)], new()).TrackContributions);
    }
    [Fact] public void DisabledAndZeroWeightNeverDilute()
    {
        var a = Feature(1, .8, .3, 1); var empty = Feature(2, 0, 0, 0);
        var expected = calculator.Calculate([a], [new(1)], new());
        Assert.Equal(expected.RawScore, calculator.Calculate([a, empty], [new(1), new(2, false)], new()).RawScore);
        Assert.Equal(expected.RawScore, calculator.Calculate([a, empty], [new(1), new(2, true, 0)], new()).RawScore);
    }
    [Fact] public void AllOffAndFailedTracksHaveNoScore()
    {
        Assert.False(calculator.Calculate([Feature(1, 1)], [new(1, false)], new()).HasTracks);
        Assert.False(calculator.Calculate([], [new(1)], new()).HasTracks);
    }
    [Fact] public void SingleTrackReaches100WithoutBonus()
    {
        var s = calculator.Calculate([Feature(1, 1)], [new(1)], new());
        Assert.Equal(100, s.RawScore[0]); Assert.Equal(0, s.Coincidence[0]);
    }
    [Fact] public void DelayedReactionAddsBonusAtTwoSeconds()
    {
        double[] a = new double[30], b = new double[30]; a[10] = 1; b[14] = 1;
        var score = calculator.Calculate([Feature(1, a), Feature(2, b)], [new(1), new(2)], new());
        Assert.Equal(1, score.Coincidence[10]);
        Assert.True(score.RawScore[10] > score.BaseScore[10]);
        var single = calculator.Calculate([Feature(1, a)], [new(1)], new());
        Assert.Equal(0, single.Coincidence[10]);
    }
    [Fact] public void SteadyBackgroundAndSilenceStayQuiet()
    {
        foreach (double level in new[] { -8.0, -80, -240 })
            Assert.All(ActivityNormalizer.Calculate(Enumerable.Repeat(level, 1200).ToArray()), x => Assert.Equal(0, x));
    }
    [Fact] public void BurstIsDetectedAndTinyNoiseIsGated()
    {
        double[] db = Enumerable.Repeat(-60.0, 200).ToArray(); db[80] = -10;
        Assert.True(ActivityNormalizer.Calculate(db)[80] > .7);
        db[80] = -70; db[81] = -90;
        Assert.Equal(0, ActivityNormalizer.Calculate(db)[80]);
    }
    [Fact] public void PartialFramePreservesCountAndEnergy()
    {
        var acc = new FeatureAccumulator(); acc.Add(Enumerable.Repeat(.5f, 8100).ToArray());
        var f = acc.Finish(4);
        Assert.Equal([8000, 100], f.SampleCounts); Assert.Equal([.5, .5], f.Rms);
        Assert.Equal([.5, .5], f.Peak); Assert.True(f.IsValid);
    }
    [Fact] public void NonFiniteSamplesAreRejected()
    { Assert.Throws<InvalidDataException>(() => new FeatureAccumulator().Add([float.NaN])); }
    [Fact] public void ShorterTrackTailRemainsFinite()
    {
        var s = calculator.Calculate([Feature(1, .8), Feature(2, .2, .5, 1)], [new(1), new(2)], new());
        Assert.Equal(3, s.RawScore.Length); Assert.All(s.RawScore, x => Assert.InRange(x, 0, 100));
    }
    [Fact] public void FiveHourRecalculationUsesOnlyFeatureArrays()
    {
        var f = Enumerable.Range(0, 6).Select(id => Feature(id, Enumerable.Range(0, 36000).Select(i => i % 100 / 100.0).ToArray())).ToArray();
        var selections = Enumerable.Range(0, 6).Select(id => new TrackSelection(id)).ToArray();
        var first = calculator.Calculate(f, selections, new());
        selections[0] = new(0, false);
        var second = calculator.Calculate(f, selections, new());
        Assert.Equal(36000, first.RawScore.Length); Assert.Equal(36000, second.RawScore.Length);
    }
}
