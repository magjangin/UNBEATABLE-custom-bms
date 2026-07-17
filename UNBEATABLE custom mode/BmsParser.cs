using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace BmsToUnbeatable;

public static class BmsParser
{
    private static readonly Regex ExtBpmRegex = new(@"^#BPM([0-9A-Za-z]+)\s+(.+)$", RegexOptions.Compiled);
    private static readonly Regex InitialBpmRegex = new(@"^#BPM\s+(.+)$", RegexOptions.Compiled);
    private static readonly Regex StopRegex = new(@"^#STOP([0-9A-Za-z]+)\s+(.+)$", RegexOptions.Compiled);
    private static readonly Regex MeasureRegex = new(@"^#(\d{3})(\d{2}):(.*)$", RegexOptions.Compiled);
    private static readonly Regex WavRegex = new(@"^#WAV([0-9A-Za-z]+)\s+(.+)$", RegexOptions.Compiled);

    public static BmsChart Parse(string path)
    {
        var chart = new BmsChart();
        // BMS files are commonly Shift-JIS; fall back to UTF-8 if that codepage isn't available.
        Encoding enc;
        try
        {
            enc = Encoding.GetEncoding("shift_jis");
        }
        catch (NotSupportedException)
        {
            enc = Encoding.UTF8;
        }

        int maxWavIdLength = 2;

        foreach (var rawLine in File.ReadLines(path, enc))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || !line.StartsWith("#")) continue;

            var wav = WavRegex.Match(line);
            if (wav.Success)
            {
                string wavId = wav.Groups[1].Value;
                maxWavIdLength = Math.Max(maxWavIdLength, wavId.Length);
                chart.WavDefs[wavId.ToUpperInvariant()] = wav.Groups[2].Value.Trim();
                continue;
            }

            var m = MeasureRegex.Match(line);
            if (m.Success)
            {
                int measure = int.Parse(m.Groups[1].Value);
                string channel = m.Groups[2].Value;
                string data = m.Groups[3].Value.Trim();
                if (!chart.Measures.TryGetValue(measure, out var chans))
                {
                    chans = new Dictionary<string, string>();
                    chart.Measures[measure] = chans;
                }
                chans[channel] = data;
                continue;
            }

            var extBpm = ExtBpmRegex.Match(line);
            if (extBpm.Success)
            {
                chart.BpmDefs[extBpm.Groups[1].Value.ToUpperInvariant()] = ParseInvariant(extBpm.Groups[2].Value);
                continue;
            }

            var stop = StopRegex.Match(line);
            if (stop.Success)
            {
                chart.StopDefs[stop.Groups[1].Value.ToUpperInvariant()] = ParseInvariant(stop.Groups[2].Value);
                continue;
            }

            var initBpm = InitialBpmRegex.Match(line);
            if (initBpm.Success)
            {
                chart.InitialBpm = ParseInvariant(initBpm.Groups[1].Value);
                continue;
            }

            if (line.StartsWith("#TITLE", StringComparison.OrdinalIgnoreCase))
                chart.Title = line.Substring(6).Trim();
            else if (line.StartsWith("#ARTIST", StringComparison.OrdinalIgnoreCase))
                chart.Artist = line.Substring(7).Trim();
            else if (line.StartsWith("#GENRE", StringComparison.OrdinalIgnoreCase))
                chart.Genre = line.Substring(6).Trim();
        }

        chart.CellWidth = maxWavIdLength;
        return chart;
    }

    private static double ParseInvariant(string s) =>
        double.Parse(s.Trim(), System.Globalization.CultureInfo.InvariantCulture);
}
