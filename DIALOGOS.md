# SISTEMA DE DIÁLOGO E REVISÃO DE FALAS — CONCERTO

Documentação do sistema de diálogos de CONCERTO, guia de tom do protagonista **Dez** e revisão completa (antes → depois) de todas as falas do jogo.

---

## 1. Declaração de Conformidade — `DESIGN_BIBLE.md` v1.0

- **Pré-requisito (`BL-001` / Prompt 9):** Resolvido e protegido por regressão (`Smoke_08` + `Smoke_16`); repetir diálogo com qualquer NPC exibe fala curta e **nunca** reseta nem troca a missão ativa.
- **Legendas integradas (GATE 4):** Todas as falas disparam `AccessibilityRuntimeController.ShowSubtitle(speaker, line)` respeitando `GameSettingsManager.SubtitlesEnabled` e `FontScaleFactor` (Prompt 12).
- **100% teclado e controle:** Avançar (`[E]` / `Espaço` / Gamepad `A`) e pular tudo segurando (`[Esc]` / `[Backspace]` / `[Tab]` / Gamepad `B` por `0.65s`).
- **Máximo 2 linhas por balão:** Todas as falas revisadas para concisão e leitura imediata.
- **Modelos 3D TripoSG criados nesta tarefa:** `0 modelos criados`.

---

## 2. Guia de Tom — 3 Regras do Jeito de Falar do Dez

1. **Alien mecânico e preciso:** Dez observa o mundo por mecanismos, frequências, encaixes e leituras — nada de jargão humano forçado (evitar *"respira fundo"*, *"pode deixar comigo"*, *"maravilha"*).
2. **Curioso, nunca dramático:** Reage ao inesperado com interesse técnico genuíno (*"Módulos soltos? Curioso..."*, *"Pulso artificial no registro? Parece disparo de máquina"*).
3. **Curto e direto (máximo 2 linhas por balão):** Zero paredes de exposição; NPCs falam o essencial em 1–2 frases memoráveis.

---

## 3. Como o Sistema de Diálogo Funciona (`DialogueController.cs`)

- **Fila data-driven (JSON):** `DialogueConversationData` carrega pares `(speakerKey, textKey)` via `LocalizationTable` (`strings_pt_br.json`).
- **Trava e liberação limpa de input:** `StartConversation()` trava `DezLocomotionController` e a rotina do NPC; `EndConversation()` libera ambos integralmente sem estado sujo.
- **Avançar e Pular Tudo:** `[E]`/`Espaço`/`A` revela a linha em digitação ou avança à próxima; segurar `[Esc]`/`[Backspace]`/`[Tab]`/`B` por `0.65s` chama `SkipAllConversation()`, encerrando todo o diálogo limpo.
- **Fala curta de repetição (GATE 2):** Se `dialogueId` já consta em `_completedDialogueIds` (persistido no Save JSON versionado), `StartConversation()` entrega 1 linha curta (`repeat_juno` / `repeat_vera`) com `postObjectiveKey = ""` — **nunca reativa nem reseta a missão**.

---

## 4. Tabela Antes → Depois de Todas as Falas Revisadas (GATE 3)

