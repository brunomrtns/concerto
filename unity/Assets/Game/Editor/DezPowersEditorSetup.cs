#if UNITY_EDITOR
using System.IO;
using Studio.Game.Powers;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Studio.Game.Editor
{
    public static class DezPowersEditorSetup
    {
        private const string ConfigDir = "Assets/Game/Config";
        private const string ConfigAssetPath = ConfigDir + "/DezPowersConfig.asset";

        [MenuItem("Concerto/Powers (#93)/Build Config & Wire Scenes")]
        public static void RunSetup()
        {
            if (!Directory.Exists(ConfigDir))
                Directory.CreateDirectory(ConfigDir);

            AssetDatabase.Refresh();

            var cfg = AssetDatabase.LoadAssetAtPath<DezPowersConfig>(ConfigAssetPath);
            if (cfg == null)
            {
                cfg = ScriptableObject.CreateInstance<DezPowersConfig>();
                AssetDatabase.CreateAsset(cfg, ConfigAssetPath);
                EditorUtility.SetDirty(cfg);
            }

            var ecoClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Game/Audio/SFX/Powers/sfx_eco_temporal_reverse_TEMP.wav");
            var stasisClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Game/Audio/SFX/Powers/sfx_estase_pulse_TEMP.wav");

            WireScene("Assets/Game/Scenes/Caldeiral.unity", cfg, ecoClip, stasisClip);
            WireScene("Assets/Game/Scenes/SetorExterno.unity", cfg, ecoClip, stasisClip);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[DezPowersEditorSetup] Setup #93 (Eco Temporal + Estase) concluido.");
        }

        private static void WireScene(string scenePath, DezPowersConfig cfg, AudioClip ecoClip, AudioClip stasisClip)
        {
            if (!File.Exists(scenePath)) return;
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            var dez = GameObject.Find("Player_Dez_Placeholder");
            if (dez != null)
            {
                var ctrl = dez.GetComponent<DezPowersController>();
                if (ctrl == null) ctrl = dez.AddComponent<DezPowersController>();
                ctrl.config = cfg;
                ctrl.ecoReverseClip = ecoClip;
                ctrl.stasisPulseClip = stasisClip;
                EditorUtility.SetDirty(ctrl);
                EditorSceneManager.SaveScene(scene);
                Debug.Log($"[DezPowersEditorSetup] DezPowersController ligado em {scenePath}.");
            }
        }
    }
}
#endif
