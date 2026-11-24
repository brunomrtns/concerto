# TOOL SYSTEM — Ferramenta Multi-Uso (`CONCERTAR` / `COMBATE`)

## 1. Arquitetura Extensível por Módulos (`Assets/Game/Scripts/Tool/`)
A ferramenta do Dez reusa integralmente o modelo TripoSG `dez_repair_tool.prefab` (`Dez_RepairTool_InHand`) e separa o core dos módulos de função:
- **`MultiToolController`**: gerencia lista plugável de `ToolModuleBase`, troca de modo (`[Tab]` ou `1`/`2`), barra de energia, contador de células, `ToolHUD_Canvas` (sem HP ainda), hitstop (`65ms`), camera shake sutil e SFX.
- **`ToolModuleBase`**: classe base extensível (`ModuleId`, `DisplayName`, `AccentColor`, `AllowsRepairInteraction`, `OnEquip()`, `OnUnequip()`, `OnTick(float dt)`).
- **`RepairToolModule` (`CONCERTAR`)**: `AllowsRepairInteraction = true`, **custa `0` energia e `0` células SEMPRE** (regra inviolável do jogo). O sistema de reparo real (`Repairable` / `RepairRitualController`) funciona identicamente.
- **`CombatToolModule` (`COMBATE`)**:
  - **Combo leve de 2 golpes** (toque rápido `Clique Esquerdo` / `J`, custo `0`).
  - **Golpe carregado** (segurar `Clique Esquerdo` / `J` por `>= 0.45s`, custo `0`).
  - **Tiro carregado (Bolt de energia ciano)** (`Clique Direito` / `K`, consome **`1` célula**).
  - **Hitbox honesta** (`OverlapSphere` + `SphereCast` no `EnergyBoltProjectile`) contra qualquer `IDamageable`.
  - **Feedback obrigatório no acerto:** hitstop `65ms` (`0.065s`, faixa `50–80ms`), camera shake sutil no `Dez_CameraTarget` (`0.065m` / `0.12s`) e som seco.

## 2. Como Adicionar uma Nova Função/Modo no Futuro (Zero Refactor no Core)
1. Crie uma classe herdando de `ToolModuleBase` (ex: `ScannerToolModule : ToolModuleBase`).
2. Implemente `ModuleId`, `DisplayName`, `AccentColor` e `OnTick(float dt)`.
3. Anexe o componente ao GameObject `MultiTool_System` e adicione-o à lista `MultiToolController.modules` — o `[Tab]` e o HUD passam a incluí-lo automaticamente.

## 3. Valores Tunáveis (`Assets/Game/Config/MultiToolConfig.asset`)

| Parâmetro | Default | Função |
|---|---:|---|
| `toggleModeKey` | `Tab` | Alterna entre `CONCERTAR` e `COMBATE` (sem conflitar com `Q`/`E`/`Esc`) |
| `startingCells` / `maxCells` | `3` / `6` | Células iniciais e capacidade máxima |
| `boltCellCost` / `boltEnergyCost` | `1` / `25` | Custo do tiro carregado (melee e reparo sempre custam `0`) |
| `lightHit1Damage` / `lightHit2Damage` | `15` / `22` | Dano dos 2 golpes do combo leve |
| `heavyChargedDamage` / `chargeHoldThresholdSec` | `45` / `0.45s` | Dano e tempo de carga do golpe carregado |
| `boltDamage` / `boltSpeed` | `30` / `18 m/s` | Dano e velocidade do projétil ciano |
| `hitstopDurationSec` | `0.065s` | Hitstop de impacto (dentro da janela `50–80ms`) |
| `cameraShakeAmplitude` / `cameraShakeDurationSec` | `0.065m` / `0.12s` | Camera shake sutil no acerto |

## 4. Células Coletáveis (`Assets/Game/Tool/Data/energy_cells_config.json`)
Spawns placeholder configurados via JSON (`cell_01_bancada`, `cell_02_estante`, `cell_03_nave`) até a entrada dos drops de inimigos na task de Manifestações.
