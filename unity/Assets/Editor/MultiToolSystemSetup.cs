using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Studio.Editor
{
    /// <summary>
    /// MultiToolSystemSetup — Instala a Ferramenta Multi-Uso (CONCERTAR / COMBATE) em Caldeiral.unity:
    ///   - Cria Assets/Game/Config/MultiToolConfig.asset (ScriptableObject)
    ///   - Reusa Dez_RepairTool_InHand (TripoSG dez_repair_tool.prefab) sem gerar modelo novo
    ///   - Adiciona MultiToolController + modulos plugaveis (RepairToolModule, CombatToolModule)
    ///   - Cria ToolHUD_Canvas (modo + barra de energia + contador de celulas, sem HP ainda)
    ///   - Spawna celulas coletaveis a partir de energy_cells_config.json + 1 alvo de treino mecanico
    /// </summary>
    public static class MultiToolSystemSetup
    {
        private const string ScenePath      = "Assets/Game/Scenes/Caldeiral.unity";
        private const string ConfigPath     = "Assets/Game/Config/MultiToolConfig.asset";
        private const string CellsJsonPath  = "Assets/Game/Tool/Data/energy_cells_config.json";
        private const string ToolAudioDir   = "Assets/Game/Audio/SFX/Tool";
        private const string GlowMatPath    = "Assets/Game/Cutscenes/Mat_CS02_Particle_Glow.mat";

        [MenuItem("Studio/Concerto/Tool/Setup Multi-Use Tool (CONCERTAR / COMBATE) in Caldeiral")]
        public static void ApplyMenu() => Apply();

        public static void ApplyBatch()
        {
            try
            {
                Apply();
                EditorApplication.Exit(0);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[MultiToolSetup] Fatal: {ex}");
                EditorApplication.Exit(1);
            }
        }

        public static void Apply()
        {
            EnsureDir("Assets/Game/Config");
            AssetDatabase.Refresh();

            var cfgType = Type.GetType("Studio.Game.Tool.MultiToolConfig, Assembly-CSharp");
            if (cfgType == null) throw new Exception("MultiToolConfig nao encontrado!");
            var cfgAsset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(ConfigPath);
            if (cfgAsset == null)
            {
                var created = ScriptableObject.CreateInstance(cfgType);
                AssetDatabase.CreateAsset(created, ConfigPath);
                AssetDatabase.SaveAssets();
                AssetDatabase.ImportAsset(ConfigPath);
            }

            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            cfgAsset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(ConfigPath);

            var dezGo = GameObject.Find("Player_Dez_Placeholder");
            if (dezGo == null) throw new Exception("Player_Dez_Placeholder nao encontrado!");

            var handTool = dezGo.transform.Find("Dez_RepairTool_InHand");
            var camTarget = dezGo.transform.Find("Dez_CameraTarget");

            var ctrlType   = Type.GetType("Studio.Game.Tool.MultiToolController, Assembly-CSharp");
            var repModType = Type.GetType("Studio.Game.Tool.RepairToolModule, Assembly-CSharp");
            var cmbModType = Type.GetType("Studio.Game.Tool.CombatToolModule, Assembly-CSharp");
            var cellType   = Type.GetType("Studio.Game.Tool.EnergyCellPickup, Assembly-CSharp");
            var cellCfgT   = Type.GetType("Studio.Game.Tool.EnergyCellsConfigData, Assembly-CSharp");
            var dummyType  = Type.GetType("Studio.Game.Tool.WorkshopTrainingTarget, Assembly-CSharp");

            var oldSys = GameObject.Find("MultiTool_System");
            if (oldSys != null) UnityEngine.Object.DestroyImmediate(oldSys);
            var sysRoot = new GameObject("MultiTool_System");
            var ctrlComp = sysRoot.AddComponent(ctrlType);
            var repMod   = sysRoot.AddComponent(repModType);
            var cmbMod   = sysRoot.AddComponent(cmbModType);

            // Particulas de impacto + AudioSource
            var sparksGo = new GameObject("Tool_HitSparks");
            sparksGo.transform.SetParent(sysRoot.transform, false);
            var ps = sparksGo.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = 0.25f;
            main.startLifetime = 0.32f;
            main.startSpeed = 4.2f;
            main.startSize = 0.11f;
            main.startColor = new Color(0.45f, 0.94f, 1.0f, 0.95f);
            var em = ps.emission;
            em.rateOverTime = 0f;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, 18) });
            var glowMat = AssetDatabase.LoadAssetAtPath<Material>(GlowMatPath);
            if (glowMat != null) ps.GetComponent<ParticleSystemRenderer>().sharedMaterial = glowMat;

            var sfxSrc = sysRoot.AddComponent<AudioSource>();
            sfxSrc.playOnAwake = false;
            sfxSrc.spatialBlend = 0f;

            // HUD da Ferramenta (canto inferior direito, mesmo estilo do HUD atual — sem HP!)
            var (hudCg, modeTxt, accentBar, energyFill, cellsTxt) = BuildToolHud(sysRoot.transform);

            // Liga referencias no MultiToolController
            ctrlType.GetField("config").SetValue(ctrlComp, cfgAsset);
            var so = new SerializedObject(ctrlComp);
            so.Update();
            so.FindProperty("config").objectReferenceValue = cfgAsset;
            var modProp = so.FindProperty("modules");
            modProp.ClearArray();
            modProp.InsertArrayElementAtIndex(0);
            modProp.GetArrayElementAtIndex(0).objectReferenceValue = repMod;
            modProp.InsertArrayElementAtIndex(1);
            modProp.GetArrayElementAtIndex(1).objectReferenceValue = cmbMod;
            so.FindProperty("playerTransform").objectReferenceValue = dezGo.transform;
            so.FindProperty("cameraShakePivot").objectReferenceValue = camTarget;
            so.FindProperty("toolVisualTransform").objectReferenceValue = handTool;
            so.FindProperty("hitSparksParticles").objectReferenceValue = ps;
            so.FindProperty("hudGroup").objectReferenceValue = hudCg;
            so.FindProperty("modeLabelText").objectReferenceValue = modeTxt;
            so.FindProperty("modeAccentBar").objectReferenceValue = accentBar;
            so.FindProperty("energyBarFill").objectReferenceValue = energyFill;
            so.FindProperty("cellsCountText").objectReferenceValue = cellsTxt;
            so.FindProperty("sfxSource").objectReferenceValue = sfxSrc;
            so.FindProperty("modeSwitchClip").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>($"{ToolAudioDir}/tool_mode_switch_TEMP.wav");
            so.FindProperty("meleeSwingClip").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>($"{ToolAudioDir}/tool_melee_swing_TEMP.wav");
            so.FindProperty("meleeHitClip").objectReferenceValue   = AssetDatabase.LoadAssetAtPath<AudioClip>($"{ToolAudioDir}/tool_melee_hit_TEMP.wav");
            so.FindProperty("boltFireClip").objectReferenceValue   = AssetDatabase.LoadAssetAtPath<AudioClip>($"{ToolAudioDir}/tool_bolt_fire_TEMP.wav");
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(ctrlComp);

            // Spawna celulas de energia coletaveis a partir de energy_cells_config.json
            var cellsRoot = new GameObject("Tool_EnergyCells_Spawns").transform;
            cellsRoot.SetParent(sysRoot.transform, false);
            var cellsJson = AssetDatabase.LoadAssetAtPath<TextAsset>(CellsJsonPath);
            var parsedCells = cellCfgT.GetMethod("FromJson").Invoke(null, new object[] { cellsJson });
            var list = cellCfgT.GetField("cells").GetValue(parsedCells) as System.Collections.IList;
            var cellMat = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default"));
            cellMat.color = new Color(0.28f, 0.92f, 1.0f, 1f);

            foreach (var c in list)
            {
                string id = (string)c.GetType().GetField("id").GetValue(c);
                Vector3 pos = (Vector3)c.GetType().GetMethod("ToVector3").Invoke(c, null);
                var cg = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                cg.name = $"EnergyCell_{id}";
                cg.transform.SetParent(cellsRoot, false);
                cg.transform.position = pos;
                cg.transform.localScale = new Vector3(0.22f, 0.28f, 0.22f);
                cg.GetComponent<Collider>().isTrigger = true;
                cg.GetComponent<MeshRenderer>().sharedMaterial = cellMat;
                cg.AddComponent(cellType);
            }

            // 1 Alvo de treino mecanico na oficina (NAO e inimigo — apenas prova hitbox/hitstop/shake/bolt!)
            var dummy = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            dummy.name = "Tool_TrainingTarget_Dummy";
            dummy.transform.SetParent(sysRoot.transform, false);
            dummy.transform.position = new Vector3(-26.6f, 0.75f, -20.5f);
            dummy.transform.localScale = new Vector3(0.68f, 0.75f, 0.68f);
            var dummyMat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            dummyMat.color = new Color(0.74f, 0.42f, 0.20f);
            dummy.GetComponent<MeshRenderer>().sharedMaterial = dummyMat;
            dummy.AddComponent(dummyType);

            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log("[MultiToolSetup] Ferramenta Multi-Uso (CONCERTAR / COMBATE), HUD, celulas config-driven e alvo de treino montados em Caldeiral.unity!");
        }

        private static (CanvasGroup, Text, Image, Image, Text) BuildToolHud(Transform parent)
        {
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var canvasGo = new GameObject("ToolHUD_Canvas");
            canvasGo.transform.SetParent(parent, false);
            var c = canvasGo.AddComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            c.sortingOrder = 50;
            var sc = canvasGo.AddComponent<CanvasScaler>();
            sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            sc.referenceResolution = new Vector2(1920f, 1080f);

            var panel = new GameObject("ToolPanel");
            panel.transform.SetParent(canvasGo.transform, false);
            var rt = panel.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(1f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(1f, 0f);
            rt.anchoredPosition = new Vector2(-28f, 28f);
            rt.sizeDelta = new Vector2(340f, 118f);
            var cg = panel.AddComponent<CanvasGroup>();
            panel.AddComponent<Image>().color = new Color(0.06f, 0.08f, 0.12f, 0.88f);
            var outl = panel.AddComponent<Outline>();
            outl.effectColor = new Color(0.76f, 0.54f, 0.28f, 0.95f);
            outl.effectDistance = new Vector2(1.5f, -1.5f);

            var accent = new GameObject("ModeAccentBar");
            accent.transform.SetParent(panel.transform, false);
            var aRt = accent.AddComponent<RectTransform>();
            aRt.anchorMin = new Vector2(0f, 0f);
            aRt.anchorMax = new Vector2(0f, 1f);
            aRt.pivot = new Vector2(0f, 0.5f);
            aRt.sizeDelta = new Vector2(8f, 0f);
            var accentImg = accent.AddComponent<Image>();
            accentImg.color = new Color(0.99f, 0.85f, 0.58f, 1f);

            var modeTxt = MakeText(panel.transform, "ModeLabel", "[TAB] MODO: CONCERTAR", font, 18, Color.white, FontStyle.Bold, new Vector2(10f, 34f), new Vector2(300f, 28f));

            var barBg = new GameObject("EnergyBarBg");
            barBg.transform.SetParent(panel.transform, false);
            var bRt = barBg.AddComponent<RectTransform>();
            bRt.sizeDelta = new Vector2(280f, 14f);
            bRt.anchoredPosition = new Vector2(10f, 2f);
            barBg.AddComponent<Image>().color = new Color(0.16f, 0.20f, 0.26f, 1f);

            var fillGo = new GameObject("EnergyBarFill");
            fillGo.transform.SetParent(barBg.transform, false);
            var fRt = fillGo.AddComponent<RectTransform>();
            fRt.anchorMin = Vector2.zero;
            fRt.anchorMax = Vector2.one;
            fRt.offsetMin = Vector2.zero;
            fRt.offsetMax = Vector2.zero;
            var fillImg = fillGo.AddComponent<Image>();
            fillImg.color = new Color(0.30f, 0.88f, 0.96f, 1f);
            fillImg.type = Image.Type.Filled;
            fillImg.fillMethod = Image.FillMethod.Horizontal;
            fillImg.fillAmount = 1f;

            var cellsTxt = MakeText(panel.transform, "CellsCount", "CÉLULAS: 3/6", font, 17, new Color(0.99f, 0.85f, 0.58f), FontStyle.Bold, new Vector2(10f, -30f), new Vector2(300f, 26f));

            return (cg, modeTxt, accentImg, fillImg, cellsTxt);
        }

        private static Text MakeText(Transform parent, string name, string content, Font font, int size, Color color, FontStyle style, Vector2 pos, Vector2 dims)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.sizeDelta = dims;
            rt.anchoredPosition = pos;
            var txt = go.AddComponent<Text>();
            txt.font = font;
            txt.text = content;
            txt.fontSize = size;
            txt.color = color;
            txt.fontStyle = style;
            txt.alignment = TextAnchor.MiddleLeft;
            return txt;
        }

        private static void EnsureDir(string assetPath)
        {
            string full = Path.Combine(Directory.GetCurrentDirectory(), assetPath);
            if (!Directory.Exists(full)) Directory.CreateDirectory(full);
        }
    }
}
