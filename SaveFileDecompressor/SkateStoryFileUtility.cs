using System.IO.Compression;

namespace SaveFileDecompressor;

internal static class SkateStoryFileUtility
{
    static void Main(string[] args)
    {
        WriteLine("SKATE STORY Savefile Tool", ConsoleColor.Red);
        WriteLine("\tby lukecreator", ConsoleColor.Gray);
        Console.WriteLine();

        string? saveFilePath = SaveFile.GetSaveFilePath();

        while (saveFilePath == null)
        {
            WriteLine("Couldn't auto-detect the SKATE STORY save file (dang it). Please input it here:", ConsoleColor.Yellow);
            WriteLine(@"It's supposed to be in %appdata%\..\LocalLow\by Sam Eng\SKATE STORY\" + SaveFile.FILE_NAME, ConsoleColor.DarkGray);
            saveFilePath = Console.ReadLine();

            if (!File.Exists(saveFilePath))
            {
                WriteLine("The provided file path doesn't exist. Try once more.", ConsoleColor.White);
                saveFilePath = null;
            }
        }

        WriteLine("Found SKATE STORY save file at: " + saveFilePath, ConsoleColor.Green);

        // make a backup so we don't screw anything up
        string backupFileName = string.Format(SaveFile.BACKUP_FILE_NAME, DateTime.Now.ToString("yyyyMMddHH-mm-ss"));
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
            WriteLine("[S] Save Changes", ConsoleColor.Cyan);
            WriteLine("[A] Change Achievement Counter/Stat", ConsoleColor.Yellow);
            WriteLine("[R] Revert to Post-Epilogue", ConsoleColor.Yellow);
            WriteLine("[L] Change Level", ConsoleColor.White);
            WriteLine("[Shift + L] Change Sinkhole Level", ConsoleColor.White);
            WriteLine("[H] Change Hub World Level", ConsoleColor.White);
            WriteLine("[Shift + H] Change Skate Level", ConsoleColor.White);
            WriteLine("[ESC] Exit Without Saving", ConsoleColor.White);

            ConsoleKeyInfo keyInfo = Console.ReadKey(true);
            ConsoleKey key = keyInfo.Key;
            bool shift = keyInfo.Modifiers.HasFlag(ConsoleModifiers.Shift);

            if (key == ConsoleKey.S)
            {
                byte[] bytes = file.Compress();
                File.WriteAllBytes(saveFilePath, bytes);
                WriteLine("Changes saved!", ConsoleColor.Green);
                Thread.Sleep(1000);
                continue;
            }

            if (key == ConsoleKey.A)
            {
                Console.WriteLine("----------------------------------------------");
                WriteLine($"[D] Deaths: {file.achievementCounters.PlayerWreckedCount} (\"YOU MUST SKATE\" achievement)", ConsoleColor.White);
                WriteLine($"[T] Tricks: {file.achievementCounters.TricksPerformed} (\"Over Several Eternities\" achievement)", ConsoleColor.White);
                WriteLine($"[S] Stickers Placed: {file.achievementCounters.StickersPlaced} (\"Stickerbook\" achievement)", ConsoleColor.White);
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
                WriteLine("Reverted savefile. Press [S] to save changes.", ConsoleColor.Green);
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