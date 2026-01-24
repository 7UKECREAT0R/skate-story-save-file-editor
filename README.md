![Skate Story marketing banner](https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/1263240/fcd83e0b0fdddef3f4db4718fcd3a84f184dafe3/header.jpg)

# Update as of January 23rd, 2026!
With the patch released today, this tool is mostly unneeded now (thankfully!). It seems to fix a lot of the softlocks people are experiencing, and introduces a proper New Game+ mode which doesn't wipe achievement stats. This tool should still work though, in case you want to mess around with your save file!

# Skate Story (save file editor)
This is a quick little tool I wrote up to modify a [Skate Story](https://store.steampowered.com/app/1263240/Skate_Story/) save file. The tool does its best to auto-locate your file and also creates automatic backups.

# How to Use
Download a (Windows only for now!) build from the [Releases](https://github.com/7UKECREAT0R/skate-story-save-file-editor/releases) page, unzip all the files, and run the executable. The interface is through a terminal. If you don't see anything appear (it usually should, though), try opening the program through Command Prompt, Windows Terminal, or another terminal.

# Loading Your Save File
You can press `A` to automatically obtain your save file which should work 99% of the time on Windows. If it's not working or you want to modify a save file somewhere else, press `M` to manually enter either a `.json` or `.sav` file.

# Options
There's a few options you can use once you've loaded a save file.

## Saving
You have to save your changes after you're done messing with your savefile! You can choose two different ways to save:
- `S` Save as .SAV: Saves in SKATE STORY's native compressed format. This is the option you want to use to actually *play* on the file.
- `J` Save as .JSON: Saves as a .JSON file so you can poke around and tweak stuff.

## `O` Open Game Directory
If you let the tool automatically find your save file, you can press `O` to open the directory.

## `ESC` Exit Without Saving
Does nothing and closes the program.

## `A` Change Achievement Counter/Stat
After pressing this option, you can choose which stat to modify. This is made to transfer your stats to a new save file (kind of like a New Game+). I absolutely will judge you if you use this to cheat achievements.
- `D` Deaths: Number of times you've died. The "YOU MUST SKATE" achievement requires 500.
- `T` Tricks: The number of tricks you've popped (not including grinds, manuals, reverses, etc...). The "Over Several Eternities" achievement requires 10,000.
- `S` Stickers Placed: The number of stickers you've placed on boards. The "Stickerbook" achievement requires 100.

## `R` Revert to Pre-Epilogue
Reverts your savefile to right before the epilogue. Tested and working, heck yea! Remember to save `S`.

## `L` Change Level
Changes the level you'll be in once you press "Continue" on the main menu. This is probably the option you want to use to get yourself un-soft-locked. The tool shows the internal names of the levels, but they have a bit of a logical format.

## `Shift + L` Change Sinkhole Level
Changes the level the sinkhole (in skater's dream) takes you to.
