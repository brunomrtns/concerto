using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace Studio.Editor
{
    /// <summary>
    /// Monta a Cutscene 4 ("O Ceu Rachou" — ponte Oficina -> Ato 3 / Setor Externo) em Caldeiral.unity:
    ///   - Cria CS04_O_Ceu_Rachou_Timeline.playable (36s, 4 Beats)
    ///   - Reusa 100% CutsceneUI_Canvas.prefab (letterbox, legendas, skip) + NPC_Juno_Oficina + Door_Caldeiral_To_Sector + SetorExterno.unity
    ///   - Instala luzes de emergencia/backup da oficina (desligadas fora da CS04) + SFX TEMP
    /// </summary>
    public static class Cutscene04SkyCrackedBuilder
    {
        private const string CaldeiralPath  = "Assets/Game/Scenes/Caldeiral.unity";
        private const string TimelinePath   = "Assets/Game/Cutscenes/CS04_O_Ceu_Rachou_Timeline.playable";
        private const string SubtitlePath   = "Assets/Game/Cutscenes/Data/cs04_subtitles_pt_br.json";
        private const string UiPrefabPath   = "Assets/Game/Prefabs/UI/CutsceneUI_Canvas.prefab";
        private const string AlarmSfxPath   = "Assets/Game/Audio/SFX/Cutscenes/cs04_alarm_backup_TEMP.wav";
        private const string DoorSfxPath    = "Assets/Game/Audio/SFX/Cutscenes/cs04_heavy_door_unlock_TEMP.wav";
        private const string WindSfxPath    = "Assets/Game/Audio/SFX/Sector/sector_cold_wind_ambience_TEMP.wav";

        [MenuItem("Studio/Concerto/Cutscenes/Build Cutscene 04 (O Ceu Rachou)")]
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
                Debug.LogError($"[Cutscene04Builder] Fatal: {ex}");
                EditorApplication.Exit(1);
            }
        }

        public static void Build()
        {
            AssetDatabase.Refresh();

            // 1. Cria ou recria TimelineAsset (36s)
            if (AssetDatabase.LoadAssetAtPath<TimelineAsset>(TimelinePath) != null)
                AssetDatabase.DeleteAsset(TimelinePath);

            var tl = ScriptableObject.CreateInstance<TimelineAsset>();
            tl.durationMode = TimelineAsset.DurationMode.FixedLength;
            tl.fixedDuration = 36.0;
            AssetDatabase.CreateAsset(tl, TimelinePath);
            tl.CreateTrack<SignalTrack>(null, "CS04_Beats_Track");
            EditorUtility.SetDirty(tl);
            AssetDatabase.SaveAssets();

            // 2. Abre Caldeiral.unity e instala CS04_O_Ceu_Rachou_Rig (idempotente)
            var scene = EditorSceneManager.OpenScene(CaldeiralPath, OpenSceneMode.Single);
            var oldRig = GameObject.Find("CS04_O_Ceu_Rachou_Rig");
            if (oldRig != null) UnityEngine.Object.DestroyImmediate(oldRig);

            var rig = new GameObject("CS04_O_Ceu_Rachou_Rig");
            var director = rig.AddComponent<PlayableDirector>();
            director.playableAsset = tl;
            director.playOnAwake = false;
            director.extrapolationMode = DirectorWrapMode.Hold;

            var ctrlType = Type.GetType("Studio.Game.Cutscenes.Cutscene04SkyCrackedController, Assembly-CSharp");
            if (ctrlType == null) throw new Exception("Cutscene04SkyCrackedController nao encontrado!");
            var ctrl = rig.AddComponent(ctrlType);

            // Reusa CutsceneUI_Canvas.prefab existente (letterbox, legendas, skip, fade)
            var uiPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(UiPrefabPath);
            GameObject uiInst = null;
            if (uiPrefab != null)
            {
                uiInst = (GameObject)PrefabUtility.InstantiatePrefab(uiPrefab, rig.transform);
                uiInst.name = "CS04_CutsceneUI_Canvas";
            }

            // Luzes de backup da oficina (desligadas no estado normal; acendem no Beat 1 quando o alarme entra)
            var backupRoot = new GameObject("CS04_Workshop_BackupLights");
            backupRoot.transform.SetParent(rig.transform, false);
            CreateBackupLight(backupRoot.transform, "BackupLight_Workbench", new Vector3(-24.0f, 3.4f, -16.5f), new Color(1.0f, 0.24f, 0.14f), 3.6f, 16f);
            CreateBackupLight(backupRoot.transform, "BackupLight_DoorCorridor", new Vector3(-18.2f, 3.2f, -13.5f), new Color(1.0f, 0.35f, 0.18f), 3.2f, 14f);
            backupRoot.SetActive(false);

            // AudioSources dedicados (sobrevivem com o rig durante a transicao seamless!)
            var sfxSrc = rig.AddComponent<AudioSource>();
            sfxSrc.playOnAwake = false;
            sfxSrc.spatialBlend = 0f;

            var windGo = new GameObject("CS04_WindAudio");
            windGo.transform.SetParent(rig.transform, false);
            var windSrc = windGo.AddComponent<AudioSource>();
            windSrc.playOnAwake = false;
            windSrc.spatialBlend = 0f;
            windSrc.loop = true;

            // Tenta localizar o ceilingLightsRoot ja referenciado pelo Repair_System_Manager ou CS03
            GameObject ceilingRoot = null;
            var repMgr = GameObject.Find("Repair_System_Manager");
            if (repMgr != null)
            {
                var rcType = Type.GetType("Studio.Game.Repair.RepairRitualController, Assembly-CSharp");
                var rc = rcType != null ? repMgr.GetComponent(rcType) : null;
                if (rc != null)
                {
                    var rso = new SerializedObject(rc);
                    ceilingRoot = rso.FindProperty("ceilingLightsRoot")?.objectReferenceValue as GameObject;
                }
            }

            var uiType = Type.GetType("Studio.Game.Cutscenes.CutsceneUIOverlay, Assembly-CSharp");
            var so = new SerializedObject(ctrl);
            so.FindProperty("subtitleJson").objectReferenceValue        = AssetDatabase.LoadAssetAtPath<TextAsset>(SubtitlePath);
            so.FindProperty("uiOverlay").objectReferenceValue           = (uiInst != null && uiType != null) ? uiInst.GetComponent(uiType) : null;
            so.FindProperty("ceilingLightsRoot").objectReferenceValue   = ceilingRoot;
            so.FindProperty("backupLightsRoot").objectReferenceValue    = backupRoot;
            so.FindProperty("sfxSource").objectReferenceValue           = sfxSrc;
            so.FindProperty("windSource").objectReferenceValue          = windSrc;
            so.FindProperty("alarmBackupClip").objectReferenceValue     = AssetDatabase.LoadAssetAtPath<AudioClip>(AlarmSfxPath);
            so.FindProperty("heavyDoorUnlockClip").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(DoorSfxPath);
            so.FindProperty("coldWindClip").objectReferenceValue        = AssetDatabase.LoadAssetAtPath<AudioClip>(WindSfxPath);
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, CaldeiralPath);
            AssetDatabase.SaveAssets();
            Debug.Log("[Cutscene04Builder] Cutscene 4 ('O Ceu Rachou') instalada em Caldeiral.unity com sucesso!");
        }

        private static void CreateBackupLight(Transform parent, string name, Vector3 pos, Color color, float intensity, float range)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            var l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = color;
            l.intensity = intensity;
            l.range = range;
        }
    }
}
