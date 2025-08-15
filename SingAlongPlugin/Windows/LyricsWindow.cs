using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using Dalamud.Interface.ManagedFontAtlas;
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
        string mainLyric = "";
        string upcomingLyric = "";
        
        // Get current lyrics from the music observer
        var lrcParser = Plugin.GetCurrentLyrics();
        if (lrcParser != null && lrcParser.IsLoaded && Plugin.MusicObserver != null)
        {
            // Get current song timestamp
            var currentTimestampMs = Plugin.MusicObserver.GetCurrentSongTimestamp();
            var currentTime = TimeSpan.FromMilliseconds(currentTimestampMs);
            
            // Get current and next lyrics based on timestamp
            mainLyric = lrcParser.GetCurrentLyric(currentTime);
            upcomingLyric = lrcParser.GetNextLyric(currentTime);
        }
        
        // Only show window if there are lyrics to display
        if (!string.IsNullOrEmpty(mainLyric) || !string.IsNullOrEmpty(upcomingLyric))
        {
            DrawLyrics(mainLyric, upcomingLyric);
        }
        else
        {
            // Show a placeholder when no lyrics are available
            ImGui.TextDisabled("No lyrics available");
        }
    }
    
    private void DrawLyrics(string mainLyric, string upcomingLyric)
    {
        var windowWidth = ImGui.GetWindowSize().X;
        var scaleFactor = Plugin.Configuration.LyricsScaleFactor;
        
        // Main lyric - bright and large
        DrawCenteredText(mainLyric, 1.5f * scaleFactor, new Vector4(1.0f, 1.0f, 1.0f, 1.0f), Plugin.MainLyricsFont);
        
        // Spacing between lyrics
        ImGuiHelpers.ScaledDummy(15.0f);
        
        // Upcoming lyric - dimmer and normal size
        DrawCenteredText(upcomingLyric, 1.0f * scaleFactor, new Vector4(0.7f, 0.7f, 0.7f, 0.8f), Plugin.UpcomingLyricsFont);
        
        // Reset font scale
        ImGui.SetWindowFontScale(1.0f);
    }
    
    private void DrawCenteredText(string text, float fontScale, Vector4 color, IFontHandle? fontHandle)
    {
        ImGui.PushStyleColor(ImGuiCol.Text, color);
        
        if (fontHandle != null && fontHandle.Available)
        {
            // Use crisp font handles when available
            fontHandle.Push();
            
            var textSize = ImGui.CalcTextSize(text);
            var windowWidth = ImGui.GetWindowSize().X;
            var cursorX = (windowWidth - textSize.X) * 0.5f;
            
            if (cursorX > 0) 
                ImGui.SetCursorPosX(cursorX);
            
            ImGui.TextUnformatted(text);
            fontHandle.Pop();
        }
        else
        {
            // Fallback to scaling if font handle is not available
            ImGui.SetWindowFontScale(fontScale);
            
            var textSize = ImGui.CalcTextSize(text);
            var windowWidth = ImGui.GetWindowSize().X;
            var cursorX = (windowWidth - textSize.X) * 0.5f;
            
            if (cursorX > 0) 
                ImGui.SetCursorPosX(cursorX);
            
            ImGui.TextUnformatted(text);
            ImGui.SetWindowFontScale(1.0f);
        }
        
        ImGui.PopStyleColor();
    }
}
