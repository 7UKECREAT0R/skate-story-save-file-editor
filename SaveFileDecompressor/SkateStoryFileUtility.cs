using System.Diagnostics;
using System.IO.Compression;

namespace SaveFileDecompressor;

internal static class SkateStoryFileUtility
{
    static void Main(string[] args)
    {
        WriteLine("SKATE STORY Savefile Tool", ConsoleColor.Red);
        WriteLine("\tby lukecreator", ConsoleColor.Gray);
        Console.WriteLine();

        bool saveFileIsCompressed, saveFileIsJSON;
        string? gameDirectory;
        string? saveFilePath;

        WriteLine("[A] Auto-Detect Save File", ConsoleColor.Green);
        WriteLine("[M] Specify Save File Manually", ConsoleColor.Green);

        while (true)
        {
            ConsoleKeyInfo keyInfo = Console.ReadKey(true);
            ConsoleKey key = keyInfo.Key;
            if (key == ConsoleKey.A)
            {
                saveFilePath = SaveFile.FindSaveFilePath();
                if (saveFilePath == null)
                {
                    WriteLine("Couldn't auto-detect the SKATE STORY save file.", ConsoleColor.Red);
                    continue;
                }

                saveFileIsCompressed = true;
                saveFileIsJSON = false;
                gameDirectory = Path.GetDirectoryName(saveFilePath);
                WriteLine("Found SKATE STORY save file at: " + saveFilePath, ConsoleColor.Green);
                break;
            }

            if (key == ConsoleKey.M)
            {
                WriteLine(@"Usually: %appdata%\..\LocalLow\by Sam Eng\SKATE STORY\" + SaveFile.FILE, ConsoleColor.Gray);
                WriteLine("Input the path to the savefile:", ConsoleColor.White);
                saveFilePath = Console.ReadLine()!.Trim('"', ' ', '\t');

                string extension = Path.GetExtension(saveFilePath);
                saveFileIsCompressed = extension.Equals(SaveFile.COMPRESSED_EXTENSION, StringComparison.OrdinalIgnoreCase);
                saveFileIsJSON = extension.Equals(SaveFile.UNCOMPRESSED_EXTENSION, StringComparison.OrdinalIgnoreCase);

                if (!File.Exists(saveFilePath))
                {
                    WriteLine("The provided file doesn't exist. Please try again.", ConsoleColor.Red);
                    continue;
                }

                if (!saveFileIsCompressed && !saveFileIsJSON)
                {
                    WriteLine($"The input file needs to be either a {SaveFile.COMPRESSED_EXTENSION} or a {SaveFile.UNCOMPRESSED_EXTENSION} file.", ConsoleColor.Red);
                    continue;
                }

                string saveFileType = saveFileIsCompressed ? "(compressed)" : "(json)";
                WriteLine($"Using savefile {saveFileType}: " + saveFilePath, ConsoleColor.Green);
                gameDirectory = null; // this directory might not be the actual game directory
                break;
            }
        }

        string saveFilePathNoExtension = saveFilePath[..saveFilePath.LastIndexOf('.')];
        string saveFilePathCompressed = saveFilePathNoExtension + SaveFile.COMPRESSED_EXTENSION;
        string saveFilePathJSON = saveFilePathNoExtension + SaveFile.UNCOMPRESSED_EXTENSION;

        // make a backup so we don't screw anything up
        string backupFileName = string.Format(SaveFile.BACKUP_FILE_NAME, DateTime.Now.ToString("yyyyMMddHH-mm-ss"));
        backupFileName += saveFileIsCompressed ? SaveFile.COMPRESSED_EXTENSION : SaveFile.UNCOMPRESSED_EXTENSION;
        string backupFilePath = Path.Combine(Path.GetDirectoryName(saveFilePath)!, backupFileName);
        WriteLine($"Creating backup of it... ({backupFilePath})", ConsoleColor.Gray);
        File.Copy(saveFilePath, backupFilePath, false);
        WriteLine("Backup created. Thank me later!", ConsoleColor.Green);
        SaveFile file = SaveFile.LoadFromFile(saveFilePath);

        bool firstRun = true;

        while (true)
        {
            if(!firstRun)
                Console.Clear();
            firstRun = false;
            WriteLine("---save file info ----------------------------", ConsoleColor.Gray);
            file.WriteToConsole();
            WriteLine("---operations --------------------------------", ConsoleColor.Gray);
            WriteLine("[S]         Save as .SAV", ConsoleColor.Cyan);
            WriteLine("[Shift + S] Save as .JSON", ConsoleColor.Cyan);
            if(gameDirectory != null)
                WriteLine("[O] Open Game Directory", ConsoleColor.White);
            WriteLine("[ESC] Exit Without Saving", ConsoleColor.White);
            Console.WriteLine();
            WriteLine("[A] Change Achievement Counter/Stat", ConsoleColor.White);
            WriteLine("[R] Revert to Post-Epilogue", ConsoleColor.White);
            WriteLine("[L]         Change Level", ConsoleColor.White);
            WriteLine("[Shift + L] Change Sinkhole Level", ConsoleColor.Gray);
            WriteLine("[H]         Change Hub World Level (unknown what this does)", ConsoleColor.DarkGray);
            WriteLine("[Shift + H] Change Skate Level (unknown what this does)", ConsoleColor.DarkGray);

            ConsoleKeyInfo keyInfo = Console.ReadKey(true);
            ConsoleKey key = keyInfo.Key;
            bool shift = keyInfo.Modifiers.HasFlag(ConsoleModifiers.Shift);

            if (key == ConsoleKey.S)
            {
                if (shift)
                {
                    string json = file.AsJSONPretty();
                    File.WriteAllText(saveFilePathJSON, json);
                } else
                {
                    byte[] bytes = file.Compress();
                    File.WriteAllBytes(saveFilePathCompressed, bytes);
                }

                WriteLine("Changes saved!", ConsoleColor.Green);
                Thread.Sleep(1000);
                continue;
            }

            if(key == ConsoleKey.O && gameDirectory != null)
            {
                Process.Start("explorer.exe", gameDirectory);
                continue;
            }

            if (key == ConsoleKey.A)
            {
                Console.WriteLine("----------------------------------------------");
                WriteLine($"[D] Deaths: {file.achievementCounters.PlayerWreckedCount:N0} (\"YOU MUST SKATE\" achievement)", ConsoleColor.White);
                WriteLine($"[T] Tricks: {file.achievementCounters.TricksPerformed:N0} (\"Over Several Eternities\" achievement)", ConsoleColor.White);
                WriteLine($"[S] Stickers Placed: {file.achievementCounters.StickersPlaced:N0} (\"Stickerbook\" achievement)", ConsoleColor.White);
                WriteLine("[ESC/Other] Cancel", ConsoleColor.White);
                Console.WriteLine("----------------------------------------------");
                keyInfo = Console.ReadKey(true);
                key = keyInfo.Key;

                if (key == ConsoleKey.D)
                {
                    Console.Write("New death count: ");
                    string newDeathsStr = Console.ReadLine()!.Trim();
                    if (int.TryParse(newDeathsStr, out int newDeaths))
                        file.SetDeaths(newDeaths);
                }

                if (key == ConsoleKey.T)
                {
                    Console.Write("New trick count: ");
                    string newTricksStr = Console.ReadLine()!.Trim();
                    if (int.TryParse(newTricksStr, out int newTricks))
                        file.SetTricksPerformed(newTricks);
                }

                if (key == ConsoleKey.S)
                {
                    Console.Write("New stickers-placed count: ");
                    string newStickersStr = Console.ReadLine()!.Trim();
                    if (int.TryParse(newStickersStr, out int newStickers))
                        file.SetStickersPlaced(newStickers);
                }

                continue;
            }

            if (key == ConsoleKey.L)
            {
                if (shift)
                {
                    Level? newLevel = file.PickLevelTUI("Pick Sinkhole Level");
                    if (newLevel.HasValue)
                        file.SetSinkholeLevel(newLevel.Value);
                    continue;
                }
                else
                {
                    Level? newLevel = file.PickLevelTUI("Pick Level");
                    if (newLevel.HasValue)
                        file.SetContinueLevel(newLevel.Value);
                }
            }

            if (key == ConsoleKey.H)
            {
                if (shift)
                {
                    Level? newSkateLevel = file.PickLevelTUI("Pick Skate Level");
                    if (newSkateLevel.HasValue)
                        file.SetSkateLevel(newSkateLevel.Value);
                }
                else
                {
                    Level? newHubLevel = file.PickLevelTUI("Pick Hub Level");
                    if (newHubLevel.HasValue)
                        file.SetHubWorldLevel(newHubLevel.Value);
                }
            }

            if (key == ConsoleKey.R)
            {
                // try to fix the savefile to be located just before the epilogue
                file.RevertPostEpilogue();
                WriteLine("Reverted savefile. Press [S] to save changes!", ConsoleColor.Green);
                Thread.Sleep(2000);
                continue;
            }

            if (key == ConsoleKey.Escape)
            {
                break;
            }
        }
    }

    /// <summary>
    /// It's like <see cref="Console.WriteLine(string)"/>, but with a custom color! Resets the console's color to the default afterward.
    /// </summary>
    /// <param name="message">The message to display.</param>
    /// <param name="color">The ConsoleColor to use for the duration of the message.</param>
    private static void WriteLine(string message, ConsoleColor color)
    {
        Console.ForegroundColor = color;
        Console.WriteLine(message);
        Console.ResetColor();
    }
    private static void WriteLine() => Console.WriteLine();
}