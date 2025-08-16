# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Commands

### Building
- `dotnet build` - Builds the solution
- Use `cmd.exe /c "dotnet build"` to avoid WSL issues when running on Windows

### Testing
- `dotnet run --project Tests/LrcParser/LrcParserTest.csproj` - Runs the LRC parser tests

### Development
- Build outputs to `SingAlongPlugin/bin/x64/Debug/SingAlongPlugin.dll` (Debug) or `SingAlongPlugin/bin/x64/Release/SingAlongPlugin.dll` (Release)
- The plugin JSON manifest is at `SingAlongPlugin/SingAlongPlugin.json`

## Architecture

This is a FFXIV Dalamud plugin that displays synchronized lyrics for background music using LRC files.

### Core Components

**Plugin.cs** - Main plugin entry point and coordinator:
- Manages Dalamud plugin lifecycle and services
- Coordinates between MusicObserver and LyricsWindow
- Handles font scaling and lyrics folder management
- Auto-loads LRC files when music changes

**MusicObserver.cs** - Background music detection:
- Uses memory scanning to detect current BGM ID from game memory
- Tracks song changes and playback timing
- Based on BGMAddressResolver pattern for memory access
- Runs on background thread with 100ms polling

**LrcParser.cs** - LRC file parsing and lyric timing:
- Parses standard LRC format with timestamps `[mm:ss.cs]`
- Supports metadata tags (title, artist, offset)
- Binary search for efficient lyric lookup by timestamp
- Handles lyric synchronization with song playback

**LyricsWindow.cs** - UI display:
- ImGui-based overlay window with configurable transparency
- Shows current lyric (bright) and upcoming lyric (dimmed)
- Auto-hides when no lyrics available
- Supports window locking and scaling

**Configuration.cs** - Plugin settings:
- Lyrics scale factor, background opacity, window lock
- Auto-updates fonts when settings change

### Data Flow

1. MusicObserver detects BGM change → fires MusicChanged event
2. Plugin loads corresponding `{songId}.lrc` file from Lyrics folder
3. LyricsWindow queries current timestamp and gets synchronized lyrics
4. Display updates in real-time with current/upcoming lyrics

### File Structure

- Lyrics files stored in plugin config directory under `Lyrics/` folder
- Format: `{bgmId}.lrc` (e.g., `938.lrc`)
- Plugin auto-creates Lyrics folder and copies any existing files from source

### Window Behavior

- Auto-shows when lyrics are available for current song
- Auto-hides when no lyrics or music stops
- Window flags updated in PreDraw() for lock/transparency settings
- Font handles managed for crisp scaling

### Development Notes

- Uses Dalamud.NET.Sdk for FFXIV plugin framework
- Requires .NET 8 and FFXIV/Dalamud installation
- Memory scanning requires specific game signatures that may need updates
- Debug window available in debug builds via `/singalong debug`