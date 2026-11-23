#!/usr/bin/env python3
"""
Gera pacote completo de clips de locomocao 60 fps via MoMask (MIT, local, guard 10 GB)
para o piloto Dez A (RUN mega_v6 — TAREFA 2).
"""
from __future__ import annotations

import subprocess
from pathlib import Path

RUNNER = "/mnt/c/Users/Bruno/workspace/3dprinter-setup-ia/tools/animation/momask_to_fbx.py"
OUT_DIR = Path("/mnt/c/Users/Bruno/workspace/game-concerto/unity/Assets/Game/Animations/MoMask")
OUT_DIR.mkdir(parents=True, exist_ok=True)

CLIPS = [
    ("dez_idle_breath",   "a person stands still breathing gently, shifting weight slightly from one foot to the other and looking around calmly", 120, 42, True),
    ("dez_walk_loop",     "a person walks forward naturally at a steady comfortable pace with relaxed swinging arms", 80, 42, True),
    ("dez_walk_start",    "a person starts walking forward from a standing idle position", 48, 43, True),
    ("dez_run_loop",      "a person runs forward energetically leaning slightly forward with pumping arms", 64, 42, True),
    ("dez_turn_left",     "a person standing in place turns 90 degrees to the left stepping naturally with both feet", 56, 42, True),
    ("dez_turn_right",    "a person standing in place turns 90 degrees to the right stepping naturally with both feet", 56, 43, True),
    ("dez_jump_full",     "a person bends knees, jumps high up into the air, and lands softly bending knees to absorb impact", 72, 42, True),
]

for name, prompt, mlen, seed, in_place in CLIPS:
    out_fbx = OUT_DIR / f"{name}.fbx"
    if out_fbx.exists():
        print(f"[batch_momask] Pulando {name} (ja existe)", flush=True)
        continue
    cmd = [
        "python3", RUNNER,
        "--prompt", prompt,
        "--name", name,
        "--seed", str(seed),
        "--motion-length", str(mlen),
        "--fps", "60",
        "--out", str(out_fbx),
    ]
    if in_place:
        cmd.append("--in-place")
    print(f"\n[batch_momask] === Gerando {name} (len={mlen}, seed={seed}) ===", flush=True)
    res = subprocess.run(cmd)
    if res.returncode != 0:
        raise RuntimeError(f"Falha ao gerar {name}")

print("\n[batch_momask] CONCLUIDO 7/7 CLIPS MOMASK 60 FPS!", flush=True)
