using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using Lumina.Excel.Sheets;

namespace SingAlongPlugin.Windows;

public class LyricsWindow : Window, IDisposable
{
    private Plugin Plugin;

    // We give this window a hidden ID using ##.
    // The user will see "My Amazing Window" as window title,
    // but for ImGui the ID is "My Amazing Window##With a hidden ID"
    public LyricsWindow(Plugin plugin)
        : base("Song Lyrics##SingAlongLyrics", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(300, 200),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };

        Plugin = plugin;
    }

    public void Dispose() { }

    public override void Draw()
    {
        if (ImGui.Button("Show Settings"))
        {
            Plugin.ToggleConfigUI();
        }

        ImGui.Spacing();
        
        ImGui.TextUnformatted("SingAlong Plugin - Song Lyrics Display");
        ImGui.Separator();
        
        ImGui.TextUnformatted("No song detected yet...");
        ImGui.TextUnformatted("Lyrics will appear here when a song with lyrics is playing.");
    }
}
