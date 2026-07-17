using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BmsToUnbeatable;
using MelonLoader;
using MelonLoader.Utils;
using UnityEngine;

namespace UnbeatableCustomMode;

/// <summary>
/// On startup, scans "&lt;GameRoot&gt;/hwa/&lt;songName&gt;/" folders for a BMS chart + audio file,
/// converts each one into the game's native CustomSongs format, and drops it into
/// Application.persistentDataPath/CustomSongs so the untouched ArcadeSongDatabase pipeline picks it up
/// (see Patches.cs for the Harmony patch that makes that pipeline run without "-customsongs").
/// </summary>
public static class HwaSongLoader
{
    private static readonly string[] BmsExtensions = { ".bms", ".bme", ".bml" };
    private static readonly string[] AudioExtensions = { ".ogg", ".mp3", ".wav" };

    // channel -> (Side: 0=Right, 1=Left; Height: 0=Top, 1=Low, 2=Mid)
    private static readonly Dictionary<string, (int Side, int Height)> ChannelMap = new()
    {
        { "16", (0, 0) }, // Right, Top
        { "11", (0, 2) }, // Right, Mid
        { "12", (0, 1) }, // Right, Low
        { "13", (1, 0) }, // Left, Top
        { "14", (1, 2) }, // Left, Mid
        { "15", (1, 1) }, // Left, Low
    };

    // WAV filename (minus "_end"/" end" suffix and extension) -> (IsHold, HitSound bits, IsBrawl).
    // HitSound values match HitObjectInfo.HitSound: 0=Normal, 2=Whistle, 4=Finish.
    // Hold/Double/Spam carry an end time, so consecutive same-type hits in the same lane are
    // paired start->end by alternation (matches muse-dash-test's BmsNoteMatcher convention).
    private static readonly Dictionary<string, (bool IsHold, int HitSound, bool IsBrawl)> TypeEncoding =
        new(StringComparer.OrdinalIgnoreCase)
    {
        { "Default",   (false, 0, false) },
        { "Dodge",     (false, 2, false) },
        { "Setpiece",  (false, 4, false) },
        { "Hold",      (true,  0, false) },
        { "Double",    (true,  2, false) },
        { "Spam",      (true,  4, false) },   // Mid-lane only
        { "Freestyle", (false, 0, false) },   // Mid-lane only
        { "Nothing",   (false, 2, false) },   // Mid-lane only
        { "Brawl",     (false, 0, true) },
    };

    public static void ConvertAll(MelonLogger.Instance logger)
    {
        string hwaRoot = Path.Combine(MelonEnvironment.GameRootDirectory, "hwa");
        Directory.CreateDirectory(hwaRoot);

        string customSongsRoot = Path.Combine(Application.persistentDataPath, "CustomSongs");
        Directory.CreateDirectory(customSongsRoot);

        foreach (var songDir in Directory.GetDirectories(hwaRoot))
        {
            try
            {
                ConvertOne(songDir, customSongsRoot, logger);
            }
            catch (Exception ex)
            {
                logger.Error($"Failed to convert '{songDir}': {ex}");
            }
        }
    }

    private static void ConvertOne(string songDir, string customSongsRoot, MelonLogger.Instance logger)
    {
        string songName = Path.GetFileName(songDir);

        string bmsPath = Directory.GetFiles(songDir)
            .FirstOrDefault(f => BmsExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()));
        if (bmsPath == null)
        {
            return;
        }

        string audioPath = Directory.GetFiles(songDir)
            .Where(f => AudioExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
            .OrderByDescending(f => Path.GetFileNameWithoutExtension(f).Equals("music", StringComparison.OrdinalIgnoreCase))
            .FirstOrDefault();
        if (audioPath == null)
        {
            logger.Warning($"'{songName}' has a chart but no audio file (.ogg/.mp3/.wav) next to it - skipped.");
            return;
        }

        var chart = BmsParser.Parse(bmsPath);
        var timing = new BmsTiming(chart);
        var notes = BuildNotes(chart, timing, songName, logger);
        var channel18Events = CollectChannel18Events(chart, timing, songName, logger);
        if (notes.Count == 0)
        {
            logger.Warning($"'{songName}' converted to 0 notes (cell width detected: {chart.CellWidth}) - check that it uses channels 11/12/16 (Right) or 13/14/15 (Left) with #WAVxxx names matching a known note type.");
            return;
        }

        string outDir = Path.Combine(customSongsRoot, songName);
        Directory.CreateDirectory(outDir);

        string audioFileName = Path.GetFileName(audioPath);
        File.Copy(audioPath, Path.Combine(outDir, audioFileName), overwrite: true);

        string coverSrc = Path.Combine(songDir, "cover.png");
        if (File.Exists(coverSrc))
        {
            File.Copy(coverSrc, Path.Combine(outDir, "cover.png"), overwrite: true);
        }

        // ArcadeSongDatabase.LoadCustoms() looks for these exact filenames to wire up a BGA video.
        foreach (var videoName in new[] { "video.mp4", "video.webm" })
        {
            string videoSrc = Path.Combine(songDir, videoName);
            if (File.Exists(videoSrc))
            {
                File.Copy(videoSrc, Path.Combine(outDir, videoName), overwrite: true);
            }
        }

        string creator = string.IsNullOrWhiteSpace(chart.Artist) ? "hwa" : chart.Artist;
        UnbeatableExporter.Export(chart, timing, notes, channel18Events, outDir, audioFileName, "Hard", creator);

        int realTurns = channel18Events.Count(e => e.IsRealTurn);
        int centerFlourishes = channel18Events.Count - realTurns;
        logger.Msg($"Converted '{songName}' ({notes.Count} notes, {realTurns} real turns, {centerFlourishes} center-camera flourishes, {chart.MaxMeasure + 1} measures) -> {outDir}");
    }

