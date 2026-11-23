#!/usr/bin/env python3
"""
RUN mega_v4 — TAREFA 2: A/B controlado SDXL-Turbo vs FLUX.1-dev FP8.
Le evidence/concepts/c5_props/provenance.json do batch-1 (v3), regenera os MESMOS 4 prompts
nas MESMAS seeds usando ComfyUI + FLUX.1-dev FP8 (1 unico boot do servidor, guard 10 GB),
e monta o painel A/B lado a lado em director_review/2026-11-22_ab_gerador_imagem/.
"""
from __future__ import annotations

import json
import shutil
import subprocess
from pathlib import Path
from PIL import Image, ImageDraw

CONCERTO = Path("/mnt/c/Users/Bruno/workspace/game-concerto")
FAB3D = Path("/mnt/c/Users/Bruno/workspace/3dprinter-setup-ia")
COMFY_PY = "/home/bruno/.venvs/comfyui/bin/python"
FLUX_RUNNER = str(FAB3D / "tools" / "generator" / "concept_flux.py")

PROV_IN = CONCERTO / "evidence" / "concepts" / "c5_props" / "provenance.json"
AB_DIR = CONCERTO / "director_review" / "2026-11-22_ab_gerador_imagem"


def main() -> None:
    AB_DIR.mkdir(parents=True, exist_ok=True)
    prov = json.loads(PROV_IN.read_text(encoding="utf-8"))
    props = prov["props"]

    jobs = []
    for p in props:
        pid = p["id"]
        # Copia original SDXL-Turbo para o painel A/B
        sdxl_src = CONCERTO / "evidence" / "concepts" / "c5_props" / p["file"]
        sdxl_dst = AB_DIR / f"{pid}__sdxl_turbo.png"
        shutil.copy2(sdxl_src, sdxl_dst)

        flux_dst = AB_DIR / f"{pid}__flux1_dev_fp8.png"
        jobs.append({
            "id": pid,
            "prompt": p["prompt"],
            "seed": p["seed"],
            "size": 768,
            "steps": 20,
            "guidance": 3.5,
            "out": str(flux_dst),
        })

    batch_path = AB_DIR / "_flux_batch_jobs.json"
    batch_path.write_text(json.dumps(jobs, indent=2, ensure_ascii=False), encoding="utf-8")

    print("[ab_test] Rodando FLUX.1-dev FP8 em batch unico para os 4 props do Batch-1...", flush=True)
    res = subprocess.run([COMFY_PY, FLUX_RUNNER, "--batch-json", str(batch_path)])
    if res.returncode != 0:
        raise RuntimeError(f"Falha no batch FLUX A/B (exit {res.returncode})")
    batch_path.unlink(missing_ok=True)

    # Monta contact sheet lado a lado (4 linhas x 2 colunas: SDXL-Turbo | FLUX.1-dev FP8)
    cw, ch = 512, 512
    pad = 18
    header = 64
    row_lbl = 34
    sheet_w = cw * 2 + pad * 3
    sheet_h = header + len(props) * (ch + row_lbl + pad) + pad
    sheet = Image.new("RGB", (sheet_w, sheet_h), (20, 18, 16))
    d = ImageDraw.Draw(sheet)
    d.text((pad, 14), "A/B CONTROLADO — SDXL-Turbo (ESQ) vs FLUX.1-dev FP8 (DIR) — MESMOS PROMPTS & SEEDS", fill=(238, 174, 72))
    d.text((pad, 38), "Coluna A: stabilityai/sdxl-turbo (4 steps)     |     Coluna B: Comfy-Org/flux1-dev-fp8 (20 steps, guidance 3.5)", fill=(200, 192, 180))

    ab_records = []
    for idx, p in enumerate(props):
        pid = p["id"]
        seed = p["seed"]
        sdxl_im = Image.open(AB_DIR / f"{pid}__sdxl_turbo.png").convert("RGB").resize((cw, ch), Image.LANCZOS)
        flux_im = Image.open(AB_DIR / f"{pid}__flux1_dev_fp8.png").convert("RGB").resize((cw, ch), Image.LANCZOS)
        fprov = json.loads((AB_DIR / f"{pid}__flux1_dev_fp8.provenance.json").read_text(encoding="utf-8"))

        y0 = header + idx * (ch + row_lbl + pad)
        d.text((pad, y0 + 8), f"{idx+1}. {pid} (seed={seed}) — FLUX: {fprov['elapsed_sec']}s | peak VRAM {fprov['peak_vram_mib']} MiB", fill=(238, 174, 72))
        sheet.paste(sdxl_im, (pad, y0 + row_lbl))
        sheet.paste(flux_im, (pad * 2 + cw, y0 + row_lbl))

        ab_records.append({
            "id": pid,
            "seed": seed,
            "prompt": p["prompt"],
            "sdxl_png": f"{pid}__sdxl_turbo.png",
            "flux_png": f"{pid}__flux1_dev_fp8.png",
            "flux_elapsed_sec": fprov["elapsed_sec"],
            "flux_peak_vram_mib": fprov["peak_vram_mib"],
            "flux_guard_ok": fprov["guard_ok"],
            "flux_sha256": fprov["weights_sha256"],
        })

    sheet.save(AB_DIR / "ab_contact_sheet_sdxl_vs_flux.png")
    (AB_DIR / "ab_provenance.json").write_text(json.dumps(ab_records, indent=2, ensure_ascii=False), encoding="utf-8")
    print("[ab_test] CONCLUIDO! Contact sheet salvo em ab_contact_sheet_sdxl_vs_flux.png", flush=True)


if __name__ == "__main__":
    main()
