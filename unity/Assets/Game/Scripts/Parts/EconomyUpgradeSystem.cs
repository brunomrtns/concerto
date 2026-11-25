using System;
using System.Collections.Generic;
using Studio.Game.Audio;
using Studio.Game.Combat;
using Studio.Game.Powers;
using Studio.Game.Save;
using Studio.Game.Tool;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Studio.Game.Parts
{
    public sealed class UpgradeDefinition
    {
        public string Id;
        public string DisplayName;
        public string EffectDescription;
        public int CostInParts;
    }

    /// <summary>
    /// EconomyUpgradeSystem — Economia e Progressao (Pecas -> Upgrades na Bancada do Dez).
    ///
    /// Conformidade DESIGN_BIBLE.md & ECONOMIA.md:
    ///   - 4 upgrades canonicos (custo total = 5 pecas; total coletavel no jogo = 6 pecas -> margem de 1 peca)
    ///   - Contador de pecas integrado no HUD (coletadas/total + saldo disponivel)
    ///   - Ponto de troca na bancada do Dez ([U] ou [E] proximo a bancada), 100% teclado + controle
    ///   - Progressao persistida EXCLUSIVAMENTE no SaveSystem JSON versionado (ZERO PlayerPrefs!)
    ///   - 0 modelos 3D criados
    /// </summary>
    public sealed class EconomyUpgradeSystem : MonoBehaviour
    {
        public const int TotalCollectiblePartsInGame = 6; // 5 Ato 1 + 1 Fragmento de Nucleo
        public const int TotalCostAllUpgrades = 5;        // 1 + 1 + 1 + 2 = 5 (margem de 1 peca)

        public static readonly UpgradeDefinition[] Catalog =
        {
            new UpgradeDefinition
            {
                Id = "upg_vida_extra",
                DisplayName = "Blindagem de Caldeiral",
                EffectDescription = "+1 Segmento de Vida Maxima (4 -> 5) + Cura imediata",
                CostInParts = 1
            },
            new UpgradeDefinition
            {
                Id = "upg_eco_recarga",
                DisplayName = "Capacitor de Eco",
                EffectDescription = "Cooldown do Eco Temporal -33% (3.0s -> 2.0s)",
                CostInParts = 1
            },
            new UpgradeDefinition
            {
                Id = "upg_estase_duracao",
                DisplayName = "Bobina de Estase",
                EffectDescription = "Duracao da Estase +50% (3.0s -> 4.5s)",
                CostInParts = 1
            },
            new UpgradeDefinition
            {
                Id = "upg_celula_extra",
                DisplayName = "Nucleo de Reserva",
                EffectDescription = "+2 Celulas Maximas de Energia (6 -> 8) + Recarga",
                CostInParts = 2
            }
        };

        public static EconomyUpgradeSystem Instance { get; private set; }

        public int PartsEarned { get; private set; }
        public int PartsSpent { get; private set; }
        public int PartsAvailable => Mathf.Max(0, PartsEarned - PartsSpent);
        public bool IsBenchUiOpen { get; private set; }
        public int FocusedUpgradeIndex => _focusedIndex;
        public int PurchasedCount => _purchasedIds.Count;

        public event Action OnEconomyChanged;
        public event Action<string> OnUpgradePurchased;

        private readonly HashSet<string> _purchasedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // HUD contador (GATE 2) + Painel da Bancada de Upgrades (GATE 3)
        private Canvas _canvas;
        private GameObject _hudCounterRoot;
        private Text _hudCounterText;
        private GameObject _benchPanelRoot;
        private Text _benchBalanceText;
        private readonly Image[] _rowBgs = new Image[4];
        private readonly Text[] _rowTexts = new Text[4];
        private int _focusedIndex;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;
            var go = new GameObject("Concerto_EconomyUpgradeSystem");
            go.AddComponent<EconomyUpgradeSystem>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            BuildEconomyCanvas();
            SceneManager.sceneLoaded += HandleSceneLoaded;
            RefreshHudVisibility(SceneManager.GetActiveScene().name);
            RefreshVisuals();
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            if (Instance == this) Instance = null;
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            CloseBenchUI();
            RefreshHudVisibility(scene.name);
            ReapplyAllPurchasedUpgrades();
            RefreshVisuals();
        }

        private void RefreshHudVisibility(string sceneName)
        {
            bool show = !string.Equals(sceneName, "MainMenu", StringComparison.OrdinalIgnoreCase);
            if (_hudCounterRoot != null) _hudCounterRoot.SetActive(show);
        }

        public bool HasUpgrade(string upgradeId) => !string.IsNullOrEmpty(upgradeId) && _purchasedIds.Contains(upgradeId);

        public void CopyPurchasedUpgradesTo(List<string> destination)
        {
            if (destination == null) return;
            foreach (var id in _purchasedIds)
            {
                if (!destination.Contains(id)) destination.Add(id);
            }
        }

        public void ResetStateForNewGame()
        {
            PartsEarned = 0;
            PartsSpent = 0;
            _purchasedIds.Clear();
            CloseBenchUI();
            RefreshVisuals();
            OnEconomyChanged?.Invoke();
        }

        public void RestoreFromSave(int earned, int spent, List<string> purchasedIds)
        {
            PartsEarned = Mathf.Max(0, earned);
            PartsSpent = Mathf.Clamp(spent, 0, PartsEarned);
            _purchasedIds.Clear();
            if (purchasedIds != null)
            {
                for (int i = 0; i < purchasedIds.Count; i++)
                {
                    if (!string.IsNullOrEmpty(purchasedIds[i]))
                        _purchasedIds.Add(purchasedIds[i]);
                }
            }
            ReapplyAllPurchasedUpgrades();
            RefreshVisuals();
            OnEconomyChanged?.Invoke();
        }

        /// <summary>
        /// GATE 2: Registra peca coletada, atualiza HUD e persiste no Save JSON.
        /// </summary>
        public void NotifyPartEarned(int amount = 1)
        {
            if (amount <= 0) return;
            PartsEarned = Mathf.Min(TotalCollectiblePartsInGame, PartsEarned + amount);
            RefreshVisuals();
            SaveSystem.Save();
            OnEconomyChanged?.Invoke();
            Debug.Log($"[Economia] +{amount} peca coletada! Total={PartsEarned}/{TotalCollectiblePartsInGame} | Saldo={PartsAvailable}");
        }

        public void OpenBenchUI()
        {
            if (_benchPanelRoot == null) return;
            IsBenchUiOpen = true;
            _focusedIndex = 0;
            _benchPanelRoot.SetActive(true);
            RefreshVisuals();
            if (AudioMixController.Instance != null) AudioMixController.Instance.PlayUiOpen();
        }

        public void CloseBenchUI()
        {
            IsBenchUiOpen = false;
            if (_benchPanelRoot != null) _benchPanelRoot.SetActive(false);
        }

        public void NavigateBench(int delta)
        {
            if (!IsBenchUiOpen) return;
            _focusedIndex = (_focusedIndex + delta + Catalog.Length) % Catalog.Length;
            RefreshVisuals();
            if (AudioMixController.Instance != null) AudioMixController.Instance.PlayUiConfirm();
        }

        public bool ConfirmFocusedPurchase()
        {
            if (!IsBenchUiOpen || _focusedIndex < 0 || _focusedIndex >= Catalog.Length) return false;
            return TryPurchaseUpgrade(Catalog[_focusedIndex].Id);
        }

        /// <summary>
        /// GATE 3: Gasta pecas, aplica o efeito na hora no gameplay/HUD e salva no JSON versionado.
        /// </summary>
        public bool TryPurchaseUpgrade(string upgradeId)
        {
            UpgradeDefinition def = null;
            for (int i = 0; i < Catalog.Length; i++)
            {
                if (string.Equals(Catalog[i].Id, upgradeId, StringComparison.OrdinalIgnoreCase))
                {
                    def = Catalog[i];
                    break;
                }
            }
            if (def == null || _purchasedIds.Contains(def.Id)) return false;
            if (PartsAvailable < def.CostInParts)
            {
                if (AudioMixController.Instance != null) AudioMixController.Instance.PlayUiDeny();
                return false;
            }

            PartsSpent += def.CostInParts;
            _purchasedIds.Add(def.Id);
            ApplySingleUpgradeEffect(def.Id, grantImmediateRefill: true);
            RefreshVisuals();
            SaveSystem.Save();

            if (AudioMixController.Instance != null) AudioMixController.Instance.PlayUiConfirm();
            OnUpgradePurchased?.Invoke(def.Id);
            OnEconomyChanged?.Invoke();
            Debug.Log($"[Economia] Upgrade comprado: {def.DisplayName} (-{def.CostInParts} peca(s), saldo={PartsAvailable})");
            return true;
        }

        public void ReapplyAllPurchasedUpgrades()
        {
            foreach (var id in _purchasedIds)
                ApplySingleUpgradeEffect(id, grantImmediateRefill: false);
        }

        private void ApplySingleUpgradeEffect(string upgradeId, bool grantImmediateRefill)
        {
            switch (upgradeId)
            {
                case "upg_vida_extra":
                {
                    var hp = PlayerHealthController.Instance ?? FindAnyObjectByType<PlayerHealthController>();
                    if (hp != null)
                    {
                        if (hp.config != null) hp.config = Instantiate(hp.config);
                        if (hp.Cfg.playerMaxHpSegments < 5)
                            hp.Cfg.playerMaxHpSegments = 5;
                        if (grantImmediateRefill)
                            hp.RestoreFullHealth();
                    }
                    break;
                }
                case "upg_eco_recarga":
                {
                    var powers = DezPowersController.Instance ?? FindAnyObjectByType<DezPowersController>();
                    if (powers != null)
                    {
                        if (powers.config != null) powers.config = Instantiate(powers.config);
                        powers.Cfg.ecoCooldownSec = 2.0f;
                    }
                    break;
                }
                case "upg_estase_duracao":
                {
                    var powers = DezPowersController.Instance ?? FindAnyObjectByType<DezPowersController>();
                    if (powers != null)
                    {
                        if (powers.config != null) powers.config = Instantiate(powers.config);
                        powers.Cfg.stasisDurationSec = 4.5f;
                    }
                    break;
                }
                case "upg_celula_extra":
                {
                    var tool = MultiToolController.Instance ?? FindAnyObjectByType<MultiToolController>();
                    if (tool != null)
                    {
                        if (tool.config != null) tool.config = Instantiate(tool.config);
                        if (tool.Cfg.maxCells < 8)
                            tool.Cfg.maxCells = 8;
                        if (grantImmediateRefill)
                            tool.AddEnergyCells(2);
                    }
                    break;
                }
            }
        }

        private void Update()
        {
            if (SceneManager.GetActiveScene().name == "MainMenu") return;

            if (Input.GetKeyDown(KeyCode.U))
            {
                if (IsBenchUiOpen) CloseBenchUI();
                else OpenBenchUI();
                return;
            }

            if (!IsBenchUiOpen) return;

            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.JoystickButton1))
            {
                CloseBenchUI();
            }
            else if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S))
            {
                NavigateBench(+1);
            }
            else if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W))
            {
                NavigateBench(-1);
            }
            else if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.JoystickButton0))
            {
                ConfirmFocusedPurchase();
            }
        }

        private void RefreshVisuals()
        {
            if (_hudCounterText != null)
                _hudCounterText.text = $"PECAS: {PartsEarned}/{TotalCollectiblePartsInGame}   |   SALDO: {PartsAvailable}   [U: Upgrades]";

            if (_benchBalanceText != null)
                _benchBalanceText.text = $"BANCADA DE UPGRADES DO DEZ   |   SALDO: {PartsAvailable} PECA(S)   ({PurchasedCount}/{Catalog.Length} instalados)";

            for (int i = 0; i < Catalog.Length; i++)
            {
                var def = Catalog[i];
                bool owned = _purchasedIds.Contains(def.Id);
                bool canBuy = !owned && PartsAvailable >= def.CostInParts;
                bool focused = (i == _focusedIndex);

                if (_rowBgs[i] != null)
                {
                    _rowBgs[i].color = owned
                        ? new Color(0.10f, 0.34f, 0.24f, 0.94f)
                        : (focused ? new Color(0.14f, 0.48f, 0.68f, 0.96f) : new Color(0.09f, 0.13f, 0.19f, 0.88f));
                }
                if (_rowTexts[i] != null)
                {
                    string status = owned ? "[INSTALADO]" : (canBuy ? $"[CUSTO: {def.CostInParts}]" : $"[PRECISA {def.CostInParts}]");
                    string prefix = focused ? "> " : "  ";
                    _rowTexts[i].text = $"{prefix}{def.DisplayName}  {status}  —  {def.EffectDescription}";
                    _rowTexts[i].color = owned ? new Color(0.45f, 1f, 0.72f, 1f) : (canBuy ? Color.white : new Color(0.70f, 0.76f, 0.82f, 0.9f));
                }
            }
        }

        private void BuildEconomyCanvas()
        {
            var go = new GameObject("EconomyHUD_Canvas");
            go.transform.SetParent(transform, false);
            _canvas = go.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 140;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            scaler.matchWidthOrHeight = 1f;

            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            // GATE 2: Contador compacto no topo-direito (nao sobrepoe vida/objetivo no topo-esquerdo)
            _hudCounterRoot = new GameObject("PartsCounterPill");
            _hudCounterRoot.transform.SetParent(go.transform, false);
            var pillBg = _hudCounterRoot.AddComponent<Image>();
            pillBg.color = new Color(0.05f, 0.09f, 0.15f, 0.82f);
            pillBg.raycastTarget = false;
            var pr = _hudCounterRoot.GetComponent<RectTransform>();
            pr.anchorMin = new Vector2(1f, 1f);
            pr.anchorMax = new Vector2(1f, 1f);
            pr.pivot = new Vector2(1f, 1f);
            pr.anchoredPosition = new Vector2(-24f, -62f);
            pr.sizeDelta = new Vector2(460f, 38f);

            var cntTxtGo = new GameObject("CounterText");
            cntTxtGo.transform.SetParent(_hudCounterRoot.transform, false);
            _hudCounterText = cntTxtGo.AddComponent<Text>();
            _hudCounterText.font = font;
            _hudCounterText.fontSize = 17;
            _hudCounterText.fontStyle = FontStyle.Bold;
            _hudCounterText.alignment = TextAnchor.MiddleCenter;
            _hudCounterText.color = new Color(0.98f, 0.86f, 0.36f, 1f);
            _hudCounterText.raycastTarget = false;
            var ctr = cntTxtGo.GetComponent<RectTransform>();
            ctr.anchorMin = Vector2.zero;
            ctr.anchorMax = Vector2.one;
            ctr.offsetMin = Vector2.zero;
            ctr.offsetMax = Vector2.zero;

            // GATE 3: Painel do Posto de Troca (Bancada de Upgrades do Dez)
            _benchPanelRoot = new GameObject("UpgradeBench_Panel");
            _benchPanelRoot.transform.SetParent(go.transform, false);
            var panelBg = _benchPanelRoot.AddComponent<Image>();
            panelBg.color = new Color(0.05f, 0.08f, 0.14f, 0.94f);
            var br = _benchPanelRoot.GetComponent<RectTransform>();
            br.anchorMin = new Vector2(0.5f, 0.5f);
            br.anchorMax = new Vector2(0.5f, 0.5f);
            br.pivot = new Vector2(0.5f, 0.5f);
            br.anchoredPosition = Vector2.zero;
            br.sizeDelta = new Vector2(920f, 380f);

            var hdrGo = new GameObject("Header");
            hdrGo.transform.SetParent(_benchPanelRoot.transform, false);
            _benchBalanceText = hdrGo.AddComponent<Text>();
            _benchBalanceText.font = font;
            _benchBalanceText.fontSize = 22;
            _benchBalanceText.fontStyle = FontStyle.Bold;
            _benchBalanceText.alignment = TextAnchor.MiddleCenter;
            _benchBalanceText.color = new Color(0.32f, 0.92f, 1f, 1f);
            var hr = hdrGo.GetComponent<RectTransform>();
            hr.anchorMin = new Vector2(0f, 1f);
            hr.anchorMax = new Vector2(1f, 1f);
            hr.pivot = new Vector2(0.5f, 1f);
            hr.anchoredPosition = new Vector2(0f, -18f);
            hr.sizeDelta = new Vector2(-32f, 40f);

            for (int i = 0; i < Catalog.Length; i++)
            {
                var row = new GameObject($"UpgradeRow_{i}");
                row.transform.SetParent(_benchPanelRoot.transform, false);
                var rBg = row.AddComponent<Image>();
                rBg.color = new Color(0.09f, 0.13f, 0.19f, 0.88f);
                var rr = row.GetComponent<RectTransform>();
                rr.anchorMin = new Vector2(0.5f, 1f);
                rr.anchorMax = new Vector2(0.5f, 1f);
                rr.pivot = new Vector2(0.5f, 1f);
                rr.anchoredPosition = new Vector2(0f, -76f - i * 64f);
                rr.sizeDelta = new Vector2(860f, 54f);
                _rowBgs[i] = rBg;

                var rtGo = new GameObject("Label");
                rtGo.transform.SetParent(row.transform, false);
                var rTxt = rtGo.AddComponent<Text>();
                rTxt.font = font;
                rTxt.fontSize = 18;
                rTxt.fontStyle = FontStyle.Bold;
                rTxt.alignment = TextAnchor.MiddleLeft;
                rTxt.color = Color.white;
                var rtr = rtGo.GetComponent<RectTransform>();
                rtr.anchorMin = Vector2.zero;
                rtr.anchorMax = Vector2.one;
                rtr.offsetMin = new Vector2(18f, 0f);
                rtr.offsetMax = new Vector2(-18f, 0f);
                _rowTexts[i] = rTxt;
            }

            _benchPanelRoot.SetActive(false);
        }
    }
}