    // Channel 18 carries two distinct WAV references:
    //  - "Flip"    -> a REAL flip (player.ChangeSide() actually fires - the character turns for real)
    //  - "Nothing" -> a toggleCenter flip (camera re-centers, but side does NOT change)
    private static List<(double TimeMs, bool IsRealTurn)> CollectChannel18Events(BmsChart chart, BmsTiming timing, string songName, MelonLogger.Instance logger)
    {
        var events = new List<(double TimeMs, bool IsRealTurn)>();
        for (int measure = 0; measure <= chart.MaxMeasure; measure++)
        {
            var tokens = chart.GetChannel(measure, "18").ToList();
            for (int i = 0; i < tokens.Count; i++)
            {
                string token = tokens[i];
                if (BmsChart.IsEmptyToken(token)) continue;

                string name = chart.WavDefs.TryGetValue(token, out var filename)
                    ? Path.GetFileNameWithoutExtension(filename).Trim()
                    : null;

                bool? isRealTurn = name switch
                {
                    _ when string.Equals(name, "Flip", StringComparison.OrdinalIgnoreCase) => true,
                    _ when string.Equals(name, "Nothing", StringComparison.OrdinalIgnoreCase) => false,
                    _ => null
                };

                if (isRealTurn == null)
                {
                    logger.Warning($"'{songName}': channel 18 hit references '{name ?? token}', expected 'Flip' or 'Nothing' - skipped.");
                    continue;
                }

                events.Add((timing.GetTime(measure, (double)i / tokens.Count), isRealTurn.Value));
            }
        }
        return events;
    }

    private readonly record struct RawHit(double TimeMs, int Height, int Side, string BaseType);

    private static List<OutNote> BuildNotes(BmsChart chart, BmsTiming timing, string songName, MelonLogger.Instance logger)
    {
        var raw = new List<RawHit>();
        for (int measure = 0; measure <= chart.MaxMeasure; measure++)
        {
            foreach (var entry in ChannelMap)
            {
                string channel = entry.Key;
                int side = entry.Value.Side;
                int height = entry.Value.Height;
                var tokens = chart.GetChannel(measure, channel).ToList();
                for (int i = 0; i < tokens.Count; i++)
                {
                    string token = tokens[i];
                    if (BmsChart.IsEmptyToken(token)) continue;
                    if (!chart.WavDefs.TryGetValue(token, out var filename))
                    {
                        logger.Warning($"'{songName}': channel {channel} references undefined #WAV{token} - skipped.");
                        continue;
                    }

                    string name = Path.GetFileNameWithoutExtension(filename).Trim();
                    if (name.EndsWith(" end", StringComparison.OrdinalIgnoreCase))
                    {
                        name = name.Substring(0, name.Length - " end".Length).Trim();
                    }

                    double t = timing.GetTime(measure, (double)i / tokens.Count);
                    raw.Add(new RawHit(t, height, side, name));
                }
            }
        }
        raw.Sort((a, b) => a.TimeMs.CompareTo(b.TimeMs));

        var pendingStart = new Dictionary<(int Side, int Height, string BaseType), RawHit>();
        var outNotes = new List<OutNote>();

        foreach (var hit in raw)
        {
            if (!TypeEncoding.TryGetValue(hit.BaseType, out var enc))
            {
                logger.Warning($"'{songName}': unknown note type WAV name '{hit.BaseType}' - skipped.");
                continue;
            }

            if (enc.IsHold)
            {
                var key = (hit.Side, hit.Height, hit.BaseType);
                if (pendingStart.TryGetValue(key, out var start))
                {
                    outNotes.Add(new OutNote
                    {
                        TimeMs = start.TimeMs,
                        HeightIndex = start.Height,
                        SideIndex = start.Side,
                        IsHoldType = true,
                        HitSound = enc.HitSound,
                        IsBrawl = false,
                        EndTimeMs = hit.TimeMs
                    });
                    pendingStart.Remove(key);
                }
                else
                {
                    pendingStart[key] = hit;
                }
                continue;
            }

            outNotes.Add(new OutNote
            {
                TimeMs = hit.TimeMs,
                HeightIndex = hit.Height,
                SideIndex = hit.Side,
                IsHoldType = false,
                HitSound = enc.HitSound,
                IsBrawl = enc.IsBrawl,
                EndTimeMs = null
            });
        }

        foreach (var kvp in pendingStart)
        {
            logger.Warning($"'{songName}': unmatched '{kvp.Key.BaseType}' start with no paired end at {kvp.Value.TimeMs:F0}ms (Side={kvp.Key.Side},Height={kvp.Key.Height}) - dropped.");
        }

        return outNotes.OrderBy(n => n.TimeMs).ToList();
    }
}
