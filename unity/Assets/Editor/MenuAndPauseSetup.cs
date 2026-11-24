using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Studio.Editor
{
    /// <summary>
    /// MenuAndPauseSetup — Monta o fluxo completo Boot -> Menu Principal -> Pause & Opcoes:
    ///   1. Cria Prefab compartilhado Assets/Game/Prefabs/UI/OptionsPanel.prefab (Master/Musica/SFX/Fullscreen/Qualidade)
    ///   2. Cria Assets/Game/Scenes/MainMenu.unity (Boot logo TEMP + Menu com foto da bancada + motif caixinha CS01)
    ///   3. Instala PauseMenu_Canvas (com o MESMO OptionsPanel.prefab + modal de confirmacao) em Caldeiral.unity
    ///   4. Registra [MainMenu.unity, Caldeiral.unity] no EditorBuildSettings
    /// </summary>
    public static class MenuAndPauseSetup
    {
        private const string MainMenuScenePath = "Assets/Game/Scenes/MainMenu.unity";
        private const string GameScenePath     = "Assets/Game/Scenes/Caldeiral.unity";
        private const string OptionsPrefabPath = "Assets/Game/Prefabs/UI/OptionsPanel.prefab";
        private const string WorkbenchPhotoPath = "Assets/Game/Art/Textures/Cutscenes/cs01_foto_dez_esposa.png";
        private const string MusicBoxClipPath   = "Assets/Game/Audio/Cutscenes/CS01/cs01_musicbox_motif_TEMP.wav";

        private static readonly Color BgDark       = new Color(0.06f, 0.07f, 0.10f, 1.00f);
        private static readonly Color CardBg       = new Color(0.08f, 0.10f, 0.14f, 0.94f);
        private static readonly Color BorderCopper = new Color(0.76f, 0.54f, 0.28f, 0.95f);
        private static readonly Color GoldTitle    = new Color(0.99f, 0.85f, 0.58f, 1.00f);
        private static readonly Color CyanAccent   = new Color(0.30f, 0.88f, 0.96f, 1.00f);
        private static readonly Color BtnNormal    = new Color(0.14f, 0.17f, 0.23f, 0.96f);
        private static readonly Color BtnHover     = new Color(0.24f, 0.30f, 0.38f, 1.00f);
        private static readonly Color TextLight    = new Color(0.95f, 0.96f, 0.98f, 1.00f);

        private static Font _font;

        [MenuItem("Studio/Concerto/Menu/Build Boot, MainMenu & Pause Flow")]
        public static void BuildMenu() => Build();

        public static void BuildBatch()
        {
            try
            {
                Build();
                EditorApplication.Exit(0);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[MenuAndPauseSetup] Fatal: {ex}");
                EditorApplication.Exit(1);
            }
        }

        public static void Build()
        {
            EnsureDir("Assets/Game/Prefabs/UI");
            EnsureDir("Assets/Game/Scenes");
            AssetDatabase.Refresh();

            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            // 1. Prefab compartilhado de Opcoes (usado no Menu Principal E no Pause!)
            var optionsPrefab = BuildOptionsPanelPrefab();

            // 2. Cena MainMenu.unity (Boot Splash TEMP + Menu Principal)
            BuildMainMenuScene(optionsPrefab);

            // 3. PauseMenu_Canvas dentro de Caldeiral.unity (sem mexer em gameplay/cutscene/dialogo!)
            InstallPauseInCaldeiral(optionsPrefab);

            // 4. Registra cenas no Build Settings: [0] MainMenu, [1] Caldeiral
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(MainMenuScenePath, true),
                new EditorBuildSettingsScene(GameScenePath, true),
            };

            AssetDatabase.SaveAssets();
            Debug.Log("[MenuAndPauseSetup] Boot + MainMenu.unity + PauseMenu (Caldeiral.unity) + OptionsPanel.prefab montados e registrados no BuildSettings!");
        }

        private static GameObject BuildOptionsPanelPrefab()
        {
            var root = new GameObject("OptionsPanel");
            var rt = root.AddComponent<RectTransform>();
            StretchFull(rt);

            // Scrim escuro
            var scrim = root.AddComponent<Image>();
            scrim.color = new Color(0.02f, 0.03f, 0.05f, 0.80f);

            // Card central com borda cobre
            var card = CreatePanel(root.transform, "OptionsCard", new Vector2(680f, 560f), CardBg, BorderCopper);

            CreateText(card.transform, "Title", "OPÇÕES", 34, GoldTitle, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(0f, 225f), new Vector2(560f, 48f));

            var optType = Type.GetType("Studio.Game.Menu.OptionsPanelUI, Assembly-CSharp");
            if (optType == null) throw new Exception("OptionsPanelUI nao encontrado em Assembly-CSharp!");
            var optComp = root.AddComponent(optType);

            var (masterSlider, masterVal) = CreateSliderRow(card.transform, "MasterRow", "Volume Master", 145f);
            var (musicSlider, musicVal)   = CreateSliderRow(card.transform, "MusicRow",  "Música",         75f);
            var (sfxSlider, sfxVal)       = CreateSliderRow(card.transform, "SfxRow",    "Efeitos (SFX)",   5f);

            // Toggle Tela Cheia
            var fsToggle = CreateToggleRow(card.transform, "FullscreenRow", "Tela Cheia", -65f);

            // Seletor Qualidade Grafica
            var (qPrev, qNext, qVal) = CreateSelectorRow(card.transform, "QualityRow", "Qualidade Gráfica", -135f);

            // Botao Voltar
            var backBtn = CreateStyledButton(card.transform, "Btn_Voltar", "VOLTAR", new Vector2(0f, -220f), new Vector2(240f, 50f));

            var so = new SerializedObject(optComp);
            so.FindProperty("masterSlider").objectReferenceValue = masterSlider;
            so.FindProperty("masterValueText").objectReferenceValue = masterVal;
            so.FindProperty("musicSlider").objectReferenceValue = musicSlider;
            so.FindProperty("musicValueText").objectReferenceValue = musicVal;
            so.FindProperty("sfxSlider").objectReferenceValue = sfxSlider;
            so.FindProperty("sfxValueText").objectReferenceValue = sfxVal;
            so.FindProperty("fullscreenToggle").objectReferenceValue = fsToggle;
            so.FindProperty("qualityPrevButton").objectReferenceValue = qPrev;
            so.FindProperty("qualityNextButton").objectReferenceValue = qNext;
            so.FindProperty("qualityValueText").objectReferenceValue = qVal;
            so.FindProperty("backButton").objectReferenceValue = backBtn;
            so.ApplyModifiedPropertiesWithoutUndo();

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, OptionsPrefabPath);
            UnityEngine.Object.DestroyImmediate(root);
            return prefab;
        }

        private static void BuildMainMenuScene(GameObject optionsPrefab)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Camera + AudioListener
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = BgDark;
            cam.orthographic = true;
            camGo.AddComponent<AudioListener>();

            // EventSystem
            var esGo = new GameObject("EventSystem");
            esGo.AddComponent<EventSystem>();
            esGo.AddComponent<StandaloneInputModule>();

            // Canvas 1920x1080
            var canvasGo = CreateCanvas("MainMenu_Canvas", sortOrder: 10);

            var ctrlType = Type.GetType("Studio.Game.Menu.BootAndMainMenuController, Assembly-CSharp");
            if (ctrlType == null) throw new Exception("BootAndMainMenuController nao encontrado!");
            var ctrlComp = canvasGo.AddComponent(ctrlType);

            // AudioSource com o motif da caixinha da Cutscene 1 tocando baixinho em loop!
            var musicSrc = canvasGo.AddComponent<AudioSource>();
            musicSrc.playOnAwake = false;
            musicSrc.loop = true;
            musicSrc.volume = 0.35f;
            musicSrc.spatialBlend = 0f;
            musicSrc.clip = AssetDatabase.LoadAssetAtPath<AudioClip>(MusicBoxClipPath);

            // === MAIN MENU ROOT ===
            var menuRoot = new GameObject("MainMenu_Root");
            menuRoot.transform.SetParent(canvasGo.transform, false);
            var menuRt = menuRoot.AddComponent<RectTransform>();
            StretchFull(menuRt);
            var menuCg = menuRoot.AddComponent<CanvasGroup>();
            var menuBg = menuRoot.AddComponent<Image>();
            menuBg.color = BgDark;

            // Identidade Visual Direita: Foto da bancada (Dez e Lira) emoldurada em cobre/ouro!
            var photoFrame = CreatePanel(menuRoot.transform, "WorkbenchPhotoFrame", new Vector2(780f, 640f),
                new Color(0.10f, 0.08f, 0.07f, 1f), BorderCopper);
            photoFrame.GetComponent<RectTransform>().anchoredPosition = new Vector2(360f, 15f);

            var photoGo = new GameObject("WorkbenchPhoto");
            photoGo.transform.SetParent(photoFrame.transform, false);
            var photoRt = photoGo.AddComponent<RectTransform>();
            photoRt.anchorMin = new Vector2(0.03f, 0.08f);
            photoRt.anchorMax = new Vector2(0.97f, 0.96f);
            photoRt.offsetMin = Vector2.zero;
            photoRt.offsetMax = Vector2.zero;
            var rawImg = photoGo.AddComponent<RawImage>();
            var photoTex = AssetDatabase.LoadAssetAtPath<Texture2D>(WorkbenchPhotoPath);
            if (photoTex != null) rawImg.texture = photoTex;
            rawImg.color = new Color(1.00f, 0.96f, 0.90f, 0.96f);

            CreateText(photoFrame.transform, "PhotoCaption", "Oficina do Caldeiral — Projeto Echo", 18,
                new Color(0.90f, 0.78f, 0.58f, 0.85f), FontStyle.Italic, TextAnchor.MiddleCenter,
                new Vector2(0f, -296f), new Vector2(700f, 34f));

            // Coluna Esquerda: Titulo CONCERTO + Botoes coerentes com o HUD
            CreateText(menuRoot.transform, "GameTitle", "CONCERTO", 84, GoldTitle, FontStyle.Bold, TextAnchor.MiddleLeft,
                new Vector2(-470f, 240f), new Vector2(640f, 100f));
            CreateText(menuRoot.transform, "GameSubtitle", "ATO 1 — A OFICINA", 24, CyanAccent, FontStyle.Normal, TextAnchor.MiddleLeft,
                new Vector2(-468f, 170f), new Vector2(560f, 36f));

            // Linha divisoria cobre
            var lineGo = new GameObject("TitleDivider");
            lineGo.transform.SetParent(menuRoot.transform, false);
            var lineRt = lineGo.AddComponent<RectTransform>();
            lineRt.sizeDelta = new Vector2(380f, 3f);
            lineRt.anchoredPosition = new Vector2(-598f, 136f);
            lineGo.AddComponent<Image>().color = BorderCopper;

            // Container de botoes (VerticalLayoutGroup para facilitar adicionar novos itens!)
            var btnStack = new GameObject("MenuButtonStack");
            btnStack.transform.SetParent(menuRoot.transform, false);
            var stackRt = btnStack.AddComponent<RectTransform>();
            stackRt.sizeDelta = new Vector2(360f, 320f);
            stackRt.anchoredPosition = new Vector2(-608f, -60f);
            var vlg = btnStack.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 16f;
            vlg.childAlignment = TextAnchor.UpperLeft;
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            var btnContinue = CreateStyledButton(btnStack.transform, "Btn_Continuar", "CONTINUAR", Vector2.zero, new Vector2(360f, 58f));
            var btnNewGame  = CreateStyledButton(btnStack.transform, "Btn_NovoJogo",  "NOVO JOGO", Vector2.zero, new Vector2(360f, 58f));
            var btnOptions  = CreateStyledButton(btnStack.transform, "Btn_Opcoes",    "OPÇÕES",    Vector2.zero, new Vector2(360f, 58f));
            var btnQuit     = CreateStyledButton(btnStack.transform, "Btn_Sair",      "SAIR",      Vector2.zero, new Vector2(360f, 58f));

            // === OPTIONS PANEL (PREFAB COMPARTILHADO) ===
            var optInst = (GameObject)PrefabUtility.InstantiatePrefab(optionsPrefab, canvasGo.transform);
            optInst.name = "OptionsPanel_MainMenu";
            optInst.SetActive(false);
            var optComp = optInst.GetComponent(Type.GetType("Studio.Game.Menu.OptionsPanelUI, Assembly-CSharp"));

            // === BOOT SPLASH (LOGO TEMP) ===
            var bootGo = new GameObject("BootSplash_Overlay");
            bootGo.transform.SetParent(canvasGo.transform, false);
            var bootRt = bootGo.AddComponent<RectTransform>();
            StretchFull(bootRt);
            var bootCg = bootGo.AddComponent<CanvasGroup>();
            bootGo.AddComponent<Image>().color = new Color(0.03f, 0.04f, 0.06f, 1f);

            CreateText(bootGo.transform, "BootLogoMain", "CONCERTO", 96, GoldTitle, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(0f, 24f), new Vector2(900f, 120f));
            CreateText(bootGo.transform, "BootLogoSub", "STUDIO CONCERTO  •  LOGO TEMP", 22, CyanAccent, FontStyle.Normal, TextAnchor.MiddleCenter,
                new Vector2(0f, -52f), new Vector2(700f, 36f));

            // Liga referencias no BootAndMainMenuController
            var so = new SerializedObject(ctrlComp);
            so.FindProperty("bootSplashGroup").objectReferenceValue = bootCg;
            so.FindProperty("mainMenuRoot").objectReferenceValue = menuRoot;
            so.FindProperty("mainMenuGroup").objectReferenceValue = menuCg;
            so.FindProperty("continueButton").objectReferenceValue = btnContinue;
            so.FindProperty("newGameButton").objectReferenceValue = btnNewGame;
            so.FindProperty("optionsButton").objectReferenceValue = btnOptions;
            so.FindProperty("quitButton").objectReferenceValue = btnQuit;
            so.FindProperty("optionsPanel").objectReferenceValue = optComp;
            so.FindProperty("menuMusicSource").objectReferenceValue = musicSrc;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, MainMenuScenePath);
        }

        private static void InstallPauseInCaldeiral(GameObject optionsPrefab)
        {
            var scene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);

            // Garante EventSystem na cena da oficina para cliques de Pause/Opcoes
            if (UnityEngine.Object.FindAnyObjectByType<EventSystem>() == null)
            {
                var esGo = new GameObject("EventSystem");
                esGo.AddComponent<EventSystem>();
                esGo.AddComponent<StandaloneInputModule>();
            }

            var oldPause = GameObject.Find("PauseMenu_Canvas");
            if (oldPause != null) UnityEngine.Object.DestroyImmediate(oldPause);

            var canvasGo = CreateCanvas("PauseMenu_Canvas", sortOrder: 80);
            var ctrlType = Type.GetType("Studio.Game.Menu.PauseMenuController, Assembly-CSharp");
            if (ctrlType == null) throw new Exception("PauseMenuController nao encontrado!");
            var ctrlComp = canvasGo.AddComponent(ctrlType);

            // Pause Root (Scrim + Card central)
            var pauseRoot = new GameObject("Pause_Root");
            pauseRoot.transform.SetParent(canvasGo.transform, false);
            StretchFull(pauseRoot.AddComponent<RectTransform>());
            pauseRoot.AddComponent<Image>().color = new Color(0.02f, 0.03f, 0.05f, 0.72f);

            var card = CreatePanel(pauseRoot.transform, "PauseCard", new Vector2(460f, 400f), CardBg, BorderCopper);
            var cardCg = card.AddComponent<CanvasGroup>();

            CreateText(card.transform, "PauseTitle", "PAUSADO", 40, GoldTitle, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(0f, 138f), new Vector2(380f, 54f));

            var btnStack = new GameObject("PauseButtonStack");
            btnStack.transform.SetParent(card.transform, false);
            var stackRt = btnStack.AddComponent<RectTransform>();
            stackRt.sizeDelta = new Vector2(320f, 220f);
            stackRt.anchoredPosition = new Vector2(0f, -35f);
            var vlg = btnStack.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 16f;
            vlg.childAlignment = TextAnchor.MiddleCenter;
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            var btnResume = CreateStyledButton(btnStack.transform, "Btn_Continuar",  "CONTINUAR",      Vector2.zero, new Vector2(320f, 56f));
            var btnOpts   = CreateStyledButton(btnStack.transform, "Btn_Opcoes",     "OPÇÕES",         Vector2.zero, new Vector2(320f, 56f));
            var btnMenu   = CreateStyledButton(btnStack.transform, "Btn_VoltarMenu", "VOLTAR AO MENU", Vector2.zero, new Vector2(320f, 56f));

            // Modal de confirmacao para Voltar ao Menu (se houver progresso)
            var modal = new GameObject("ConfirmReturnModal");
            modal.transform.SetParent(pauseRoot.transform, false);
            StretchFull(modal.AddComponent<RectTransform>());
            modal.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.65f);

            var mCard = CreatePanel(modal.transform, "ConfirmCard", new Vector2(540f, 250f), CardBg, BorderCopper);
            CreateText(mCard.transform, "ConfirmTitle", "VOLTAR AO MENU?", 28, GoldTitle, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(0f, 68f), new Vector2(480f, 40f));
            CreateText(mCard.transform, "ConfirmBody", "Seu progresso atual na oficina será salvo.\nDeseja retornar ao Menu Principal?", 20, TextLight, FontStyle.Normal, TextAnchor.MiddleCenter,
                new Vector2(0f, 8f), new Vector2(480f, 64f));

            var btnYes = CreateStyledButton(mCard.transform, "Btn_ConfirmYes", "SIM, VOLTAR", new Vector2(-115f, -72f), new Vector2(200f, 48f));
            var btnNo  = CreateStyledButton(mCard.transform, "Btn_ConfirmNo",  "CANCELAR",    new Vector2(115f, -72f),  new Vector2(200f, 48f));
            modal.SetActive(false);

            // Instancia do MESMO OptionsPanel.prefab!
            var optInst = (GameObject)PrefabUtility.InstantiatePrefab(optionsPrefab, pauseRoot.transform);
            optInst.name = "OptionsPanel_Pause";
            optInst.SetActive(false);
            var optComp = optInst.GetComponent(Type.GetType("Studio.Game.Menu.OptionsPanelUI, Assembly-CSharp"));

            pauseRoot.SetActive(false);

            var so = new SerializedObject(ctrlComp);
            so.FindProperty("pauseRoot").objectReferenceValue = pauseRoot;
            so.FindProperty("pauseButtonsGroup").objectReferenceValue = cardCg;
            so.FindProperty("optionsPanel").objectReferenceValue = optComp;
            so.FindProperty("confirmMenuModal").objectReferenceValue = modal;
            so.FindProperty("resumeButton").objectReferenceValue = btnResume;
            so.FindProperty("optionsButton").objectReferenceValue = btnOpts;
            so.FindProperty("returnToMenuButton").objectReferenceValue = btnMenu;
            so.FindProperty("confirmReturnYesButton").objectReferenceValue = btnYes;
            so.FindProperty("confirmReturnNoButton").objectReferenceValue = btnNo;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, GameScenePath);
        }

        // === UI HELPERS ===
        private static GameObject CreateCanvas(string name, int sortOrder)
        {
            var go = new GameObject(name);
            var c = go.AddComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            c.sortingOrder = sortOrder;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();
            return go;
        }

        private static GameObject CreatePanel(Transform parent, string name, Vector2 size, Color bg, Color border)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.sizeDelta = size;
            rt.anchoredPosition = Vector2.zero;
            var img = go.AddComponent<Image>();
            img.color = bg;
            var outline = go.AddComponent<Outline>();
            outline.effectColor = border;
            outline.effectDistance = new Vector2(2f, -2f);
            return go;
        }

        private static Text CreateText(Transform parent, string name, string content, int size, Color color, FontStyle style, TextAnchor align, Vector2 pos, Vector2 dims)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.sizeDelta = dims;
            rt.anchoredPosition = pos;
            var txt = go.AddComponent<Text>();
            txt.font = _font;
            txt.text = content;
            txt.fontSize = size;
            txt.color = color;
            txt.fontStyle = style;
            txt.alignment = align;
            txt.raycastTarget = false;
            return txt;
        }

        private static Button CreateStyledButton(Transform parent, string name, string label, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.sizeDelta = size;
            rt.anchoredPosition = pos;

            var le = go.AddComponent<LayoutElement>();
            le.preferredWidth = size.x;
            le.preferredHeight = size.y;

            var img = go.AddComponent<Image>();
            img.color = BtnNormal;
            var outline = go.AddComponent<Outline>();
            outline.effectColor = BorderCopper;
            outline.effectDistance = new Vector2(1.5f, -1.5f);

            var btn = go.AddComponent<Button>();
            var cb = btn.colors;
            cb.normalColor = BtnNormal;
            cb.highlightedColor = BtnHover;
            cb.pressedColor = new Color(0.18f, 0.42f, 0.48f, 1f);
            cb.selectedColor = BtnHover;
            btn.colors = cb;

            CreateText(go.transform, "Label", label, 22, TextLight, FontStyle.Bold, TextAnchor.MiddleCenter, Vector2.zero, size);
            return btn;
        }

        private static (Slider, Text) CreateSliderRow(Transform parent, string name, string label, float y)
        {
            var row = new GameObject(name);
            row.transform.SetParent(parent, false);
            var rt = row.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(580f, 44f);
            rt.anchoredPosition = new Vector2(0f, y);

            CreateText(row.transform, "Label", label, 22, TextLight, FontStyle.Normal, TextAnchor.MiddleLeft,
                new Vector2(-175f, 0f), new Vector2(220f, 40f));

            var sliderGo = new GameObject("Slider");
            sliderGo.transform.SetParent(row.transform, false);
            var sRt = sliderGo.AddComponent<RectTransform>();
            sRt.sizeDelta = new Vector2(240f, 24f);
            sRt.anchoredPosition = new Vector2(55f, 0f);

            var bg = new GameObject("Background");
            bg.transform.SetParent(sliderGo.transform, false);
            StretchFull(bg.AddComponent<RectTransform>());
            bg.AddComponent<Image>().color = new Color(0.18f, 0.22f, 0.28f, 1f);

            var fillArea = new GameObject("FillArea");
            fillArea.transform.SetParent(sliderGo.transform, false);
            StretchFull(fillArea.AddComponent<RectTransform>());
            var fill = new GameObject("Fill");
            fill.transform.SetParent(fillArea.transform, false);
            var fRt = fill.AddComponent<RectTransform>();
            StretchFull(fRt);
            fill.AddComponent<Image>().color = CyanAccent;

            var handleArea = new GameObject("HandleArea");
            handleArea.transform.SetParent(sliderGo.transform, false);
            StretchFull(handleArea.AddComponent<RectTransform>());
            var handle = new GameObject("Handle");
            handle.transform.SetParent(handleArea.transform, false);
            var hRt = handle.AddComponent<RectTransform>();
            hRt.sizeDelta = new Vector2(18f, 30f);
            var hImg = handle.AddComponent<Image>();
            hImg.color = GoldTitle;

            var slider = sliderGo.AddComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.fillRect = fRt;
            slider.handleRect = hRt;
            slider.targetGraphic = hImg;

            var valTxt = CreateText(row.transform, "Value", "85%", 20, GoldTitle, FontStyle.Bold, TextAnchor.MiddleRight,
                new Vector2(235f, 0f), new Vector2(80f, 40f));

            return (slider, valTxt);
        }

        private static Toggle CreateToggleRow(Transform parent, string name, string label, float y)
        {
            var row = new GameObject(name);
            row.transform.SetParent(parent, false);
            var rt = row.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(580f, 44f);
            rt.anchoredPosition = new Vector2(0f, y);

            CreateText(row.transform, "Label", label, 22, TextLight, FontStyle.Normal, TextAnchor.MiddleLeft,
                new Vector2(-175f, 0f), new Vector2(220f, 40f));

            var togGo = new GameObject("Toggle");
            togGo.transform.SetParent(row.transform, false);
            var tRt = togGo.AddComponent<RectTransform>();
            tRt.sizeDelta = new Vector2(36f, 36f);
            tRt.anchoredPosition = new Vector2(-45f, 0f);
            var bgImg = togGo.AddComponent<Image>();
            bgImg.color = new Color(0.18f, 0.22f, 0.28f, 1f);

            var check = new GameObject("Checkmark");
            check.transform.SetParent(togGo.transform, false);
            var cRt = check.AddComponent<RectTransform>();
            cRt.sizeDelta = new Vector2(22f, 22f);
            var cImg = check.AddComponent<Image>();
            cImg.color = CyanAccent;

            var tog = togGo.AddComponent<Toggle>();
            tog.targetGraphic = bgImg;
            tog.graphic = cImg;
            tog.isOn = true;
            return tog;
        }

        private static (Button, Button, Text) CreateSelectorRow(Transform parent, string name, string label, float y)
        {
            var row = new GameObject(name);
            row.transform.SetParent(parent, false);
            var rt = row.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(580f, 44f);
            rt.anchoredPosition = new Vector2(0f, y);

            CreateText(row.transform, "Label", label, 22, TextLight, FontStyle.Normal, TextAnchor.MiddleLeft,
                new Vector2(-175f, 0f), new Vector2(220f, 40f));

            var prev = CreateStyledButton(row.transform, "Btn_Prev", "<", new Vector2(-30f, 0f), new Vector2(44f, 40f));
            var val  = CreateText(row.transform, "Value", "Medium", 21, GoldTitle, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(85f, 0f), new Vector2(170f, 40f));
            var next = CreateStyledButton(row.transform, "Btn_Next", ">", new Vector2(200f, 0f), new Vector2(44f, 40f));

            return (prev, next, val);
        }

        private static void StretchFull(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private static void EnsureDir(string assetPath)
        {
            string full = Path.Combine(Directory.GetCurrentDirectory(), assetPath);
            if (!Directory.Exists(full)) Directory.CreateDirectory(full);
        }
    }
}
