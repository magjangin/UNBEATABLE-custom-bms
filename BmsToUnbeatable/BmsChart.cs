namespace BmsToUnbeatable;

public sealed class BmsChart
{
    public string Title = "";
    public string Artist = "";
    public string Genre = "";
    public double InitialBpm = 130.0;

    public Dictionary<string, double> BpmDefs = new();   // #BPMxx -> value
    public Dictionary<string, double> StopDefs = new();  // #STOPxx -> value (in 1/192 of a whole measure)

    // measure -> channel -> raw object string (e.g. "0011000000000000")
    public Dictionary<int, Dictionary<string, string>> Measures = new();

    public int MaxMeasure => Measures.Count == 0 ? 0 : Measures.Keys.Max();

    public IEnumerable<string> GetChannel(int measure, string channel)
    {
        if (Measures.TryGetValue(measure, out var chans) && chans.TryGetValue(channel, out var raw))
            return Tokenize(raw);
        return Array.Empty<string>();
    }

    public static IEnumerable<string> Tokenize(string raw)
    {
        for (int i = 0; i + 1 < raw.Length; i += 2)
            yield return raw.Substring(i, 2);
    }
}

public readonly record struct BmsNote(int Measure, double Fraction, int Channel, string WavId);
