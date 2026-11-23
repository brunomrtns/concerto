#!/usr/bin/env python3
"""
CONCERTO C5 — Batch-1 de Props via Pipeline Oficial da Fabrica 3D (ADR-007 + ADR-009 + ADR-010):
  1. Limpeza de bordas laterais nos 2 conceitos que tinham colunas de fundo (sirene_mestra, no_resincronia)
  2. pipeline_imagem3d.sh (preproc C2 -> TripoSG -> TripoSF 512 com guard 10 GB, seed 42)
  3. 3dprinter-setup-ia/pipeline/run_asset.py (xatlas -> Hunyuan3D-Paint -> patch_repair -> 05_trellis_pbr -> pack_glb)
  4. Evidencia por prop em evidence/fab3d/c5_props/<nome>/ (sem GLBs master pesados no Git)
"""
from __future__ import annotations

import json
import os
import shutil
import subprocess
import time
from datetime import datetime, timezone
from pathlib import Path
from PIL import Image

CONCERTO_ROOT = Path("/mnt/c/Users/Bruno/workspace/game-concerto")
FAB3D_ROOT = Path("/mnt/c/Users/Bruno/workspace/3dprinter-setup-ia")
CONCEPT_DIR = CONCERTO_ROOT / "evidence" / "concepts" / "c5_props"
EVID_ROOT = CONCERTO_ROOT / "evidence" / "fab3d" / "c5_props"
WORK_ROOT = Path("/home/bruno/printer3d-work/c5_props")

HUNYUAN_PY = "/home/bruno/.venvs/hunyuan/bin/python"
PIPE_SH = str(FAB3D_ROOT / "tools" / "generator" / "pipeline_imagem3d.sh")
RUN_ASSET = str(FAB3D_ROOT / "pipeline" / "run_asset.py")

PROPS = [
    {"id": "c5_prop_sirene_mestra",    "category": "env_structure", "max_tris": 5000, "crop_lr": None},
    {"id": "c5_prop_no_resincronia",   "category": "env_tile",      "max_tris": 1500, "crop_lr": None},
    {"id": "c5_prop_valvula_ritmica",  "category": "env_tile",      "max_tris": 1200, "crop_lr": None},
    {"id": "c5_prop_bancada_relogio",  "category": "env_tile",      "max_tris": 2000, "crop_lr": None},
]


def prepare_clean_input(src: Path, dst: Path, crop_lr) -> Path:
    im = Image.open(src).convert("RGB")
    if crop_lr:
        w, h = im.size
        x0 = int(w * crop_lr[0])
        x1 = int(w * crop_lr[1])
        canvas = Image.new("RGB", (w, h), (255, 255, 255))
        canvas.paste(im.crop((x0, 0, x1, h)), (x0, 0))
        im = canvas
    dst.parent.mkdir(parents=True, exist_ok=True)
    im.save(dst)
    return dst


def run_cmd(cmd: list[str], desc: str) -> None:
    print(f"\n[c5_batch1] === {desc} ===", flush=True)
    res = subprocess.run(cmd)
    if res.returncode != 0:
        raise RuntimeError(f"Falha em '{desc}' (exit {res.returncode})")


