# AUDITORIA VISUAL — CATÁLOGO DE DEFEITOS E POLIMENTO POR ATO

**Projeto:** CONCERTO (`game-concerto`)
**Referência canônica:** [docs/DESIGN_BIBLE.md](docs/DESIGN_BIBLE.md) v1.0
**Data:** 2026-11-25
**Modelos 3D criados nesta tarefa:** **0 modelos 3D criados** (Regra 2 — apenas reposicionamento de props existentes e ajuste de cor/material existente).
**Escopo:** Correções rápidas (`CORRIGÍVEL_AGORA`) aplicadas diretamente; itens que exigem remodelagem/substituição grande (`REFAZER_DEPOIS`) encaminhados ao [BACKLOG.md](BACKLOG.md) para decisão.

---

## 1. Declaração de Conformidade — `DESIGN_BIBLE.md` v1.0

- **Qualidade Visual & Consistência de Paleta:** CONFORME — Todos os props flutuantes/enterrados detectados na varredura geométrica foram apoiados no chão (`0` restantes), `0` materiais quebrados/rosa em todas as cenas, e o arco-guia do Epicentro teve a tintura ajustada à paleta ciano-obsidiana da fase.
- **Pipeline 3D (TripoSG-only):** CONFORME — **0 modelos 3D criados** nesta tarefa; demandas de novos modelos foram registradas no `BACKLOG.md` como TripoSG-only.
- **Estabilidade & Testes:** CONFORME — `0 warnings CS`, `0 errors CS`, suíte `Tests/SmokeTest/` **21/21 VERDE**, baseline de regressão visual atualizada intencionalmente.

---

## 2. Catálogo Completo de Defeitos (Registrado ANTES das Correções — GATE 2)

