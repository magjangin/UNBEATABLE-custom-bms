using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace BmsToUnbeatable;

/// <summary>A fully-resolved note ready to be written as one [HitObjects] line.</summary>
public struct OutNote
{
    public double TimeMs;
    public int HeightIndex; // 0=Top, 1=Low, 2=Mid
    public int SideIndex;   // 0=Right, 1=Left
    public bool IsHoldType; // Hold/Double/Spam - carries an end time
    public int HitSound;    // 0=Normal, 2=Whistle, 4=Finish (matches HitObjectInfo.HitSound bits)
    public bool IsBrawl;    // forces hitSample[0]="3" regardless of HitSound
    public double? EndTimeMs;
}

public static class UnbeatableExporter
{
    // x-values chosen so that (x * 6 / 512) + 1 lands on lane 3 (Top), 4 (Low) and 6 (Mid).
    private static readonly int[] XForHeight = { 200, 300, 480 };

    // x chosen so that (x * 6 / 512) + 1 lands on lane 5 (Flip).
    private const int FlipX = 380;

    public static void Export(
        BmsChart chart,
        BmsTiming timing,
        List<OutNote> notes,
        List<(double TimeMs, bool IsRealTurn)> channel18Events,
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

        // Merge real notes with the channel-18 flip events into one time-ordered stream.
        //  - IsRealTurn=true  -> hitSound=Normal, toggleCenter=false -> player.ChangeSide() fires
        //                        for real (the character actually turns), and updates currentSide.
        //  - IsRealTurn=false -> hitSound=Whistle, toggleCenter=true -> camera re-centers only,
        //                        side is untouched.
        // The automatic side-bookkeeping below (triggered by note.SideIndex mismatches) still runs
        // as a correctness safety net on top of whatever the manual "Flip" events already did.
        var timeline = new List<(double TimeMs, int Kind, OutNote Note)>(); // Kind: 0=note, 1=realTurn, 2=centerFlourish
        foreach (var note in notes) timeline.Add((note.TimeMs, 0, note));
        foreach (var ev in channel18Events) timeline.Add((ev.TimeMs, ev.IsRealTurn ? 1 : 2, default));

        // The game's own parser tracks "side" as a running toggle (starts at Right, flips on
        // every non-toggleCenter lane-5 hit), not as per-note data - so a note only lands on the
        // intended side if we emit exactly the Flip hits needed to match, immediately before it.
        int currentSide = 0; // 0 = Right (engine default), 1 = Left
        foreach (var entry in timeline.OrderBy(e => e.TimeMs))
        {
            if (entry.Kind == 2)
            {
                lines.Add(Inv($"{FlipX},192,{entry.TimeMs:F0},1,2,0:0:0:0:"));
                continue;
            }
            if (entry.Kind == 1)
            {
                lines.Add(Inv($"{FlipX},192,{entry.TimeMs:F0},1,0,0:0:0:0:"));
                currentSide = 1 - currentSide;
                continue;
            }

            var note = entry.Note;
            if (note.SideIndex != currentSide)
            {
                lines.Add(Inv($"{FlipX},192,{note.TimeMs:F0},1,0,0:0:0:0:"));
                currentSide = note.SideIndex;
            }

            int x = XForHeight[note.HeightIndex];
            int type = note.IsHoldType ? 128 : 1;
            string hs0 = note.IsBrawl ? "3" : "0";
            string tail = note.EndTimeMs.HasValue
                ? Inv($"{note.EndTimeMs.Value:F0}:{hs0}:0:0:0:")
                : $"{hs0}:0:0:0:";

            lines.Add(Inv($"{x},192,{note.TimeMs:F0},{type},{note.HitSound},") + tail);
        }

        string txtPath = Path.Combine(songFolder, difficultyName + ".txt");
        File.WriteAllText(txtPath, string.Join("\r\n", lines), new UTF8Encoding(false));
    }

    private static string Inv(FormattableString s) => s.ToString(CultureInfo.InvariantCulture);
}
