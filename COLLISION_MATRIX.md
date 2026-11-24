# COLLISION_MATRIX.md — Sistema de Colisão & Física de CONCERTO

Referência canônica de camadas, matriz de colisão, regras de separação de atores e anti-tunneling (em conformidade com `docs/DESIGN_BIBLE.md` v1.0, Seções 2, 4.4 e 8.1).

---

## 1. Camadas Canônicas (`ProjectSettings/TagManager.asset` & `CollisionMatrixBootstrap.cs`)

| Layer # | Nome | Uso |
|---|---|---|
| `0` | `Default` | Geometria genérica / piso legado |
| `6` | `NPC` | NPCs aliados (`NPC_Juno_Oficina`, `NPC_Engenheira_Vera`, etc.) + `SolidBody_Capsule` |
| `7` | `Projectile` | Projéteis (`EnergyBoltProjectile` e futuros projéteis) |
| `9` | `Player` | `Player_Dez_Placeholder` |
| `12` | `CutsceneActor` | Atores sob coreografia de cutscene (ignora cenário/props) |
| `13` | `CameraBlocker` | Barreiras invisíveis exclusivas de câmera |
| `16` | `Environment` | Paredes, pisos, rampas e arquitetura sólida |
| `23` | `Enemy` | Futuras Manifestações (`Estilhaço`, `Flutuante`) e Mini-Boss |
| `25` | `Props` | Bancadas, máquinas, POIs TripoSG, caixas |

---

## 2. Matriz de Colisão (Layer × Layer — aplicada no boot por `CollisionMatrixBootstrap`)

| | Default | Environment | Props | Player | NPC | Enemy | Projectile | CutsceneActor | CameraBlocker |
|---|:---:|:---:|:---:|:---:|:---:|:---:|:---:|:---:|:---:|
| **Player (9)** | ✅ | ✅ | ✅ | — | ✅ | ✅ | ❌ | ❌ | ❌ |
| **NPC (6)** | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ❌ | ❌ | ❌ |
| **Enemy (23)** | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ❌ | ❌ |
| **Projectile (7)** | ✅ | ✅ | ✅ | ❌ | ❌ | ✅ | ❌ | ❌ | ❌ |
| **CutsceneActor (12)** | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ |
| **CameraBlocker (13)** | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ |

---

## 3. Causas Raiz Encontradas & Regras por Ator

1. **Player atravessando NPCs (`NPC_Engenheira_Vera`, NPCs da oficina):**
   - **Causa raiz:** dois `CharacterController` não se empurram/bloqueiam de forma estável na Unity, e `NPC_Engenheira_Vera` possuía apenas um `SphereCollider` com `isTrigger = true`.
   - **Correção sistêmica:** `ActorSolidSeparation.cs` cria `SolidBody_Capsule` (`isTrigger = false`, `radius = 0.30m`, `PhysMat_ZeroFriction`, layer `NPC`) e executa resolução determinística de sobreposição em `LateUpdate` (desempate por `GetInstanceID()`, nunca aleatório).
   - **Armadilha validada (Interação > Colisão):** `solidRadius (0.30m) + playerRadius (0.28m) = 0.58m << interactRadius (2.50m)` — conversar com Vera/Juno funciona normalmente com o corpo sólido ativo.
2. **Regra de Justiça no Combate (Player ↔ Enemy):**
   - `ActorSolidSeparation` aplica **30% no Player / 70% no Enemy** — um inimigo nunca consegue prender o Dez em um canto. Entre `Enemy ↔ Enemy`, a separação é **50% / 50%** (nunca empilham).
3. **Anti-Tunneling Matemático no Dash (`13.5 m/s`):**
   - A `13.5 m/s` em um hitch de `50ms`, o passo chega a `0.675m` (> `2× radius`). `DezLocomotionController` executa `Physics.CapsuleCast` contra `SolidWorldMask` sempre que `horizDist > radius * 0.55f`, limitando o movimento a `hit.distance - skinWidth`. Testado contra parede de `0.15m` sem tunelamento.
4. **Anti-Tunneling de Projéteis (`EnergyBoltProjectile`):**
   - Antes usava `SphereCast` único com `QueryTriggerInteraction.Collide`, onde um trigger na frente mascarava a parede sólida logo atrás. Agora usa `Physics.SphereCastAll` ordenado por distância, ignorando o atirador e triggers não-`IDamageable`, destruindo-se na primeira superfície sólida.
5. **Cutscene Actors:**
   - Obedecem `DESIGN_BIBLE.md` Seção 4.4 via `CutsceneActorGuard` (`CharacterController` desligado + rotinas pausadas durante cutscenes).

---

## 4. Como Adicionar um Objeto Físico Novo no Futuro
1. **Parede / Piso / Rampa:** atribua layer `Environment (16)`, collider sólido (`isTrigger = false`) com espessura mínima `>= 0.15m` e `PhysMat_ZeroFriction` em superfícies de contato lateral.
2. **Prop Interativo / Estrutural:** atribua layer `Props (25)`, collider limpo (Box/Capsule — evitar MeshCollider côncavo para corpo físico); trigger de interação fica num filho com `isTrigger = true` e raio `>= 1.8m`.
3. **Novo NPC:** atribua layer `NPC (6)` e adicione `ActorSolidSeparation` (`bodyKind = NPC`, `solidRadius = 0.30f`, `createSolidCapsuleIfMissing = true`).
4. **Novo Inimigo:** atribua layer `Enemy (23)`, adicione `ActorSolidSeparation` (`bodyKind = Enemy`, `solidRadius = 0.35f`) e configure o `NavMeshAgent` com `radius >= 0.35f` e `height >= 1.25f`.
5. **Novo Projétil:** atribua layer `Projectile (7)` e use `Physics.SphereCastAll` ordenado por distância contra `CollisionMatrixBootstrap.ProjectileHitMask`.
