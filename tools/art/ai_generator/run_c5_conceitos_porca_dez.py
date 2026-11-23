#!/usr/bin/env python3
"""
CONCERTO C5 — TAREFA 3: 4 Candidatas de Conceito para Porca + 4 para Dez Base.
Segue docs/art/plano-producao-personagens.md e docs/art/art-bible.yaml via concept_bridge.py (sdxl-turbo local).
ZERO 3D nesta tarefa — apenas conceitos 2D para gate de aprovacao do diretor.
"""
from __future__ import annotations

import json
import subprocess
import sys
from datetime import datetime, timezone
from pathlib import Path
from PIL import Image, ImageDraw

ROOT = Path(r"C:\Users\Bruno\workspace\game-concerto")
BRIDGE = ROOT / "tools" / "art" / "ai_generator" / "concept_bridge.py"
ART_BIBLE = ROOT / "docs" / "art" / "art-bible.yaml"
EVID_DIR = ROOT / "evidence" / "concepts" / "c5_personagens"
REV_DIR = ROOT / "director_review" / "2026-11-22_c5_conceitos_porca_dez"

SEEDS = [("A", 42), ("B", 142), ("C", 242), ("D", 342)]

SPECS = [
    {
        "character": "porca",
        "title": "Porca (Drone de Manutencao)",
        "height_m": 0.45,
        "extra_prompt": (
            "full body centered 80 percent of frame, 3/4 view, riveted weathered brass and dark iron "
            "clockwork sphere drone, Kit 4.0 brass copper dark iron language, single warm glowing lens eye, "
            "articulated magnetic claw underneath, compact brass propeller fins, pure clean light neutral "
            "studio background, diffuse 3-point lighting, no text, no watermark, no scenery"
        ),
    },
    {
        "character": "dez",
        "title": "Dez (Heroi Base — 4 Bracos)",
        "height_m": 1.25,
        "extra_prompt": (
            "full body head to toe visible, centered 80 percent of frame, A-pose arms slightly open, "
            "front 3/4 view, small friendly alien clockwork mechanic standing upright, 4 arms clearly readable "
            "(2 main arms plus 2 smaller auxiliary mechanical arms mounted on oversized riveted brass tool belt), "
            "Kit 4.0 weathered brass copper dark iron language, patched copper fireproof jumpsuit, "
            "pure clean light neutral studio background, diffuse 3-point lighting, no text, no watermark, no scenery"
        ),
    },
]


def draw_scale_ruler(im: Image.Image, height_m: float, label: str) -> Image.Image:
    """Adiciona regua de escala limpa na margem esquerda (art-bible: scale_ruler required_for_characters)."""
    w, h = im.size
    margin = 64
    out = Image.new("RGB", (w + margin, h), (242, 240, 236))
    out.paste(im, (margin, 0))
    d = ImageDraw.Draw(out)

    top_y = int(h * 0.10)
    bot_y = int(h * 0.90)
    rx = 42
    d.line([(rx, top_y), (rx, bot_y)], fill=(60, 52, 46), width=3)
    steps = 5
    for i in range(steps + 1):
        y = bot_y - int((bot_y - top_y) * (i / steps))
        tick_len = 14 if i in (0, steps) else 8
        d.line([(rx - tick_len, y), (rx + 4, y)], fill=(60, 52, 46), width=2)
        val = height_m * (i / steps)
        d.text((4, y - 6), f"{val:.2f}m", fill=(45, 38, 32))

    # Badge do candidato no canto superior direito
    d.rectangle([(margin + 12, 12), (margin + 170, 44)], fill=(28, 24, 20))
    d.text((margin + 22, 22), label, fill=(238, 174, 72))
    return out


def make_grid_2x2(images: list[Image.Image], title: str) -> Image.Image:
    cw, ch = images[0].size
    header = 54
    pad = 16
    grid = Image.new("RGB", (cw * 2 + pad * 3, ch * 2 + pad * 3 + header), (22, 19, 16))
    d = ImageDraw.Draw(grid)
    d.text((pad, 18), title, fill=(238, 174, 72))
    for idx, im in enumerate(images):
        r, c = divmod(idx, 2)
        grid.paste(im, (pad + c * (cw + pad), header + pad + r * (ch + pad)))
    return grid


def make_unified_4x2(porca_imgs: list[Image.Image], dez_imgs: list[Image.Image]) -> Image.Image:
    tw, th = 420, 388
    pad = 14
    header = 56
    grid = Image.new("RGB", (tw * 4 + pad * 5, th * 2 + pad * 3 + header), (20, 17, 14))
    d = ImageDraw.Draw(grid)
    d.text((pad, 18), "CONCERTO C5 — CONCEITOS PORCA (A-D, H=0.45m) & DEZ BASE (A-D, H=1.25m) — KIT 4.0", fill=(238, 174, 72))
    for i, im in enumerate(porca_imgs):
        grid.paste(im.resize((tw, th), Image.LANCZOS), (pad + i * (tw + pad), header + pad))
    for i, im in enumerate(dez_imgs):
        grid.paste(im.resize((tw, th), Image.LANCZOS), (pad + i * (tw + pad), header + pad * 2 + th))
    return grid


