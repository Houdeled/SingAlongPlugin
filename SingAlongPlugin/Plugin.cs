using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Plugin;
using System.IO;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using SingAlongPlugin.Windows;
using Dalamud.Interface.ManagedFontAtlas;
using Dalamud.Game;
using System;
using System.Threading.Tasks;

namespace SingAlongPlugin;

public sealed class Plugin : IDalamudPlugin
{
    public static Plugin? Instance { get; private set; }
    
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ITextureProvider TextureProvider { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IClientState ClientState { get; private set; } = null!;
    [PluginService] internal static IDataManager DataManager { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;
    [PluginService] internal static ISigScanner SigScanner { get; private set; } = null!;

    private const string CommandName = "/singalong";

    public Configuration Configuration { get; init; }

    public readonly WindowSystem WindowSystem = new("SingAlongPlugin");
    private ConfigWindow ConfigWindow { get; init; }
    private LyricsWindow LyricsWindow { get; init; }
#if DEBUG
    private DebugWindow DebugWindow { get; init; }
#endif
    
    // Font management for crisp scaling
    public IFontHandle? MainLyricsFont { get; private set; }
    public IFontHandle? UpcomingLyricsFont { get; private set; }
    private float _currentFontScale = 1.0f;
    
    // Music observation
    public MusicObserver? MusicObserver { get; private set; }

    public Plugin()
    {
        Instance = this;
        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();

        ConfigWindow = new ConfigWindow(this);
        LyricsWindow = new LyricsWindow(this);
#if DEBUG
        DebugWindow = new DebugWindow(this);
#endif

        WindowSystem.AddWindow(ConfigWindow);
        WindowSystem.AddWindow(LyricsWindow);
#if DEBUG
        WindowSystem.AddWindow(DebugWindow);
#endif

        CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "Toggle the SingAlong lyrics window"
        });

        PluginInterface.UiBuilder.Draw += DrawUI;
        
        // Initialize lyrics font for crisp scaling
        UpdateLyricsFont();
        
        // Initialize music observer
        try
        {
            MusicObserver = new MusicObserver(SigScanner, Log);
            Log.Info("MusicObserver initialized successfully");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to initialize MusicObserver");
        }

        // This adds a button to the plugin installer entry of this plugin which allows
        // toggling the display status of the configuration ui
        PluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUI;

        // Adds another button doing the same but for the lyrics ui of the plugin
        PluginInterface.UiBuilder.OpenMainUi += ToggleLyricsUI;

        // Add a simple message to the log with level set to information
        // Use /xllog to open the log window in-game
        // Example Output: 00:57:54.959 | INF | [SamplePlugin] ===A cool log message from Sample Plugin===
        Log.Information($"===A cool log message from {PluginInterface.Manifest.Name}===");
    }

    public void Dispose()
    {
        WindowSystem.RemoveAllWindows();

        ConfigWindow.Dispose();
        LyricsWindow.Dispose();
#if DEBUG
        DebugWindow.Dispose();
#endif
        
        // Dispose font handles
        MainLyricsFont?.Dispose();
        UpcomingLyricsFont?.Dispose();
        
        // Dispose music observer
        MusicObserver?.Dispose();

        CommandManager.RemoveHandler(CommandName);
        
        Instance = null;
    }

    private void OnCommand(string command, string args)
    {
#if DEBUG
        // In debug mode, check for debug argument
        if (!string.IsNullOrEmpty(args) && args.Trim().ToLowerInvariant() == "debug")
        {
            DebugWindow.Toggle();
            return;
        }
#endif
        // In response to the slash command, toggle the display status of our lyrics ui
        ToggleLyricsUI();
    }

    private void DrawUI() => WindowSystem.Draw();

    public void ToggleConfigUI() => ConfigWindow.Toggle();
    public void ToggleLyricsUI() => LyricsWindow.Toggle();
    
    public void UpdateLyricsFont()
    {
        // Only update if plugin is fully initialized
        if (PluginInterface?.UiBuilder?.FontAtlas == null || Configuration == null)
            return;
            
        var scaleFactor = Configuration.LyricsScaleFactor;
        
        // If scale hasn't changed significantly, don't update
        if (Math.Abs(_currentFontScale - scaleFactor) < 0.01f)
            return;
            
        _currentFontScale = scaleFactor;
        
        // Calculate font sizes based on scale factor
        var baseFontSize = 16.0f;
        var mainLyricSize = Math.Max(8, baseFontSize * 1.5f * scaleFactor);
        var upcomingLyricSize = Math.Max(8, baseFontSize * 1.0f * scaleFactor);

        // Dispose of the current Lyrics Font
        MainLyricsFont?.Dispose();
        MainLyricsFont = null;

        UpcomingLyricsFont?.Dispose();
        UpcomingLyricsFont = null;

        // Create new font handles
        MainLyricsFont = PluginInterface.UiBuilder.FontAtlas.NewDelegateFontHandle(e =>
        {
            e.OnPreBuild(tk => tk.AddDalamudDefaultFont(mainLyricSize));
        });
        
        UpcomingLyricsFont = PluginInterface.UiBuilder.FontAtlas.NewDelegateFontHandle(e =>
        {
            e.OnPreBuild(tk => tk.AddDalamudDefaultFont(upcomingLyricSize));
        });
    }
    
    public float GetCurrentFontScale() => _currentFontScale;
}
