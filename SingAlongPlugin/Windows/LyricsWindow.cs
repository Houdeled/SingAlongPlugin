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
    
    // Animation state tracking
    private enum AnimationState { Idle, Transitioning }
    private AnimationState _animationState = AnimationState.Idle;
    private DateTime _animationStartTime;
    private string _previousMainLyric = "";
    private string _currentMainLyric = "";
    private float _animationDurationMs = 500f; // milliseconds

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
            Flags |= ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoInputs;
        }
        else
        {
            Flags &= ~(ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoInputs);
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
        
        // Detect lyric change and start animation
        if (_currentMainLyric != mainLyric)
        {
            _previousMainLyric = _currentMainLyric;
            _currentMainLyric = mainLyric;
            
            // Start animation if we have a previous lyric (not first lyric) and new lyric is not empty
            if (!string.IsNullOrEmpty(_previousMainLyric) && !string.IsNullOrEmpty(mainLyric))
            {
                _animationState = AnimationState.Transitioning;
                _animationStartTime = DateTime.UtcNow;
            }
        }
        
        // Only show window content if there are lyrics to display
        if (!string.IsNullOrEmpty(mainLyric) || !string.IsNullOrEmpty(upcomingLyric))
        {
            DrawLyrics(mainLyric, upcomingLyric);
        }
        else
        {
            // Auto-hide window when no lyrics are available
            IsOpen = false;
        }
    }
    
    private void DrawLyrics(string mainLyric, string upcomingLyric)
    {
        var windowWidth = ImGui.GetWindowSize().X;
        var scaleFactor = Plugin.Configuration.LyricsScaleFactor;
        
        if (_animationState == AnimationState.Transitioning)
        {
            // Calculate animation progress
            var elapsed = (float)(DateTime.UtcNow - _animationStartTime).TotalMilliseconds;
            var progress = Math.Min(elapsed / _animationDurationMs, 1.0f);
            var easedProgress = EaseInOutQuad(progress);
            
            // Animation complete?
            if (progress >= 1.0f)
            {
                _animationState = AnimationState.Idle;
            }
            
            // Calculate offsets and alphas for slide-up animation
            var oldLyricOffset = -50f * easedProgress; // Slide up
            var newLyricOffset = 50f * (1f - easedProgress); // Slide up from below
            var oldLyricAlpha = 1f - easedProgress; // Fade out
            var newLyricAlpha = easedProgress; // Fade in
            
            // Draw old lyric sliding up and fading out
            if (!string.IsNullOrEmpty(_previousMainLyric))
            {
                DrawCenteredText(_previousMainLyric, 1.5f * scaleFactor, new Vector4(1.0f, 1.0f, 1.0f, 1.0f), Plugin.MainLyricsFont, oldLyricOffset, oldLyricAlpha);
            }
            
            // Draw new lyric sliding up and fading in
            DrawCenteredText(mainLyric, 1.5f * scaleFactor, new Vector4(1.0f, 1.0f, 1.0f, 1.0f), Plugin.MainLyricsFont, newLyricOffset, newLyricAlpha);
        }
        else
        {
            // Normal static display
            DrawCenteredText(mainLyric, 1.5f * scaleFactor, new Vector4(1.0f, 1.0f, 1.0f, 1.0f), Plugin.MainLyricsFont);
        }
        
        // Spacing between lyrics
        ImGuiHelpers.ScaledDummy(15.0f);
        
        // Upcoming lyric - always static, no animation
        DrawCenteredText(upcomingLyric, 1.0f * scaleFactor, new Vector4(0.7f, 0.7f, 0.7f, 0.8f), Plugin.UpcomingLyricsFont);
        
        // Reset font scale
        ImGui.SetWindowFontScale(1.0f);
    }
    
    private void DrawCenteredText(string text, float fontScale, Vector4 color, IFontHandle? fontHandle, float yOffset = 0f, float alphaMultiplier = 1f)
    {
        // Apply alpha multiplier to color
        var animatedColor = new Vector4(color.X, color.Y, color.Z, color.W * alphaMultiplier);
        ImGui.PushStyleColor(ImGuiCol.Text, animatedColor);
        
        if (fontHandle != null && fontHandle.Available)
        {
            // Use crisp font handles when available
            fontHandle.Push();
            
            var textSize = ImGui.CalcTextSize(text);
            var windowWidth = ImGui.GetWindowSize().X;
            var cursorX = (windowWidth - textSize.X) * 0.5f;
            var currentCursorY = ImGui.GetCursorPosY();
            
            if (cursorX > 0) 
                ImGui.SetCursorPosX(cursorX);
            
            // Apply Y offset for animation
            ImGui.SetCursorPosY(currentCursorY + yOffset);
            
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
            var currentCursorY = ImGui.GetCursorPosY();
            
            if (cursorX > 0) 
                ImGui.SetCursorPosX(cursorX);
            
            // Apply Y offset for animation
            ImGui.SetCursorPosY(currentCursorY + yOffset);
            
            ImGui.TextUnformatted(text);
            ImGui.SetWindowFontScale(1.0f);
        }
        
        ImGui.PopStyleColor();
    }
    
    private float EaseInOutQuad(float t)
    {
        return t < 0.5f ? 2f * t * t : 1f - 2f * (1f - t) * (1f - t);
    }
}
