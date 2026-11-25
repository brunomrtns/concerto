using System;
using System.Collections.Generic;
using Studio.Game.Animation;
using Studio.Game.Cutscenes;
using Studio.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace Studio.Game.Dialogue
{
    /// <summary>
    /// DialogueController — Sistema completo de Interacao (proximidade ~2.5m + mira) + Dialogo datilografavel
    /// 100% data-driven (JSON chave -> texto) + integracao com ObjectiveSystem.
    /// </summary>
    public sealed class DialogueController : MonoBehaviour
    {
        public static DialogueController Instance { get; private set; }

        [Header("Dados de Localizacao (JSON)")]
        public TextAsset localizationJson;
        public string initialObjectiveKey = "obj_talk_juno";

        [Header("Parametros de Interacao (Proximidade + Mira)")]
        public float interactRadius = 2.5f;
        [Range(-1f, 1f)] public float minAimDot = 0.25f;
        public KeyCode interactKey = KeyCode.E;

        [Header("Datilografia (Typewriter)")]
        public float charsPerSecond = 44f;

        [Header("UI uGUI (DialogueHUD_Canvas)")]
        public CanvasGroup promptGroup;
        public Text promptText;
        public CanvasGroup dialogueBoxGroup;
        public Text speakerNameText;
        public Text dialogueBodyText;
        public Text advanceHintText;
        public ObjectiveSystem objectiveSystem;

        private readonly LocalizationTable _loc = new LocalizationTable();
        private Transform _player;
        private DezLocomotionController _playerLoco;
        private Animator _playerAnim;
        private CutsceneManager _cutsceneMgr;

        private IInteractable _focused;
        private NpcDialogueInteractable _focusedNpc;
        private NpcDialogueInteractable _activeNpc;
        private AlienNpcRoutineController _activeNpcRoutine;

        private DialogueConversationData _activeData;
        private int _lineIndex;
        private string _currentFullLine = string.Empty;
        private float _revealedChars;
        private bool _isTyping;
        private bool _isDialogueActive;

        public bool IsDialogueActive => _isDialogueActive;
        public bool HasFocusedInteractable => _focused != null;
        public IInteractable FocusedInteractable => _focused;
        public bool IsTyping => _isTyping;
        public string CurrentDisplayedText => dialogueBodyText != null ? dialogueBodyText.text : string.Empty;
        public string CurrentFullText => _currentFullLine;

        public event Action<string> OnConversationFinished;

        private void Awake()
        {
            Instance = this;
            _loc.Load(localizationJson);
            if (objectiveSystem != null)
                objectiveSystem.SetPrefix(_loc.Get("objective_prefix"));

            if (promptGroup != null) promptGroup.alpha = 0f;
            if (dialogueBoxGroup != null) dialogueBoxGroup.alpha = 0f;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Start()
        {
            CacheActors();
            // Se nao estiver em cutscene, garante objetivo inicial "Fale com Juno" visivel no HUD
            if (_cutsceneMgr != null)
            {
                _cutsceneMgr.OnCutsceneEnded += OnCutsceneCompleted;
                if (!_cutsceneMgr.IsPlaying && string.IsNullOrEmpty(ObjectiveSystem.CurrentObjective))
                    ObjectiveSystem.Set(_loc.Get(initialObjectiveKey));
            }
            else if (string.IsNullOrEmpty(ObjectiveSystem.CurrentObjective))
            {
                ObjectiveSystem.Set(_loc.Get(initialObjectiveKey));
            }
        }

        private void OnCutsceneCompleted()
        {
            if (string.IsNullOrEmpty(ObjectiveSystem.CurrentObjective))
                ObjectiveSystem.Set(_loc.Get(initialObjectiveKey));
        }

        private void CacheActors()
        {
            var dezGo = GameObject.Find("Player_Dez_Placeholder");
            if (dezGo != null)
            {
                _player = dezGo.transform;
                _playerLoco = dezGo.GetComponent<DezLocomotionController>();
                _playerAnim = dezGo.GetComponentInChildren<Animator>(true);
            }
            _cutsceneMgr = UnityEngine.Object.FindAnyObjectByType<CutsceneManager>();
        }

        public string Localize(string key) => _loc.Get(key);

        private void Update()
        {
            // Nao interfere durante cutscene ativa
            if (_cutsceneMgr != null && _cutsceneMgr.IsPlaying)
            {
                ClearFocus();
                if (promptGroup != null) promptGroup.alpha = 0f;
                return;
            }

            if (_player == null) CacheActors();
            if (_player == null) return;

            if (_isDialogueActive)
            {
                UpdateDialogue(Time.unscaledDeltaTime);
                return;
            }

            ScanFocusedInteractable();

            if (_focused != null && Input.GetKeyDown(interactKey))
            {
                TriggerFocusedInteraction();
            }
        }

        public void TriggerFocusedInteraction()
        {
            if (_focused == null || _player == null) return;
            _focused.Interact(_player.gameObject);
        }

        public void StartConversation(NpcDialogueInteractable npc, TextAsset jsonAsset)
        {
            if (_isDialogueActive || jsonAsset == null) return;
            var data = DialogueConversationData.FromJson(jsonAsset);
            if (data == null || data.lines == null || data.lines.Count == 0) return;

            ClearFocus();
            if (promptGroup != null) promptGroup.alpha = 0f;

            _activeNpc = npc;
            _activeData = data;
            _lineIndex = 0;
            _isDialogueActive = true;

            // Trava movimento do jogador e rotina do NPC durante o dialogo
            if (_playerLoco != null) _playerLoco.enabled = false;
            if (_playerAnim != null) _playerAnim.SetFloat("Speed", 0f);

            if (_activeNpc != null)
            {
                _activeNpcRoutine = _activeNpc.GetComponent<AlienNpcRoutineController>();
                if (_activeNpcRoutine != null) _activeNpcRoutine.enabled = false;
                var npcAnim = _activeNpc.GetComponentInChildren<Animator>(true);
                if (npcAnim != null) npcAnim.SetFloat("Speed", 0f);
            }

            if (dialogueBoxGroup != null) dialogueBoxGroup.alpha = 1f;
            ShowCurrentLine();
        }

        public void PressAdvanceInput()
        {
            if (!_isDialogueActive) return;

            // Regra canonica: apertar durante a digitacao revela a linha inteira primeiro!
            if (_isTyping)
            {
                _revealedChars = _currentFullLine.Length;
                _isTyping = false;
                if (dialogueBodyText != null) dialogueBodyText.text = _currentFullLine;
                UpdateAdvanceHint();
                return;
            }

            // Se ja revelou toda a linha, avanca para a proxima ou encerra
            _lineIndex++;
            if (_activeData != null && _lineIndex < _activeData.lines.Count)
            {
                ShowCurrentLine();
            }
            else
            {
                EndConversation();
            }
        }

        private void ShowCurrentLine()
        {
            var entry = _activeData.lines[_lineIndex];
            string speaker = _loc.Get(entry.speakerKey);
            _currentFullLine = _loc.Get(entry.textKey);
            _revealedChars = 0f;
            _isTyping = true;

            if (speakerNameText != null) speakerNameText.text = speaker;
            if (dialogueBodyText != null) dialogueBodyText.text = string.Empty;
            UpdateAdvanceHint();
        }

        private void UpdateAdvanceHint()
        {
            if (advanceHintText == null || _activeData == null) return;
            bool isLast = (_lineIndex >= _activeData.lines.Count - 1) && !_isTyping;
            advanceHintText.text = _loc.Get(isLast ? "prompt_finish" : "prompt_advance");
        }

        private void UpdateDialogue(float dt)
        {
            // Vira suavemente Dez e NPC um para o outro durante a conversa
            if (_player != null && _activeNpc != null)
            {
                FaceTowards(_player, _activeNpc.transform.position, dt);
                FaceTowards(_activeNpc.transform, _player.position, dt);
            }

            if (_isTyping)
            {
                _revealedChars += charsPerSecond * dt;
                int count = Mathf.Clamp(Mathf.FloorToInt(_revealedChars), 0, _currentFullLine.Length);
                if (dialogueBodyText != null)
                    dialogueBodyText.text = _currentFullLine.Substring(0, count);

                if (count >= _currentFullLine.Length)
                {
                    _isTyping = false;
                    UpdateAdvanceHint();
                }
            }

            if (Input.GetKeyDown(interactKey) || Input.GetKeyDown(KeyCode.Space))
            {
                PressAdvanceInput();
            }
        }

        private void EndConversation()
        {
            string finishedId = _activeData != null ? _activeData.dialogueId : string.Empty;
            string postObjKey = _activeData != null ? _activeData.postObjectiveKey : string.Empty;
            int partsTotal    = _activeData != null ? _activeData.partsTotal : 3;

            _isDialogueActive = false;
            _isTyping = false;
            _activeData = null;

            if (dialogueBoxGroup != null) dialogueBoxGroup.alpha = 0f;

            // Devolve controle limpo ao jogador e rotina ao NPC
            if (_playerLoco != null) _playerLoco.enabled = true;
            if (_activeNpcRoutine != null) _activeNpcRoutine.enabled = true;
            _activeNpc = null;
            _activeNpcRoutine = null;

            // BL-001 (CRITICO): Nunca sobrescreve/reseta objetivo ao re-conversar com NPC ou quando a progressao ja avancou!
            bool isFirstCompletion = string.IsNullOrEmpty(finishedId) || _completedDialogueIds.Add(finishedId);
            if (isFirstCompletion && !string.IsNullOrEmpty(postObjKey) && ShouldApplyPostObjective(postObjKey))
            {
                ObjectiveSystem.Set(_loc.Format(postObjKey, partsTotal));
            }

            OnConversationFinished?.Invoke(finishedId);
        }

        private static bool ShouldApplyPostObjective(string postObjKey)
        {
            var parts = Studio.Game.Parts.PartsLoopManager.Instance;
            if (postObjKey == "obj_find_parts")
            {
                if (parts != null && (parts.CanCollectParts || parts.CollectedCount > 0 || parts.Act1Completed))
                    return false;
            }
            else if (postObjKey == "obj_act1_done")
            {
                var anoms = Studio.Game.Anomalies.WorkshopAnomalyManager.Instance;
                if (anoms != null && anoms.ActiveAnomalyCount > 0)
                    return false;
                if (parts != null && parts.Act1Completed)
                    return false;
            }
            return true;
        }

        private readonly Collider[] _scanBuf = new Collider[24];
        private readonly HashSet<string> _completedDialogueIds = new HashSet<string>();
        private string _lastPromptShown = string.Empty;
        private Camera _cachedMainCam;

        public bool HasCompletedDialogue(string dialogueId) => !string.IsNullOrEmpty(dialogueId) && _completedDialogueIds.Contains(dialogueId);

        public void ScanFocusedInteractable()
        {
            if (_player == null) return;

            Vector3 aimOrigin = _player.position + Vector3.up * 0.9f;
            Vector3 aimFwd = _player.forward;
            if (_cachedMainCam == null || !_cachedMainCam.isActiveAndEnabled)
                _cachedMainCam = Camera.main;
            var cam = _cachedMainCam;
            if (cam != null)
            {
                Vector3 cf = cam.transform.forward;
                cf.y = 0f;
                if (cf.sqrMagnitude > 0.01f) aimFwd = cf.normalized;
            }

            int hitCount = Physics.OverlapSphereNonAlloc(_player.position + Vector3.up * 0.6f, interactRadius, _scanBuf, ~0, QueryTriggerInteraction.Collide);
            IInteractable best = null;
            NpcDialogueInteractable bestNpc = null;
            float bestScore = float.MinValue;

            for (int i = 0; i < hitCount; i++)
            {
                var col = _scanBuf[i];
                if (col == null || col.transform.root == _player.root) continue;
                var cand = col.GetComponentInParent<IInteractable>();
                if (cand == null || !cand.CanInteract(_player.gameObject)) continue;

                // Prioriza NpcDialogueInteractable quando dentro do cone de mira
                Transform targetT = cand.IkHandTarget;
                Vector3 toTarget = targetT.position - aimOrigin;
                toTarget.y = 0f;
                float dist = toTarget.magnitude;
                if (dist > interactRadius + 0.35f) continue;

                float camDot = dist > 0.05f ? Vector3.Dot(aimFwd, toTarget / dist) : 1f;
                float playerDot = dist > 0.05f ? Vector3.Dot(_player.forward, toTarget / dist) : 1f;
                float dot = Mathf.Max(camDot, playerDot);
                if (dot < minAimDot && dist > 1.65f) continue;

                float score = dot * 1.6f - dist * 0.95f;
                var npcCand = (cand as MonoBehaviour)?.GetComponent<NpcDialogueInteractable>();

                if (score > bestScore)
                {
                    bestScore = score;
                    best = cand;
                    bestNpc = npcCand;
                }
            }

            if (!ReferenceEquals(best, _focused))
            {
                if (_focusedNpc != null) _focusedNpc.SetFocused(false);
                _focused = best;
                _focusedNpc = bestNpc;
                if (_focusedNpc != null) _focusedNpc.SetFocused(true);
            }

            if (promptGroup != null)
            {
                bool show = _focused != null;
                float targetAlpha = show ? 1f : 0f;
                if (!Mathf.Approximately(promptGroup.alpha, targetAlpha))
                    promptGroup.alpha = targetAlpha;
                if (show && promptText != null)
                {
                    string nextPrompt = _focused.PromptText;
                    if (nextPrompt != _lastPromptShown)
                    {
                        _lastPromptShown = nextPrompt;
                        promptText.text = nextPrompt;
                    }
                }
            }
        }

        private void ClearFocus()
        {
            if (_focusedNpc != null) _focusedNpc.SetFocused(false);
            _focused = null;
            _focusedNpc = null;
        }

        private static void FaceTowards(Transform self, Vector3 targetPos, float dt)
        {
            Vector3 dir = targetPos - self.position;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.01f) return;
            Quaternion targetRot = Quaternion.LookRotation(dir.normalized, Vector3.up);
            self.rotation = Quaternion.RotateTowards(self.rotation, targetRot, 300f * dt);
        }
    }
}
