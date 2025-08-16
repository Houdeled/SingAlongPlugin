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
    // Colors (keep as constants since they don't need to be configurable)
    private static readonly Vector4 MainLyricColor = new(1.0f, 1.0f, 1.0f, 1.0f);
    private static readonly Vector4 UpcomingLyricColor = new(0.7f, 0.7f, 0.7f, 0.9f);
    private static readonly Vector4 TransparentBackground = new(0.0f, 0.0f, 0.0f, 0.0f);
    private const float BackgroundOpacityThreshold = 0.0f;

    private Plugin Plugin;
    
    // Smart centering fields
    private Vector2 _lastWindowSize = Vector2.Zero;
    private bool _isFirstDraw = true;
    private Vector2 _currentFrameSize = Vector2.Zero;
    private Vector2 _currentFramePos = Vector2.Zero;
    
    // Animation state tracking
    private enum AnimationState { Idle, Transitioning }
    private AnimationState _animationState = AnimationState.Idle;
    private DateTime _animationStartTime;
    private string _previousMainLyric = "";
    private string _currentMainLyric = "";
    private string _previousUpcomingLyric = "";
    private string _currentUpcomingLyric = "";

    public LyricsWindow(Plugin plugin)
        : base("Song Lyrics##SingAlongLyrics", 
               ImGuiWindowFlags.NoScrollbar | 
               ImGuiWindowFlags.NoScrollWithMouse | 
               ImGuiWindowFlags.NoBackground | 
               ImGuiWindowFlags.NoTitleBar |
               ImGuiWindowFlags.NoResize |
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
        if (Plugin.Configuration.BackgroundOpacity <= BackgroundOpacityThreshold)
        {
            Flags |= ImGuiWindowFlags.NoBackground;
        }
        else
        {
            Flags &= ~ImGuiWindowFlags.NoBackground;
            // Set window background alpha using color style
            ImGui.PushStyleColor(ImGuiCol.WindowBg, TransparentBackground with { W = Plugin.Configuration.BackgroundOpacity });
        }
        
        // Manual sizing and centering (always enabled)
        if (IsOpen)
        {
            // Calculate content size for current lyrics
            var contentSize = CalculateContentSize();
            
            if (contentSize != Vector2.Zero)
            {
                // If this is the first draw or size changed significantly
                if (_isFirstDraw || Math.Abs(contentSize.X - _lastWindowSize.X) > Plugin.Configuration.SizeChangeThreshold || 
                    Math.Abs(contentSize.Y - _lastWindowSize.Y) > Plugin.Configuration.SizeChangeThreshold)
                {
                    if (!_isFirstDraw)
                    {
                        // Calculate center offset for size change
                        var sizeChange = contentSize - _lastWindowSize;
                        var currentPos = _currentFramePos;
                        var newPos = currentPos - sizeChange * 0.5f;
                        
                        // Set centered position and size
                        ImGui.SetNextWindowPos(newPos);
                        ImGui.SetNextWindowSize(contentSize);
                        
#if DEBUG
                        if (Plugin.IsFakeLyricsActive())
                        {
                            Plugin.Log.Debug($"PreDraw centering: Size {_lastWindowSize} -> {contentSize}, Pos {currentPos} -> {newPos}");
                        }
#endif
                    }
                    else
                    {
                        // First draw - just set size
                        ImGui.SetNextWindowSize(contentSize);
                    }
                    
                    _lastWindowSize = contentSize;
                    _isFirstDraw = false;
                }
                else
                {
                    // Size hasn't changed, keep current size
                    ImGui.SetNextWindowSize(contentSize);
                }
            }
        }
    }
    
    public override void PostDraw()
    {
        // Clean up style changes
        if (Plugin.Configuration.BackgroundOpacity > BackgroundOpacityThreshold)
        {
            ImGui.PopStyleColor(); // Pop WindowBg color
        }
        
        // Smart centering logic (always enabled)
        if (IsOpen)
        {
            var currentSize = _currentFrameSize; // Use size captured at end of Draw()
            
            // Skip on first draw to establish baseline
            if (_isFirstDraw)
            {
                _lastWindowSize = currentSize;
                _isFirstDraw = false;
                return;
            }
            
            // Check if window size changed (lower threshold for better detection)
            if (Math.Abs(currentSize.X - _lastWindowSize.X) > Plugin.Configuration.SizeChangeThreshold || 
                Math.Abs(currentSize.Y - _lastWindowSize.Y) > Plugin.Configuration.SizeChangeThreshold)
            {
                // Calculate offset to keep window centered
                var sizeChange = currentSize - _lastWindowSize;
                var currentPos = _currentFramePos; // Use position captured at end of Draw()
                var newPos = currentPos - sizeChange * 0.5f; // Move half the size change in opposite direction
                
                // Apply the centered position
                ImGui.SetWindowPos(newPos);
                
#if DEBUG
                // Debug output for fake lyrics testing
                if (Plugin.IsFakeLyricsActive())
                {
                    Plugin.Log.Debug($"Smart centering: Size changed from {_lastWindowSize} to {currentSize}, moved from {currentPos} to {newPos}");
                }
#endif
                
                _lastWindowSize = currentSize;
            }
        }
    }

    public override void Draw()
    {
        string mainLyric = "";
        string upcomingLyric = "";
        
#if DEBUG
        // Check for fake lyrics first in debug mode
        if (Plugin.IsFakeLyricsActive())
        {
            (mainLyric, upcomingLyric) = Plugin.GetFakeLyrics();
        }
        else
#endif
        {
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
        }
        
        // Detect lyric change and start animation
        if (_currentMainLyric != mainLyric)
        {
            _previousMainLyric = _currentMainLyric;
            _previousUpcomingLyric = _currentUpcomingLyric; // Capture current upcoming before it changes
            _currentMainLyric = mainLyric;
            _currentUpcomingLyric = upcomingLyric; // Update both at the same time
            
            // Start animation if enabled and we have a previous lyric (not first lyric) and new lyric is not empty
            if (Plugin.Configuration.EnableAnimations && !string.IsNullOrEmpty(_previousMainLyric) && !string.IsNullOrEmpty(mainLyric))
            {
                _animationState = AnimationState.Transitioning;
                _animationStartTime = DateTime.UtcNow;
            }
        }
        else if (_currentUpcomingLyric != upcomingLyric)
        {
            // Only upcoming changed (shouldn't happen often, but handle it)
            _currentUpcomingLyric = upcomingLyric;
        }
        
        // Only show window content if there are lyrics to display
        if (!string.IsNullOrEmpty(mainLyric) || !string.IsNullOrEmpty(upcomingLyric))
        {
            DrawLyrics(mainLyric, upcomingLyric);
        }
        else
        {
#if DEBUG
            // In debug mode, don't auto-hide if fake lyrics are active
            if (!Plugin.IsFakeLyricsActive())
#endif
            {
                // Auto-hide window when no lyrics are available
                IsOpen = false;
            }
        }
        
        // Capture window size and position at end of Draw() for smart centering
        _currentFrameSize = ImGui.GetWindowSize();
        _currentFramePos = ImGui.GetWindowPos();
        
#if DEBUG
        // Debug output for position tracking
        // if (Plugin.IsFakeLyricsActive())
        // {
        //     Plugin.Log.Debug($"Draw() end - Size: {_currentFrameSize}, Position: {_currentFramePos}");
        // }
#endif
    }
    
    private void DrawLyrics(string mainLyric, string upcomingLyric)
    {
        var windowWidth = ImGui.GetWindowSize().X;
        var scaleFactor = Plugin.Configuration.LyricsScaleFactor;
        
        if (_animationState == AnimationState.Transitioning)
        {
            // Calculate animation progress
            var elapsed = (float)(DateTime.UtcNow - _animationStartTime).TotalMilliseconds;
            var progress = Math.Min(elapsed / Plugin.Configuration.AnimationDurationMs, 1f);
            var easedProgress = EaseInOutQuad(progress);
            
            // Animation complete?
            if (progress >= 1f)
            {
                _animationState = AnimationState.Idle;
            }
            
            // Calculate positions and properties for the blend effect
            var upcomingToMainOffset = Plugin.Configuration.UpcomingToMainOffset * (1f - easedProgress); // Upcoming moves up to main position
            var oldMainOffset = Plugin.Configuration.OldMainSlideOffset * easedProgress; // Old main slides up and out
            
            // Alpha transitions for smooth blending
            var oldMainAlpha = 1f - easedProgress; // Old main fades out
            var upcomingToMainAlpha = easedProgress; // Upcoming becomes main (fades in as main)
            var newUpcomingAlpha = easedProgress * Plugin.Configuration.UpcomingAlphaMultiplier; // New upcoming fades in
            
            // Calculate scale transitions - upcoming grows to main size
            var upcomingToMainScale = Plugin.Configuration.UpcomingLyricScale + (Plugin.Configuration.UpcomingScaleGrowth * easedProgress); // Grows from 1.0 to 1.5
            var mainToUpcomingScale = Plugin.Configuration.MainLyricScale - (Plugin.Configuration.UpcomingScaleGrowth * easedProgress); // Shrinks from 1.5 to 1.0 (if any)
            
            // Draw old main lyric sliding up and fading out
            if (!string.IsNullOrEmpty(_previousMainLyric))
            {
                DrawCenteredText(_previousMainLyric, Plugin.Configuration.MainLyricScale * scaleFactor, MainLyricColor, Plugin.MainLyricsFont, oldMainOffset, oldMainAlpha);
            }
            
            // Check if this is a natural progression (upcoming becomes main)
            bool isNaturalProgression = !string.IsNullOrEmpty(_previousUpcomingLyric) && _previousUpcomingLyric == mainLyric;
            
            if (isNaturalProgression)
            {
                // This is the blend effect - previous upcoming becomes new main
                DrawCenteredText(mainLyric, upcomingToMainScale * scaleFactor, MainLyricColor, Plugin.MainLyricsFont, upcomingToMainOffset, upcomingToMainAlpha);
            }
            else
            {
                // Fallback: new lyric sliding up from below (for skips or unexpected changes)
                var newLyricOffset = Plugin.Configuration.NewLyricSlideOffset * (1f - easedProgress);
                DrawCenteredText(mainLyric, Plugin.Configuration.MainLyricScale * scaleFactor, MainLyricColor, Plugin.MainLyricsFont, newLyricOffset, easedProgress);
            }
        }
        else
        {
            // Normal static display
            DrawCenteredText(mainLyric, Plugin.Configuration.MainLyricScale * scaleFactor, MainLyricColor, Plugin.MainLyricsFont);
        }
        
        // Spacing between lyrics
        ImGuiHelpers.ScaledDummy(Plugin.Configuration.LyricSpacing);
        
        // Upcoming lyric with animation support
        if (_animationState == AnimationState.Transitioning)
        {
            var elapsed = (float)(DateTime.UtcNow - _animationStartTime).TotalMilliseconds;
            var progress = Math.Min(elapsed / Plugin.Configuration.AnimationDurationMs, 1f);
            var easedProgress = EaseInOutQuad(progress);
            
            // New upcoming lyric fades in
            var newUpcomingAlpha = easedProgress * Plugin.Configuration.UpcomingAlphaMultiplier;
            DrawCenteredText(upcomingLyric, Plugin.Configuration.UpcomingLyricScale * scaleFactor, UpcomingLyricColor, Plugin.UpcomingLyricsFont, 0f, newUpcomingAlpha);
        }
        else
        {
            // Static upcoming lyric display
            DrawCenteredText(upcomingLyric, Plugin.Configuration.UpcomingLyricScale * scaleFactor, UpcomingLyricColor, Plugin.UpcomingLyricsFont);
        }
        
        // Reset font scale
        ImGui.SetWindowFontScale(1f);
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
            
            if (cursorX > BackgroundOpacityThreshold) 
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
            
            if (cursorX > BackgroundOpacityThreshold) 
                ImGui.SetCursorPosX(cursorX);
            
            // Apply Y offset for animation
            ImGui.SetCursorPosY(currentCursorY + yOffset);
            
            ImGui.TextUnformatted(text);
            ImGui.SetWindowFontScale(1f);
        }
        
        ImGui.PopStyleColor();
    }
    
    private Vector2 CalculateContentSize()
    {
        string mainLyric = "";
        string upcomingLyric = "";
        
#if DEBUG
        // Get fake lyrics in debug mode
        if (Plugin.IsFakeLyricsActive())
        {
            (mainLyric, upcomingLyric) = Plugin.GetFakeLyrics();
        }
        else
#endif
        {
            // Get real lyrics
            var lrcParser = Plugin.GetCurrentLyrics();
            if (lrcParser != null && lrcParser.IsLoaded && Plugin.MusicObserver != null)
            {
                var currentTimestampMs = Plugin.MusicObserver.GetCurrentSongTimestamp();
                var currentTime = TimeSpan.FromMilliseconds(currentTimestampMs);
                
                mainLyric = lrcParser.GetCurrentLyric(currentTime);
                upcomingLyric = lrcParser.GetNextLyric(currentTime);
            }
        }
        
        if (string.IsNullOrEmpty(mainLyric) && string.IsNullOrEmpty(upcomingLyric))
            return Vector2.Zero;
        
        // Calculate text sizes
        var scaleFactor = Plugin.Configuration.LyricsScaleFactor;
        
        Vector2 mainSize = Vector2.Zero;
        Vector2 upcomingSize = Vector2.Zero;
        
        if (!string.IsNullOrEmpty(mainLyric))
        {
            if (Plugin.MainLyricsFont?.Available == true)
            {
                Plugin.MainLyricsFont.Push();
                mainSize = ImGui.CalcTextSize(mainLyric);
                Plugin.MainLyricsFont.Pop();
            }
            else
            {
                var oldScale = ImGui.GetFont().Scale;
                ImGui.GetFont().Scale = Plugin.Configuration.MainLyricScale * scaleFactor;
                mainSize = ImGui.CalcTextSize(mainLyric);
                ImGui.GetFont().Scale = oldScale;
            }
        }
        
        if (!string.IsNullOrEmpty(upcomingLyric))
        {
            if (Plugin.UpcomingLyricsFont?.Available == true)
            {
                Plugin.UpcomingLyricsFont.Push();
                upcomingSize = ImGui.CalcTextSize(upcomingLyric);
                Plugin.UpcomingLyricsFont.Pop();
            }
            else
            {
                var oldScale = ImGui.GetFont().Scale;
                ImGui.GetFont().Scale = Plugin.Configuration.UpcomingLyricScale * scaleFactor;
                upcomingSize = ImGui.CalcTextSize(upcomingLyric);
                ImGui.GetFont().Scale = oldScale;
            }
        }
        
        // Calculate total window size
        var maxWidth = Math.Max(mainSize.X, upcomingSize.X);
        var totalHeight = mainSize.Y + upcomingSize.Y;
        
        // Add spacing between lyrics if both exist
        if (!string.IsNullOrEmpty(mainLyric) && !string.IsNullOrEmpty(upcomingLyric))
        {
            totalHeight += Plugin.Configuration.LyricSpacing * ImGuiHelpers.GlobalScale; // Same spacing as in DrawLyrics
        }
        
        // Add padding
        var padding = ImGui.GetStyle().WindowPadding * Plugin.Configuration.WindowPaddingMultiplier;
        return new Vector2(maxWidth + padding.X, totalHeight + padding.Y);
    }
    
    private float EaseInOutQuad(float t)
    {
        // Smoothstep function: f(t) = 3t² - 2t³
        // Always continuous, beautiful S-curve, no configuration needed
        return 3f * t * t - 2f * t * t * t;
    }
    
#if DEBUG
    public void TriggerFakeLyricsAnimation()
    {
        // Force animation by clearing current state so next draw detects change
        if (Plugin.IsFakeLyricsActive())
        {
            var (currentMain, currentUpcoming) = Plugin.GetFakeLyrics();
            
            // Store current as previous
            _previousMainLyric = _currentMainLyric;
            _previousUpcomingLyric = _currentUpcomingLyric;
            
            // Update current to new values
            _currentMainLyric = currentMain;
            _currentUpcomingLyric = currentUpcoming;
            
            // Start animation if enabled and we have meaningful content
            if (Plugin.Configuration.EnableAnimations && !string.IsNullOrEmpty(_previousMainLyric) && !string.IsNullOrEmpty(currentMain))
            {
                _animationState = AnimationState.Transitioning;
                _animationStartTime = DateTime.UtcNow;
            }
        }
    }
#endif
}
