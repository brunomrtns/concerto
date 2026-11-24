#!/usr/bin/env bash
set -euo pipefail
CAP="/mnt/c/Users/Bruno/workspace/game-concerto/captures/cs01_abertura"
AUD="/mnt/c/Users/Bruno/workspace/game-concerto/unity/Assets/Game/Audio/Cutscenes/CS01"

ffmpeg -y -framerate 30 -i "$CAP/frames_raw/frame_%05d.jpg" \
  -i "$AUD/cs01_musicbox_motif_TEMP.wav" \
  -i "$AUD/cs01_radio_chime_TEMP.wav" \
  -i "$AUD/cs01_machine_hum_TEMP.wav" \
  -i "$AUD/cs01_echo_memory_TEMP.wav" \
  -i "$AUD/cs01_musicbox_motif_TEMP.wav" \
  -filter_complex "[1:a]adelay=200:all=1,volume=0.7[a1];[2:a]adelay=2000:all=1,volume=0.55[a2];[3:a]adelay=10200:all=1,aloop=loop=3:size=705600,atrim=0:61,volume=0.45[a3];[4:a]adelay=47500:all=1,volume=0.75[a4];[5:a]adelay=61500:all=1,volume=0.8[a5];[a1][a2][a3][a4][a5]amix=inputs=5:duration=longest:normalize=0[aout]" \
  -map 0:v -map "[aout]" -c:v libx264 -pix_fmt yuv420p -crf 20 -preset fast -c:a aac -b:a 192k -shortest "$CAP/cs01_concerto_1080p.mp4"

rm -rf "$CAP/frames_raw"
ls -lh "$CAP"
