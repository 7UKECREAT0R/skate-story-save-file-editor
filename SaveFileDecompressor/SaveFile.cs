using System.IO.Compression;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace SaveFileDecompressor;

/// <summary>
/// A Skate Story save file.
/// </summary>
public class SaveFile
{
    public const string COMPRESSED_EXTENSION = ".sav";
    public const string UNCOMPRESSED_EXTENSION = ".json";
    public const string FILE = "skatestory_savedata0" + COMPRESSED_EXTENSION;
    public const string BACKUP_FILE_NAME = "skatestory_savedata0_backup{0}";

    public readonly string originPath;
    public readonly string originPathJSON;
    public readonly JObject saveData;
    public readonly Dictionary<string, Level> discoveredLevelsByGUID;
    public readonly Dictionary<string, Level> discoveredLevelsByName;
    public AchievementCounters achievementCounters;
    public readonly List<string> flags;
    public readonly int deckCount;
    public readonly double totalPlaytimeSeconds;
    public int deaths;
    public readonly int stumbles;

    public string CurrentHubWorldLevelId { get; private set; }
    public string CurrentSkateLevelId { get; private set; }
    public string LastSkateLevelId { get; private set; }
    public string LastJustClearedSkateLevelId { get; private set; }
    public Level ContinueLevel { get; private set; }
    public Level SinkholeLevel { get; private set; }

