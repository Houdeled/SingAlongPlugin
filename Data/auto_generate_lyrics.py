#!/usr/bin/env python3
"""
Minimal lyrics generation using OpenLRC
Usage: python auto_generate_lyrics.py <input_audio_file> <output_lrc_file>
"""

import sys
from pathlib import Path

try:
    from openlrc import LRCer
except ImportError:
    print("Error: Install openlrc first: pip install openlrc")
    sys.exit(1)

def main():
    if len(sys.argv) != 3:
        print("Usage: python auto_generate_lyrics.py <input_audio> <output_lrc>")
        print("Example: python auto_generate_lyrics.py Shadowbringers.ogg Shadowbringers.lrc")
        sys.exit(1)
    
    input_file = Path(sys.argv[1])
    output_file = Path(sys.argv[2])
    
    if not input_file.exists():
        print(f"Error: {input_file} not found")
        sys.exit(1)
    
    print(f"Processing: {input_file.name}")
    
    try:
        # Use OpenAI GPT-4o for transcription with VAD disabled
        vad_options = {"threshold": 0}  # Disable VAD by setting threshold to 0
        lrcer = LRCer(chatbot_model='gpt-4o-transcribe', vad_options=vad_options)
        
        # Generate transcription with noise suppression for better vocal detection
        result = lrcer.run(str(input_file), src_lang='en', target_lang='en', skip_trans=True, clear_temp=True, noise_suppress=True)
        
        if result and len(result) > 0:
            generated_lrc = Path(result[0])
            # Move to desired output location
            generated_lrc.rename(output_file)
            print(f"✅ Generated: {output_file}")
        else:
            print("❌ Failed to generate lyrics")
            sys.exit(1)
            
    except Exception as e:
        print(f"❌ Error: {e}")
        sys.exit(1)

if __name__ == '__main__':
    main()