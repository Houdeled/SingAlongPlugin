using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace SingAlongPlugin;

class TestScream
{
    static void Main(string[] args)
    {
        Console.WriteLine("=== LRC Parser Test Program ===\n");
        Console.WriteLine($"Current working directory: {Environment.CurrentDirectory}");
        // Path to your Scream.lrc file
        string lrcPath = Path.Combine("..", "..", "SingAlongPlugin", "Lyrics", "Scream.lrc");
         lrcPath = Path.Combine("Scream.lrc");
        
        if (!File.Exists(lrcPath))
        {
            Console.WriteLine($"ERROR: Could not find Scream.lrc at: {lrcPath}");
            Console.WriteLine("Please make sure you have placed Scream.lrc in the SingAlongPlugin/Lyrics/ folder");
            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
            return;
        }
        
        // Create and test the parser
        var parser = new LrcParser();
        bool loaded = parser.LoadFromFile(lrcPath);
        
        if (!loaded)
        {
            Console.WriteLine("ERROR: Failed to load LRC file!");
            Console.WriteLine($"Parser found {parser.Lyrics.Count} lyrics after attempting to load");
            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
            return;
        }
        
        Console.WriteLine("✅ LRC file loaded successfully!\n");
        
        // Display metadata
        Console.WriteLine("=== METADATA ===");
        Console.WriteLine($"Title: {parser.Metadata.Title}");
        Console.WriteLine($"Artist: {parser.Metadata.Artist}");
        Console.WriteLine($"Album: {parser.Metadata.Album}");
        Console.WriteLine($"Creator: {parser.Metadata.Creator}");
        Console.WriteLine($"Offset: {parser.Metadata.OffsetMs}ms");
        Console.WriteLine($"Total lyrics lines: {parser.Lyrics.Count}\n");
        
        // Display first few lyrics with timestamps
        Console.WriteLine("=== FIRST 10 LYRICS ===");
        for (int i = 0; i < Math.Min(10, parser.Lyrics.Count); i++)
        {
            var lyric = parser.Lyrics[i];
            Console.WriteLine($"[{lyric.Timestamp:mm\\:ss\\.ff}] {lyric.Text}");
        }
        
        Console.WriteLine("\n=== INTERACTIVE TEST ===");
        Console.WriteLine("Enter time in format MM:SS (e.g., 01:30) to get lyrics at that time.");
        Console.WriteLine("Type 'quit' to exit.\n");
        
        while (true)
        {
            Console.Write("Time (MM:SS): ");
            string? input = Console.ReadLine();
            
            if (string.IsNullOrEmpty(input) || input.ToLower() == "quit")
                break;
                
            if (TimeSpan.TryParseExact(input, @"mm\:ss", null, out TimeSpan testTime))
            {
                string currentLyric = parser.GetCurrentLyric(testTime);
                string nextLyric = parser.GetNextLyric(testTime);
                TimeSpan nextTime = parser.GetNextTimestamp(testTime);
                
                Console.WriteLine($"  Current lyric: \"{currentLyric}\"");
                Console.WriteLine($"  Next lyric: \"{nextLyric}\"");
                Console.WriteLine($"  Next timestamp: {nextTime:mm\\:ss\\.ff}");
                Console.WriteLine();
            }
            else
            {
                Console.WriteLine("Invalid time format. Use MM:SS (e.g., 01:30)\n");
            }
        }
        
        Console.WriteLine("Goodbye!");
    }
}
