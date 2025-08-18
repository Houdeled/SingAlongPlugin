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
        Flags = ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.AlwaysAutoResize;

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
        
        // Enable Animations Setting
        var enableAnimations = Configuration.EnableAnimations;
        if (ImGui.Checkbox("Enable Animations", ref enableAnimations))
        {
            Configuration.EnableAnimations = enableAnimations;
            Configuration.Save();
        }
        ImGui.SameLine();
        ImGui.TextDisabled("(?)");
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Enables smooth transitions when lyrics change");
        
        // Animation Speed Setting (only show if animations are enabled)
        if (Configuration.EnableAnimations)
        {
            var animationSpeed = Configuration.AnimationSpeed;
            if (ImGui.SliderFloat("Animation Speed", ref animationSpeed, 0.5f, 3.0f, "%.1fx"))
            {
                Configuration.AnimationSpeed = animationSpeed;
                Configuration.Save();
            }
            ImGui.SameLine();
            ImGui.TextDisabled("(?)");
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Adjusts how fast animations play (0.5x = slow, 1.0x = normal, 3.0x = very fast)");
        }
        
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Text("Color Settings");
        
        // Lyrics Color Setting
        var lyricsColor = Configuration.LyricsColor;
        var colorBytes = new byte[4] 
        {
            (byte)((lyricsColor >> 16) & 0xFF), // R
            (byte)((lyricsColor >> 8) & 0xFF),  // G
            (byte)(lyricsColor & 0xFF),         // B
            (byte)((lyricsColor >> 24) & 0xFF)  // A
        };
        var colorVector = new Vector4(colorBytes[0] / 255.0f, colorBytes[1] / 255.0f, colorBytes[2] / 255.0f, colorBytes[3] / 255.0f);
        
        if (ImGui.ColorEdit4("Lyrics Color", ref colorVector, ImGuiColorEditFlags.AlphaPreviewHalf))
        {
            var newColor = ((uint)(colorVector.W * 255) << 24) |
                          ((uint)(colorVector.X * 255) << 16) |
                          ((uint)(colorVector.Y * 255) << 8) |
                          ((uint)(colorVector.Z * 255));
            Configuration.LyricsColor = newColor;
            Configuration.Save();
        }
        ImGui.SameLine();
        ImGui.TextDisabled("(?)");
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Color for lyrics text");
        
        // Upcoming Lyrics Opacity Setting
        var upcomingAlpha = Configuration.UpcomingAlphaMultiplier;
        if (ImGui.SliderFloat("Upcoming Lyrics Opacity", ref upcomingAlpha, 0.1f, 1.0f, "%.1f"))
        {
            Configuration.UpcomingAlphaMultiplier = upcomingAlpha;
            Configuration.Save();
        }
        ImGui.SameLine();
        ImGui.TextDisabled("(?)");
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Opacity of upcoming lyrics (0.1 = very transparent, 1.0 = fully opaque)");
        
    }
}
