# 10 — Especificação Técnica

> Unity 6.3 URP `[DEFINIDO]` · híbrido Android + PC `[DECISÃO]` · limites do ambiente local respeitados `[DEFINIDO: RTX 3060 12GB, WSL2]` · tags: ver README

## 1. Alvos e versões `[NOVO]`

| Item | Especificação |
|---|---|
| Engine | Unity 6.3 LTS, URP (config do Studio Core) |
| Player Android | arm64-v8a, IL2CPP, Vulkan (GLES3 fallback), .NET Standard 2.1 |
| Player PC | Windows x64 (DX11/12) — alvo de desenvolvimento e playtest rápido |
| Versão mínima Android | 10 (API 29) — cobre o parque com Vulkan razoável |
| Resolução de render | scale 0,8 em telas > 1080p (URP Render Scale) |
| Idioma runtime | PT-BR padrão, EN opcional (assets VO já embutidos) |

## 2. Orçamentos de performance (mobile-first) `[NOVO]`

| Métrica | Alvo Android mid (Adreno 640-class) | Alvo PC |
|---|---|---|
| FPS | 60 estável (30 no modo viagem) | 120+ |
| Tris por zona | ≤ 150k visíveis | ≤ 400k |
| Draw calls | ≤ 350 (SRP Batcher ON, GPU instancing em kits) | — |
| Luzes em tempo real | 1 direcional + até 4 pontos sem sombra | + volumétricos PC |
| Shaders | URP Lit + 3 custom: TimeWarp (estados), Emissivo-Sarda, Ghost-Wireframe | mesmos |
| Memória de assets por fuso | ≤ 400 MB (Addressables por fuso) | — |
| Tamanho APK base | ≤ 400 MB (Corona + Caldeiral); fusos via download (Addressables remotos) | — |
| Bateria | modo viagem: -40% consumo estimado (partículas/sombra reduzidos) | — |

Regra do Studio Core aplicada ao jogo: `validate_scene` rejeita zona acima do orçamento antes do merge (gate técnico, não cultura).

## 3. Arquitetura de gameplay `[NOVO]`

- **Estados de mundo:** cada zona registra 2 prefabs de estado (`state_desync`, `state_sync`) sob um `ReSyncZone` (script do jogo). A alternância faz swap com transição de 0,8 s (shader TimeWarp + wireframe ghost do estado futuro — a "escrita fantasma" de 04 §1).
- **Bis/Re-Sincronia:** `BisController` (cooldown, nós) → evento `ReSyncRequested(zone)` → zonas registradas respondem; desacoplado por mensagens (facilita cutscenes controlarem o Bis).
- **Combate:** CharacterController padrão + Cinemachine 3.x; ataques por hitbox temporizada (sem física de projétil pesada); pool de partículas compartilhado.
- **Câmera:** vcam seguimento com lookahead; FOV dinâmico ±6°; mobile: deadzone maior + assist de mira 8°.
- **Save:** JSON versionado (`save_v1`) com Relíquias/Melhorias/estado de fusos/beats completos + screenshot de thumbnail (evidence também serve de memória visual).
- **Animação:** Animator por personagem + retarget da biblioteca de poses; eventos de animação disparam SFX/VO (barks).
- **Localização:** strings via tabela (PT-BR/EN); legendas de VO via .json gerado pelo tts_bridge.

## 4. Scenes e Addressables `[NOVO]`

```
scenes/
  boot/            (boot, settings, splash CONCERTO)
  hub_orquestra/   (A Orquestra — sempre carregada em Ato 2+)
  fusos/
    caldeiral/     (additive: base + zona_01..zona_05)
    vitralha/ marésia/ ecodia/ retrogrado/ corona/
```

Cada fuso é um grupo de Addressables com catálogo próprio (download sob demanda — APK enxuto `[NOVO]`). O hub carrega o fuso ativo additivo; troca de fuso = unload + load com tela de vinheta (que já é conteúdo, não loading "morto").

## 5. Testes e QA autônomo `[DEFINIDO: TDD do estúdio] + [NOVO]`

- **Testes de mecânica** (Unity Test Framework): pulo/coyote/buffer, dash i-frames, Contratempo (janela), Re-Sincronia (idempotência de estado), save round-trip, retarget de animação (toda pose nova roda suite).
- **Testes de regressão visual:** capturas por zona em 3 ângulos canônicos (mesma câmera/cenas) — diff contra baseline aprovado (evidence/captures).
- **Testes de áudio:** cada id de `dialogues.yaml` deve ter clip gerado + duração ≤ max_duration_ms (falta = falha de build).
- **Smoke de sessão:** bot playtest headless: 8 min de "rota de ouro" do Caldeiral sem erro; métrica de FPS média ≥ 55 no device-alvo emulado.

## 6. Organização Studio Core vs. Jogo `[DEFINIDO pilar 9] + [NOVO]`

- O repo `concerto` nasce **do template do Studio Core** (sem NEON DISTRICT, sem scripts soltos de tools/art no jogo).
- Validadores, bridges (fab3d/tts/concept), CutsceneBuilder e skills ficam no `gamedev-setup-ia` — o jogo só **consome**.
- Contratos entre os dois lados são os YAMLs deste pacote: `art-bible.yaml` (pilar 8), `dialogues.yaml` (pilar 5), `cutscene_*.yaml` (pilar 7) — o GDD já nasce "compilável" pelo estúdio.
- Git LFS para binários (`.fbx/.png/.wav/.ogg`); provenance log por asset (herdado do 3dprinter-setup-ia).

## 7. Riscos técnicos e mitigação `[NOVO]`

| Risco | Impacto | Mitigação |
|---|---|---|
| URP + 2 estados por zona dobra draw calls | FPS mobile | Estados compartilham geometria (05 §2): troca só de material/textura + toggles; partículas por pool |
| CutsceneBuilder complexo (pilar 7 ainda não existe) | Ato 0 depende dele | v1 mínima: static/dolly/closeup + VO + legenda + fade (suficiente p/ cs01); orbit/crane em v2 |
| TTS XTTS-v2 VRAM junto de build | Job falha | Fila sequencial de jobs (.studio/tasks); build nunca roda junto de geração |
| Android Vulkan em dispositivos antigos | Crashes | GLES3 fallback + lista de device-alvo de teste (3 aparelhos reais) |
| Escopo de 6 fusos | Projeto infinito | M1 = Caldeiral só (vertical slice fechada) antes de qualquer fuso 2 (ver 11) |