    private SaveFile(string originPath, JObject saveData)
    {
        this.originPath = originPath;
        this.originPathJSON = originPath.Replace(COMPRESSED_EXTENSION, UNCOMPRESSED_EXTENSION,
            StringComparison.OrdinalIgnoreCase);
        this.saveData = saveData;

        // register all the known levels
        this.discoveredLevelsByGUID = new Dictionary<string, Level>();
        this.discoveredLevelsByName = new Dictionary<string, Level>();
        var levelEntries = saveData.Value<JObject>("levels")!.Value<JArray>("entries")!;
        foreach (JToken levelEntry in levelEntries)
        {
            if (levelEntry is not JObject levelObject)
                continue;
            Level level = Level.FromJSON(levelObject);
            this.discoveredLevelsByGUID[level.GUID] = level;
            this.discoveredLevelsByName[level.Name] = level;
        }

        // get static data from the save file
        this.achievementCounters = AchievementCounters.FromJSON(saveData);
        this.flags = saveData.Value<JArray>("flags")!.Values<string>().ToList()!;
        this.deckCount = saveData.Value<JArray>("decks")!.Count;
        this.totalPlaytimeSeconds = saveData.Value<double>("totalPlaytime");
        this.deaths = saveData.Value<int>("deaths");
        this.stumbles = saveData.Value<int>("stumbles");

        // get the level-related stuff.
        this.CurrentHubWorldLevelId = saveData.Value<string>("currentHubWorldLevelId")!;
        this.CurrentSkateLevelId = saveData.Value<string>("currentSkateLevelId")!;
        this.LastSkateLevelId = saveData.Value<string>("lastSkateLevelId")!;
        this.LastJustClearedSkateLevelId = saveData.Value<string>("lastJustClearedSkateLevelId")!;
        string continueLevelGuid = saveData.Value<string>("continueLevelGuid")!;
        string sinkholeLevelGuid = saveData.Value<string>("sinkHoleLevelGuid")!;
        this.ContinueLevel = this.discoveredLevelsByGUID[continueLevelGuid];
        this.SinkholeLevel = this.discoveredLevelsByGUID[sinkholeLevelGuid];
    }
    /// <summary>
    /// Writes the save file's summarized details and debug information to the console.
    /// Displays information including deck count, total playtime, deaths, stumbles,
    /// achievement statistics, and key level identifiers.
    /// </summary>
    public void WriteToConsole()
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"- deck count: {this.deckCount:N0}");
        Console.WriteLine($"- total playtime: {(Math.Floor(this.totalPlaytimeSeconds / 60.0 / 60.0 * 10.0) / 10.0):N} hours");
        Console.WriteLine($"- deaths: {this.deaths:N0} / 500");
        Console.WriteLine($"- stumbles: {this.stumbles:N0}");
        Console.WriteLine($"- tricks performed: {this.achievementCounters.TricksPerformed:N0} / 10,000");
        Console.WriteLine($"- stickers placed: {this.achievementCounters.StickersPlaced:N0} / 100");
        Console.WriteLine($"- stickers bought: {this.achievementCounters.StickersBought:N0}");
        Console.WriteLine($"- 'Continue' level: {this.ContinueLevel.Name}");
        Console.WriteLine($"- sinkhole level: {this.SinkholeLevel.Name}");
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Gray;
        Console.WriteLine("Other debug stuff:");
        Console.WriteLine($"- currentHubWorldLevelId: {this.CurrentHubWorldLevelId}");
        Console.WriteLine($"- currentSkateLevelId: {this.CurrentSkateLevelId}");
        Console.WriteLine($"- lastSkateLevelId: {this.LastSkateLevelId}");
        Console.WriteLine($"- lastJustClearedSkateLevelId: {this.LastJustClearedSkateLevelId}");
        Console.WriteLine($"- discovered level count: {this.discoveredLevelsByGUID.Count}");
        Console.WriteLine($"- flag count: {this.flags.Count}");
        Console.ResetColor();
    }
    /// <summary>
    /// Obnoxious method which provides a TUI for picking a level.
    /// </summary>
    /// <param name="prompt">The prompt to show at the top.</param>
    /// <returns></returns>
    public Level? PickLevelTUI(string prompt)
    {
        const ConsoleKey UP_KEY = ConsoleKey.UpArrow;
        const ConsoleKey DOWN_KEY = ConsoleKey.DownArrow;
        const ConsoleKey LAST_PAGE_KEY = ConsoleKey.LeftArrow;
        const ConsoleKey NEXT_PAGE_KEY = ConsoleKey.RightArrow;
        const ConsoleKey ENTER_KEY = ConsoleKey.Enter;
        const ConsoleKey CANCEL_KEY = ConsoleKey.Escape;
        const int TOPBAR_LEN = 40;
        const int TOPBAR_PAD = 3;
        const int MAX_ENTRIES_SHOWN_PER_PAGE = 20;
        var topbarBuilder = new StringBuilder();
        topbarBuilder.Append('-', TOPBAR_PAD);
        topbarBuilder.Append(prompt);
        topbarBuilder.Append(' ');
        topbarBuilder.Append('-', TOPBAR_LEN - TOPBAR_PAD - prompt.Length - 1);
        string topbar = topbarBuilder.ToString();
        string fullbar = new('-', TOPBAR_LEN);
        string navbar = "---UP/DOWN pick --ENTER choose ---- ESC-";

        int page = 0;
        int selectedIndex = 0;
        var searchQuery = new StringBuilder();
        List<Level> filteredLevels = this.discoveredLevelsByGUID.Values.ToList();
        int filteredLevelCount = filteredLevels.Count;
        int pages = filteredLevelCount / MAX_ENTRIES_SHOWN_PER_PAGE +
                    (filteredLevelCount % MAX_ENTRIES_SHOWN_PER_PAGE == 0 ? 0 : 1);

        void Search()
        {
            filteredLevels.Clear();
            string searchQueryStr = searchQuery.ToString().Trim();
            if (string.IsNullOrEmpty(searchQueryStr))
                filteredLevels.AddRange(this.discoveredLevelsByGUID.Values);
            else
            {
                IEnumerable<Level> filtered = this.discoveredLevelsByGUID.Values.Where(level =>
                    level.Name.Contains(searchQueryStr, StringComparison.OrdinalIgnoreCase));
                filteredLevels.AddRange(filtered);
            }

            filteredLevelCount = filteredLevels.Count;
            pages = filteredLevelCount / MAX_ENTRIES_SHOWN_PER_PAGE +
                    (filteredLevelCount % MAX_ENTRIES_SHOWN_PER_PAGE == 0 ? 0 : 1);
        }

        while (true)
        {
            Console.Clear();
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine(topbar);
            Console.ResetColor();
            Console.Write("Search: ");
            if (searchQuery.Length == 0)
                Console.WriteLine("(type)");
            else
                Console.WriteLine(searchQuery);
            Console.WriteLine(fullbar);

            int startInclusive = page * MAX_ENTRIES_SHOWN_PER_PAGE;
            int endExclusive = startInclusive + MAX_ENTRIES_SHOWN_PER_PAGE;
            if (endExclusive > filteredLevelCount)
                endExclusive = filteredLevelCount;

            if (filteredLevelCount == 0)
            {
                Console.ForegroundColor = ConsoleColor.DarkRed;
                Console.WriteLine("No results were found.");
                Console.ResetColor();
            }
            else if (pages > 1)
                Console.WriteLine(
                    $"Showing {MAX_ENTRIES_SHOWN_PER_PAGE} results; page {page + 1}/{pages} (LEFT/RIGHT arrows to navigate)");
            else
                Console.WriteLine("Showing all results.");

            // draw all the entries
            Console.ForegroundColor = ConsoleColor.DarkGray;
            for (int i = startInclusive; i < endExclusive; i++)
            {
                Level level = filteredLevels[i];
                bool selected = i == selectedIndex;
                if (selected)
                {
                    Console.ForegroundColor = ConsoleColor.White;
                    Console.WriteLine("> {0} ({1})", level.Name, level.GUID);
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                }
                else
                    Console.WriteLine("{0} ({1})", level.Name, level.GUID);
            }

            // draw the navigation bar
            Console.ResetColor();
            Console.WriteLine(navbar);

            // get input
            ConsoleKeyInfo keyInfo = Console.ReadKey(true);
            ConsoleKey key = keyInfo.Key;

            if (filteredLevelCount > 0)
            {
                if (key == UP_KEY)
                {
                    selectedIndex--;
                    if (selectedIndex < startInclusive)
                        selectedIndex = endExclusive - 1;
                    continue;
                }

                if (key == DOWN_KEY)
                {
                    selectedIndex++;
                    if (selectedIndex >= endExclusive)
                        selectedIndex = startInclusive;
                    continue;
                }

                if (key == NEXT_PAGE_KEY)
                {
                    page++;
                    if (page >= pages)
                        page = 0;
                    selectedIndex = page * MAX_ENTRIES_SHOWN_PER_PAGE;
                    continue;
                }

                if (key == LAST_PAGE_KEY)
                {
                    page--;
                    if (page < 0)
                        page = pages - 1;
                    selectedIndex = page * MAX_ENTRIES_SHOWN_PER_PAGE;
                    continue;
                }

                if (key == ENTER_KEY)
                {
                    return filteredLevels[selectedIndex];
                }
            }

            if (key == CANCEL_KEY)
                return null;

            // searching support
            if (key == ConsoleKey.Backspace)
            {
                if (searchQuery.Length > 0)
                {
                    searchQuery.Remove(searchQuery.Length - 1, 1);
                    Search();
                }

                continue;
            }

            char keyChar = keyInfo.KeyChar;
            if (!char.IsControl(keyChar))
            {
                searchQuery.Append(keyChar);
                Search();
                continue;
            }
        }
    }

    /// <summary>
    /// Compresses the current save data into a byte array using the Brotli compression algorithm.
    /// </summary>
    /// <returns>
    /// A byte array containing the compressed version of the current save data.
    /// </returns>
    public byte[] Compress() => CompressFile(Encoding.UTF8.GetBytes(this.saveData.ToString(Formatting.None)));
    /// <summary>
    /// Converts the save file's data to its JSON string representation.
    /// </summary>
    /// <returns>A JSON string representing the save file's data.</returns>
    public string AsJSON() => this.saveData.ToString(Formatting.None);
    /// <summary>
    /// Converts the save file's data to its JSON string representation, but pretty.
    /// </summary>
    /// <returns>A JSON string representing the save file's data with formatting.</returns>
    public string AsJSONPretty() => this.saveData.ToString(Formatting.Indented);

    /// <summary>
    /// Loads a Skate Story save file from the specified file path.
    /// </summary>
    /// <param name="filePath">The path of the save file to load. It can be a compressed or uncompressed save file.</param>
    /// <returns>
    /// A <see cref="SaveFile"/> instance representing the loaded save file.
    /// </returns>
    /// <exception cref="FileNotFoundException">Thrown when the specified file does not exist.</exception>
    /// <exception cref="Exception">Thrown when the file has an unknown or unsupported extension.</exception>
    public static SaveFile LoadFromFile(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException("Not found: " + filePath);

        string extension = Path.GetExtension(filePath);
        byte[] uncompressedBytes;

        if (extension.Equals(COMPRESSED_EXTENSION, StringComparison.OrdinalIgnoreCase))
            uncompressedBytes = DecompressFile(File.ReadAllBytes(filePath));
        else if (extension.Equals(UNCOMPRESSED_EXTENSION, StringComparison.OrdinalIgnoreCase))
            uncompressedBytes = File.ReadAllBytes(filePath);
        else
            throw new Exception($"Unknown save file extension: \"{extension}\"");

        string jsonString = Encoding.UTF8.GetString(uncompressedBytes);
        JObject saveData = JObject.Parse(jsonString);
        return new SaveFile(filePath, saveData);
    }

    public void SetHubWorldLevel(Level level)
    {
        this.saveData["currentHubWorldLevelId"] = level.Name;
        this.CurrentHubWorldLevelId = level.Name;
    }
    public void SetSkateLevel(Level level)
    {
        this.saveData["currentSkateLevelId"] = level.Name;
        this.CurrentSkateLevelId = level.Name;
    }
    public void SetLastSkateLevel(Level level)
    {
        this.saveData["lastSkateLevelId"] = level.Name;
        this.LastSkateLevelId = level.Name;
    }
    public void SetLastJustClearedSkateLevel(Level level)
    {
        this.saveData["lastJustClearedSkateLevelId"] = level.Name;
        this.LastJustClearedSkateLevelId = level.Name;
    }
    public void SetContinueLevel(Level level)
    {
        this.saveData["continueLevelId"] = level.Name;
        this.saveData["continueLevelGuid"] = level.GUID;
        this.ContinueLevel = level;
    }
    public void SetSinkholeLevel(Level level)
    {
        this.saveData["sinkHoleLevelId"] = level.Name;
        this.saveData["sinkHoleLevelGuid"] = level.GUID;
        this.SinkholeLevel = level;
    }
    /// <summary>
    /// Reverts the save file state to just before the SKATE STORY's epilogue.
    /// All of this is just speculation and guessing by looking over my own save file, but it does seem
    /// to work fine and lets you replay the final boss when you're ready.      -lukecreator
    /// </summary>
    public void RevertPostEpilogue()
    {
        // fix flags
        this.flags.Remove("ch10-game-done");
        this.saveData["flags"] = new JArray(this.flags.Cast<object>().ToArray());

        // fix levels
        Level prebossFlare = this.discoveredLevelsByName["ch9-13-preboss-flare"];
        Level burndream = this.discoveredLevelsByName["ch9-14-burndream"];
        SetContinueLevel(prebossFlare);
        SetSinkholeLevel(burndream);

        // fix vars
        (int Index, JObject Item)[] vars = this.saveData.Value<JObject>("dict")!.Value<JArray>("entries")!
            .Cast<JObject>().Index().ToArray();
        SetVar("var-ch9centipedeshaking", "0");
        SetVar("ch10-0CHAPTER TITLE V2", "0");
        SetVar("ch10-15-slug>> intro", "0");
        SetVar("ch10-15-slug>>01 intro", "0");
        SetVar("ch10-15-slug>> get skateboard", "0");
        SetVar("ch10-poem>> poem cutscene", "0");
        SetVar("ch9-14-burndream>>01 intro", "0");
        SetVar("ch9-14-burndream>> beam", "0");

        return;

        // sets a var in the 'dict.entries' array if it's there
        void SetVar(string key, JToken value)
        {
            (int Index, JObject Item) matchingVar = vars.FirstOrDefault(pair => pair.Item.Value<string>("key")!.Equals(key));
            if (matchingVar.Item != null)
            {
                this.saveData["dict"]!["entries"]![matchingVar.Index]!["val"] = value;
            }
        }
    }
    public void SetDeaths(int newDeaths)
    {
        this.saveData["deaths"] = newDeaths;
        this.saveData.Value<JObject>("achievementCounters")!["playerWreckedCount"] = newDeaths;
        this.deaths = newDeaths;
        this.achievementCounters.PlayerWreckedCount = newDeaths;
    }
    public void SetStickersPlaced(int newStickersPlaced)
    {
        this.saveData.Value<JObject>("achievementCounters")!["stickersPlaced"] = newStickersPlaced;
        this.saveData.Value<JObject>("achievementCounters")!["stickersBought"] = newStickersPlaced - 18;
        this.achievementCounters.StickersPlaced = newStickersPlaced;
        this.achievementCounters.StickersBought = newStickersPlaced - 18;
    }
    public void SetTricksPerformed(int newTricksPerformed)
    {
        this.saveData.Value<JObject>("achievementCounters")!["tricksPerformed"] = newTricksPerformed;
        this.achievementCounters.TricksPerformed = newTricksPerformed;

        // check and see if the "pop" counters are still valid
        JToken? popTotalProperty = this.saveData["pop_total"];
        if (popTotalProperty == null || popTotalProperty.Value<int>() < newTricksPerformed)
        {
            // update'em
            int popTotalNew = newTricksPerformed + 162;
            this.saveData["pop_total"] = popTotalNew;
            this.saveData["pop_fakie"] = 0;
            this.saveData["pop_normal"] = popTotalNew; // kinda screws stats up, but whatever; nobody will see it anyway
            this.saveData["pop_switch"] = 0;
            this.saveData["pop_nollie"] = 0;
        }
    }

    #region PathStuff

    /// <summary>
    /// Gets the path to the LocalLow directory, which is a subdirectory of the AppData folder.
    /// </summary>
    private static string LocalLow => Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + "Low";
    /// <summary>
    /// Retrieves the file path for the Skate Story save file if it exists.
    /// </summary>
    /// <returns>
    /// A string representing the full path to the default save file if it exists; otherwise, <see langword="null"/>.
    /// </returns>
    internal static string? FindSaveFilePath()
    {
        // "AppData\LocalLow\by Sam Eng\SKATE STORY"
        string appDataPath = Path.Combine(LocalLow, "by Sam Eng", "SKATE STORY");
        string saveFilePath = Path.Combine(appDataPath, FILE);
        return File.Exists(saveFilePath) ? saveFilePath : null;
    }

    #endregion

    #region BrotliCompressionStuff

    /// <summary>
    /// Compresses the provided byte array using the Brotli compression algorithm.
    /// </summary>
    /// <param name="decompressedBytes">The byte array containing the uncompressed data to be compressed.</param>
    /// <returns>
    /// A byte array containing the compressed data.
    /// </returns>
    private static byte[] CompressFile(byte[] decompressedBytes)
    {
        using var memoryStream = new MemoryStream();
        using (var brotliStream = new BrotliStream(memoryStream, CompressionLevel.Fastest))
            brotliStream.Write(decompressedBytes, 0, decompressedBytes.Length);
        return memoryStream.ToArray();
    }
    /// <summary>
    /// Decompresses the provided byte array using the Brotli compression algorithm.
    /// </summary>
    /// <param name="compressedBytes">The byte array containing the compressed data to be decompressed.</param>
    /// <returns>
    /// A byte array containing the decompressed data.
    /// </returns>
    private static byte[] DecompressFile(byte[] compressedBytes)
    {
        using var memoryStream = new MemoryStream(compressedBytes);
        using var destination = new MemoryStream();
        using var brotliStream = new BrotliStream(memoryStream, CompressionMode.Decompress);
        brotliStream.CopyTo(destination);
        return destination.ToArray();
    }

    #endregion
}

