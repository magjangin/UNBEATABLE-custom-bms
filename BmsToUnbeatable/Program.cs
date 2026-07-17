using BmsToUnbeatable;

if (args.Length < 3)
{
    Console.WriteLine("BmsToUnbeatable - converts a .bms/.bme/.bml chart into an UNBEATABLE custom-song folder.");
    Console.WriteLine();
    Console.WriteLine("Usage:");
    Console.WriteLine("  BmsToUnbeatable <chart.bms> <audioFile> <songFolderName> [--out <customsongsRoot>] [--difficulty Hard] [--creator name]");
    Console.WriteLine();
    Console.WriteLine("  <chart.bms>       Path to the source BMS/BME/BML file.");
    Console.WriteLine("  <audioFile>       Path to the full song audio (mp3/ogg/wav) - BMS keysounds are NOT mixed down automatically.");
    Console.WriteLine("  <songFolderName>  Folder name to create under the customsongs root.");
    Console.WriteLine();
    Console.WriteLine("  Default --out is %UserProfile%\\AppData\\LocalLow\\D-CELL GAMES\\UNBEATABLE\\customsongs");
    Console.WriteLine("  Known-valid --difficulty values (seen in game strings): Beginner, Easy, Normal, Hard, Expert.");
    return 1;
}

string bmsPath = args[0];
string audioPath = args[1];
string songFolderName = args[2];

string? outRoot = null;
string difficulty = "Hard";
string creator = "";

for (int i = 3; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--out" when i + 1 < args.Length:
            outRoot = args[++i];
            break;
        case "--difficulty" when i + 1 < args.Length:
            difficulty = args[++i];
            break;
        case "--creator" when i + 1 < args.Length:
            creator = args[++i];
            break;
    }
}

if (!File.Exists(bmsPath))
{
    Console.Error.WriteLine($"BMS file not found: {bmsPath}");
    return 1;
}
if (!File.Exists(audioPath))
{
    Console.Error.WriteLine($"Audio file not found: {audioPath}");
    return 1;
}

outRoot ??= Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
    "AppData", "LocalLow", "D-CELL GAMES", "UNBEATABLE", "customsongs");

Console.WriteLine($"Parsing {bmsPath} ...");
var chart = BmsParser.Parse(bmsPath);
Console.WriteLine($"  Title: {chart.Title}  Artist: {chart.Artist}  Initial BPM: {chart.InitialBpm}");

if (string.IsNullOrWhiteSpace(creator))
    creator = string.IsNullOrWhiteSpace(chart.Artist) ? "BmsToUnbeatable" : chart.Artist;

var timing = new BmsTiming(chart);

// Gather distinct key indices (P1 11-19 and P2 21-29 folded onto the same key index) that
// actually contain notes, then bucket them cyclically into Top/Low/Mid per "simple height split".
var noteChannels = new List<string>();
for (int ch = 11; ch <= 19; ch++) noteChannels.Add(ch.ToString());
for (int ch = 21; ch <= 29; ch++) noteChannels.Add(ch.ToString());

var presentKeys = new SortedSet<int>();
for (int measure = 0; measure <= chart.MaxMeasure; measure++)
{
    foreach (var channel in noteChannels)
    {
        int key = int.Parse(channel) % 10;
        foreach (var token in chart.GetChannel(measure, channel))
        {
            if (token != "00") { presentKeys.Add(key); break; }
        }
    }
}

if (presentKeys.Count == 0)
{
    Console.Error.WriteLine("No notes found on channels 11-19 / 21-29 - is this a valid key-mode BMS file?");
    return 1;
}

var keyList = presentKeys.ToList();
var keyToHeight = new Dictionary<int, int>();
for (int i = 0; i < keyList.Count; i++)
    keyToHeight[keyList[i]] = i % 3; // 0=Top, 1=Low, 2=Mid

Console.WriteLine($"  Keys in use: {string.Join(",", keyList)}  -> heights: {string.Join(",", keyList.Select(k => "TLM"[keyToHeight[k]]))}");

var notes = new List<(int Measure, double Fraction, int HeightIndex)>();
for (int measure = 0; measure <= chart.MaxMeasure; measure++)
{
    foreach (var channel in noteChannels)
    {
        int key = int.Parse(channel) % 10;
        if (!keyToHeight.TryGetValue(key, out int heightIndex)) continue;
        var tokens = chart.GetChannel(measure, channel).ToList();
        for (int i = 0; i < tokens.Count; i++)
        {
            if (tokens[i] == "00") continue;
            notes.Add((measure, (double)i / tokens.Count, heightIndex));
        }
    }
}

Console.WriteLine($"  Converted {notes.Count} notes across {chart.MaxMeasure + 1} measures.");

string songFolder = Path.Combine(outRoot, songFolderName);
Directory.CreateDirectory(songFolder);

string audioFileName = Path.GetFileName(audioPath);
string audioDest = Path.Combine(songFolder, audioFileName);
File.Copy(audioPath, audioDest, overwrite: true);

UnbeatableExporter.Export(chart, timing, notes, songFolder, audioFileName, difficulty, creator);

Console.WriteLine();
Console.WriteLine($"Done -> {songFolder}");
Console.WriteLine($"  {difficulty}.txt + {audioFileName}");
Console.WriteLine();
Console.WriteLine("To make the game load it: add the Steam launch option \"-customsongs\" to UNBEATABLE");
Console.WriteLine("(the customsongs folder already exists, so that's the only remaining gate).");
Console.WriteLine();
Console.WriteLine("Known limitations of this v1 converter:");
Console.WriteLine("  - Long/hold notes (BMS LN) are not converted yet - all notes become instant taps.");
Console.WriteLine("  - Scratch is folded into the same 3-height bucket as regular keys, not treated specially.");
Console.WriteLine("  - Flip (side-swap) events are never emitted; the whole chart plays on one side.");
return 0;
