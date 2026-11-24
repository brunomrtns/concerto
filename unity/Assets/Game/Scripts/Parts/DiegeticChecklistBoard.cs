using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Studio.Game.Parts
{
    /// <summary>
    /// Quadro diegetico de checklist montado sobre o prop CS01_Quadro_Checklist_Pecas na oficina.
    /// Atualiza em tempo real cada slot (PENDENTE -> OK) conforme Dez coleta as pecas!
    /// </summary>
    public sealed class DiegeticChecklistBoard : MonoBehaviour
    {
        private Canvas _worldCanvas;
        private Text _headerText;
        private readonly List<Image> _slotBgs = new List<Image>();
        private readonly List<Text> _slotLabels = new List<Text>();
        private readonly List<Text> _slotStatuses = new List<Text>();

        private static readonly Color ColPendingBg   = new Color(0.14f, 0.11f, 0.12f, 0.92f);
        private static readonly Color ColPendingTxt  = new Color(0.98f, 0.45f, 0.35f, 1.00f);
        private static readonly Color ColCollectedBg = new Color(0.06f, 0.28f, 0.32f, 0.96f);
        private static readonly Color ColCollectedTxt= new Color(0.32f, 0.98f, 0.96f, 1.00f);

        public int SlotCount => _slotStatuses.Count;

        public void BuildSlots(List<PartPlacementData> activeParts)
        {
            var old = transform.Find("DiegeticChecklist_WorldCanvas");
            if (old != null) Destroy(old.gameObject);
            _slotBgs.Clear();
            _slotLabels.Clear();
            _slotStatuses.Clear();

            var cGo = new GameObject("DiegeticChecklist_WorldCanvas");
            cGo.transform.SetParent(transform, false);
            // Fica rente a face -Z do quadro (voltada para dentro da oficina!)
            cGo.transform.localPosition = new Vector3(0f, 0f, -0.052f);
            cGo.transform.localRotation = Quaternion.identity;
            cGo.transform.localScale = Vector3.one * 0.002f; // 1000px -> 2.0m

            _worldCanvas = cGo.AddComponent<Canvas>();
            _worldCanvas.renderMode = RenderMode.WorldSpace;
            var rt = cGo.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(980f, 710f);

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            // Fundo semi-opaco escuro sobre a textura do quadro
            var bg = MakeBox(cGo.transform, "Bg", new Vector2(0, 0), new Vector2(1, 1), Vector2.zero, Vector2.zero, new Color(0.07f, 0.09f, 0.12f, 0.92f));
            _headerText = MakeTxt(bg, "Header", font, 34, TextAnchor.MiddleLeft, new Color(0.95f, 0.82f, 0.50f), new Vector2(0, 1), new Vector2(1, 1), new Vector2(36, -48), new Vector2(-72, 62));
            _headerText.fontStyle = FontStyle.Bold;

            int count = activeParts != null ? activeParts.Count : 0;
            _headerText.text = $"PROJETO ECHO  //  PEÇAS (0/{count})";

            float startY = -125f;
            float rowH = count > 0 ? Mathf.Min(104f, 540f / count - 10f) : 96f;

            for (int i = 0; i < count; i++)
            {
                float y = startY - i * (rowH + 10f);
                var row = MakeBox(bg, $"Slot_{i}", new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, y), new Vector2(-64, rowH), ColPendingBg);
                row.pivot = new Vector2(0.5f, 1f);
                row.anchoredPosition = new Vector2(0, y);

                var lbl = MakeTxt(row, "Name", font, 28, TextAnchor.MiddleLeft, Color.white, new Vector2(0, 0), new Vector2(0.70f, 1), new Vector2(24, 0), new Vector2(-24, 0));
                lbl.text = $"{i + 1:D2}. {activeParts[i].displayName}";

                var st = MakeTxt(row, "Status", font, 26, TextAnchor.MiddleRight, ColPendingTxt, new Vector2(0.68f, 0), new Vector2(1, 1), new Vector2(-24, 0), new Vector2(-24, 0));
                st.fontStyle = FontStyle.Bold;
                st.text = "[ PENDENTE ]";

                _slotBgs.Add(row.GetComponent<Image>());
                _slotLabels.Add(lbl);
                _slotStatuses.Add(st);
            }
        }

        public void MarkSlotCollected(int index, int collectedTotal, int requiredTotal)
        {
            if (_headerText != null)
                _headerText.text = collectedTotal >= requiredTotal
                    ? $"PROJETO ECHO  //  COMPLETO ({collectedTotal}/{requiredTotal})"
                    : $"PROJETO ECHO  //  PEÇAS ({collectedTotal}/{requiredTotal})";

            if (index >= 0 && index < _slotStatuses.Count)
            {
                _slotBgs[index].color = ColCollectedBg;
                _slotStatuses[index].color = ColCollectedTxt;
                _slotStatuses[index].text = "[ COLETADA ]";
            }
        }

        private static RectTransform MakeBox(Transform p, string n, Vector2 aMin, Vector2 aMax, Vector2 pos, Vector2 sz, Color col)
        {
            var go = new GameObject(n);
            go.transform.SetParent(p, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = aMin; rt.anchorMax = aMax;
            rt.anchoredPosition = pos; rt.sizeDelta = sz;
            go.AddComponent<Image>().color = col;
            return rt;
        }

        private static Text MakeTxt(Transform p, string n, Font f, int sz, TextAnchor align, Color col, Vector2 aMin, Vector2 aMax, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(n);
            go.transform.SetParent(p, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = aMin; rt.anchorMax = aMax;
            rt.anchoredPosition = pos; rt.sizeDelta = size;
            var t = go.AddComponent<Text>();
            t.font = f; t.fontSize = sz; t.alignment = align; t.color = col;
            return t;
        }
    }
}
