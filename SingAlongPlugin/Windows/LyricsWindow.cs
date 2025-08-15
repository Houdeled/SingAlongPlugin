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

    public LyricsWindow(Plugin plugin)
        : base("Song Lyrics##SingAlongLyrics", 
               ImGuiWindowFlags.NoScrollbar | 
               ImGuiWindowFlags.NoScrollWithMouse | 
               ImGuiWindowFlags.NoBackground | 
               ImGuiWindowFlags.NoTitleBar |
               ImGuiWindowFlags.AlwaysAutoResize |
               ImGuiWindowFlags.NoMove |
               ImGuiWindowFlags.NoCollapse)
    {
        Plugin = plugin;
    }

    public void Dispose() { }
    
    public override void PreDraw()
    {
        // Apply window lock setting
        if (Plugin.Configuration.LockWindow)
        {
            Flags |= ImGuiWindowFlags.NoMove;
        }
        else
        {
            Flags &= ~ImGuiWindowFlags.NoMove;
        }
        
        // Apply background opacity setting
        if (Plugin.Configuration.BackgroundOpacity <= 0.0f)
        {
            Flags |= ImGuiWindowFlags.NoBackground;
        }
        else
        {
            Flags &= ~ImGuiWindowFlags.NoBackground;
            // Set window background alpha using color style
            ImGui.PushStyleColor(ImGuiCol.WindowBg, new Vector4(0.0f, 0.0f, 0.0f, Plugin.Configuration.BackgroundOpacity));
        }
        
        // Window auto-resizes based on content with AlwaysAutoResize flag
    }
    
    public override void PostDraw()
    {
        // Clean up style changes
        if (Plugin.Configuration.BackgroundOpacity > 0.0f)
        {
            ImGui.PopStyleColor(); // Pop WindowBg color
        }
    }

    public override void Draw()
    {
        // Sample lyrics for testing UI styling
        string mainLyric = "Foul child, bastard and beast";
        string upcomingLyric = "O lost lamb, first to the feast";
        
        DrawLyrics(mainLyric, upcomingLyric);
    }
    
    private void DrawLyrics(string mainLyric, string upcomingLyric)
    {
        var windowWidth = ImGui.GetWindowSize().X;
        var scaleFactor = Plugin.Configuration.LyricsScaleFactor;
        
        // Main lyric - bright and large
        DrawCenteredText(mainLyric, 1.5f * scaleFactor, new Vector4(1.0f, 1.0f, 1.0f, 1.0f));
        
        // Spacing between lyrics
        ImGuiHelpers.ScaledDummy(15.0f);
        
        // Upcoming lyric - dimmer and normal size
        DrawCenteredText(upcomingLyric, 1.0f * scaleFactor, new Vector4(0.7f, 0.7f, 0.7f, 0.8f));
        
        // Reset font scale
        ImGui.SetWindowFontScale(1.0f);
    }
    
    private void DrawCenteredText(string text, float fontScale, Vector4 color)
    {
        ImGui.PushStyleColor(ImGuiCol.Text, color);
        ImGui.SetWindowFontScale(fontScale);
        
        var textSize = ImGui.CalcTextSize(text);
        var windowWidth = ImGui.GetWindowSize().X;
        var cursorX = (windowWidth - textSize.X) * 0.5f;
        
        if (cursorX > 0) 
            ImGui.SetCursorPosX(cursorX);
        
        ImGui.TextUnformatted(text);
        ImGui.PopStyleColor();
    }
}
