using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace SingAlongPlugin.Windows;

public class ConfigWindow : Window, IDisposable
{
    private Configuration Configuration;
    private Plugin Plugin;

    // We give this window a constant ID using ###.
    // This allows for labels to be dynamic, like "{FPS Counter}fps###XYZ counter window",
    // and the window ID will always be "###XYZ counter window" for ImGui
    public ConfigWindow(Plugin plugin) : base("SingAlong Configuration###SingAlongConfig")
    {
        Flags = ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoScrollbar;

        Size = new Vector2(600, 200);
        SizeCondition = ImGuiCond.Always;

        Configuration = plugin.Configuration;
        Plugin = plugin;
    }

    public void Dispose() { }

    public override void PreDraw()
    {
        // Config window is always movable
    }

    public override void Draw()
    {
        ImGui.Text("Lyrics Display Settings");
        ImGui.Separator();
        
        // Scale Factor Setting
        var scaleFactor = Configuration.LyricsScaleFactor;
        if (ImGui.SliderFloat("Scale Factor", ref scaleFactor, 0.5f, 5.0f, "%.1f"))
        {
            Configuration.LyricsScaleFactor = scaleFactor;
            Configuration.Save();
        }
        ImGui.SameLine();
        ImGui.TextDisabled("(?)");
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Adjusts the size of lyrics text");
        
        // Background Opacity Setting
        var bgOpacity = Configuration.BackgroundOpacity;
        if (ImGui.SliderFloat("Background Opacity", ref bgOpacity, 0.0f, 1.0f, "%.2f"))
        {
            Configuration.BackgroundOpacity = bgOpacity;
            Configuration.Save();
        }
        ImGui.SameLine();
        ImGui.TextDisabled("(?)");
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("0.0 = Transparent, 1.0 = Solid background");
        
        // Lock Window Setting
        var lockWindow = Configuration.LockWindow;
        if (ImGui.Checkbox("Lock Window Position", ref lockWindow))
        {
            Configuration.LockWindow = lockWindow;
            Configuration.Save();
        }
        ImGui.SameLine();
        ImGui.TextDisabled("(?)");
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Prevents accidentally moving the lyrics window");
    }
}
