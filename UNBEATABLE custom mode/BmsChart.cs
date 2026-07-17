using System;
using System.Collections.Generic;
using System.Linq;

namespace BmsToUnbeatable;

public sealed class BmsChart
{
    public string Title = "";
    public string Artist = "";
    public string Genre = "";
    public double InitialBpm = 130.0;

    public Dictionary<string, double> BpmDefs = new();   // #BPMxx -> value
    public Dictionary<string, double> StopDefs = new();  // #STOPxx -> value (in 1/192 of a whole measure)
    public Dictionary<string, string> WavDefs = new(StringComparer.OrdinalIgnoreCase); // #WAVxxx -> filename

    // measure -> channel -> raw object string (e.g. "0011000000000000")
    public Dictionary<int, Dictionary<string, string>> Measures = new();

    // Cell width in characters for measure-data slot codes. Standard BMS is fixed at 2 (base36),
    // but the custom hwa editor supports variable width - see docs/BMS_PARSING.md "가변 셀 폭":
    // width = the longest #WAVxxx declaration id in the whole chart (minimum 2).
    public int CellWidth = 2;

    public int MaxMeasure => Measures.Count == 0 ? 0 : Measures.Keys.Max();

    public IEnumerable<string> GetChannel(int measure, string channel)
    {
        if (Measures.TryGetValue(measure, out var chans) && chans.TryGetValue(channel, out var raw))
            return Tokenize(raw, CellWidth);
        return Array.Empty<string>();
    }

    public static IEnumerable<string> Tokenize(string raw, int width)
    {
        for (int i = 0; i + width <= raw.Length; i += width)
            yield return raw.Substring(i, width);
    }

    public static bool IsEmptyToken(string token) => token.All(c => c == '0');
}

public readonly record struct BmsNote(int Measure, double Fraction, int Channel, string WavId);
