namespace BmsToUnbeatable;

/// <summary>
/// Converts BMS measure/fraction positions into absolute milliseconds, honoring
/// measure-length changes (channel 02), BPM changes (channels 03/08) and stops (channel 09).
/// Base formula: a full (ratio 1.0) measure lasts 240000/BPM ms - same tick x 240/bpm relationship
/// as plain BMS, just generalized to handle changes that land mid-measure.
/// </summary>
public sealed class BmsTiming
{
    private readonly record struct Breakpoint(double Fraction, double Time, double BpmAfter);

    private readonly Dictionary<int, List<Breakpoint>> _measureBreakpoints = new();
    private readonly Dictionary<int, double> _measureLengthRatio = new();

    public List<(double TimeMs, double Bpm)> BpmTimeline { get; } = new();

    public BmsTiming(BmsChart chart)
    {
        double currentTime = 0.0;
        double currentBpm = chart.InitialBpm;
        BpmTimeline.Add((0.0, currentBpm));

        for (int measure = 0; measure <= chart.MaxMeasure; measure++)
        {
            double lengthRatio = GetMeasureLengthRatio(chart, measure);
            _measureLengthRatio[measure] = lengthRatio;

            var events = new List<(double Frac, EventKind Kind, double Value)>();

            var bpmHex = chart.GetChannel(measure, "03").ToList();
            for (int i = 0; i < bpmHex.Count; i++)
            {
                if (bpmHex[i] == "00") continue;
                double bpm = Convert.ToInt32(bpmHex[i], 16);
                events.Add(((double)i / bpmHex.Count, EventKind.Bpm, bpm));
            }

            var bpmRef = chart.GetChannel(measure, "08").ToList();
            for (int i = 0; i < bpmRef.Count; i++)
            {
                string id = bpmRef[i];
                if (id == "00") continue;
                if (chart.BpmDefs.TryGetValue(id.ToUpperInvariant(), out var bpm))
                    events.Add(((double)i / bpmRef.Count, EventKind.Bpm, bpm));
            }

            var stopRef = chart.GetChannel(measure, "09").ToList();
            for (int i = 0; i < stopRef.Count; i++)
            {
                string id = stopRef[i];
                if (id == "00") continue;
                if (chart.StopDefs.TryGetValue(id.ToUpperInvariant(), out var stopUnits))
                    events.Add(((double)i / stopRef.Count, EventKind.Stop, stopUnits));
            }

            events.Sort((a, b) => a.Frac.CompareTo(b.Frac));

            var breakpoints = new List<Breakpoint> { new(0.0, currentTime, currentBpm) };
            double prevFrac = 0.0, prevTime = currentTime, prevBpm = currentBpm;

            foreach (var ev in events)
            {
                double segDur = (ev.Frac - prevFrac) * lengthRatio * 240000.0 / prevBpm;
                double timeAtFrac = prevTime + segDur;
                double bpmAfter = prevBpm;

                if (ev.Kind == EventKind.Stop)
                {
                    double stopMs = ev.Value * (240000.0 / prevBpm) / 192.0;
                    timeAtFrac += stopMs;
                }
                else
                {
                    bpmAfter = ev.Value;
                }

                breakpoints.Add(new Breakpoint(ev.Frac, timeAtFrac, bpmAfter));
                prevFrac = ev.Frac;
                prevTime = timeAtFrac;
                prevBpm = bpmAfter;

                if (ev.Kind == EventKind.Bpm)
                    BpmTimeline.Add((timeAtFrac, bpmAfter));
            }

            double endDur = (1.0 - prevFrac) * lengthRatio * 240000.0 / prevBpm;
            currentTime = prevTime + endDur;
            currentBpm = prevBpm;

            _measureBreakpoints[measure] = breakpoints;
        }
    }

    public double GetTime(int measure, double fraction)
    {
        var breakpoints = _measureBreakpoints[measure];
        double lengthRatio = _measureLengthRatio[measure];

        Breakpoint chosen = breakpoints[0];
        foreach (var bp in breakpoints)
        {
            if (bp.Fraction <= fraction) chosen = bp;
            else break;
        }

        if (Math.Abs(chosen.Fraction - fraction) < 1e-9)
            return chosen.Time;

        return chosen.Time + (fraction - chosen.Fraction) * lengthRatio * 240000.0 / chosen.BpmAfter;
    }

    private static double GetMeasureLengthRatio(BmsChart chart, int measure)
    {
        if (chart.Measures.TryGetValue(measure, out var chans) && chans.TryGetValue("02", out var raw) &&
            double.TryParse(raw.Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var ratio))
        {
            return ratio;
        }
        return 1.0;
    }

    private enum EventKind { Bpm, Stop }
}
