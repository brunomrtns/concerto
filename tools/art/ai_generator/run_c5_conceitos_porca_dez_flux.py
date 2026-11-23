#!/usr/bin/env python3
"""
RUN mega_v4 — TAREFA 3 (Ordem direta do dono):
Refaz as 4 candidatas de conceito 2D da PORCA (H=0.45m) + 4 candidatas do DEZ BASE (H=1.25m)
usando o vencedor do A/B: FLUX.1-dev FP8 via ComfyUI headless (--lowvram --reserve-vram 3.5, guard 10 GB).
ZERO 3D nesta etapa — apenas conceitos 2D de alta fidelidade + regua de escala + painel 4x2.
"""
from __future__ import annotations

import json
import subprocess
from datetime import datetime, timezone
from pathlib import Path
from PIL import Image, ImageDraw

CONCERTO = Path("/mnt/c/Users/Bruno/workspace/game-concerto")
FAB3D = Path("/mnt/c/Users/Bruno/workspace/3dprinter-setup-ia")
COMFY_PY = "/home/bruno/.venvs/comfyui/bin/python"
FLUX_RUNNER = str(FAB3D / "tools" / "generator" / "concept_flux.py")

EVID_DIR = CONCERTO / "evidence" / "concepts" / "c5_personagens_flux"
REV_DIR = CONCERTO / "director_review" / "2026-11-22_c5_conceitos_porca_dez_flux"

SEEDS = [("A", 42), ("B", 142), ("C", 242), ("D", 342)]

SPECS = [
    {
        "character": "porca",
        "height_m": 0.45,
        "prompt": (
            "stylized 3d game character concept art, clockwork-whimsy Pixar/Ratchet-and-Clank quality, "
            "a cute small floating spherical brass and dark-iron maintenance drone hovering in mid-air with NO legs and NO stand, "
            "large expressive single glowing warm amber camera lens eye in front, riveted weathered brass and copper shell plates, "
            "compact brass side propeller fins, small articulated magnetic repair claw folded neatly underneath, "
            "single isolated object centered occupying 75 percent of frame, 3/4 view, pure solid white studio background, "
            "soft diffuse 3-point lighting, matte weathered metal, no shadows on floor, no text, no watermark"
        ),
    },
    {
        "character": "dez",
        "height_m": 1.25,
        "prompt": (
            "stylized 3d game hero character concept art, clockwork-whimsy Pixar/Ratchet-and-Clank quality, "
            "a small friendly bipedal alien mechanic boy standing upright in clean A-pose with arms slightly open, "
            "full body visible from head to boots, warm oxidized-brass organic alien skin with subtle glowing golden freckles, "
            "expressive friendly face with big warm eyes, wearing a patched fireproof copper-colored mechanic jumpsuit, "
            "oversized riveted leather and brass tool belt around waist, wrist-mounted brass turnstile gadget, "
            "EXACTLY 4 arms clearly readable: 2 main alien arms at shoulders PLUS 2 smaller brass mechanical auxiliary helper arms "
            "mounted on the back of his tool belt, single isolated character centered occupying 80 percent of frame, "
            "front 3/4 view, pure solid white studio background, diffuse 3-point studio lighting, no weapons, no text, no watermark"
        ),
    },
]


def draw_scale_ruler(im: Image.Image, height_m: float, label: str) -> Image.Image:
    w, h = im.size
    margin = 64
    out = Image.new("RGB", (w + margin, h), (246, 245, 242))
    out.paste(im, (margin, 0))
    d = ImageDraw.Draw(out)

    top_y = int(h * 0.10)
    bot_y = int(h * 0.90)
    rx = 42
    d.line([(rx, top_y), (rx, bot_y)], fill=(55, 48, 42), width=3)
    for i in range(6):
        y = bot_y - int((bot_y - top_y) * (i / 5))
        tick = 14 if i in (0, 5) else 8
        d.line([(rx - tick, y), (rx + 4, y)], fill=(55, 48, 42), width=2)
        d.text((4, y - 6), f"{height_m * (i / 5):.2f}m", fill=(45, 38, 32))

    d.rectangle([(margin + 12, 12), (margin + 235, 44)], fill=(24, 20, 18))
    d.text((margin + 22, 22), label, fill=(238, 174, 72))
    return out


def make_grid_2x2(images: list[Image.Image], title: str) -> Image.Image:
    cw, ch = images[0].size
    header, pad = 54, 16
    grid = Image.new("RGB", (cw * 2 + pad * 3, ch * 2 + pad * 3 + header), (22, 19, 16))
    d = ImageDraw.Draw(grid)
    d.text((pad, 18), title, fill=(238, 174, 72))
    for idx, im in enumerate(images):
        r, c = divmod(idx, 2)
        grid.paste(im, (pad + c * (cw + pad), header + pad + r * (ch + pad)))
    return grid


def make_unified_4x2(porca_imgs: list[Image.Image], dez_imgs: list[Image.Image]) -> Image.Image:
    tw, th = 420, 388
    pad, header = 14, 56
    grid = Image.new("RGB", (tw * 4 + pad * 5, th * 2 + pad * 3 + header), (20, 17, 14))
    d = ImageDraw.Draw(grid)
    d.text((pad, 18), "CONCERTO C5 — FLUX.1-dev FP8: PORCA (A-D, H=0.45m) & DEZ BASE (A-D, H=1.25m) — KIT 4.0", fill=(238, 174, 72))
    for i, im in enumerate(porca_imgs):
        grid.paste(im.resize((tw, th), Image.LANCZOS), (pad + i * (tw + pad), header + pad))
    for i, im in enumerate(dez_imgs):
        grid.paste(im.resize((tw, th), Image.LANCZOS), (pad + i * (tw + pad), header + pad * 2 + th))
    return grid