def main() -> None:
    EVID_DIR.mkdir(parents=True, exist_ok=True)
    REV_DIR.mkdir(parents=True, exist_ok=True)

    provenance = {
        "run": "RUN mega_v3 — TAREFA 3 (Conceitos Porca + Dez Base)",
        "date": "2026-11-22",
        "model": "stabilityai/sdxl-turbo",
        "art_bible": "docs/art/art-bible.yaml",
        "plan": "docs/art/plano-producao-personagens.md",
        "fuso": "caldeiral",
        "size": 768,
        "steps": 4,
        "candidates": [],
    }

    rendered_by_char: dict[str, list[Image.Image]] = {}

    for spec in SPECS:
        ch = spec["character"]
        rendered_by_char[ch] = []
        for letter, seed in SEEDS:
            slug = f"c5_conceito_{ch}_{letter}"
            out_evid = EVID_DIR / f"{ch}_{letter}.png"
            out_rev = REV_DIR / f"{ch}_{letter}.png"

            cmd = [
                sys.executable, str(BRIDGE),
                "--name", slug,
                "--art-bible", str(ART_BIBLE),
                "--character", ch,
                "--fuso", "caldeiral",
                "--prompt", spec["extra_prompt"],
                "--model", "stabilityai/sdxl-turbo",
                "--size", "768",
                "--steps", "4",
                "--seed", str(seed),
                "--out", str(out_evid),
            ]
            print(f"\n[c5_conceitos] Gerando {ch.upper()} {letter} (seed={seed})...", flush=True)
            res = subprocess.run(cmd)
            if res.returncode != 0 or not out_evid.exists():
                raise RuntimeError(f"Falha ao gerar {slug} (exit {res.returncode})")

            raw_im = Image.open(out_evid).convert("RGB")
            ruled_im = draw_scale_ruler(raw_im, spec["height_m"], f"{ch.upper()} {letter} (seed {seed})")
            ruled_im.save(out_rev)
            rendered_by_char[ch].append(ruled_im)

            provenance["candidates"].append({
                "character": ch,
                "candidate": letter,
                "seed": seed,
                "height_m": spec["height_m"],
                "raw_png": f"evidence/concepts/c5_personagens/{ch}_{letter}.png",
                "review_png": f"director_review/2026-11-22_c5_conceitos_porca_dez/{ch}_{letter}.png",
                "timestamp_utc": datetime.now(timezone.utc).isoformat(),
            })

    # Sheets 2x2 individuais + painel unificado 4x2
    porca_sheet = make_grid_2x2(rendered_by_char["porca"], "PORCA (Drone Manutencao, H=0.45m) — Candidatas A, B, C, D (Kit 4.0 / Caldeiral)")
    porca_sheet.save(REV_DIR / "porca_4_candidatas_sheet.png")

    dez_sheet = make_grid_2x2(rendered_by_char["dez"], "DEZ BASE (Heroi 4 Bracos, H=1.25m) — Candidatas A, B, C, D (Kit 4.0 / Caldeiral)")
    dez_sheet.save(REV_DIR / "dez_4_candidatas_sheet.png")

    unified = make_unified_4x2(rendered_by_char["porca"], rendered_by_char["dez"])
    unified.save(REV_DIR / "painel_unificado_porca_dez_4x2.png")

    (EVID_DIR / "provenance.json").write_text(json.dumps(provenance, indent=2, ensure_ascii=False), encoding="utf-8")
    (REV_DIR / "provenance.json").write_text(json.dumps(provenance, indent=2, ensure_ascii=False), encoding="utf-8")

    # LEIA-ME + HTML
    readme = (
        "O QUE É: Painel unificado de conceitos 2D para gate humano do diretor — 4 candidatas da Porca (A–D, H=0.45m) "
        "e 4 candidatas do Dez Base (A–D, H=1.25m), geradas via concept_bridge.py (stabilityai/sdxl-turbo local, art-bible.yaml, fuso caldeiral, Kit 4.0).\n"
        "O QUE VOCÊ DECIDE: Escolher 1 candidata vencedora para Porca (A/B/C/D ou Rota Híbrida) e 1 para Dez Base (A/B/C/D ou Rota Híbrida).\n"
        "ZERO 3D foi gerado nesta tarefa — o 3D só começa após sua escolha.\n"
    )
    (REV_DIR / "LEIA-ME.txt").write_text(readme, encoding="utf-8")

    html = """<!doctype html><html lang="pt-BR"><head><meta charset="utf-8">
<title>CONCERTO C5 — Conceitos Porca &amp; Dez (Gate Diretor 2026-11-22)</title>
<style>body{background:#141210;color:#ece3d5;font-family:system-ui,sans-serif;margin:24px}
h1,h2{color:#e8a13c}img{max-width:100%;border-radius:8px;border:1px solid #3a3026;margin-bottom:18px}
.row{display:grid;grid-template-columns:repeat(4,1fr);gap:12px;margin-bottom:24px}</style></head><body>
<h1>CONCERTO C5 — Gate de Conceitos 2D: Porca &amp; Dez Base</h1>
<p>Modelo: <code>stabilityai/sdxl-turbo</code> (local, zero downloads) via <code>concept_bridge.py</code> + <code>docs/art/art-bible.yaml</code> (fuso <code>caldeiral</code>, Kit 4.0). Régua de escala incluída conforme contrato.</p>
<h2>Painel Unificado (4×2)</h2>
<img src="painel_unificado_porca_dez_4x2.png">
<h2>Porca — Contact Sheet (A–D, H = 0.45 m)</h2>
<img src="porca_4_candidatas_sheet.png">
<div class="row">
  <img src="porca_A.png"><img src="porca_B.png"><img src="porca_C.png"><img src="porca_D.png">
</div>
<h2>Dez Base — Contact Sheet (A–D, H = 1.25 m)</h2>
<img src="dez_4_candidatas_sheet.png">
<div class="row">
  <img src="dez_A.png"><img src="dez_B.png"><img src="dez_C.png"><img src="dez_D.png">
</div>
</body></html>"""
    (REV_DIR / "review_c5_conceitos.html").write_text(html, encoding="utf-8")
    print("\n[c5_conceitos] CONCLUIDO 8/8 CONCEITOS + SHEETS + HTML!")


if __name__ == "__main__":
    main()
