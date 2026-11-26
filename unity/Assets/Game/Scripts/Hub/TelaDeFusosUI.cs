using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Studio.Game.Hub
{
    /// <summary>
    /// GATE 2: Tela de Fusos — mapa-grafo em tela cheia (GDD 04 §6 + GDD 01 §5):
    ///   - Exibe todos os 5 fusos + Hub central "A Orquestra" conectados por arestas.
    ///   - Cada fuso mostra Estado (Desincronizado / Restaurado), Reliquia, BENEFICIO e CUSTO pendentes
    ///     ANTES de o jogador confirmar o conserto (decisao informada, sem tela de moralidade).
    /// </summary>
    public sealed class TelaDeFusosUI : MonoBehaviour
    {
        public HubOrquestraController hubController;

        public bool IsOpen => _rootPanel != null && _rootPanel.activeSelf;
        public string SelectedFusoId => _selectedFusoId;

        private GameObject _rootPanel;
        private readonly Dictionary<string, Image> _nodeImages = new Dictionary<string, Image>();
        private readonly Dictionary<string, Text> _nodeLabels = new Dictionary<string, Text>();
        private Text _detailTitle;
        private Text _detailRelic;
        private Text _detailStatus;
        private Text _detailBenefit;
        private Text _detailCost;
        private Text _detailNpc;
        private Button _confirmButton;
        private Text _confirmButtonText;
        private string _selectedFusoId = "caldeiral";

        private void Awake()
        {
            if (hubController == null) hubController = FindAnyObjectByType<HubOrquestraController>();
            BuildFullscreenGraphUI();
            Close();
        }

        public void Open()
        {
            if (_rootPanel == null) BuildFullscreenGraphUI();
            _rootPanel.SetActive(true);
            RefreshVisuals();
        }

        public void Close()
        {
            if (_rootPanel != null) _rootPanel.SetActive(false);
        }

        public void SelectFuso(string fusoId)
        {
            _selectedFusoId = fusoId;
            RefreshVisuals();
        }

        public bool ConfirmSelectedFuso()
        {
            if (hubController == null || string.IsNullOrEmpty(_selectedFusoId)) return false;
            bool ok = hubController.ConfirmRestoreFuso(_selectedFusoId);
            RefreshVisuals();
            return ok;
        }

        public void RefreshVisuals()
        {
            if (hubController == null) return;
            var entries = hubController.Entries;

            for (int i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                bool restored = hubController.IsFusoRestored(e.fusoId);
                bool selected = e.fusoId == _selectedFusoId;
                if (_nodeImages.TryGetValue(e.fusoId, out var img) && img != null)
                {
                    if (restored)
                        img.color = selected ? new Color(0.24f, 0.92f, 0.68f, 1f) : new Color(0.16f, 0.68f, 0.52f, 0.96f);
                    else
                        img.color = selected ? new Color(0.96f, 0.72f, 0.24f, 1f) : new Color(0.24f, 0.30f, 0.42f, 0.94f);
                }
                if (_nodeLabels.TryGetValue(e.fusoId, out var lbl) && lbl != null)
                {
                    lbl.text = $"{e.displayName}\n[{(restored ? "RESTAURADO" : "DESINCRONIZADO")}]";
                }
            }

            var sel = hubController.GetEntry(_selectedFusoId);
            if (sel != null)
            {
                bool restored = hubController.IsFusoRestored(sel.fusoId);
                if (_detailTitle != null)   _detailTitle.text   = $"FUSO: {sel.displayName.ToUpperInvariant()}";
                if (_detailRelic != null)   _detailRelic.text   = sel.relicName;
                if (_detailStatus != null)  _detailStatus.text  = restored ? "ESTADO: RESTAURADO" : "ESTADO: DESINCRONIZADO (PRONTO PARA CONSERTO)";
                if (_detailBenefit != null) _detailBenefit.text = $"BENEFÍCIO: {sel.benefitDescription}";
                if (_detailCost != null)    _detailCost.text    = $"CUSTO ({sel.costTargetFusoId.ToUpperInvariant()}): {sel.costDescription}";
                if (_detailNpc != null)     _detailNpc.text     = $"CHEGADA NO HUB: {sel.npcArrivalName} (+1 camada da marchinha)";

                if (_confirmButton != null) _confirmButton.interactable = !restored;
                if (_confirmButtonText != null) _confirmButtonText.text = restored ? "FUSO JÁ RESTAURADO" : "CONFIRMAR CONSERTO";
            }
        }

        private void BuildFullscreenGraphUI()
        {
            if (_rootPanel != null) return;

            var canvasGo = new GameObject("TelaDeFusos_Canvas");
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 60;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            scaler.matchWidthOrHeight = 1f;
            canvasGo.AddComponent<GraphicRaycaster>();

            _rootPanel = new GameObject("TelaDeFusos_Root");
            _rootPanel.transform.SetParent(canvasGo.transform, false);
            var rrt = _rootPanel.AddComponent<RectTransform>();
            rrt.anchorMin = Vector2.zero;
            rrt.anchorMax = Vector2.one;
            rrt.offsetMin = Vector2.zero;
            rrt.offsetMax = Vector2.zero;
            var bg = _rootPanel.AddComponent<Image>();
            bg.color = new Color(0.06f, 0.08f, 0.13f, 0.94f);

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            // Titulo superior
            CreateLabel(_rootPanel.transform, "Header", "TELA DE FUSOS — LINHA DO EQUILÍBRIO", font, 30, TextAnchor.MiddleCenter,
                new Color(0.95f, 0.85f, 0.45f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -44f), new Vector2(1200f, 52f));

            // Area do Grafo (esquerda 62%)
            var graphArea = new GameObject("GraphArea");
            graphArea.transform.SetParent(_rootPanel.transform, false);
            var grt = graphArea.AddComponent<RectTransform>();
            grt.anchorMin = new Vector2(0.03f, 0.10f);
            grt.anchorMax = new Vector2(0.64f, 0.88f);
            grt.offsetMin = Vector2.zero;
            grt.offsetMax = Vector2.zero;
            var gImg = graphArea.AddComponent<Image>();
            gImg.color = new Color(0.09f, 0.12f, 0.19f, 0.92f);

            // Hub Central "A ORQUESTRA"
            var hubNode = CreateBox(graphArea.transform, "Node_AOrquestra", new Vector2(0.50f, 0.50f), new Vector2(210f, 74f), new Color(0.18f, 0.58f, 0.88f, 1f));
            CreateLabel(hubNode.transform, "HubTxt", "A ORQUESTRA\n(HUB CENTRAL)", font, 18, TextAnchor.MiddleCenter, Color.white, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(200f, 68f));

            // Nos dos 5 Fusos
            var entries = hubController != null ? hubController.Entries : BalanceLineDatabase.BuildCanonicalGddEntries();
            _nodeImages.Clear();
            _nodeLabels.Clear();
            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                var nodeGo = CreateBox(graphArea.transform, $"Node_{entry.fusoId}", entry.graphNormalizedPos, new Vector2(220f, 82f), new Color(0.24f, 0.30f, 0.42f, 0.95f));
                var btn = nodeGo.AddComponent<Button>();
                string capturedId = entry.fusoId;
                btn.onClick.AddListener(() => SelectFuso(capturedId));
                _nodeImages[entry.fusoId] = nodeGo.GetComponent<Image>();
                _nodeLabels[entry.fusoId] = CreateLabel(nodeGo.transform, "Label", entry.displayName, font, 17, TextAnchor.MiddleCenter, Color.white, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(210f, 76f));
            }

            // Painel de Inspecao (direita 32% — mostra Estado, Reliquia, BENEFICIO e CUSTO ANTES de confirmar)
            var detailArea = new GameObject("DetailPanel");
            detailArea.transform.SetParent(_rootPanel.transform, false);
            var drt = detailArea.AddComponent<RectTransform>();
            drt.anchorMin = new Vector2(0.66f, 0.10f);
            drt.anchorMax = new Vector2(0.97f, 0.88f);
            drt.offsetMin = Vector2.zero;
            drt.offsetMax = Vector2.zero;
            detailArea.AddComponent<Image>().color = new Color(0.10f, 0.14f, 0.21f, 0.96f);

            _detailTitle   = CreateLabel(detailArea.transform, "Title",   "", font, 26, TextAnchor.UpperLeft, new Color(0.95f, 0.88f, 0.50f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -50f),  new Vector2(520f, 44f));
            _detailRelic   = CreateLabel(detailArea.transform, "Relic",   "", font, 20, TextAnchor.UpperLeft, new Color(0.62f, 0.90f, 1.00f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -104f), new Vector2(520f, 36f));
            _detailStatus  = CreateLabel(detailArea.transform, "Status",  "", font, 18, TextAnchor.UpperLeft, Color.white,                          new Vector2(0.5f, 1f), new Vector2(0f, -152f), new Vector2(520f, 36f));
            _detailBenefit = CreateLabel(detailArea.transform, "Benefit", "", font, 20, TextAnchor.UpperLeft, new Color(0.32f, 0.96f, 0.62f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -245f), new Vector2(520f, 120f));
            _detailCost    = CreateLabel(detailArea.transform, "Cost",    "", font, 20, TextAnchor.UpperLeft, new Color(1.00f, 0.64f, 0.30f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -385f), new Vector2(520f, 120f));
            _detailNpc     = CreateLabel(detailArea.transform, "Npc",     "", font, 18, TextAnchor.UpperLeft, new Color(0.84f, 0.88f, 0.96f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -510f), new Vector2(520f, 64f));

            // Botao Confirmar Conserto
            var confirmGo = CreateBox(detailArea.transform, "Btn_ConfirmarConserto", new Vector2(0.5f, 0.14f), new Vector2(420f, 66f), new Color(0.18f, 0.72f, 0.50f, 1f));
            _confirmButton = confirmGo.AddComponent<Button>();
            _confirmButton.onClick.AddListener(() => ConfirmSelectedFuso());
            _confirmButtonText = CreateLabel(confirmGo.transform, "Txt", "CONFIRMAR CONSERTO", font, 22, TextAnchor.MiddleCenter, Color.white, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(400f, 58f));

            // Botao Fechar
            var closeGo = CreateBox(_rootPanel.transform, "Btn_FecharTelaFusos", new Vector2(0.92f, 0.94f), new Vector2(180f, 48f), new Color(0.34f, 0.20f, 0.24f, 1f));
            closeGo.AddComponent<Button>().onClick.AddListener(Close);
            CreateLabel(closeGo.transform, "Txt", "FECHAR [M]", font, 18, TextAnchor.MiddleCenter, Color.white, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(170f, 42f));
        }

        private static GameObject CreateBox(Transform parent, string name, Vector2 anchorPos, Vector2 size, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchorPos;
            rt.anchorMax = anchorPos;
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = size;
            go.AddComponent<Image>().color = color;
            return go;
        }

        private static Text CreateLabel(Transform parent, string name, string content, Font font, int fontSize,
            TextAnchor align, Color color, Vector2 anchor, Vector2 anchoredPos, Vector2 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;
            var txt = go.AddComponent<Text>();
            txt.font = font;
            txt.fontSize = fontSize;
            txt.alignment = align;
            txt.color = color;
            txt.text = content;
            txt.horizontalOverflow = HorizontalWrapMode.Wrap;
            txt.verticalOverflow = VerticalWrapMode.Overflow;
            return txt;
        }
    }
}
