# MINI-BOSS: "GUARDIÃO DA FENDA" (#103)

**Projeto:** CONCERTO (`game-concerto`)
**Referência canônica:** [docs/DESIGN_BIBLE.md](docs/DESIGN_BIBLE.md) v1.0
**Pré-requisitos verificados:** Arena Final do Epicentro (`Epicentro.unity`, `S3_HeartOfRift_BossTriggerStub`, `#102@7f1c08c`), combate completo + Eco/Estase (`#93@8949c9a`), Manifestações + HP/knockdown (`#94@1ce2dd8`) e `SaveSystem` JSON.

---

## 1. FSM das 2 Fases (`RiftGuardianBossController.cs`)

```text
Dormant ──(EpicenterArenaBossStub.OnBossArenaTriggered)──► Chase
   ▲                                                         │
   │ (Retry limpo no checkpoint da Arena após Knockdown)     ├──► ChargeTelegraph (0.72s) ──► Charge (12 m/s) ──► Chase
   │                                                         ├──► SlamTelegraph (0.80s) ──► SlamShockwave (onda Eco) ──► CoreExposedStagger (~3.0s, 1.65x dano)
   └─────────────────────────────────────────────────────────┴──► [Fase 2 <= 50% HP]: invoca 2 Estilhaços TripoSG + SprayTelegraph ──► Spray 7 projéteis (Estase!)
                                                             │
                                                             ▼ (HP <= 0)
Dead (dissolve grande + silêncio) ──► Fragmento de Núcleo ──► [E] Flash Holograma 0.52s (oficina + esposa, ZERO diálogo) ──► Cartão "FIM DO ATO 3" + Auto-Save
```

### Semente Narrativa — O Flash de Holograma (`CoreFragmentInteractable.cs`)
- Ao tocar o **Fragmento de Núcleo**, o jogador vê por **0.52s** um flash holográfico ciano da **oficina no passado com a esposa** (`cs01_foto_dez_esposa.png`), acompanhado apenas por estática fria + acorde distante da caixinha — **ZERO diálogo, ZERO explicação**.
- **O que isso semeia para o final do jogo:** o Epicentro guarda uma memória pessoal do Dez porque a própria fenda nasceu do salto temporal dele; o jogador sente o arrepio agora e só conecta todas as peças no clímax do Ato 4.

---

## 2. Tabela de Balance (`RiftGuardianConfig.asset`)

| Parâmetro | Valor | O que ajustar 1º se fácil / difícil |
|---|---:|---|
| `maxHp` | `320` | Subir/descer se a luta fugir de 2–3 min |
| `phase2Threshold01` | `50%` | Gatilho dos 2 Estilhaços + Spray |
| `exposedCoreDamageMultiplier` | `1.65x` | Recompensa punição com golpe carregado após slam |
| `armoredDamageMultiplier` | `0.45x` | Evita spam fora da janela sem zerar progresso |
| `chargeTelegraphSec` / `slamTelegraphSec` | `0.72s` / `0.80s` | Aumentar se o jogador não ler a onda/investida |
| `shockwaveExpandSpeed` | `13.5 m/s` | Onda expansiva desviável com Eco Temporal |
| `coreExposedStaggerSec` | `3.0 s` | Janela de punição com golpe carregado |
| `phase2SprayProjectileCount` / `Speed` | `7` / `6.8 m/s` | Estase reduz para `3.4 m/s` |
| `hologramFlashDurationSec` | `0.52 s` | Meio segundo canônico (sem diálogo) |
| Música da luta (TEMP) | `caldeiral_combat_contratempo.ogg` (`pitch 0.86`) | Variação distorcida provisória marcada como TEMP |

---

## 3. Modelo TripoSG Gerado & Áudio Novo

| Asset | Pipeline / Registro | Polígonos / Altura |
|---|---|---|
| `boss_guardiao_fenda.fbx` + PBR 4K (`Mat_Fab3D_boss_guardiao_fenda.mat`) | **TripoSG exclusivo** (`run_boss_triposg_factory.sh`, seed `1030`, `triposg_runner.py` + `pipeline/run_asset.py`) | `8499 polys`, `3.20m` |
| `sfx_boss_slam_TEMP.wav`, `sfx_hologram_flash_TEMP.wav` | Síntese procedural original (CC0 1.0) | 44.1 kHz mono WAV |

---

## 4. Como Testar & Evidências (Gates)

- **Compilação:** `0 warnings CS`, `0 errors CS`.
- **Teste PlayMode:** `RiftGuardianBossValidationAndCaptureTest.ValidateBossFullFightRetryPhase2CoreFragmentHologramAndAct3Card` — **PASSED (`11.83s`)**:
  - `retryReset=1`, `phase2=True`, `defeated=True`, `hologramFlash=True (0.52s)`, `act3EndCard=True`, `SaveSystem.Save("act3_boss_defeated")` executado.
- **MP4 de Evidência:** `captures/boss_guardiao/guardiao_luta_completa_vitoria_1080p.mp4` (`1920x1080 H.264 + AAC 44.1kHz stereo`).

---

## CONFORMIDADE — DESIGN_BIBLE.md v1.0
- **Seção 2 & 3 (Combate & Métricas):** CONFORME — loop desviar (Eco) → punir núcleo exposto (`3.0s`) + Estase no spray da Fase 2; retry limpo no checkpoint da arena sem punir com backtrack da fase.
- **Seção 5 (Teach → Test → Twist):** CONFORME — o boss combina tudo que o jogador aprendeu (Eco na onda de choque, Estase no spray + Estilhaços, golpe carregado no núcleo exposto).
- **Seção 7 & 9 (Legibilidade & Juice):** CONFORME — silhueta colossal de `3.2m`, telegraphs de cor/áudio distintos, barra de HP dedicada indicando `NÚCLEO EXPOSTO!`, dissolve grande + silêncio antes do flash de 0.52s.