| Chave (`strings_pt_br.json`) | Falante | ANTES | DEPOIS (Revisado — máx. 2 linhas) |
|---|---|---|---|
| `juno_intro_01` | Juno | Até que enfim saiu da bancada, Dez. Tenho uma notícia ótima e outra que vai te dar dor de cabeça. | Saiu da bancada, Dez? O Projeto Echo travou antes do teste. |
| `juno_intro_02` | Juno | O Projeto Echo está completamente travado. O delivery com as peças principais tombou na entrega e espalhou os módulos pela oficina. | O carrinho de entrega tombou. Os módulos se espalharam pela oficina. |
| `dez_intro_03` | **Dez** | Maravilha... E o conselho quer tudo montado antes do próximo ciclo. Quantas peças ficaram faltando? | Módulos soltos? Curioso... Quantas peças faltam no esquema? |
| `juno_intro_04` | Juno | Todas as peças marcadas no quadro da parede. Se você recuperar cada uma delas aqui pela oficina, eu preparo a calibração no terminal. | Cinco no quadro da parede. Recupere todas que eu calibro o terminal. |
| `juno_intro_05` | Juno | Olha pelo lado positivo: se a gente atrasar mais um dia, pelo menos você não precisa explicar pro conselho por que a máquina ainda parece uma torradeira gigante. | E depressa — sem elas o Echo parece só uma caixa de cobre. |
| `juno_deliver_01` | Juno | Todas as peças inteiras... confesso que eu estava apostando que pelo menos uma tinha caído no fosso. | Cinco de cinco intactas. Achei que uma teria rolado pro fosso. |
| `dez_deliver_02` | **Dez** | Estão todas aí. Pode iniciar a calibração da estrutura principal. | Encaixes conferidos. Pode iniciar a calibração do núcleo. |
| `juno_deliver_03` | Juno | Fechado, chefe. Segura firme — o Projeto Echo finalmente vai sair do papel. | Fechado. Ative o terminal quando estiver pronto. |
| `dlg_vera_01` | Vera | Dez... não encosta na grade. Eu estava sozinha aqui fora quando o céu rasgou. | Dez, não encoste na grade. O céu rasgou enquanto eu checava o pátio. |
| `dlg_vera_02` | **Dez** | Vera, respira. O que aconteceu com o gerador e a antena? | Frequência anômala detectada. O gerador e a antena falharam juntos? |
| `dlg_vera_03` | Vera | Primeiro o vento parou. Depois a aurora subiu ao contrário e tudo no pátio começou a sair do compasso. | O vento parou, a aurora inverteu e tudo perdeu o compasso. |
| `dlg_vera_04` | Vera | Se você ainda tem sua ferramenta, estabiliza os três pontos do pátio antes que essa rachadura desça até a instalação. | Estabilize os três nós do pátio antes que a fenda desça. |
| `sig_intro_01` | Vera | Dez... ainda bem que você saiu. Eu tentei puxar telemetria da rachadura pela antena de comunicação, mas o barramento caiu. | Tentei puxar telemetria da rachadura, mas o barramento caiu. |
| `sig_intro_02` | Vera | A gente precisa saber o tamanho disso antes que a estrutura inteira entre em colapso. | Sem leitura limpa, a estrutura pode colapsar sem aviso. |
| `sig_intro_03` | Vera | Restaura os dois alimentadores do gerador aqui no pátio. Assim que a carga voltar, captura a leitura direto na antena. | Religue os dois alimentadores do pátio e capture o sinal na antena. |
| `sig_intro_04` | **Dez** | Pode deixar comigo, Vera. Vou religar os alimentadores e trazer essa leitura. | Entendido. Alimentadores primeiro, leitura de frequência depois. |
| `sig_result_01` | **Dez** | Consegui a captura da antena, Vera. O terminal cuspiu uma curva toda quebrada. | Sinal capturado, Vera. A curva tem um corte perfeito no meio. |
| `sig_result_02` | Vera | Espera... isso não faz sentido. O pico inicial não é gradual. A perturbação começou num instante exato: 14h02m11s. | Não foi gradual... o pulso começou exato às 14h02m11s. |
| `sig_result_03` | **Dez** | Um pulso limpo no meio do nada... deve ser um registro corrompido, erro de relógio dos sensores da torre. | Pulso artificial no registro? Parece disparo de máquina, não erro. |
| `sig_result_04` | Vera | Mas o vetor de propagação não mente: a origem está no epicentro, logo além do portão leste. | O vetor aponta direto pro epicentro, além do portão leste. |
| `sig_result_05` | Vera | Destravei a trava de emergência do portão leste. Vai até o epicentro e descobre o que está rasgando o céu. | Trava de emergência aberta. Vá ao epicentro e descubra a origem. |
| `repeat_juno` *(Novo)* | Juno | *(repetia todo o diálogo e resetava missão)* | Já alinhamos isso, Dez. Traga as peças do quadro. |
| `repeat_vera` *(Novo)* | Vera | *(repetia todo o diálogo)* | Sem novidade no barramento. Siga o sinal para o epicentro. |

---

## 5. Evidências e Testes (GATES 5, 6 e 8)

- **MP4 (GATE 6):** `captures/dialogos/dialogos_ato1.mp4` (`1920x1080`, H.264) — falar com Juno, avançar falas revisadas, pular tudo segurando botão, repetir NPC com fala curta (sem resetar missão) e seguir jogando livremente.
- **Testes Smoke (`Tests/SmokeTest/ConcertoSmokeTests.cs`):** `Smoke_08` (não reseta missão) + `Smoke_16_DialogueQueueHoldSkipRepeatAndGameplayRestore` (**16/16 PASSED**, `0 warnings CS`, `0 errors CS`).