public struct AchievementCounters
{
    public int StickersBought { get; internal set; }
    public int StickersPlaced { get; internal set; }
    public int TricksPerformed { get; internal set; }
    public int PlayerWreckedCount { get; internal set; }

    private AchievementCounters(int stickersBought, int stickersPlaced, int tricksPerformed, int playerWreckedCount)
    {
        this.StickersBought = stickersBought;
        this.StickersPlaced = stickersPlaced;
        this.TricksPerformed = tricksPerformed;
        this.PlayerWreckedCount = playerWreckedCount;
    }
    public static AchievementCounters FromJSON(JObject saveFileData)
    {
        if (saveFileData["achievementCounters"] is not JObject countersObject)
            throw new Exception("Unexpected save file format. Missing achievementCounters object.");

        return new AchievementCounters(
            countersObject.Value<int>("stickersBought"),
            countersObject.Value<int>("stickersPlaced"),
            countersObject.Value<int>("tricksPerformed"),
            countersObject.Value<int>("playerWreckedCount")
        );
    }
}

public struct Level
{
    public string Name { get; init; }
    public string GUID { get; init; }
    public Level(string name, string guid)
    {
        this.Name = name;
        this.GUID = guid;
    }
    public static Level FromJSON(JObject levelEntry)
    {
        if (levelEntry["val"] is not JObject valObject)
            throw new Exception("Unexpected save file format. Missing 'val' object on input level: " + levelEntry);
        return new Level(
            valObject.Value<string>("id")!,
            valObject.Value<string>("guid")!
        );
    }
}