def main() -> None:
    EVID_DIR.mkdir(parents=True, exist_ok=True)
    REV_DIR.mkdir(parents=True, exist_ok=True)

    jobs = []
    for spec in SPECS:
        ch = spec["character"]
        for letter, seed in SEEDS:
            jobs.append({
                "character": ch,
                "candidate": letter,
                "height_m": spec["height_m"],
                "prompt": spec["prompt"],
                "seed": seed,
                "size": 768,
                "steps": 22,
                "guidance": 3.5,
                "out": str(EVID_DIR / f"{ch}_{letter}.png"),
            })

    batch_file = EVID_DIR / "_flux_chars_batch.json"
    batch_file.write_text(json.dumps(jobs, indent=2, ensure_ascii=False), encoding="utf-8")

    print("[c5_chars_flux] Gerando 8 conceitos (4 Porca + 4 Dez) em 1 unico boot ComfyUI FLUX.1-dev FP8...", flush=True)
    res = subprocess.run([COMFY_PY, FLUX_RUNNER, "--batch-json", str(batch_file)])
    if res.returncode != 0:
        raise RuntimeError(f"Falha no batch FLUX personagens (exit {res.returncode})")
    batch_file.unlink(missing_ok=True)

    rendered: dict[str, list[Image.Image]] = {"porca": [], "dez": []}
    prov_all = {
        "run": "RUN mega_v4 — TAREFA 3 (Re-geracao Porca + Dez com FLUX.1-dev FP8)",
        "date": "2026-11-22",
        "model": "Comfy-Org/flux1-dev-fp8",
        "plan": "docs/art/plano-producao-personagens.md",
        "candidates": [],
    }

    for j in jobs:
        ch, letter, seed, hm = j["character"], j["candidate"], j["seed"], j["height_m"]
        src = Path(j["out"])
        pjson = json.loads(src.with_suffix(".provenance.json").read_text(encoding="utf-8"))
        ruled = draw_scale_ruler(Image.open(src).convert("RGB"), hm, f"{ch.upper()} {letter} (FLUX seed {seed})")
        ruled.save(REV_DIR / f"{ch}_{letter}.png")
        rendered[ch].append(ruled)
        prov_all["candidates"].append({
            "character": ch,
            "candidate": letter,
            "seed": seed,
            "height_m": hm,
            "elapsed_sec": pjson["elapsed_sec"],
            "peak_vram_mib": pjson["peak_vram_mib"],
            "guard_ok": pjson["guard_ok"],
            "weights_sha256": pjson["weights_sha256"],
            "raw_png": f"evidence/concepts/c5_personagens_flux/{ch}_{letter}.png",
            "review_png": f"director_review/2026-11-22_c5_conceitos_porca_dez_flux/{ch}_{letter}.png",
        })

    make_grid_2x2(rendered["porca"], "PORCA (FLUX.1-dev FP8, H=0.45m) — Candidatas A, B, C, D").save(REV_DIR / "porca_4_candidatas_flux_sheet.png")
    make_grid_2x2(rendered["dez"], "DEZ BASE (FLUX.1-dev FP8, H=1.25m, 4 Bracos) — Candidatas A, B, C, D").save(REV_DIR / "dez_4_candidatas_flux_sheet.png")
    make_unified_4x2(rendered["porca"], rendered["dez"]).save(REV_DIR / "painel_unificado_porca_dez_flux_4x2.png")

    (EVID_DIR / "provenance.json").write_text(json.dumps(prov_all, indent=2, ensure_ascii=False), encoding="utf-8")
    (REV_DIR / "provenance.json").write_text(json.dumps(prov_all, indent=2, ensure_ascii=False), encoding="utf-8")

    html = """<!doctype html><html lang="pt-BR"><head><meta charset="utf-8">
<title>CONCERTO C5 — Conceitos Porca &amp; Dez (FLUX.1-dev FP8)</title>
<style>body{background:#141210;color:#ece3d5;font-family:system-ui,sans-serif;margin:24px}
h1,h2{color:#e8a13c}img{max-width:100%;border-radius:8px;border:1px solid #3a3026;margin-bottom:18px}
.row{display:grid;grid-template-columns:repeat(4,1fr);gap:12px;margin-bottom:24px}</style></head><body>
<h1>CONCERTO C5 — Conceitos Porca &amp; Dez Base (FLUX.1-dev FP8)</h1>
<p>Gerados via <code>Comfy-Org/flux1-dev-fp8</code> (ComfyUI headless <code>--lowvram --reserve-vram 3.5</code>, guard 10 GB). Zero 3D nesta tarefa.</p>
<h2>Painel Unificado (4×2)</h2>
<img src="painel_unificado_porca_dez_flux_4x2.png">
<h2>Porca — Candidatas A–D (H = 0,45 m)</h2>
<img src="porca_4_candidatas_flux_sheet.png">
<h2>Dez Base — Candidatas A–D (H = 1,25 m, 4 Braços)</h2>
<img src="dez_4_candidatas_flux_sheet.png">
</body></html>"""
    (REV_DIR / "review_c5_conceitos_flux.html").write_text(html, encoding="utf-8")
    print("[c5_chars_flux] CONCLUIDO 8/8 CONCEITOS FLUX + SHEETS + HTML!", flush=True)


if __name__ == "__main__":
    main()
