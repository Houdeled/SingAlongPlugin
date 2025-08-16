#if DEBUG
using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace SingAlongPlugin.Windows;

public class DebugWindow : Window, IDisposable
{
    private Plugin Plugin;
    private int _fakeLyricsState = 0; // 0=Off, 1=Short+Short, 2=Short+Long, 3=Long+Short, 4=Long+Long
    
    // Predefined fake lyrics
    private readonly string _shortLyric = "Short lyric";
    private readonly string _longLyric = "This is a much longer lyric that should demonstrate how the window handles different text lengths";

    public DebugWindow(Plugin plugin) : base("SingAlong Debug###SingAlongDebug")
    {
        Flags = ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoScrollbar;

        Size = new Vector2(400, 200);
        SizeCondition = ImGuiCond.Always;

        Plugin = plugin;
    }

    public void Dispose() { }
    
    public bool IsFakeLyricsActive => _fakeLyricsState > 0;
    
    public (string current, string upcoming) GetFakeLyrics()
    {
        return _fakeLyricsState switch
        {
            1 => (_shortLyric, _shortLyric),
            2 => (_shortLyric, _longLyric),
            3 => (_longLyric, _shortLyric),
            4 => (_longLyric, _longLyric),
            _ => (string.Empty, string.Empty)
        };
    }

    public override void PreDraw()
    {
        // Debug window is always movable
    }

    public override void Draw()
    {
        ImGui.Text("Music Debug Information");
        ImGui.Separator();
        
        if (Plugin.MusicObserver != null)
        {
            var currentSongId = Plugin.MusicObserver.GetCurrentBgm();
            var currentTimestamp = Plugin.MusicObserver.GetCurrentSongTimestamp();
            
            ImGui.Text($"Song ID: {currentSongId}");
            ImGui.Text($"Timestamp: {FormatTimestamp(currentTimestamp)}");
            
            if (currentSongId == 0)
            {
                ImGui.TextDisabled("No music currently playing");
            }
        }
        else
        {
            ImGui.TextDisabled("Music Observer not initialized");
        }
        
        ImGui.Spacing();
        ImGui.Separator();
        
        // Fake lyrics button
        var buttonText = _fakeLyricsState switch
        {
            0 => "Show Fake Lyrics (Off)",
            1 => "Show Fake Lyrics (Short+Short)",
            2 => "Show Fake Lyrics (Short+Long)",
            3 => "Show Fake Lyrics (Long+Short)",
            4 => "Show Fake Lyrics (Long+Long)",
            _ => "Show Fake Lyrics (Off)"
        };
        
        if (ImGui.Button(buttonText))
        {
            _fakeLyricsState = (_fakeLyricsState + 1) % 5;
            
            // Open or close lyrics window based on state
            if (_fakeLyricsState > 0)
            {
                Plugin.SetLyricsWindowOpen(true);
            }
            else
            {
                Plugin.SetLyricsWindowOpen(false);
            }
        }
        
        ImGui.Spacing();
        if (ImGui.Button("Close Debug Window"))
        {
            IsOpen = false;
        }
    }
    
    private string FormatTimestamp(uint timestampMs)
    {
        var totalSeconds = timestampMs / 1000;
        var minutes = totalSeconds / 60;
        var seconds = totalSeconds % 60;
        var milliseconds = timestampMs % 1000;
        
        return $"{minutes:D2}:{seconds:D2}.{milliseconds:D3}";
    }
}
#endif