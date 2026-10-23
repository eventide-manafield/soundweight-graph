// Copyright 2026 eventide-manafield
// SPDX-License-Identifier: Apache-2.0
using ScottPlot;
using ScottPlot.Interactivity;
using ScottPlot.Interactivity.UserActions;

namespace SoundWeightGraph.App;

/// <summary>Wheel zooms time around the pointer; Shift+wheel pans without changing scale.</summary>
internal sealed class TimelineWheelResponse : IUserActionResponse
{
    public void ResetState(IPlotControl plotControl) { }

    public ResponseInfo Execute(IPlotControl plotControl, IUserAction input, KeyboardState keys)
    {
        Pixel pixel;
        bool up;
        if (input is MouseWheelUp wheelUp) { pixel = wheelUp.Pixel; up = true; }
        else if (input is MouseWheelDown wheelDown) { pixel = wheelDown.Pixel; up = false; }
        else return ResponseInfo.NoActionRequired;

        var plot = plotControl.GetPlotAtPixel(pixel);
        if (plot is null) return ResponseInfo.NoActionRequired;
        if (keys.IsPressed(StandardKeys.Shift))
        {
            var limits = plot.Axes.GetLimits();
            double delta = (limits.Right - limits.Left) * (up ? -.15 : .15);
            plot.Axes.SetLimitsX(limits.Left + delta, limits.Right + delta);
        }
        else
        {
            MouseAxisManipulation.MouseWheelZoom(plot, up ? 1.15 : 1 / 1.15, 1, pixel, false);
        }
        plot.Axes.SetLimitsY(0, 100);
        return new ResponseInfo { RefreshNeeded = true };
    }
}
