# Lyrics Directory

This folder contains .lrc (lyrics) files for FFXIV background music.

## File Naming Convention

Name your .lrc files to match the song titles or territory names where they play.

Examples:
- `Limsa Lominsa.lrc`
- `Answers.lrc`
- `Dragonsong.lrc`

## LRC File Format

LRC files should follow the standard format:
```
[ti:Song Title]
[ar:Artist Name]
[al:Album Name]
[by:Creator]

[00:12.34]First line of lyrics
[00:18.50]Second line of lyrics
```

## Adding New Lyrics

1. Place your .lrc file in this directory
2. Ensure the filename matches how the plugin detects the song
3. The plugin will automatically load lyrics when the corresponding song is detected