| ID | Ato / Cena | Local | Descrição do Defeito | Classificação | Screenshot ANTES |
|---|---|---|---|---|---|
| `VA-01` | Ato 1 (`Caldeiral`) | Z1 Oficina — Bancada | `EnergyCell_cell_01_bancada` flutuando `+17.0cm` acima do piso `Z1_Floor_14x12` (`minY=0.170`, `chaoY=0.000`). | `CORRIGÍVEL_AGORA` | `captures/visual_auditoria/ato1/antes/01_z1_oficina_bancada.png` |
| `VA-02` | Ato 1 (`Caldeiral`) | Z1 Oficina — Estante | `EnergyCell_cell_02_estante` flutuando `+17.0cm` acima do piso `Z1_Floor_14x12` (`minY=0.170`, `chaoY=0.000`). | `CORRIGÍVEL_AGORA` | `captures/visual_auditoria/ato1/antes/01_z1_oficina_bancada.png` |
| `VA-03` | Ato 1 (`Caldeiral`) | Z1 Oficina — Baía da Nave | `Tool_TrainingTarget_Dummy` enterrado `-9.3cm` dentro de `Bay_PlatformBase` (`minY=0.013`, `chaoY=0.106`). | `CORRIGÍVEL_AGORA` | `captures/visual_auditoria/ato1/antes/01_z1_oficina_bancada.png` |
| `VA-04` | Ato 1 (`Caldeiral`) | Z4 Câmara Central | `I2_Valvula_A` (`c5_prop_valvula_ritmica`) enterrada `-36.9cm` em `Z4_C3_ValveConduits` (`minY=0.006`, `chaoY=0.375`). | `CORRIGÍVEL_AGORA` | `captures/visual_auditoria/ato1/antes/05_z4_camara_central.png` |
| `VA-05` | Ato 1 (`Caldeiral`) | Z4 Câmara Central | `I3_Valvula_B` (`c5_prop_valvula_ritmica`) enterrada `-36.9cm` em `Z4_C3_ValveConduits` (`minY=0.006`, `chaoY=0.375`). | `CORRIGÍVEL_AGORA` | `captures/visual_auditoria/ato1/antes/05_z4_camara_central.png` |
| `VA-06` | Ato 1 (`Caldeiral`) | Z4 Câmara Central | `I4_Valvula_C` (`c5_prop_valvula_ritmica`) enterrada `-36.6cm` em `Z4_C3_ValveConduits` (`minY=0.006`, `chaoY=0.372`). | `CORRIGÍVEL_AGORA` | `captures/visual_auditoria/ato1/antes/05_z4_camara_central.png` |
| `VA-07` | Ato 2 (`SetorExterno`) | Pátio & Zona Geradores | Piso e paredes de blocos estruturais usando material liso azul-cinza sem contraste/textura PBR (troca rápida por materiais PBR existentes no projeto para leitura imediata). | `CORRIGÍVEL_AGORA` | `captures/visual_auditoria/ato2/antes/01_patio_entrada.png` |
| `VA-08` | Ato 3 (`Epicentro`) | Entrada da Arena do Guardião | Arco-guia (`EpicenterV2LevelDesignDirector`) com cor amarelo-neon estourada em conflito visual com a paleta obsidiana/ciano/roxo do Epicentro. | `CORRIGÍVEL_AGORA` | `captures/visual_auditoria/ato3/antes/03_arena_guardiao.png` |
| `BL-007` | Ato 2 (`SetorExterno`) | Cenário Externo Completo | Geometria estrutural (paredes/torres/pátio) ainda baseada em blocos greybox; exige kit arquitetônico externo TripoSG dedicado. | `REFAZER_DEPOIS` | `captures/visual_auditoria/ato2/antes/02_zona_geradores.png` |
| `BL-008` | Ato 3 (`Epicentro`) | Skybox & Portal da Arena | Skybox padrão cinza-bege do Unity e arco-guia primitivo exigem skybox cósmico da Fenda Temporal + portal arquitetônico TripoSG. | `REFAZER_DEPOIS` | `captures/visual_auditoria/ato3/antes/01_travessia_inicial.png` |
| `BL-009` | Ato 1 (`Caldeiral`) | Z1 Oficina — Pickups/Alvo | `EnergyCell_cell_*` e `Tool_TrainingTarget_Dummy` ainda usam primitivos lisos (cápsula ciano / cilindro laranja); trocar pela malha TripoSG `prop_celula_energia` e alvo mecânico. | `REFAZER_DEPOIS` | `captures/visual_auditoria/ato1/antes/01_z1_oficina_bancada.png` |
| `BL-010` | Ato 1 (`Caldeiral`) | Z4 Câmara Central — Sirene | Malha `c5_prop_sirene_mestra` apresenta facetas esticadas nas colunas laterais; exige regeneração TripoSG com topologia limpa. | `REFAZER_DEPOIS` | `captures/visual_auditoria/ato1/antes/05_z4_camara_central.png` |

---

## 3. Tabela de Correções Aplicadas (`CORRIGÍVEL_AGORA` — GATE 3)

