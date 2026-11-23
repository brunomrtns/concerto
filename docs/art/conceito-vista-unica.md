# Template Oficial de Conceito 2D — Vista Única, Sujeito Único (`RUN mega_v5`)

> **Princípio permanente do pipeline (decisão do diretor, 2026-11-23):**
> **"SUJEITO ÚNICO, VISTA ÚNICA"** — folhas multi-view / contact sheets podem existir **somente como referência de design / revisão humana**; **NUNCA** entram no gerador 3D (`TripoSG → TripoSF 512 → Paint`). A coerência entre ângulos é responsabilidade do pipeline 3D, nunca de grades 2D.

---

## 1. Template Obrigatório (Props e Personagens)

- **Gerador padrão:** `Comfy-Org/flux1-dev-fp8` (`3dprinter-setup-ia/tools/generator/concept_flux.py`, `seed` base `42`, guard 10 GB, `--lowvram --reserve-vram 3.5`).
- **Prompt positivo (estrutura obrigatória):**
  ```text
  single [subject], full body visible without cropping, centered, isolated on pure white background, single view, 3/4 angle, Kit 4.0 weathered brass copper and dark iron clockwork-industrial style, diffuse studio lighting, matte metal, no text
  ```
- **Prompt negativo (obrigatório em geradores com CFG > 1 e como cláusula explícita no FLUX):**
  ```text
  grid, multi-view, character sheet, split panels, collage, multiple objects, cropped, deformed, text, watermark, background scenery, floor horizon
  ```

---

## 2. Gate Automático Obrigatório (`gate_concept_image.py`)

Toda imagem gerada por `concept_flux.py` ou `concept_bridge.py` passa obrigatoriamente por `3dprinter-setup-ia/tools/generator/gate_concept_image.py` (CPU-only, 0 VRAM):
- **Componentes conexos:** no máximo `1` componente grande (`>= 5%` da área); `>= 2` → **REPROVADO** (grade / multi-sujeito).
- **Calhas internas (gutters):** linha ou coluna interna (`15%..85%`) sem primeiro plano separando blocos de ambos os lados → **REPROVADO** (grade).
- **Corte na borda:** primeiro plano tocando qualquer borda em `> 8%` da extensão → **REPROVADO** (corte).
- **Provenance obrigatória (ADR-011 + mega_v5):** nenhum PNG entra no 3D sem `.provenance.json` + `.gate.json` com `gate_pass: true`.
