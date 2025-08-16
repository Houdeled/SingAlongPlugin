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

    // The below exist just to make saving less cumbersome
    public void Save()
    {
        Plugin.PluginInterface.SavePluginConfig(this);
        // Update font when configuration is saved
        Plugin.Instance?.UpdateLyricsFont();
    }
}