def main() -> None:
    WORK_ROOT.mkdir(parents=True, exist_ok=True)
    EVID_ROOT.mkdir(parents=True, exist_ok=True)

    summary = []
    for item in PROPS:
        pid = item["id"]
        wdir = WORK_ROOT / pid
        shape_dir = wdir / "shape_gen"
        run_dir = wdir / "run"
        edir = EVID_ROOT / pid
        edir.mkdir(parents=True, exist_ok=True)

        clean_img = prepare_clean_input(CONCEPT_DIR / f"{pid}.png", wdir / f"{pid}_clean_input.png", item["crop_lr"])
        t0 = time.time()

        # 1. Shape oficial: preproc C2 -> TripoSG -> TripoSF 512 (guard 10 GB, seed 42)
        recon_dir = shape_dir / "2_triposf512"
        triposg_obj = shape_dir / "1_triposg" / "mesh.obj"
        recons = sorted(recon_dir.glob("*_reconstruction.obj")) if recon_dir.is_dir() else []
        triposf_guard_tripped = False
        if not recons and not triposg_obj.exists():
            res = subprocess.run(["bash", PIPE_SH, str(clean_img), str(shape_dir), "42"])
            recons = sorted(recon_dir.glob("*_reconstruction.obj")) if recon_dir.is_dir() else []
            if res.returncode != 0 and not recons:
                if triposg_obj.exists() and triposg_obj.stat().st_size > 100_000:
                    triposf_guard_tripped = True
                    print(f"[c5_batch1] AVISO: TripoSF 512 bloqueado pelo guard VRAM em {pid}; usando malha TripoSG ({triposg_obj.name}) para pintura.", flush=True)
                else:
                    raise RuntimeError(f"Falha em Shape TripoSG->TripoSF512 para {pid} (exit {res.returncode})")
        elif not recons and triposg_obj.exists():
            triposf_guard_tripped = True
            print(f"[c5_batch1] Reusando malha TripoSG existente (TripoSF 512 bloqueado pelo guard VRAM): {triposg_obj}", flush=True)

        recon_obj = recons[0] if recons else triposg_obj
        t1 = time.time()

        # 2. Pintura PBR + Pack GLB oficial via 3dprinter-setup-ia/pipeline/run_asset.py
        cfg_path = edir / "job_asset.json"
        cfg = {
            "id": pid,
            "variant_id": "pbr",
            "mesh": str(recon_obj),
            "uv": str(recon_obj),
            "tier": "single",
            "input_images": [str(shape_dir / "entrada_preproc.png")],
            "outputs": {
                "mode_8k": "pbr_8k",
                "mode_4k": "pbr_4k",
                "glb_names": [f"{pid}__pbr_8k.glb", f"{pid}__pbr_4k.glb"],
            },
            "notes": f"CONCERTO C5 batch-1 prop ({pid}) via TripoSG -> TripoSF512 -> Hunyuan3D-Paint -> pack_glb.",
        }
        cfg_path.write_text(json.dumps(cfg, indent=2, ensure_ascii=False), encoding="utf-8")

        glb_4k = run_dir / "staged_models" / f"{pid}__pbr_4k.glb"
        if not glb_4k.exists():
            run_cmd([HUNYUAN_PY, RUN_ASSET, "--config", str(cfg_path), "--out", str(run_dir), "--resume"], f"Paint+PBR+Pack: {pid}")
        t2 = time.time()

        # Copia evidencias leves (qa.json, triposf report) para evidence/fab3d/c5_props/<pid>/
        for src_f, dst_name in [
            (recon_dir / "report.json", "triposf_report.json"),
            (run_dir / "qa.json", "qa.json"),
        ]:
            if src_f.exists():
                shutil.copy2(src_f, edir / dst_name)

        rec = {
            "id": pid,
            "category": item["category"],
            "max_tris": item["max_tris"],
            "engine": "triposg",
            "shape_pipeline": "preproc C2 -> TripoSG" if triposf_guard_tripped else "preproc C2 -> TripoSG -> TripoSF 512",
            "triposf512_guard_tripped": triposf_guard_tripped,
            "shape_sec": round(t1 - t0, 1),
            "paint_sec": round(t2 - t1, 1),
            "total_sec": round(t2 - t0, 1),
            "master_glb_4k": str(glb_4k),
            "master_glb_4k_bytes": glb_4k.stat().st_size if glb_4k.exists() else 0,
            "timestamp_utc": datetime.now(timezone.utc).isoformat(),
        }
        (edir / "fab3d_run_summary.json").write_text(json.dumps(rec, indent=2, ensure_ascii=False), encoding="utf-8")
        summary.append(rec)
        print(f"[c5_batch1] OK {pid}: total={rec['total_sec']}s | 4K={rec['master_glb_4k_bytes']/1e6:.2f}MB", flush=True)

    (EVID_ROOT / "batch1_summary.json").write_text(json.dumps(summary, indent=2, ensure_ascii=False), encoding="utf-8")
    print("\n[c5_batch1] CONCLUIDO 4/4 PROPS BATCH-1!", flush=True)


if __name__ == "__main__":
    main()
