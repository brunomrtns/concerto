#if UNITY_EDITOR
using System.IO;
using System.Linq;
using Studio.Game.Animation;
using Studio.Game.Cutscenes;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Studio.Game.EditorTools
{
    /// <summary>
    /// Constroi a cena de staging/execucao da CS02 "A Oferta" (Assets/Game/Scenes/Cs02OfertaDemo.unity)
    /// usando ZERO modelos 3D novos: Dez (hero_dez_humanoid), Porca (hero_porca) e Iria falando atraves
    /// de um Censor existente (enemy_censor_patrulha.fbx), com os VO WAVs gerados por tts_bridge.py.
    /// </summary>
    public static class Cs02OfertaSceneBuilder
    {
        private const string ScenePath = "Assets/Game/Scenes/Cs02OfertaDemo.unity";
        private const string FabDir    = "Assets/Game/Prefabs/Fab3D";
        private const string VoDir     = "Assets/Game/Audio/VO/cs02_oferta";

        [MenuItem("Tools/CONCERTO/Build CS02 A Oferta Scene")]
        public static void BuildMenu() => BuildInternal();

        public static void BuildFromCommandLine()
        {
            BuildInternal();
            EditorApplication.Exit(0);
        }

        private static void BuildInternal()
        {
            AssetDatabase.Refresh();
            var sc = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var sunGo = new GameObject("Directional_OfertaLight");
            var sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(0.84f, 0.88f, 1.0f);
            sun.intensity = 1.25f;
            sunGo.transform.rotation = Quaternion.Euler(34f, -40f, 0f);

            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 50f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.05f, 0.07f, 0.14f, 1f);
            camGo.AddComponent<AudioListener>();
            camGo.transform.position = new Vector3(0f, 3.2f, -6.5f);
            camGo.transform.LookAt(new Vector3(0f, 1.6f, 4.0f));

            var esGo = new GameObject("EventSystem");
            esGo.AddComponent<EventSystem>();
            esGo.AddComponent<StandaloneInputModule>();

            // Chao existente (Fab3D)
            PlaceAsset($"{FabDir}/caldeiral_env_floor_tile.prefab", "CS02_StageFloor", null, Vector3.zero, new Vector3(22f, 1f, 22f));

            // Atores existentes: Dez, Porca e Censor (vaso de voz da Maestrina Iria)
            var dezGo = PlaceAsset($"{FabDir}/hero_dez_humanoid.prefab", "Player_Dez_Placeholder", null, new Vector3(-1.2f, 0.1f, -1.5f), Vector3.one);
            var cc = dezGo.GetComponent<CharacterController>();
            if (cc == null) cc = dezGo.AddComponent<CharacterController>();
            cc.height = 1.25f; cc.radius = 0.28f; cc.center = new Vector3(0f, 0.625f, 0f);
            var loco = dezGo.GetComponent<DezLocomotionController>();
            if (loco == null) loco = dezGo.AddComponent<DezLocomotionController>();

            PlaceAsset($"{FabDir}/hero_porca.prefab", "Actor_Porca", null, new Vector3(-2.6f, 0.1f, -2.0f), Vector3.one);

            var censorGo = PlaceAsset("Assets/Game/Art/Generated/combat_gdd/enemy_censor_patrulha.fbx", "Actor_IriaViaCensor", null, new Vector3(0.8f, 1.8f, 4.0f), new Vector3(2.4f, 2.4f, 2.4f));
            var censorMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Game/Materials/CombatGdd/Mat_Enemy_Censor.mat");
            if (censorMat != null)
                foreach (var r in censorGo.GetComponentsInChildren<Renderer>(true))
                    r.sharedMaterial = censorMat;

            var ctrlGo = new GameObject("CS02_A_Oferta_Controller");
            var ctrl = ctrlGo.AddComponent<CutsceneAOfetaController>();
            ctrl.censorVesselActor = censorGo;
            ctrl.playerLoco = loco;
            ctrl.lines = new[]
            {
                MakeLine("cs02_iria_01",  "Iria (Censor)", "ameacador-suave",  0.0f,  6.5f,  "Devolva minhas reliquias... e devolvo sua irma."),
                MakeLine("cs02_dez_01",   "Dez",           "urgente",          6.5f,  10.5f, "Onde voce ta guardando ela?"),
                MakeLine("cs02_iria_02",  "Iria (Censor)", "calmo-persuasivo", 10.5f, 17.0f, "No ultimo segundo intacto da Corona. Traga as reliquias e eu abro a caixa."),
                MakeLine("cs02_porca_01", "Porca",         "apreensivo",       17.0f, 21.5f, "A voz dela nao tremeu nem meio compasso, Dez."),
                MakeLine("cs02_iria_03",  "Iria (Censor)", "firme",            21.5f, 27.0f, "Pense bem no que vale mais, Desiderio."),
                MakeLine("cs02_dez_02",   "Dez",           "contido",          27.0f, 32.0f, "Vamos continuar andando, Porca. Mas eu ouvi cada palavra.")
            };

            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(Path.Combine(Application.dataPath, "..", ScenePath))));
            EditorSceneManager.SaveScene(sc, ScenePath);
            AddSceneToBuildSettings(ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Cs02OfertaSceneBuilder] Cena {ScenePath} construida com ZERO modelos 3D novos + VO XTTS-v2 vinculado!");
        }

        private static Cs02OfertaLineDef MakeLine(string id, string speaker, string emotion, float s, float e, string sub)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>($"{VoDir}/{id}.wav");
            return new Cs02OfertaLineDef
            {
                lineId = id,
                speaker = speaker,
                emotion = emotion,
                startSec = s,
                endSec = e,
                subtitlePtBr = sub,
                voClip = clip
            };
        }

        private static GameObject PlaceAsset(string assetPath, string objName, Transform parent, Vector3 pos, Vector3 scale)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            GameObject go = prefab != null ? (GameObject)PrefabUtility.InstantiatePrefab(prefab) : new GameObject(objName);
            go.name = objName;
            if (parent != null) go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.transform.localScale = scale;
            return go;
        }

        private static void AddSceneToBuildSettings(string scenePath)
        {
            var list = EditorBuildSettings.scenes.ToList();
            if (!list.Any(s => s.path == scenePath))
            {
                list.Add(new EditorBuildSettingsScene(scenePath, true));
                EditorBuildSettings.scenes = list.ToArray();
            }
        }
    }
}
#endif
