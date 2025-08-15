using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Plugin.Services;
using Dalamud.Game;
using FFXIVClientStructs.FFXIV.Client.System.Framework;

namespace SingAlongPlugin;

public static class BGMAddressResolver
{
    private static nint _baseAddress;
    private static nint _musicManager;
    
    public static nint AddRestartId { get; private set; }
    public static nint GetSpecialMode { get; private set; }
    public static IPluginLog? PluginLog { get; set; }
    public static ISigScanner? SigScanner { get; set; }

    public static unsafe void Init()
    {
        if (SigScanner == null || PluginLog == null) return;
        
        _baseAddress = SigScanner.GetStaticAddressFromSig("48 8B 05 ?? ?? ?? ?? 48 85 C0 74 51 83 78 08 0B");
        AddRestartId = SigScanner.ScanText("E8 ?? ?? ?? ?? 88 9E ?? ?? ?? ?? 84 DB");
        GetSpecialMode = SigScanner.ScanText("40 57 48 83 EC 20 48 83 79 ?? ?? 48 8B F9 0F 84 ?? ?? ?? ?? 0F B6 51 4D");
            
        PluginLog.Debug($"[BGMAddressResolver] init: base address at {_baseAddress.ToInt64():X}");
            
        var musicLoc = SigScanner.ScanText("48 8B 8F ?? ?? ?? ?? 85 C0 0F 95 C2 E8 ?? ?? ?? ?? 48 8B 9F");
        var musicOffset = Marshal.ReadInt32(musicLoc + 3);
        _musicManager = Marshal.ReadIntPtr(new nint(Framework.Instance()) + musicOffset);
        PluginLog.Debug($"[BGMAddressResolver] MusicManager found at {_musicManager.ToInt64():X}");
    }
    
    public static nint BGMSceneManager
    {
        get
        {
            var baseObject = Marshal.ReadIntPtr(_baseAddress);
            return baseObject;
        }
    }
        
    public static nint BGMSceneList
    {
        get
        {
            var baseObject = Marshal.ReadIntPtr(_baseAddress);
            // I've never seen this happen, but the game checks for it in a number of places
            return baseObject == nint.Zero ? nint.Zero : Marshal.ReadIntPtr(baseObject + 0xC0);
        }
    }

    public static bool StreamingEnabled
    {
        get
        {
            var ret = Marshal.ReadByte(_musicManager + 50);
            return ret == 1;
        }
    }
}

[StructLayout(LayoutKind.Sequential)]
public unsafe struct BGMScene
{
    public int SceneIndex;
    public int Flags; // SceneFlags
    private int Padding1;
    public ushort BgmReference;       // Reference to sheet; BGM, BGMSwitch, BGMSituation
    public ushort BgmId;              // Actual BGM that's playing
    public ushort PreviousBgmId;      // BGM that was playing before this one
    public byte TimerEnable;          // whether the timer automatically counts up
    private byte Padding2;
    public float Timer;               // if enabled, seems to always count from 0 to 6
    private fixed byte DisableRestartList[24]; // 'vector' of bgm ids that will be restarted
    private byte Unknown1;
    private uint Unknown2;
    private uint Unknown3;
    private uint Unknown4;
    private uint Unknown5;
    private uint Unknown6;
    private ulong Unknown7;
    private uint Unknown8;
    private byte Unknown9;
    private byte Unknown10;
    private byte Unknown11;
    private byte Unknown12;
    private float Unknown13;
    private uint Unknown14;
}


public class MusicObserver : IDisposable
{
    private readonly IPluginLog _log;
    
    private ushort _currentBgmId = 0;
    private DateTime _songStartTime = DateTime.UtcNow;
    private uint _currentSongTimestampMs = 0;
    private bool _disposed = false;
    private CancellationTokenSource _cancellationToken;
    
    private const int ControlBlockCount = 12;
    
    // Event fired when the background music changes
    public event EventHandler<MusicChangedEventArgs>? MusicChanged;
    
    public MusicObserver(ISigScanner sigScanner, IPluginLog log)
    {
        _log = log;
        _cancellationToken = new CancellationTokenSource();
        
        // Setup BGM address resolver (matching exact OrchestrionPlugin pattern)
        try
        {
            BGMAddressResolver.PluginLog = log;
            BGMAddressResolver.SigScanner = sigScanner;
            BGMAddressResolver.Init();
            
            StartUpdate();
            _log.Info("MusicObserver initialized with BGM address resolver");
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Failed to initialize MusicObserver");
            throw;
        }
    }
    
    private void StartUpdate()
    {
        System.Threading.Tasks.Task.Factory.StartNew(async () =>
        {
            while (!_cancellationToken.Token.IsCancellationRequested)
            {
                try
                {
                    Update();
                    await System.Threading.Tasks.Task.Delay(100, _cancellationToken.Token); // Check every 100ms for smooth updates
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _log.Error(ex, "Error in MusicObserver update loop");
                }
            }
        }, _cancellationToken.Token, TaskCreationOptions.LongRunning, TaskScheduler.Default);
    }

    private unsafe void Update()
    {
        ushort currentSong = 0;
        
        if (BGMAddressResolver.BGMSceneList != IntPtr.Zero)
        {
            var bgms = (BGMScene*)BGMAddressResolver.BGMSceneList.ToPointer();
            
            // Find the highest priority active song (exact logic from newer OrchestrionPlugin)
            for (int sceneIdx = 0; sceneIdx < ControlBlockCount; sceneIdx++)
            {
                if (bgms[sceneIdx].BgmReference == 0) continue;

                if (bgms[sceneIdx].BgmId != 0 && bgms[sceneIdx].BgmId != 9999)
                {
                    currentSong = bgms[sceneIdx].BgmId; // Using BgmId from new structure
                    break;
                }
            }
        }
        
        // Check if music has changed
        if (_currentBgmId != currentSong)
        {
            var oldBgmId = _currentBgmId;
            _currentBgmId = currentSong;
            _songStartTime = DateTime.UtcNow; // Record when this song started
            _currentSongTimestampMs = 0; // Reset song timestamp to 0 for new song
            
            _log.Debug($"Music changed from {oldBgmId} to {currentSong}");
            
            // Fire the music changed event with timestamp = 0 for new song
            MusicChanged?.Invoke(this, new MusicChangedEventArgs(oldBgmId, currentSong));
        }
        else if (_currentBgmId != 0)
        {
            // Same song, update elapsed time
            _currentSongTimestampMs = (uint)(DateTime.UtcNow - _songStartTime).TotalMilliseconds;
        }
    }
    
    
    public uint GetCurrentBgm() => _currentBgmId;
    public uint GetCurrentSongTimestamp() => _currentSongTimestampMs;
    
    public void Dispose()
    {
        if (_disposed) return;
        
        _cancellationToken.Cancel();
        _disposed = true;
        
        _log.Info("MusicObserver disposed");
    }
}

public class MusicChangedEventArgs : EventArgs
{
    public uint OldBgmId { get; }
    public uint NewBgmId { get; }
    
    public MusicChangedEventArgs(uint oldBgmId, uint newBgmId)
    {
        OldBgmId = oldBgmId;
        NewBgmId = newBgmId;
    }
}
