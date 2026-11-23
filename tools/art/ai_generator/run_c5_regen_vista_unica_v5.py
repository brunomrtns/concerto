#!/usr/bin/env python3
"""
RUN mega_v5 — TAREFA 4: Re-3D oficial das 3 pecas do Batch-1 cuja imagem SDXL-Turbo reprovou no gate anti-grade
(c5_prop_bancada_relogio, c5_prop_no_resincronia, c5_prop_valvula_ritmica), usando suas versoes FLUX.1-dev FP8
ja validadas com gate_pass=true (5/5 no A/B).
c5_prop_sirene_mestra ja passou no gate -> intocada (TAREFA 4.3).
Pipeline oficial: preproc C2 -> TripoSG -> TripoSF 512 -> Hunyuan3D-Paint -> patch_repair -> 05_trellis_pbr -> pack_glb.
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
AB_DIR = CONCERTO_ROOT / "director_review" / "2026-11-22_ab_gerador_imagem"
CONCEPT_V5_DIR = CONCERTO_ROOT / "evidence" / "concepts" / "c5_props_flux_v5"
EVID_ROOT = CONCERTO_ROOT / "evidence" / "fab3d" / "c5_props_regen_v5"
WORK_ROOT = Path("/home/bruno/printer3d-work/c5_props_regen_v5")

HUNYUAN_PY = "/home/bruno/.venvs/hunyuan/bin/python"
PIPE_SH = str(FAB3D_ROOT / "tools" / "generator" / "pipeline_imagem3d.sh")
RUN_ASSET = str(FAB3D_ROOT / "pipeline" / "run_asset.py")

PROPS = [
    {"id": "c5_prop_no_resincronia",  "category": "env_tile", "max_tris": 1500},
    {"id": "c5_prop_valvula_ritmica", "category": "env_tile", "max_tris": 1200},
    {"id": "c5_prop_bancada_relogio", "category": "env_tile", "max_tris": 2000},
]


def run_cmd(cmd: list[str], desc: str) -> None:
    print(f"\n[c5_regen_v5] === {desc} ===", flush=True)
    res = subprocess.run(cmd)
    if res.returncode != 0:
        raise RuntimeError(f"Falha em '{desc}' (exit {res.returncode})")


def main() -> None:
    CONCEPT_V5_DIR.mkdir(parents=True, exist_ok=True)
    WORK_ROOT.mkdir(parents=True, exist_ok=True)
    EVID_ROOT.mkdir(parents=True, exist_ok=True)

    summary = []
    for item in PROPS:
        pid = item["id"]
        src_img = AB_DIR / f"{pid}__flux1_dev_fp8.png"
        src_prov = AB_DIR / f"{pid}__flux1_dev_fp8.provenance.json"
        src_gate = AB_DIR / f"{pid}__flux1_dev_fp8.gate.json"
        if not src_img.exists() or not src_prov.exists() or not src_gate.exists():
            raise FileNotFoundError(f"Conceito/provenance/gate ausente para {pid}")
        gate_data = json.loads(src_gate.read_text(encoding="utf-8"))
        if not gate_data.get("gate_pass"):
            raise RuntimeError(f"BLOQUEADO PELO GATE: {src_img} nao tem gate_pass=true!")

        # Copia conceito + provenance + gate para evidence/concepts/c5_props_flux_v5/
        chosen_img = CONCEPT_V5_DIR / f"{pid}.png"
        shutil.copy2(src_img, chosen_img)
        shutil.copy2(src_prov, CONCEPT_V5_DIR / f"{pid}.provenance.json")
        shutil.copy2(src_gate, CONCEPT_V5_DIR / f"{pid}.gate.json")

        wdir = WORK_ROOT / pid
        shape_dir = wdir / "shape_gen"
        run_dir = wdir / "run"
        edir = EVID_ROOT / pid
        edir.mkdir(parents=True, exist_ok=True)

        t0 = time.time()
        recon_dir = shape_dir / "2_triposf512"
        triposg_obj = shape_dir / "1_triposg" / "mesh.obj"
        recons = sorted(recon_dir.glob("*_reconstruction.obj")) if recon_dir.is_dir() else []
        triposf_guard_tripped = False

        if not recons and not triposg_obj.exists():
            res = subprocess.run(["bash", PIPE_SH, str(chosen_img), str(shape_dir), "42"])
            recons = sorted(recon_dir.glob("*_reconstruction.obj")) if recon_dir.is_dir() else []
            if res.returncode != 0 and not recons:
                if triposg_obj.exists() and triposg_obj.stat().st_size > 100_000:
                    triposf_guard_tripped = True
                    print(f"[c5_regen_v5] AVISO: TripoSF 512 guard em {pid}; usando TripoSG.", flush=True)
                else:
                    raise RuntimeError(f"Falha em Shape TripoSG->TripoSF512 para {pid} (exit {res.returncode})")
        elif not recons and triposg_obj.exists():
            triposf_guard_tripped = True

        recon_obj = recons[0] if recons else triposg_obj
        t1 = time.time()

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
            "notes": f"RUN mega_v5 TAREFA 4 re-3D ({pid}) a partir de conceito FLUX.1-dev FP8 gate_pass=true.",
        }
        cfg_path.write_text(json.dumps(cfg, indent=2, ensure_ascii=False), encoding="utf-8")

        glb_4k = run_dir / "staged_models" / f"{pid}__pbr_4k.glb"
        if not glb_4k.exists():
            run_cmd([HUNYUAN_PY, RUN_ASSET, "--config", str(cfg_path), "--out", str(run_dir), "--resume"], f"Paint+PBR+Pack: {pid}")
        t2 = time.time()

        for src_f, dst_name in [(recon_dir / "report.json", "triposf_report.json"), (run_dir / "qa.json", "qa.json")]:
            if src_f.exists():
                shutil.copy2(src_f, edir / dst_name)

        rec = {
            "id": pid,
            "source_concept": f"evidence/concepts/c5_props_flux_v5/{pid}.png (FLUX.1-dev FP8, gate_pass=true)",
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
        print(f"[c5_regen_v5] OK {pid}: total={rec['total_sec']}s | 4K={rec['master_glb_4k_bytes']/1e6:.2f}MB", flush=True)

    (EVID_ROOT / "regen_v5_summary.json").write_text(json.dumps(summary, indent=2, ensure_ascii=False), encoding="utf-8")
    print("\n[c5_regen_v5] CONCLUIDO 3/3 PROPS RE-3D FLUX!", flush=True)


if __name__ == "__main__":
    main()
