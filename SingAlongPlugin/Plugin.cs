using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Plugin;
using System.IO;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using SingAlongPlugin.Windows;
using Dalamud.Interface.ManagedFontAtlas;
using Dalamud.Game;
using Dalamud.Game.Text;
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
    [PluginService] internal static IChatGui ChatGui { get; private set; } = null!;

    private const string CommandName = "/singalong";

    public Configuration Configuration { get; init; }

    public readonly WindowSystem WindowSystem = new("SingAlongPlugin");
    private ConfigWindow ConfigWindow { get; init; }
    private LyricsWindow LyricsWindow { get; init; }
#if DEBUG
    private DebugWindow DebugWindow { get; init; }
#endif
    
    // Font management for crisp scaling - single font approach
    public IFontHandle? LyricsFont { get; private set; }
    private float _currentFontScale = 1.0f;
    
    // Music observation
    public MusicObserver? MusicObserver { get; private set; }
    
    // Lyrics management
    private LrcParser? _currentLrcParser = null;
    private string _lyricsFolder => Path.Combine(PluginInterface.ConfigDirectory.FullName, "Lyrics");

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
        
        // Ensure lyrics folder exists
        EnsureLyricsFolder();
        
        // Initialize music observer
        try
        {
            MusicObserver = new MusicObserver(SigScanner, Log);
            MusicObserver.MusicChanged += OnMusicChanged;
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
        
        // Dispose font handle
        LyricsFont?.Dispose();
        
        // Dispose music observer
        if (MusicObserver != null)
        {
            MusicObserver.MusicChanged -= OnMusicChanged;
            MusicObserver.Dispose();
        }

        CommandManager.RemoveHandler(CommandName);
        
        Instance = null;
    }

    private void OnCommand(string command, string args)
    {
        if (!string.IsNullOrEmpty(args))
        {
            var arg = args.Trim().ToLowerInvariant();
            
#if DEBUG
            // In debug mode, check for debug argument
            if (arg == "debug")
            {
                DebugWindow.Toggle();
                return;
            }
#endif
            
            // Check for settings argument
            if (arg == "settings")
            {
                ToggleConfigUI();
                return;
            }
        }
        
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
        
        // Calculate font size based on scale factor
        // Use a larger base size since we'll scale down for upcoming lyrics
        var baseFontSize = 20.0f; // Increased base size for better quality
        var lyricsFontSize = Math.Max(8, baseFontSize * scaleFactor);

        // Dispose of the current Lyrics Font
        LyricsFont?.Dispose();
        LyricsFont = null;

        // Create single font handle
        LyricsFont = PluginInterface.UiBuilder.FontAtlas.NewDelegateFontHandle(e =>
        {
            e.OnPreBuild(tk => tk.AddDalamudDefaultFont(lyricsFontSize));
        });
    }
    
    public float GetCurrentFontScale() => _currentFontScale;
    
    private void OnMusicChanged(object? sender, MusicChangedEventArgs e)
    {
        try
        {
            // When music changes, try to load corresponding lyrics file
            var songId = e.NewBgmId;
            
            if (songId == 0)
            {
                // No music playing, clear current lyrics
                _currentLrcParser = null;
                Log.Debug("Music stopped, cleared lyrics");
                return;
            }
            
            LoadLyricsForSong(songId);
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"Error handling music change to song {e.NewBgmId}");
        }
    }
    
    private void LoadLyricsForSong(uint songId)
    {
        try
        {
            // Construct path to lyrics file
            var lyricsFile = Path.Combine(_lyricsFolder, $"{songId}.lrc");
            
            if (!File.Exists(lyricsFile))
            {
                Log.Debug($"No lyrics file found for song {songId} at {lyricsFile}");
                _currentLrcParser = null;

                return;
            }
            
            // Create new parser and load lyrics
            var parser = new LrcParser();
            if (parser.LoadFromFile(lyricsFile))
            {
                _currentLrcParser = parser;
                Log.Info($"Loaded lyrics for song {songId}: {parser.Metadata.Title} - {parser.Metadata.Artist}");
                
                // Auto-show lyrics window when lyrics are available
                LyricsWindow.IsOpen = true;
            }
            else
            {
                Log.Warning($"Failed to parse lyrics file for song {songId}");
                _currentLrcParser = null;
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"Error loading lyrics for song {songId}");
            _currentLrcParser = null;
            
            ChatGui.Print(new XivChatEntry
            {
                Message = $"[SingAlong] Error loading lyrics for song ID {songId}.",
                Type = XivChatType.ErrorMessage
            });
        }
    }
    
    public LrcParser? GetCurrentLyrics() => _currentLrcParser;
    
#if DEBUG
    public bool IsFakeLyricsActive() => DebugWindow?.IsFakeLyricsActive ?? false;
    
    public (string current, string upcoming) GetFakeLyrics() => DebugWindow?.GetFakeLyrics() ?? (string.Empty, string.Empty);
    
    public void SetLyricsWindowOpen(bool isOpen) => LyricsWindow.IsOpen = isOpen;
    
    public void TriggerFakeLyricsAnimation() => LyricsWindow.TriggerFakeLyricsAnimation();
#endif
    private void EnsureLyricsFolder()
    {
        try
        {
            // Create lyrics folder in config directory if it doesn't exist
            if (!Directory.Exists(_lyricsFolder))
            {
                Directory.CreateDirectory(_lyricsFolder);
                Log.Info($"Created lyrics folder at: {_lyricsFolder}");
                
                // Copy any existing lyrics from the plugin source directory
                var sourceLyricsFolder = Path.Combine(PluginInterface.AssemblyLocation.DirectoryName!, "..", "..", "..", "Lyrics");
                if (Directory.Exists(sourceLyricsFolder))
                {
                    foreach (var file in Directory.GetFiles(sourceLyricsFolder, "*.lrc"))
                    {
                        var fileName = Path.GetFileName(file);
                        var destFile = Path.Combine(_lyricsFolder, fileName);
                        File.Copy(file, destFile, true);
                        Log.Info($"Copied lyrics file: {fileName}");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to ensure lyrics folder exists");
        }
    }
}
