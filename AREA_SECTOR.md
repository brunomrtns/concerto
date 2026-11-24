# AREA SECTOR — Setor Externo (Ato 3)

## 1. Layout do Pátio (`Assets/Game/Scenes/SetorExterno.unity`)
Área aberta caminhável (`44m × 44m`) cercada pela silhueta industrial da instalação, com **O Céu Errado** (rachadura luminosa + aurora glitch) dominando o horizonte norte:

```
         [ RACHADURA NO CÉU + AURORA GLITCH (y=26m, z=44m) ]
    ╔══[Torre NW]═══════════ Muro Norte Baixo ═══════════[Torre NE]══╗
    ║                                                                ║
    ║                          [POI 3: CARGA]                        ║
    ║                       (Anomalia 3: Flutuando)                  ║
    ║                                                                ║
    ║   [POI 1: GERADOR]                        [POI 2: ANTENA]      ║
    ║  (Anomalia 1: Flicker)                 (Anomalia 2: Reverso)   ║
    ║                                                                ║
    ║                [NPC: Engenheira Vera]                          ║
    ║                      (-2.6, 8.2)                               ║
    ║                                                                ║
    ║                    [Spawn Dez (0, 3)]                          ║
    ╚══════════════════ [PORTA ↔ OFICINA] ═══════════════════════════╝
```

## 2. Contraste de Iluminação & O Céu Errado (VFX Econômico)
- **Contraste narrativo:** Oficina (`Caldeiral.unity`) = cobre quente, luz âmbar íntima; **Setor Externo** = azul-petróleo frio (`#080E1B`), neblina exponencial fria (`density = 0.015`), luz direcional fria (`(0.58, 0.80, 1.0)`), URP Volume frio (`SectorExterno_ColdVolumeProfile.asset`) e vento pesado (`sector_cold_wind_ambience_TEMP.wav`).
- **Céu Errado econômico (`SkyRiftAnimator.cs`):** 5 segmentos em zigue-zague emissivos ciano + 4 faixas de aurora glitch ciano/magenta animadas por script + bloom URP nativo — **zero shader caro ou render pass extra**, mantendo FPS estável.

## 3. Assets 3D Gerados (Exclusivamente TripoSG + PBR 4K)

| Asset | Tipo | Ferramenta 3D / Rig | Caminho no Unity |
|---|---|---|---|
| `npc_engenheira_vera` | NPC Alienígena (Chefe de Setor) | **TripoSG** + PBR 4K + **UniRig Humanoid** | `Assets/Game/Art/Generated/npcs/npc_engenheira_vera/npc_engenheira_vera_humanoid.fbx` |
| `sector_prop_gerador` | POI 1 — Gerador de Energia | **TripoSG** + PBR 4K + emissivo ciano | `Assets/Game/Art/Generated/sector/sector_prop_gerador.fbx` |
| `sector_prop_antena` | POI 2 — Antena de Comunicação | **TripoSG** + PBR 4K + emissivo ciano | `Assets/Game/Art/Generated/sector/sector_prop_antena.fbx` |
| `sector_prop_carga` | POI 3 — Módulo de Carga | **TripoSG** + PBR 4K | `Assets/Game/Art/Generated/sector/sector_prop_carga.fbx` |

## 4. Porta Oficina ↔ Setor Externo & Flags
- Componente: `SectorDoorInteractable.cs` (`Door_Caldeiral_To_Sector` em `Caldeiral.unity` e `Door_Sector_To_Caldeiral` em `SetorExterno.unity`).
- Flag de destrave: `CONCERTO_SECTOR_EXTERNO_UNLOCKED` (`SectorDoorInteractable.IsSectorUnlocked`).
- **Atalho de debug:** pressione **`F6`** na oficina para alternar trancada/destravada (a futura Cutscene 4 setará essa flag automaticamente). Antes de trocar de cena, a porta chama `SaveSystem.Save()`.

## 5. Como Expandir o Setor
1. **Novas anomalias:** adicione entradas em `Assets/Game/Anomalies/Data/act3_sector_anomalies_config.json` e rode `Studio > Concerto > Act3 > Build Setor Externo & Connecting Door` — o `RepairRitualController` descobre automaticamente qualquer `Repairable` novo.
2. **Novas falas da Vera:** edite `Assets/Game/Dialogue/Data/dlg_vera_setor_externo.json` e `strings_pt_br.json` sem recompilar código.
3. **Expansão física:** estenda o piso `Courtyard_ConcreteFloor` além de `z = 36.5` (ou substitua `Perimeter_NorthLow` por uma nova `SectorDoorInteractable` apontando para o próximo setor).
