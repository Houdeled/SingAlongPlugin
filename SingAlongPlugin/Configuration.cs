using Dalamud.Configuration;
using System;

namespace SingAlongPlugin;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 0;

    // Lyrics Window Settings
    public float LyricsScaleFactor { get; set; } = 1.0f;
    public float BackgroundOpacity { get; set; } = 0.0f;
    public bool LockWindow { get; set; } = true;
    public bool EnableAnimations { get; set; } = true;
    
    // Animation Settings
    public float AnimationDurationMs { get; set; } = 500f;
    public float UpcomingToMainOffset { get; set; } = 65f; // 50f + 15f
    public float OldMainSlideOffset { get; set; } = -50f;
    public float NewLyricSlideOffset { get; set; } = 50f;
    public float UpcomingAlphaMultiplier { get; set; } = 0.8f;
    public float UpcomingScaleGrowth { get; set; } = 0.5f;
    
    // Layout Settings
    public float LyricSpacing { get; set; } = 15.0f;
    public float SizeChangeThreshold { get; set; } = 0.1f;
    public float MainLyricScale { get; set; } = 1.5f;
    public float UpcomingLyricScale { get; set; } = 1.0f;
    public int WindowPaddingMultiplier { get; set; } = 2;
    
    // Easing Settings - Now using fixed smoothstep function (3t² - 2t³)
    // No configuration needed - always smooth and continuous

    // The below exist just to make saving less cumbersome
    public void Save()
    {
        Plugin.PluginInterface.SavePluginConfig(this);
        // Update font when configuration is saved
        Plugin.Instance?.UpdateLyricsFont();
    }
}
