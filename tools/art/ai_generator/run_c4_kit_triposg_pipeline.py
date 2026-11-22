#!/usr/bin/env python3
"""
Fase 4.0 — Fluxo completo oficial da Fabrica 3D (3dprinter-setup-ia):
  1. Malha 3D via TripoSG (/home/bruno/.venvs/triposg + triposg_runner.py)
  2. Pintura PBR + Pack GLB via 3dprinter-setup-ia/pipeline/run_asset.py
     (00_preflight xatlas -> 02_paint_hunyuan -> 04_patch_repair -> 05_trellis_pbr -> 06_pack_glb)
"""
from __future__ import annotations

import json
import os
import subprocess
import sys
from pathlib import Path
from PIL import Image

CONCEPT_DIR = Path("/mnt/c/Users/Bruno/workspace/game-concerto/evidence/concepts/c4_kit_triposg")
WORK_DIR = Path("/mnt/c/Users/Bruno/workspace/game-concerto/evidence/art/c4_kit_triposg")
WORK_DIR.mkdir(parents=True, exist_ok=True)

FAB3D_REPO = Path("/mnt/c/Users/Bruno/workspace/3dprinter-setup-ia")
TRIPOSG_PY = "/home/bruno/.venvs/triposg/bin/python"
HUNYUAN_PY = "/home/bruno/.venvs/hunyuan/bin/python"
TRIPOSG_RUNNER = str(FAB3D_REPO / "tools" / "generator" / "triposg_runner.py")
RUN_ASSET = str(FAB3D_REPO / "pipeline" / "run_asset.py")

ASSETS = [
    {"id": "caldeiral_env_floor_tile",    "concept": "c4_env_floor_tile.png",    "seed": 501, "pad_white": True},
    {"id": "caldeiral_env_wall_panel",    "concept": "c4_env_wall_panel.png",    "seed": 502, "pad_white": True},
    {"id": "caldeiral_env_portal_pillar", "concept": "c4_env_portal_pillar.png", "seed": 413, "pad_white": False},
    {"id": "caldeiral_env_vapor_pipe",    "concept": "c4_env_vapor_pipe.png",    "seed": 504, "pad_white": False},
]


def prepare_padded(src: Path, dst: Path, pad_white: bool) -> Path:
    if not pad_white:
        return src
    im = Image.open(src).convert("RGB")
    w, h = im.size
    side = int(max(w, h) * 1.24)
    canvas = Image.new("RGB", (side, side), (255, 255, 255))
    canvas.paste(im, ((side - w) // 2, (side - h) // 2))
    dst.parent.mkdir(parents=True, exist_ok=True)
    canvas.save(dst)
    return dst


def run_cmd(cmd: list[str], env: dict | None = None, desc: str = "") -> None:
    print(f"\n[c4_triposg_pipe] === {desc} ===", flush=True)
    print(f"[c4_triposg_pipe] CMD: {' '.join(cmd)}", flush=True)
    res = subprocess.run(cmd, env=env)
    if res.returncode != 0:
        raise RuntimeError(f"Falha em '{desc}' (exit {res.returncode})")


def main() -> None:
    env_tsg = os.environ.copy()
    env_tsg["TRIPOSG_HOME"] = "/home/bruno/tools/TripoSG"

    for item in ASSETS:
        aid = item["id"]
        adir = WORK_DIR / aid
        tsg_dir = adir / "triposg"
        pipe_dir = adir / "pipe_out"
        mesh_obj = tsg_dir / "mesh.obj"

        src_img = CONCEPT_DIR / item["concept"]
        cond_img = prepare_padded(src_img, adir / f"{aid}_input_padded.png", item["pad_white"])

        # 1. Estagio 1: TripoSG (gera malha solida mesh.obj)
        if not mesh_obj.exists() or mesh_obj.stat().st_size < 10_000:
            run_cmd(
                [TRIPOSG_PY, TRIPOSG_RUNNER, "--image", str(cond_img), "--output-dir", str(tsg_dir), "--seed", str(item["seed"])],
                env=env_tsg,
                desc=f"TripoSG mesh -> {aid}",
            )
        else:
            print(f"[c4_triposg_pipe] Reusando mesh TripoSG existente: {mesh_obj}", flush=True)

        # 2. Config JSON no formato oficial do 3dprinter-setup-ia/pipeline/run_asset.py
        cfg_path = adir / f"{aid}.json"
        cfg = {
            "id": aid,
            "variant_id": "pbr",
            "mesh": str(mesh_obj),
            "uv": str(mesh_obj),
            "tier": "single",
            "input_images": [str(cond_img)],
            "outputs": {
                "mode_8k": "pbr_8k",
                "mode_4k": "pbr_4k",
                "glb_names": [f"{aid}__pbr_8k.glb", f"{aid}__pbr_4k.glb"],
            },
            "qa": {
                "lap_min": 600.0,
                "patch_max_pct": 0.08,
                "sat_min_pct": 3.0,
                "sat_max_pct": 90.0,
            },
            "notes": f"CONCERTO Fase 4.0 Kit de Direcao de Arte ({aid}) via TripoSG + 3dprinter-setup-ia pipeline.",
        }
        cfg_path.write_text(json.dumps(cfg, indent=2, ensure_ascii=False), encoding="utf-8")

        # 3. Estagio 2: Pipeline oficial do 3dprinter-setup-ia (Hunyuan3D-Paint + PBR + pack_glb)
        glb_4k = pipe_dir / "06_pack_glb" / f"{aid}__pbr_4k.glb"
        if not glb_4k.exists():
            run_cmd(
                [HUNYUAN_PY, RUN_ASSET, "--config", str(cfg_path), "--out", str(pipe_dir), "--resume"],
                desc=f"3dprinter-setup-ia run_asset.py -> {aid}",
            )
        else:
            print(f"[c4_triposg_pipe] Reusando GLB 4K existente: {glb_4k}", flush=True)

    print("\n[c4_triposg_pipe] CONCLUIDO 4/4 ASSETS VIA TRIPOSG + PIPELINE 3DPRINTER-SETUP-IA!", flush=True)


if __name__ == "__main__":
    main()
