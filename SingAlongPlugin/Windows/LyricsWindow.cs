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
    private static readonly Vector4 UpcomingLyricColor = new(0.7f, 0.7f, 0.7f, 0.8f);
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
    
    // Position tracking for smooth animations
    private float _lastUpcomingLyricPosition = 0f;
    private float _lastMainLyricPosition = 0f;

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
        var scaleFactor = Plugin.Configuration.LyricsScaleFactor;
        
        // Calculate animation progress if needed
        var animationProgress = CalculateAnimationProgress();
        
        // Draw main lyric section
        DrawMainLyric(mainLyric, scaleFactor, animationProgress);
        
        // Spacing between lyrics
        ImGuiHelpers.ScaledDummy(Plugin.Configuration.LyricSpacing);
        
        // Draw upcoming lyric section
        DrawUpcomingLyric(upcomingLyric, scaleFactor, animationProgress);
        
        // Reset font scale
        ImGui.SetWindowFontScale(1f);
    }
    
    private float CalculateAnimationProgress()
    {
        if (_animationState != AnimationState.Transitioning)
            return 0f;
            
        var elapsed = (float)(DateTime.UtcNow - _animationStartTime).TotalMilliseconds;
        var progress = Math.Min(elapsed / Plugin.Configuration.AnimationDurationMs, 1f);
        var easedProgress = EaseInOutQuad(progress);
        
        // Animation complete?
        if (progress >= 1f)
        {
            _animationState = AnimationState.Idle;
        }
        
        return easedProgress;
    }
    
    private void DrawMainLyric(string mainLyric, float scaleFactor, float easedProgress)
    {
        if (_animationState == AnimationState.Transitioning)
        {
            DrawAnimatedMainLyric(mainLyric, scaleFactor, easedProgress);
        }
        else
        {
            // Static display - capture the position for future animations
            var cursorYBeforeMain = ImGui.GetCursorPosY();
            DrawCenteredText(mainLyric, Plugin.Configuration.MainLyricScale * scaleFactor, MainLyricColor, Plugin.LyricsFont);
            _lastMainLyricPosition = cursorYBeforeMain;
        }
    }
    
    private void DrawAnimatedMainLyric(string mainLyric, float scaleFactor, float easedProgress)
    {
        // Draw old main lyric fading out
        if (!string.IsNullOrEmpty(_previousMainLyric))
        {
            var oldMainOffset = Plugin.Configuration.OldMainSlideOffset * easedProgress;
            var oldMainAlpha = 1f - easedProgress;
            DrawCenteredText(_previousMainLyric, Plugin.Configuration.MainLyricScale * scaleFactor, MainLyricColor, Plugin.LyricsFont, oldMainOffset, oldMainAlpha);
        }
        
        // Check if this is a natural progression (upcoming becomes main)
        bool isNaturalProgression = !string.IsNullOrEmpty(_previousUpcomingLyric) && _previousUpcomingLyric == mainLyric;
        
        if (isNaturalProgression)
        {
            DrawUpcomingToMainTransition(mainLyric, scaleFactor, easedProgress);
        }
        else
        {
            // Fallback: completely new lyric appears
            DrawCenteredText(mainLyric, Plugin.Configuration.MainLyricScale * scaleFactor, MainLyricColor, Plugin.LyricsFont, 0f, easedProgress);
        }
    }
    
    private void DrawUpcomingToMainTransition(string mainLyric, float scaleFactor, float easedProgress)
    {
        var upcomingPosition = CalculateUpcomingPosition();
        var mainPosition = _lastMainLyricPosition > 0f ? _lastMainLyricPosition : 0f; // Use stored main position or fallback to 0
        var currentCursorY = ImGui.GetCursorPosY();
        
        // Interpolate between upcoming position and main position
        var targetY = upcomingPosition + (mainPosition - upcomingPosition) * easedProgress;
        var upcomingToMainOffset = targetY - currentCursorY;
        
        var upcomingToMainAlpha = Plugin.Configuration.UpcomingAlphaMultiplier + (1f - Plugin.Configuration.UpcomingAlphaMultiplier) * easedProgress;
        var scaleGrowth = Plugin.Configuration.MainLyricScale - Plugin.Configuration.UpcomingLyricScale;
        var upcomingToMainScale = Plugin.Configuration.UpcomingLyricScale + (scaleGrowth * easedProgress);
        var upcomingToMainColor = LerpColor(UpcomingLyricColor, MainLyricColor, easedProgress);
        
        DrawCenteredText(mainLyric, upcomingToMainScale * scaleFactor, upcomingToMainColor, Plugin.LyricsFont, upcomingToMainOffset, upcomingToMainAlpha);
    }
    
    private void DrawUpcomingLyric(string upcomingLyric, float scaleFactor, float easedProgress)
    {
        if (string.IsNullOrEmpty(upcomingLyric))
            return;
            
        if (_animationState == AnimationState.Transitioning)
        {
            DrawAnimatedUpcomingLyric(upcomingLyric, scaleFactor, easedProgress);
        }
        else
        {
            // Static display - capture the position for future animations
            var cursorYBeforeUpcoming = ImGui.GetCursorPosY();
            DrawCenteredText(upcomingLyric, Plugin.Configuration.UpcomingLyricScale * scaleFactor, UpcomingLyricColor, Plugin.LyricsFont);
            _lastUpcomingLyricPosition = cursorYBeforeUpcoming;
        }
    }
    
    private void DrawAnimatedUpcomingLyric(string upcomingLyric, float scaleFactor, float easedProgress)
    {
        // New upcoming slides up from below its final position
        var upcomingSlideDistance = Plugin.Configuration.LyricSpacing * ImGuiHelpers.GlobalScale;
        var newUpcomingOffset = upcomingSlideDistance * (1f - easedProgress);
        var newUpcomingAlpha = easedProgress * Plugin.Configuration.UpcomingAlphaMultiplier;
        
        DrawCenteredText(upcomingLyric, Plugin.Configuration.UpcomingLyricScale * scaleFactor, UpcomingLyricColor, Plugin.LyricsFont, newUpcomingOffset, newUpcomingAlpha);
    }
    
    private void DrawCenteredText(string text, float scale, Vector4 color, IFontHandle? fontHandle, float yOffset = 0f, float alphaMultiplier = 1f)
    {
        // Apply alpha multiplier to color
        var animatedColor = new Vector4(color.X, color.Y, color.Z, color.W * alphaMultiplier);
        ImGui.PushStyleColor(ImGuiCol.Text, animatedColor);
        
        // Always use window font scale for consistent scaling behavior
        ImGui.SetWindowFontScale(scale);
        
        if (fontHandle != null && fontHandle.Available)
        {
            // Use font handle for crisp rendering
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
            // Fallback without font handle
            var textSize = ImGui.CalcTextSize(text);
            var windowWidth = ImGui.GetWindowSize().X;
            var cursorX = (windowWidth - textSize.X) * 0.5f;
            var currentCursorY = ImGui.GetCursorPosY();
            
            if (cursorX > BackgroundOpacityThreshold) 
                ImGui.SetCursorPosX(cursorX);
            
            // Apply Y offset for animation
            ImGui.SetCursorPosY(currentCursorY + yOffset);
            
            ImGui.TextUnformatted(text);
        }
        
        // Reset scale and pop color
        ImGui.SetWindowFontScale(1f);
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
            return new Vector2(100f, 20f); // Default size when no lyrics are present
        
        // Calculate text sizes
        var scaleFactor = Plugin.Configuration.LyricsScaleFactor;
        
        Vector2 mainSize = Vector2.Zero;
        Vector2 upcomingSize = Vector2.Zero;
        
        if (!string.IsNullOrEmpty(mainLyric))
        {
            if (Plugin.LyricsFont?.Available == true)
            {
                Plugin.LyricsFont.Push();
                mainSize = ImGui.CalcTextSize(mainLyric);
                Plugin.LyricsFont.Pop();
            }
            else
            {
                mainSize = ImGui.CalcTextSize(mainLyric);
            }
            // Apply scale factor manually to calculated size
            mainSize *= Plugin.Configuration.MainLyricScale * scaleFactor;
        }
        
        if (!string.IsNullOrEmpty(upcomingLyric))
        {
            if (Plugin.LyricsFont?.Available == true)
            {
                Plugin.LyricsFont.Push();
                upcomingSize = ImGui.CalcTextSize(upcomingLyric);
                Plugin.LyricsFont.Pop();
            }
            else
            {
                upcomingSize = ImGui.CalcTextSize(upcomingLyric);
            }
            // Apply scale factor manually to calculated size
            upcomingSize *= Plugin.Configuration.UpcomingLyricScale * scaleFactor;
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
    
    private float CalculateUpcomingPosition()
    {
        // Use the actual captured position from the last static render
        // This ensures the animation starts from the exact position where the upcoming lyric was drawn
        if (_lastUpcomingLyricPosition > 0f)
        {
            return _lastUpcomingLyricPosition;
        }
        
        // Fallback to spacing calculation if no position captured yet
        return Plugin.Configuration.LyricSpacing * ImGuiHelpers.GlobalScale;
        
        // Original complex calculation (currently problematic):
        /*
        var scaleFactor = Plugin.Configuration.LyricsScaleFactor;
        
        float mainLyricHeight;
        if (Plugin.MainLyricsFont?.Available == true)
        {
            Plugin.MainLyricsFont.Push();
            mainLyricHeight = ImGui.CalcTextSize("Ay").Y;
            Plugin.MainLyricsFont.Pop();
        }
        else
        {
            var oldScale = ImGui.GetFont().Scale;
            ImGui.GetFont().Scale = Plugin.Configuration.MainLyricScale * scaleFactor;
            mainLyricHeight = ImGui.CalcTextSize("Ay").Y;
            ImGui.GetFont().Scale = oldScale;
        }
        
        return mainLyricHeight + (Plugin.Configuration.LyricSpacing * ImGuiHelpers.GlobalScale);
        */
    }
    
    private Vector4 LerpColor(Vector4 from, Vector4 to, float progress)
    {
        // Linear interpolation between two colors
        return new Vector4(
            from.X + (to.X - from.X) * progress,
            from.Y + (to.Y - from.Y) * progress,
            from.Z + (to.Z - from.Z) * progress,
            from.W + (to.W - from.W) * progress
        );
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
