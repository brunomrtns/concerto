# PERFORMANCE_PASS.md — Pass Global de Performance (CONCERTO)

Relatório numérico real (medido via `Unity.Profiling.ProfilerRecorder` em `PerformancePassProfilerBenchmarkTest.cs`, em conformidade com `docs/DESIGN_BIBLE.md` v1.0, Seção 3: 60fps estável, frame budget `16,7ms`, hitches `<= 2 frames`, input→ação `< 100ms`).

---

## 1. Tabela Numérica Antes vs Depois (3 Cenas Reais)

| Cena | Métrica | Antes (`captures/profiler/antes`) | Depois (`captures/profiler/depois`) | Delta |
|---|---|---:|---:|---:|
| **Caldeiral** *(cena mais pesada)* | Avg Main Thread (ms/frame) | `1,84 ms` | `1,71 ms` | **`-7,1%`** |
| **Caldeiral** | Peak Frame (ms) | `5,38 ms` | `4,88 ms` | **`-9,3%`** |
| **Caldeiral** | Hitches `> 2 frames` (`>33,4ms`) / min | `0` | `0` | **0 hitches** |
| **Caldeiral** | **GC Allocs médio / frame** | **`1.124 B`** | **`616 B`** | **`-45,2%`** |
| **Caldeiral** | Peak GC Allocs / frame | `2.356 B` | `1.900 B` | **`-19,4%`** |
| **SetorExterno** | Avg Main Thread (ms/frame) | `1,03 ms` | `0,96 ms` | **`-6,8%`** |
| **SetorExterno** | P95 / Peak Frame (ms) | `1,53 / 1,81 ms` | `1,34 / 1,58 ms` | **`-12,4% / -12,7%`** |
| **SetorExterno** | Hitches `> 2 frames` / min | `0` | `0` | **0 hitches** |
| **SetorExterno** | **GC Allocs médio / frame** | **`752 B`** | **`620 B`** | **`-17,6%`** |
| **MainMenu** *(hub/menus)* | Avg / Peak Frame (ms) | `0,25 / 1,05 ms` | `0,26 / 0,94 ms` | **`-10,5% peak`** |
| **MainMenu** | Hitches `> 2 frames` / min | `0` | `0` | **0 hitches** |
| **Global** | Latência Input → Ação | `3,2 ms` (`1 frame`) | **`2,5 ms` (`1 frame`)** | **`< 100ms` ✅** |
| **Global** | Warnings de Compilação C# | `1` (`CS0414`) | **`0`** | **0 warnings ✅** |

---

## 2. Top 5 Hotspots Auditados e Corrigidos

1. **`DezProceduralAnimator.LateUpdate()` (`RaycastAll` todo frame):**
   - **Antes:** alocava novo `RaycastHit[]` a cada frame para atualizar a sombra de contato (`Dez_ContactShadow`).
   - **Fix:** `Physics.RaycastNonAlloc` com buffer pré-alocado `_shadowHitBuf[8]`.
2. **`DialogueController.ScanFocusedInteractable()` (`OverlapSphere` + `promptText.text` todo frame):**
   - **Antes:** alocava `Collider[]` a cada frame e reescrevia `promptText.text` todo frame (gerando dirty/layout rebuild de UI).
   - **Fix:** `Physics.OverlapSphereNonAlloc` com `_scanBuf[24]` + dirty-check `_lastPromptShown` e `Mathf.Approximately` no `CanvasGroup.alpha`.
3. **`DezLocomotionController` (`SphereCastAll`/`RaycastAll` + `ToLowerInvariant()`):**
   - **Antes:** alocava arrays nos probes de ledge mantle e alocava nova string via `ToLowerInvariant()` em cada passo.
   - **Fix:** `SphereCastNonAlloc` / `RaycastNonAlloc` com `_probeBuf[12]` + comparação `IndexOf(..., OrdinalIgnoreCase)` zero-alloc.
4. **`MultiToolController.RefreshHud()` + `CombatToolModule.EnergyBoltProjectile`:**
   - **Antes:** formatava strings `$"[TAB] MODO: ..."` e `$"CÉLULAS: ..."` a cada atualização da barra de energia; `EnergyBoltProjectile` usava `SphereCastAll` + `Array.Sort`.
   - **Fix:** dirty-check `_lastHudCells` / `_lastHudModeId` (atualiza texto só quando valor muda) + `SphereCastNonAlloc` com ordenação in-place zero-alloc.
5. **Compilação Zero Warnings (`Cutscene02TravelController.cs`):**
   - **Antes:** `warning CS0414` no campo `_inPastMode`.
   - **Fix:** exposta propriedade `public bool IsInPastMode => _inPastMode;` → **0 warnings** em toda a compilação.

---

## 3. Como a Latência Input → Ação Foi Medida
- Em `PerformancePassProfilerBenchmarkTest.cs`, com o Dez parado (`CurrentSpeed == 0`), registramos `tInput = Time.realtimeSinceStartup`, injetamos `ScriptedMoveDir = Vector3.forward` + `ScriptedRun = true` e contamos quantos frames e milissegundos decorrem até `CurrentSpeed > 0.1f`.
- **Resultado medido:** **`1 frame` (`2,5 ms`)**, bem abaixo do teto de `< 100 ms` (ideal `~67 ms`) da `DESIGN_BIBLE.md`.

---

## 4. Evidências Salvas
- Baseline ANTES: `captures/profiler/antes/profiler_summary.csv` (+ `MainMenu_baseline.txt`, `Caldeiral_baseline.txt`, `SetorExterno_baseline.txt`)
- Medição DEPOIS: `captures/profiler/depois/profiler_summary.csv` (+ `*_baseline.txt`)
- MP4 1080p pós-fix da cena mais pesada (`Caldeiral`): `captures/profiler/caldeiral_60fps_post_fix_1080p.mp4`
