using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace SingAlongPlugin;

public struct LrcLine
{
    public TimeSpan Timestamp { get; init; }
    public string Text { get; init; }
    
    public LrcLine(TimeSpan timestamp, string text)
    {
        Timestamp = timestamp;
        Text = text;
    }
}

public class LrcMetadata
{
    public string Title { get; set; } = string.Empty;
    public string Artist { get; set; } = string.Empty;
    public string Album { get; set; } = string.Empty;
    public string Creator { get; set; } = string.Empty;
    public int OffsetMs { get; set; } = 0;
}

public class LrcParser
{
    private readonly List<LrcLine> _lyrics = new();
    private readonly LrcMetadata _metadata = new();
    private int _lastSearchIndex = 0;
    
    private static readonly Regex TimeTagRegex = new(@"\[(\d{2}):(\d{2})\.(\d{2,3})\](.*)");
    private static readonly Regex MetadataRegex = new(@"\[([a-zA-Z]+):([^\]]*)\]");

    // Internal timing offset (hidden from user interface)
    // Positive values delay lyrics, negative values advance lyrics
    private const int TimingOffsetMs = 650; // Milliseconds offset for lyrics synchronization

    public LrcMetadata Metadata => _metadata;
    public IReadOnlyList<LrcLine> Lyrics => _lyrics;
    public bool IsLoaded => _lyrics.Count > 0;
    
    public bool LoadFromFile(string filePath)
    {
        if (!File.Exists(filePath))
            return false;
            
        _lyrics.Clear();
        _lastSearchIndex = 0;
        
        try
        {
            var lines = File.ReadAllLines(filePath);
            ParseLrcContent(lines);
            
            // Sort lyrics by timestamp for efficient searching
            _lyrics.Sort((a, b) => a.Timestamp.CompareTo(b.Timestamp));
            
            return _lyrics.Count > 0;
        }
        catch
        {
            return false;
        }
    }
    
    private void ParseLrcContent(string[] lines)
    {
        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;
                
            // Try to parse as metadata first
            var metadataMatch = MetadataRegex.Match(line);
            if (metadataMatch.Success)
            {
                ParseMetadata(metadataMatch.Groups[1].Value.ToLower(), metadataMatch.Groups[2].Value);
                continue;
            }
            
            // Try to parse as timed lyric
            var timeMatch = TimeTagRegex.Match(line);
            if (timeMatch.Success)
            {
                var minutes = int.Parse(timeMatch.Groups[1].Value);
                var seconds = int.Parse(timeMatch.Groups[2].Value);
                var subsecondValue = int.Parse(timeMatch.Groups[3].Value);
                var text = timeMatch.Groups[4].Value.Trim();
                
                // Handle both centiseconds (2 digits) and milliseconds (3 digits)
                int milliseconds;
                if (timeMatch.Groups[3].Value.Length == 2)
                {
                    // Centiseconds format: multiply by 10 to get milliseconds
                    milliseconds = subsecondValue * 10;
                }
                else
                {
                    // Milliseconds format: use directly
                    milliseconds = subsecondValue;
                }
                
                var timestamp = new TimeSpan(0, 0, minutes, seconds, milliseconds);
                
                // Apply offset if specified
                if (_metadata.OffsetMs != 0)
                    timestamp = timestamp.Add(TimeSpan.FromMilliseconds(_metadata.OffsetMs));
                    
                _lyrics.Add(new LrcLine(timestamp, text));
            }
        }
    }
    
    private void ParseMetadata(string tag, string value)
    {
        switch (tag)
        {
            case "ti":
                _metadata.Title = value;
                break;
            case "ar":
                _metadata.Artist = value;
                break;
            case "al":
                _metadata.Album = value;
                break;
            case "by":
                _metadata.Creator = value;
                break;
            case "offset":
                if (int.TryParse(value, out var offset))
                    _metadata.OffsetMs = offset;
                break;
        }
    }
    
    public string GetCurrentLyric(TimeSpan currentTime)
    {
        if (!IsLoaded)
            return string.Empty;
            
        // Apply timing offset (subtract offset since lyrics come too soon)
        var adjustedTime = currentTime.Subtract(TimeSpan.FromMilliseconds(TimingOffsetMs));
        
        // Use cached index as starting point for better performance
        var index = FindLyricIndex(adjustedTime);
        
        if (index >= 0 && index < _lyrics.Count)
        {
            _lastSearchIndex = index;
            return _lyrics[index].Text;
        }
        
        return string.Empty;
    }
    
    public string GetNextLyric(TimeSpan currentTime)
    {
        if (!IsLoaded)
            return string.Empty;
            
        // Apply timing offset (subtract offset since lyrics come too soon)
        var adjustedTime = currentTime.Subtract(TimeSpan.FromMilliseconds(TimingOffsetMs));
        
        var currentIndex = FindLyricIndex(adjustedTime);
        
        // If we're before the first lyric (currentIndex is -1), the next lyric is the first one
        if (currentIndex == -1)
        {
            if (_lyrics.Count > 0)
                return _lyrics[0].Text;
            return string.Empty;
        }
        
        var nextIndex = currentIndex + 1;
        
        if (nextIndex >= 0 && nextIndex < _lyrics.Count)
            return _lyrics[nextIndex].Text;
            
        return string.Empty;
    }
    
    public TimeSpan GetNextTimestamp(TimeSpan currentTime)
    {
        if (!IsLoaded)
            return TimeSpan.Zero;
            
        // Apply timing offset (subtract offset since lyrics come too soon)
        var adjustedTime = currentTime.Subtract(TimeSpan.FromMilliseconds(TimingOffsetMs));
        
        var currentIndex = FindLyricIndex(adjustedTime);
        var nextIndex = currentIndex + 1;
        
        if (nextIndex >= 0 && nextIndex < _lyrics.Count)
            return _lyrics[nextIndex].Timestamp;
            
        return TimeSpan.Zero;
    }
    
    private int FindLyricIndex(TimeSpan currentTime)
    {
        if (!IsLoaded)
            return -1;

        // Apply timing offset (subtract offset since lyrics come too soon)
        var adjustedTime = currentTime.Subtract(TimeSpan.FromMilliseconds(TimingOffsetMs));

        // Binary search for efficiency with large lyric files
        int left = 0;
        int right = _lyrics.Count - 1;
        int result = -1;
        
        while (left <= right)
        {
            int mid = (left + right) / 2;
            
            if (_lyrics[mid].Timestamp <= adjustedTime)
            {
                result = mid;
                left = mid + 1;
            }
            else
            {
                right = mid - 1;
            }
        }
        
        return result;
    }
}
