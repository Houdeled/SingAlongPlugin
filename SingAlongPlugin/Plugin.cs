using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Plugin;
using System.IO;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using SingAlongPlugin.Windows;

namespace SingAlongPlugin;

public sealed class Plugin : IDalamudPlugin
{
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ITextureProvider TextureProvider { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IClientState ClientState { get; private set; } = null!;
    [PluginService] internal static IDataManager DataManager { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;

    private const string CommandName = "/singalong";

    public Configuration Configuration { get; init; }

    public readonly WindowSystem WindowSystem = new("SingAlongPlugin");
    private ConfigWindow ConfigWindow { get; init; }
    private LyricsWindow LyricsWindow { get; init; }

    public Plugin()
    {
        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();

        ConfigWindow = new ConfigWindow(this);
        LyricsWindow = new LyricsWindow(this);

        WindowSystem.AddWindow(ConfigWindow);
        WindowSystem.AddWindow(LyricsWindow);

        CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "Toggle the SingAlong lyrics window"
        });

        PluginInterface.UiBuilder.Draw += DrawUI;

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

        CommandManager.RemoveHandler(CommandName);
    }

    private void OnCommand(string command, string args)
    {
        // In response to the slash command, toggle the display status of our lyrics ui
        ToggleLyricsUI();
    }

    private void DrawUI() => WindowSystem.Draw();

    public void ToggleConfigUI() => ConfigWindow.Toggle();
    public void ToggleLyricsUI() => LyricsWindow.Toggle();
}
