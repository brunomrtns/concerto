#!/usr/bin/env python3
"""
CONCERTO C5 — Heróis Oficiais (S-char-01 Porca D + S-char-02 Dez A) via Pipeline Oficial da Fábrica 3D:
  - Conceitos aprovados por Bruno em 2026-11-22 (FLUX.1-dev FP8, vista única):
      * hero_porca : evidence/concepts/c5_personagens_flux/porca_D.png (seed 342, H=0.45m, <= 2.500 tris)
      * hero_dez   : evidence/concepts/c5_personagens_flux/dez_A.png   (seed 42,  H=1.25m, <= 8.000 tris)
  - Pipeline serial na GPU (seed 42, guard 10 GB):
      preproc C2 -> TripoSG -> TripoSF 512 -> Hunyuan3D-Paint -> patch_repair -> 05_trellis_pbr -> pack_glb
"""
from __future__ import annotations

import json
import shutil
import subprocess
import time
from datetime import datetime, timezone
from pathlib import Path

CONCERTO_ROOT = Path("/mnt/c/Users/Bruno/workspace/game-concerto")
FAB3D_ROOT = Path("/mnt/c/Users/Bruno/workspace/3dprinter-setup-ia")
CONCEPT_DIR = CONCERTO_ROOT / "evidence" / "concepts" / "c5_personagens_flux"
EVID_ROOT = CONCERTO_ROOT / "evidence" / "fab3d" / "c5_herois"
WORK_ROOT = Path("/home/bruno/printer3d-work/c5_herois")

HUNYUAN_PY = "/home/bruno/.venvs/hunyuan/bin/python"
PIPE_SH = str(FAB3D_ROOT / "tools" / "generator" / "pipeline_imagem3d.sh")
RUN_ASSET = str(FAB3D_ROOT / "pipeline" / "run_asset.py")

HEROIS = [
    {
        "id": "hero_porca",
        "concept_png": "porca_D.png",
        "category": "env_tile",
        "max_tris": 2500,
        "height_m": 0.45,
        "tex_size": 1024,
    },
    {
        "id": "hero_dez",
        "concept_png": "dez_A.png",
        "category": "player_character",
        "max_tris": 8000,
        "height_m": 1.25,
        "tex_size": 1024,
    },
]


def run_cmd(cmd: list[str], desc: str) -> None:
    print(f"\n[c5_herois] === {desc} ===", flush=True)
    res = subprocess.run(cmd)
    if res.returncode != 0:
        raise RuntimeError(f"Falha em '{desc}' (exit {res.returncode})")


def main() -> None:
    WORK_ROOT.mkdir(parents=True, exist_ok=True)
    EVID_ROOT.mkdir(parents=True, exist_ok=True)

    summary = []
    for item in HEROIS:
        hid = item["id"]
        src_img = CONCEPT_DIR / item["concept_png"]
        if not src_img.exists():
            raise FileNotFoundError(f"Conceito aprovado nao encontrado: {src_img}")

        wdir = WORK_ROOT / hid
        shape_dir = wdir / "shape_gen"
        run_dir = wdir / "run"
        edir = EVID_ROOT / hid
        edir.mkdir(parents=True, exist_ok=True)

        t0 = time.time()
        recon_dir = shape_dir / "2_triposf512"
        triposg_obj = shape_dir / "1_triposg" / "mesh.obj"
        recons = sorted(recon_dir.glob("*_reconstruction.obj")) if recon_dir.is_dir() else []
        triposf_guard_tripped = False

        if not recons and not triposg_obj.exists():
            res = subprocess.run(["bash", PIPE_SH, str(src_img), str(shape_dir), "42"])
            recons = sorted(recon_dir.glob("*_reconstruction.obj")) if recon_dir.is_dir() else []
            if res.returncode != 0 and not recons:
                if triposg_obj.exists() and triposg_obj.stat().st_size > 100_000:
                    triposf_guard_tripped = True
                    print(f"[c5_herois] AVISO: TripoSF 512 guard em {hid}; usando malha TripoSG.", flush=True)
                else:
                    raise RuntimeError(f"Falha em Shape TripoSG->TripoSF512 para {hid} (exit {res.returncode})")
        elif not recons and triposg_obj.exists():
            triposf_guard_tripped = True

        recon_obj = recons[0] if recons else triposg_obj
        t1 = time.time()

        cfg_path = edir / "job_asset.json"
        cfg = {
            "id": hid,
            "variant_id": "pbr",
            "mesh": str(recon_obj),
            "uv": str(recon_obj),
            "tier": "single",
            "input_images": [str(shape_dir / "entrada_preproc.png")],
            "outputs": {
                "mode_8k": "pbr_8k",
                "mode_4k": "pbr_4k",
                "glb_names": [f"{hid}__pbr_8k.glb", f"{hid}__pbr_4k.glb"],
            },
            "notes": f"CONCERTO C5 Heroi aprovado por Bruno ({hid} <- {item['concept_png']}, FLUX.1-dev FP8).",
        }
        cfg_path.write_text(json.dumps(cfg, indent=2, ensure_ascii=False), encoding="utf-8")

        glb_4k = run_dir / "staged_models" / f"{hid}__pbr_4k.glb"
        if not glb_4k.exists():
            run_cmd([HUNYUAN_PY, RUN_ASSET, "--config", str(cfg_path), "--out", str(run_dir), "--resume"], f"Paint+PBR+Pack: {hid}")
        t2 = time.time()

        for src_f, dst_name in [(recon_dir / "report.json", "triposf_report.json"), (run_dir / "qa.json", "qa.json")]:
            if src_f.exists():
                shutil.copy2(src_f, edir / dst_name)

        rec = {
            "id": hid,
            "approved_concept": f"evidence/concepts/c5_personagens_flux/{item['concept_png']}",
            "category": item["category"],
            "max_tris": item["max_tris"],
            "height_m": item["height_m"],
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
        print(f"[c5_herois] OK {hid}: total={rec['total_sec']}s | 4K={rec['master_glb_4k_bytes']/1e6:.2f}MB", flush=True)

    (EVID_ROOT / "herois_summary.json").write_text(json.dumps(summary, indent=2, ensure_ascii=False), encoding="utf-8")
    print("\n[c5_herois] CONCLUIDO 2/2 HEROIS (PORCA D + DEZ A)!", flush=True)


if __name__ == "__main__":
    main()
