# Plano de Produção de Personagens — CONCERTO (22/11)

**Pergunta do dono:** "pra criação artística (personagem principal, inimigos etc.), como vamos fazer pra criá-los?"
**Resposta curta:** o GDD já definiu fichas, budgets e prompts (02 + 07 + art-bible.yaml); o Studio já tem os pipelines fechados ou planejados (concept_bridge → fab3d_bridge → UniRig/UAL → Fab3dImporter → validadores). Este documento cruza os dois e entrega a **rota de produção por personagem**, atualizando os 4 pontos em que o GDD §07 ficou atrás do Studio atual. Pronto para virar o próximo pacote de estórias da IA do projeto.

---

## 1. Correções ao GDD 07 (desatualizado vs. Studio de hoje)

| GDD dizia | Estado atual do Studio | Consequência |
| --- | --- | --- |
| ComfyUI (FLUX.1-schnell) para conceito | **sdxl-turbo via `concept_bridge.py`/WSL — Fase 4 fechada** (ComfyUI é o único item `[ ]` do PROGRESS, adiado) | Conceito 2D já roda hoje; nada a instalar |
| `auto_rig.py` (bipeds, 18 ossos, Generic) | **Fase 5.1 aprovada: UniRig headless + estória 5.1-B2 (normalizador Mecanim/keemap + retarget em bpy)** | Rig de produção = UniRig; `auto_rig.py` fica só para o vertical slice |
| Retarget de "poses JSON / Mixamo-compatible" | **Quaternius UAL = biblioteca oficial de movimento + MoMask para gestos sob medida** | Animação base de graça (UAL 120+ clips); gestos únicos por texto |
| Modelos em `D:\` | **`C:\` estrito** (AGENTS Princípio 9: `D:\` é defeituoso e proibido) | Todos os caminhos do plano apontam para `C:\` |

Os fluxos base (TRELLIS → forja Blender → `fab3d_bridge.py` → `Fab3dImporter.cs` → `validate_asset` → captura/evidence) continuam válidos — são exatamente o que a Fase 5 provou no dragonborn.

## 2. Pipeline universal de personagem (6 etapas, com gates)

1. **Ficha + prompt no art-bible** (`art-bible.yaml` → seção `characters`): prompt_base por personagem + modificador de fuso. *Gate:* entry existe (o `concept_bridge.py` levanta `ValueError` se o personagem não estiver lá — proteção já no código).
   **Gap atual:** o art-bible do pacote tem `dez`, `iria`, `porca`, `juno` — **faltam `vó_segunda`, `tio_horario`, `censor`, `ferrugem_trabalhadora/espelho/coletora`**. Estória de 1 commit antes de gerar qualquer NPC/inimigo.
2. **Conceito 2D:** `concept_bridge.py --character <id>` (sdxl-turbo, 4 candidatas, prompt_base + modificador) → **dono escolhe 1** (gate humano — Princípio 5) → PNG + JSON em `evidence/concepts/` com provenance. Imagem de referência leva **régua de escala** (exigência do art-bible: `scale_ruler: required_for_characters`).
3. **Geração 3D:** imagem aprovada → `fab3d_bridge.py` (TRELLIS → GLB) → `fab3d_glb_to_fbx.py` + forja Blender (repair → decimate → **budget da ficha**: Dez ≤ 8k, Porca ≤ 2.5k, Ferrugens ≤ 3.5k, Censores 6k/8k, Íria ≤ 12k). Detalhe de mostrador/mecânica vai para **normal-map bake**, não geometria.
4. **Rig (por rota, ver §3):** UniRig headless no WSL (≥8 GB VRAM, dentro da regra sequencial) → **estória 5.1-B2**: normalizador de ossos para convenção Mecanim (ou mapping keemap) → FBX humanoide. Não-humanoides (Porca, Ferrugens) seguem rig UniRig custom sem avatar Humanoid.
5. **Animação:** **UAL retarget** para a base (idle/walk/run/talk) — o `QuaterniusPedPipeline.cs` é o exemplo funcionando de Humanoid + `RuntimeAnimatorController` dentro do repo; **MoMask** para gestos únicos por texto (gift, trade, conduct, parry); **Animation Rigging IK** para interação (mão→alavanca) e look-at da lente da Porca.
6. **Import + gates Unity:** `Fab3dImporter.cs` com `AnimationType.Human` **+ geração de `AnimatorController`** (estória nova da auditoria — hoje o importador não cria controller) → `validate_asset` exit 0 → captura in-engine comparada ao conceito (gate de arte, Strict Reviewer pode vetar) → prefab no jogo.

## 3. Rota por personagem

| Personagem | Rota de rig | Rota de animação | Ordem |
| --- | --- | --- | --- |
| **Porca** (drone) | Sem Humanoid: UniRig custom ou float-rig manual | **Procedural em C#** (hover, inclinação, tremor de aflição) + IK look-at da lente; lip-sync = brilho pulsante | **1ª** |
| **Dez** (hero, 4 braços) | Humanoid com **2 braços nos slots Humanoid + 2 braços-auxiliares como bones fora do avatar** (decisão já canonizada no GDD §02-1) — os auxiliares animados por curvas/script | UAL retarget (locomoção/combate) + MoMask (talk, parry, repair_kneel) + IK dos 2 braços principais | **2ª** (gate da Fase 5.1-A) |
| **Vó Segunda** | Humanoid padrão via UniRig | UAL (idle/walk/talk) + MoMask (gift) | 3ª |
| **Tio Horário** | Humanoid padrão | UAL + MoMask (trade) | 3ª |
| **Ferrugens** (3 famílias) | UniRig generic (corpos-máquina, favorável ao gerador) | procedural/UAL não-humanoid + estados Patrulha→Perseguição→Ataque (estória da auditoria) | 4ª |
| **Censores** (2 formas) | Humanoid (≤6k / ≤8k) | UAL + parry_stun via MoMask | 5ª |
| **Íria** (boss) | Humanoid (≤12k, 1024+emissive) | MoMask forte: 5 padrões de conduct + attack_conduct | 5ª (Ato 2) |
| **Juno** (echo) | Humanoid (≤6k) | UAL + shader de **trail de meio segundo** (assinatura visual dela) | 6ª |

Justificativa da ordem: Porca é a companhia constante na tela, tem conceito já provado (Fase 4) e é a mais barata de animar — valida o fluxo completo sem depender de avatar Mecanim. Dez é o gate da 5.1-A e libera a rota humanoid que os NPCs reutilizam de graça. Inimigos depois dos NPCs dão vida ao Caldeiral com custo zero (UAL).

## 4. O que só o dono faz (gates humanos, insubstituíveis)

1. **Aprovar o conceito de cada personagem** (4 candidatas → 1) e o look final dos heróis (Dez, Porca) — gate do Strict Reviewer usa sua aprovação como referência.
2. **Gravar as amostras de voz** do `voice-cast.template.json`: XTTS-v2/F5-TTS **clonam** — precisam das gravações do dono (XTTS: 6–10 amostras limpas por voz de clone; F5: 1 de referência); **Porca (Kokoro-82M) dispensa amostra** (voz pronta + processamento pitch/ring). É o bloqueio real do VO — quanto antes, melhor.
3. Confirmar nomes/apelidos (Dez já fechado como DECISÃO no GDD).

## 5. Estórias para a IA do projeto (pacote pronto, com gates)

1. **S-artbib-01:** completar `art-bible.yaml` (4 personagens faltantes + negativos por personagem). Gate: `concept_bridge --character vó_segunda` roda sem ValueError.
2. **S-char-01 (Porca):** conceito → aprovação → fab3d ≤2.5k tris → rig custom → hover/lente procedural → prefab + captura. Gate: validate_asset + captura vs conceito.
3. **S-char-02 (Dez base):** conceito → fab3d ≤8k → UniRig + 5.1-B2 (normalizador) → avatar Human sem retoque manual + 3 clips UAL → captura. Gate: a estória 5.1-A do plano mestre, agora com a adição do **AnimatorController no Fab3dImporter**.
4. **S-char-03 (braços-auxiliares):** 2 bones acessórios fora do Humanoid + animação por curvas + IK de interação. Gate: repair_kneel com 4 mãos em close de cutscene (cs01 s02).
5. **S-char-04 (NPCs):** Vó Segunda + Tio Horário reutilizando a rota de Dez. Gate: 1 conversa + 2 barks com VO (depende do DialoguePresenter — P0-1 da auditoria).
6. **S-char-05 (Ferrugens):** 3 famílias ≤3.5k + shader de corrosão + FSM de combate. Gate: captura de Perseguição→Ataque com hitstop.
7. **S-char-06 (Censores + Íria):** após Ato 1 do jogo. Gate: luta rítmica do boss com contratempo.
8. **S-char-07 (Juno-echo):** modelo + shader trail. Gate: echo sussurro com reverb/lowpass (chain do 08-audio-vozes).

## 6. Rota híbrida do dono para heróis (identidade própria)

O dono pode criar a identidade visual manualmente (ex.: Gemini Nano Banana) e entrar no fluxo em dois pontos — os dois caminhos são válidos, porque os contratos do pipeline aceitam tanto **imagem** quanto **malha externa**:

**6.1 O que a imagem precisa ter (spec para entrar limpa no image→3D):**
- Corpo inteiro visível (da cabeça aos pés), personagem centrado ocupando ~80% do quadro.
- Pose A ou T (braços levemente abertos), de frente ou 3/4 — **os 4 braços do Dez claramente legíveis**.
- Fundo liso claro (sem cenário), luz difusa sem sombras duras, sem texto/marca d'água (regra do art-bible: texto nunca na imagem).
- **Régua de escala** ao lado do personagem (art-bible: `scale_ruler: required_for_characters`).
- Estilo conforme o art-bible (faction_livre: silhueta legível, detalhes mecânicos com função — engrenagens, rebites, mostradores).
- Se os 2 braços-auxiliares saírem errados na imagem: gerar só o corpo com os 2 principais — os auxiliares viram peças separadas (decisão do GDD §02-1 já prevê isso).

**6.2 Caminho A — você cria a identidade, a IA faz o 3D:**
Imagem aprovada por você entra direto como referência do `fab3d_bridge.py` (TRELLIS) — ou do TripoSG do 3dprinter-setup-ia, se preferir o seu motor; a porta de entrada no jogo é a mesma (forja → validadores), só o provenance registra o gerador. A IA segue: forja (repair → decimate ≤8k) → bake 1024 → UniRig → 5.1-B2 → UAL/MoMask → Unity.

**6.3 Caminho B — você também faz (ou retoca) o 3D do herói:**
Recomendado para o Dez: a IA entrega a base do image→3D, **você abre no Blender e refina rosto/silhueta/detalhes de identidade** (o que os geradores erram e o que mais aparece na tela), mantendo pose A/T e escala, e devolve. A IA aplica: decimate para o budget + bake (detalhe por normal-map, regra do GDD) → rig → animação → gates. Se preferir modelar do zero, o contrato é só: GLB/FBX, pose canônica, altura 1,25 m (Dez) — o fluxo continua idêntico a partir da forja.

**6.4 Regras do handoff:**
- Provenance obrigatório nos dois caminhos: registrar ferramenta (Nano Banana/TripoSG), prompt/seed e autoria no evidence.
- Se a identidade divergir do art-bible atual, **o art-bible é atualizado para a sua escolha** — ele é o contrato visual, sua decisão manda, e os NPCs/inimigos seguem o contrato novo para manter coerência.
- A rota automatizada (§2/§3) continua para NPCs e inimigos — sua atenção é recurso escasso: gaste no que está na tela o jogo inteiro (Dez + Porca).

## 7. Resumo

- O "como" já está decidido e distribuído: **art-bible → conceito (dono aprova) → fab3d/TRELLIS → UniRig → UAL+MoMask → Unity com validadores** — cada etapa com gate e evidence.
- Diferente de nada: cada personagem tem **rota própria** (Dez 2+2 braços, Porca procedural, Íria por MoMask) e **budget próprio** (fichas do GDD §02-5).
- Os 2 insumos que só você entrega: aprovações visuais e as gravações de voz. Com elas, o resto roda headless de ponta a ponta.
- 1 gap pequeno detectado: art-bible incompleto (4 personagens) — estória S-artbib-01, 1 commit.