| ID | Ato / Cena | Objeto / Script | Correção Aplicada | Status Pós-Fix |
|---|---|---|---|---|
| `VA-01` | Ato 1 (`Caldeiral`) | `EnergyCell_cell_01_bancada` | Reposicionado verticalmente (`-0.170m` em Y) apoiado sobre `Z1_Floor_14x12` (`chaoY=0.000`). | **CORRIGIDO** |
| `VA-02` | Ato 1 (`Caldeiral`) | `EnergyCell_cell_02_estante` | Reposicionado verticalmente (`-0.170m` em Y) apoiado sobre `Z1_Floor_14x12` (`chaoY=0.000`). | **CORRIGIDO** |
| `VA-03` | Ato 1 (`Caldeiral`) | `Tool_TrainingTarget_Dummy` | Reposicionado verticalmente (`+0.093m` em Y) apoiado sobre `Bay_PlatformBase` (`chaoY=0.106`). | **CORRIGIDO** |
| `VA-04` | Ato 1 (`Caldeiral`) | `I2_Valvula_A_C5Visual/c5_prop_valvula_ritmica` | Reposicionado verticalmente (`+0.369m` em Y) apoiado sobre `Z4_C3_ValveConduits` (`chaoY=0.375`). | **CORRIGIDO** |
| `VA-05` | Ato 1 (`Caldeiral`) | `I3_Valvula_B_C5Visual/c5_prop_valvula_ritmica` | Reposicionado verticalmente (`+0.369m` em Y) apoiado sobre `Z4_C3_ValveConduits` (`chaoY=0.375`). | **CORRIGIDO** |
| `VA-06` | Ato 1 (`Caldeiral`) | `I4_Valvula_C_C5Visual/c5_prop_valvula_ritmica` | Reposicionado verticalmente (`+0.366m` em Y) apoiado sobre `Z4_C3_ValveConduits` (`chaoY=0.372`). | **CORRIGIDO** |
| `VA-07` | Ato 2 (`SetorExterno`) | Pisos e paredes estruturais do `SetorExterno` | Aplicados materiais PBR existentes (`Mat_Fab3D_caldeiral_env_floor_tile` nos pisos e `Mat_Fab3D_caldeiral_env_wall_panel` nas paredes/torres) para contraste imediato e leitura arquitetônica. | **CORRIGIDO** |
| `VA-08` | Ato 3 (`Epicentro`) | `EpicenterV2LevelDesignDirector.cs` | Tintura do arco-guia da Arena ajustada de amarelo-neon estourado para ciano-obsidiana suave (`#2EB8E6`) coerente com a iluminação da Fenda. | **CORRIGIDO** |

---

## 4. Itens Enviados ao `BACKLOG.md` (`REFAZER_DEPOIS` — GATE 4)

Registrados em [BACKLOG.md](BACKLOG.md) ordenados do mais para o menos impactante:
1. **`BL-007` (ALTO | ~4h):** Substituir arquitetura greybox do `SetorExterno` por kit arquitetônico externo TripoSG + PBR.
2. **`BL-008` (ALTO | ~3h):** Skybox cósmico da Fenda Temporal + portal arquitetônico TripoSG no `Epicentro`.
3. **`BL-009` (MÉDIO | ~2h):** Substituir primitivos lisos de `EnergyCell_cell_*` e `Tool_TrainingTarget_Dummy` em `Caldeiral` Z1 por malhas TripoSG.
4. **`BL-010` (MÉDIO | ~2h):** Regenerar `c5_prop_sirene_mestra` (TripoSG) removendo facetas esticadas em `Caldeiral` Z4.

---

## 5. Atualização Intencional da Baseline de Regressão Visual (GATE 5)

Como os ajustes em `Caldeiral`, `SetorExterno` e `Epicentro` (incluindo os alinhamentos de Z-Fighting, rampas/corrimãos e os 8 itens `CORRIGÍVEL_AGORA` desta auditoria) são **mudanças visuais intencionais**, a baseline em `captures/regressao/baseline/` foi regenerada propositalmente via `VisualRegressionCaptureRunnerTest` após as correções para refletir o novo estado aprovado.

---

## 6. Vídeos Antes × Depois por Ato (GATE 6)

- **Ato 1 (`Caldeiral`):** [captures/visual_auditoria/ato1_antes_depois.mp4](captures/visual_auditoria/ato1_antes_depois.mp4)
- **Ato 2 (`SetorExterno`):** [captures/visual_auditoria/ato2_antes_depois.mp4](captures/visual_auditoria/ato2_antes_depois.mp4)
- **Ato 3 (`Epicentro`):** [captures/visual_auditoria/ato3_antes_depois.mp4](captures/visual_auditoria/ato3_antes_depois.mp4)

---

## 7. Lista de Modelos 3D TripoSG
- **0 modelos 3D criados nesta tarefa.**
