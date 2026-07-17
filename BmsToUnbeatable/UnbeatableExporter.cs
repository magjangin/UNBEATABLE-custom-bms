using System.Globalization;
using System.Text;

namespace BmsToUnbeatable;

public static class UnbeatableExporter
{
    // x-values chosen so that (x * 6 / 512) + 1 lands on lane 3 (Top), 4 (Low) and 6 (Mid).
    private static readonly int[] XForHeight = { 200, 300, 480 };

    public static void Export(
        BmsChart chart,
        BmsTiming timing,
        List<(int Measure, double Fraction, int HeightIndex)> notes,
        string songFolder,
        string audioFileNameInFolder,
        string difficultyName,
        string creator)
    {
        Directory.CreateDirectory(songFolder);

        var lines = new List<string>
        {
            "osu file format v14",
            "",
            "[General]",
            $"AudioFilename: {audioFileNameInFolder}",
            "AudioLeadIn: 0",
            "PreviewTime: -1",
            "Countdown: 0",
            "",
            "[Metadata]",
            $"Title:{chart.Title}",
            $"TitleUnicode:{chart.Title}",
            $"Artist:{chart.Artist}",
            $"ArtistUnicode:{chart.Artist}",
            $"Creator:{creator}",
            $"Version:{difficultyName}",
            $"Source:{chart.Genre}",
            "",
            "[TimingPoints]"
        };

        foreach (var (timeMs, bpm) in timing.BpmTimeline)
        {
            double beatLength = 60000.0 / bpm;
            lines.Add(Inv($"{timeMs:F0},{beatLength:F6},4,1,0,100,1,0"));
        }

        lines.Add("");
        lines.Add("[HitObjects]");

        foreach (var note in notes.OrderBy(n => timing.GetTime(n.Measure, n.Fraction)))
        {
            double t = timing.GetTime(note.Measure, note.Fraction);
            int x = XForHeight[note.HeightIndex];
            // type=1 -> instant/tap note. hitSound=0 (Normal) -> NoteType.Default.
            // trailing "0:0:0:0:" is the repurposed hitSample block (spawnMid flag, speed flag, unused, unused, unused).
            lines.Add(Inv($"{x},192,{t:F0},1,0,0:0:0:0:"));
        }

        string txtPath = Path.Combine(songFolder, difficultyName + ".txt");
        File.WriteAllText(txtPath, string.Join("\r\n", lines), new UTF8Encoding(false));
    }

    private static string Inv(FormattableString s) => s.ToString(CultureInfo.InvariantCulture);
}
