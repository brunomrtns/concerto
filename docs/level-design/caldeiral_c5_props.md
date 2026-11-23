# C5 — Plano de Produção de Props do Caldeiral (Fonte da Verdade)

> **Estágio:** C5 (`studio-scene-director` — substituição in-place de placeholders pelos assets autorais da Fábrica 3D)
> **Motor oficial:** `TripoSG → TripoSF 512 → Hunyuan3D-Paint v2-0 → patch_repair → 05_trellis_pbr → pack_glb` (`ADR-007` + `ADR-009` + `ADR-010`)
> **Idioma visual oficial (Kit 4.0 aprovado em 2026-11-22):** latão, cobre, ferro escuro, rebitado, clockwork-industrial (`docs/art/art-bible.yaml`, fuso `caldeiral`).
> **Regra de Substituição In-Place:** cada prop entra no **mesmo transform** (`posição`, `rotação`, `InteractableBase`, `IkHandTarget`, `BoxCollider` preservados), sem reorganizar a planta aprovada.

---

## 1. Inventário Completo de Props Derivado dos Contratos (`caldeiral_c1.md` §5.1–5.2)

| Prop / Entidade | Zona(s) | Coordenada(s) | Lote C5 | Critério de Prioridade | Budget Jogo (`max_tris`) | Classe `validate_visual` |
|---|---|---|---|---|---:|---|
| **`c5_prop_sirene_mestra`** (`ENT_SIRENE`) | Z4 | `(0, 0, 7)` (`H=16 m`) | **Batch-1** | **Focal Point único** do Caldeiral; placeholder cilíndrico grita no centro da Z4 | **≤ 5.000** | `env_structure` (`is_biped: false`) |
| **`c5_prop_no_resincronia`** (`I5`) | Z4 | `(0, 3.5, 0.5)` | **Batch-1** | Centro visual do quadro de Z4 + objetivo clímax de Re-Sincronia; icosaedro cru grita muito | **≤ 1.500** | `env_tile` (`is_biped: false`) |
| **`c5_prop_valvula_ritmica`** (`I2, I3, I4` ×3) | Z4 | `(-5.5,0,4)`, `(5.5,0,4)`, `(0,0,12.8)` | **Batch-1** | 3 instâncias do puzzle central conectadas aos dutos da Sirene-Mestra | **≤ 1.200** | `env_tile` (`is_biped: false`) |
| **`c5_prop_bancada_relogio`** (`I1`) | Z1 | `(-29, 0, -17.5)` | **Batch-1** | Primeira interação do jogador no Vertical Slice (`IkHandTarget` + tutorial da Porca) | **≤ 2.000** | `env_tile` (`is_biped: false`) |
| Banca do Tio Horário | Z2 | `(-13.8, 0, -21.6)` | Batch-2 | Nicho do NPC mercador em Z2 | ≤ 2.000 | `large_prop_building` |
| Catraca de Saída Sync (`I6`) | Z5 | `(-24, 3.5, 9)` | Batch-2 | Portão de saída Sync em Z5 (moldura já vestida em C4.1) | ≤ 1.500 | `env_tile` |
| Caixotes de Oficina (`prop_caixote` ×6) | Z1 | Cantos de Z1 | Batch-2 | `prop_caixote` já validado em smoke no repo (`gates-v1.0`) | ≤ 600 | `small_prop_item` |

---

## 2. Política de Budget e Artefatos por Prop (Batch-1)

- **GLB Master (Fábrica 3D):** saída 4K PBR do `3dprinter-setup-ia/pipeline/run_asset.py` preservada em `~/printer3d-work/c5_props/<id>/` (referenciada no manifest de proveniência; não infla o Git).
- **GLB / FBX de Jogo (Unity):** decimado pelo `fab3d_bridge.py` para o budget da tabela acima (**todos ≤ 5.000 tris**, `texture_size = 1024`, `ASTC_6x6`), validado obrigatoriamente por:
  1. `validate_gltf.py` (`glTF-Validator`, `0` erros)
  2. `validate_asset.py`
  3. `validate_visual.py` (`is_biped: false`, `exit 0`)
  4. `Fab3dImporter.cs` + captura datada + `validate_scene.py --strict` + Baseline PlayMode M1–M2 (`2/2 PASSED`).
