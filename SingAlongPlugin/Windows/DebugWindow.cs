#if DEBUG
using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace SingAlongPlugin.Windows;

public class DebugWindow : Window, IDisposable
{
    private Plugin Plugin;

    public DebugWindow(Plugin plugin) : base("SingAlong Debug###SingAlongDebug")
    {
        Flags = ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoScrollbar;

        Size = new Vector2(400, 150);
        SizeCondition = ImGuiCond.Always;

        Plugin = plugin;
    }

    public void Dispose() { }

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