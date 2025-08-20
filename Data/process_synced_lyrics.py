#!/usr/bin/env python3
"""
Process synced lyrics files and convert them to SingAlongPlugin format
Usage: python process_synced_lyrics.py
"""

import csv
import sys
from pathlib import Path
import re

def normalize_title(title):
    """Normalize title for comparison by removing special chars and lowercasing"""
    return re.sub(r'[^\w\s]', '', title.lower().strip())

def load_bgm_mapping():
    """Load the BGM mapping CSV"""
    mapping = {}
    
    csv_path = Path("bgm_mapping.csv")
    if not csv_path.exists():
        print("Error: bgm_mapping.csv not found")
        return mapping
    
    with open(csv_path, 'r', encoding='utf-8') as f:
        reader = csv.DictReader(f)
        for row in reader:
            bgm_id = row['ID']
            name = row['NAME'].strip()
            alt_title = row['ALT_TITLE'].strip()
            file_path = row['FILE_PATH'].strip()
            
            # Skip BGM entries that contain "Ride" in file path (compressed versions)
            if "Ride" in file_path:
                continue
            
            if name:
                mapping[normalize_title(name)] = bgm_id
            
            if alt_title and alt_title != name:
                mapping[normalize_title(alt_title)] = bgm_id
    
    return mapping

def find_lrc_files():
    """Find all LRC files in SyncedLyrics directory"""
    synced_lyrics_dir = Path("SyncedLyrics")
    if not synced_lyrics_dir.exists():
        return []
    lrc_files = list(synced_lyrics_dir.glob("*.lrc"))
    return sorted(lrc_files)

def get_song_title_from_filename(lrc_file):
    """Extract song title from LRC filename"""
    return lrc_file.stem  # filename without extension

def copy_lrc_to_plugin(lrc_file, bgm_id):
    """Copy and rename LRC file to SingAlongPlugin/Lyrics directory"""
    plugin_lyrics_dir = Path("../SingAlongPlugin/Lyrics")
    
    # Create directory if it doesn't exist
    plugin_lyrics_dir.mkdir(parents=True, exist_ok=True)
    
    # Target filename with BGM ID
    target_file = plugin_lyrics_dir / f"{bgm_id}.lrc"
    
    try:
        # Read source file and copy to target
        with open(lrc_file, 'r', encoding='utf-8') as src:
            content = src.read()
        
        with open(target_file, 'w', encoding='utf-8') as dst:
            dst.write(content)
        
        return True
    except Exception as e:
        print(f"Error copying {lrc_file.name}: {e}")
        return False

def main():
    """Main function to process synced lyrics"""
    print("Processing synced lyrics files...")
    
    # Load BGM mapping
    print("Loading BGM mapping...")
    bgm_mapping = load_bgm_mapping()
    
    if not bgm_mapping:
        print("No BGM mapping loaded. Exiting.")
        return
    
    print(f"Loaded {len(bgm_mapping)} BGM entries")
    
    # Find LRC files
    lrc_files = find_lrc_files()
    
    if not lrc_files:
        print("No LRC files found in current directory")
        return
    
    print(f"Found {len(lrc_files)} LRC files")
    
    # Process each LRC file
    processed_count = 0
    not_found_count = 0
    
    print("\nProcessing files...")
    print("-" * 40)
    
    for lrc_file in lrc_files:
        song_title = get_song_title_from_filename(lrc_file)
        normalized_title = normalize_title(song_title)
        
        if normalized_title in bgm_mapping:
            bgm_id = bgm_mapping[normalized_title]
            
            if copy_lrc_to_plugin(lrc_file, bgm_id):
                print(f"[PROCESSED] {song_title} -> {bgm_id}.lrc")
                processed_count += 1
            else:
                print(f"[ERROR] Failed to copy {song_title}")
        else:
            print(f"[NOT FOUND] No BGM ID found for: {song_title}")
            not_found_count += 1
    
    # Summary
    print("-" * 40)
    print(f"Summary:")
    print(f"  Processed: {processed_count}")
    print(f"  Not found: {not_found_count}")
    print(f"  Total: {len(lrc_files)}")
    
    if processed_count > 0:
        print(f"\nFiles saved to: ../SingAlongPlugin/Lyrics/")

if __name__ == '__main__':
    main()