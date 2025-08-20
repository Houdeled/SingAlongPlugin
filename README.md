# SingAlongPlugin

A FFXIV Dalamud plugin that displays synchronized lyrics for background music using LRC files.

## Features

* **Automatic Music Detection**: Monitors background music changes in FFXIV
* **Synchronized Lyrics Display**: Shows lyrics with precise timing using LRC format
* **Customizable UI**: Configurable font scaling, colors, transparency, and positioning
* **Animation Support**: Smooth transitions between lyrics with fade-in/fade-out effects
* **Auto-Hide/Show**: Automatically displays lyrics when available, hides when not
* **Window Management**: Lockable window with transparency options

## How It Works

SingAlongPlugin monitors FFXIV's background music using memory scanning to detect the current BGM ID. When a song changes, it automatically searches for a corresponding LRC file (`{songId}.lrc`) in the plugin's `Lyrics` folder. If found, it displays synchronized lyrics in an overlay window that can be customized through the settings.

### LRC File Format

The plugin supports standard LRC format with timestamps:
```
[mm:ss.xx] Lyric text here
[00:15.50] This is how lyrics appear
[00:18.20] With precise timing
```

Optional metadata tags are also supported:
```
[ti:Song Title]
[ar:Artist Name]
[offset:500]
```

## Installation & Usage

### Prerequisites

* XIVLauncher, FINAL FANTASY XIV, and Dalamud installed and running
* **Important**: You must run the game through XIVLauncher for plugins to work

### Easy Installation (Recommended)

1. Open Dalamud settings in-game with `/xlsettings`
2. Go to the **Experimental** tab
3. Copy and paste this URL in **Custom Plugin Repositories**:
   ```
   https://raw.githubusercontent.com/Houdeled/SingAlongPlugin/main/pluginmaster.json
   ```
4. Click **Save**
5. Go to `/xlplugins` and find **SingAlong Plugin** in the available plugins list
6. Click **Install** and enjoy synchronized lyrics!

### Manual Installation (Development)

1. Clone this repository
2. Open `SingAlongPlugin.sln` in Visual Studio 2022 or JetBrains Rider
3. Build the solution (Debug or Release)
4. Add the plugin DLL path to Dalamud's Dev Plugin Locations (`/xlsettings` → Experimental)
5. Enable the plugin in the Plugin Installer (`/xlplugins` → Dev Tools → Installed Dev Plugins)

### Getting Started

1. Use `/singalong` to toggle the lyrics window
2. Use `/singalong settings` to open the configuration window
3. The plugin comes with **15 pre-synchronized songs** - lyrics will appear automatically when these songs play in-game!

### Adding Custom Lyrics (Optional)

Want to add lyrics for songs not included? 

1. Find the BGM ID for your desired song (visible in debug mode or game logs)
2. Create or obtain an LRC file with synchronized timestamps
3. Name the file `{bgmId}.lrc` (e.g., `938.lrc`)
4. Place it in: `%APPDATA%\XIVLauncher\pluginConfigs\SingAlongPlugin\Lyrics\`
5. The plugin will automatically load and display your custom lyrics when that song plays

## Configuration

Access the configuration window through:
- `/singalong settings` command (recommended)
- `/xlplugins` → SingAlongPlugin → Settings button

### Available Settings

- **Font Scale**: Adjust lyrics text size
- **Window Transparency**: Set background opacity
- **Window Lock**: Prevent accidental movement
- **Colors**: Customize lyrics and upcoming text colors
- **Animation**: Configure transition effects and timing

## Commands

- `/singalong` - Toggle lyrics window
- `/singalong settings` - Open settings window
- `/singalong debug` - Open debug window (debug builds only)

## Planned Song Support

The following FFXIV songs with official lyrics are planned for inclusion:

### A Realm Reborn
- Answers
- Oblivion
- Through the Maelstrom
- Thunder Rolls
- Under the Weight

### Heavensward
- Heavensward
- Dragonsong
- Equilibrium
- Fiend
- Exponential Entropy
- Rise
- Locus

### Stormblood
- Wayward Daughter
- eScape
- Sunrise
- Amatsu Kaze
- Beauty's Wicked Wiles

### Shadowbringers
- Shadowbringers
- Tomorrow and Tomorrow
- A Long Fall
- To the Edge
- Return to Oblivion
- Ultima
- Landslide
- Blinding Indigo
- Who Brings Shadow
- What Angel Wakes Me

### Endwalker
- Footfalls
- Close in the Distance
- Scream
- With Hearts Aligned

### Dawntrail
- Unleashed
- Not Afraid
- Back to the Drawing Board
- Give It All
