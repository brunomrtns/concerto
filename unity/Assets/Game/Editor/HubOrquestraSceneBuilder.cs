#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Studio.Game.Hub;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Studio.Game.EditorTools
{
    /// <summary>
    /// GATE 1 & GATE 3: Constroi a cena do Hub "A Orquestra" (Assets/Game/Scenes/AOrquestra.unity)
    /// e o asset BalanceLineDatabase.asset usando exclusivamente modelos TripoSG existentes em Prefabs/Fab3D/.
    /// </summary>
    public static class HubOrquestraSceneBuilder
    {
        private const string ScenePath = "Assets/Game/Scenes/AOrquestra.unity";
        private const string DbAssetPath = "Assets/Game/Data/BalanceLineDatabase.asset";

        [MenuItem("Tools/CONCERTO/Build Hub A Orquestra Scene")]
        public static void BuildHubSceneMenu()
        {
            BuildHubSceneInternal();
        }

        public static void BuildHubSceneFromCommandLine()
        {
            BuildHubSceneInternal();
            EditorApplication.Exit(0);
        }

        private static void BuildHubSceneInternal()
        {
            // 1. Garante BalanceLineDatabase.asset (GATE 3)
            Directory.CreateDirectory(Path.GetFullPath(Path.Combine(Application.dataPath, "Game", "Data")));
            var db = AssetDatabase.LoadAssetAtPath<BalanceLineDatabase>(DbAssetPath);
            if (db == null)
            {
                db = ScriptableObject.CreateInstance<BalanceLineDatabase>();
                db.fusos = BalanceLineDatabase.BuildCanonicalGddEntries();
                AssetDatabase.CreateAsset(db, DbAssetPath);
            }
            else
            {
                db.fusos = BalanceLineDatabase.BuildCanonicalGddEntries();
                EditorUtility.SetDirty(db);
            }
            AssetDatabase.SaveAssets();

            // 2. Cria nova cena limpa "AOrquestra" (antiga sala de concertos)
            var sc = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Iluminacao quente mecanico-acolhedora
            var dirLightGo = new GameObject("Directional_WarmHallLight");
            var dirLight = dirLightGo.AddComponent<Light>();
            dirLight.type = LightType.Directional;
            dirLight.color = new Color(1.0f, 0.90f, 0.74f);
            dirLight.intensity = 1.25f;
            dirLightGo.transform.rotation = Quaternion.Euler(42f, -35f, 0f);

            var fillGo = new GameObject("Point_StageWarmGlow");
            var fill = fillGo.AddComponent<Light>();
            fill.type = LightType.Point;
            fill.color = new Color(1.0f, 0.76f, 0.42f);
            fill.intensity = 3.2f;
            fill.range = 24f;
            fillGo.transform.position = new Vector3(0f, 5.5f, 2f);

            // Camera principal
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 55f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.08f, 0.09f, 0.14f, 1f);
            camGo.AddComponent<AudioListener>();
            camGo.transform.position = new Vector3(0f, 5.2f, -13.5f);
            camGo.transform.LookAt(new Vector3(0f, 1.5f, 1.0f));

            // EventSystem para UI
            var esGo = new GameObject("EventSystem");
            esGo.AddComponent<EventSystem>();
            esGo.AddComponent<StandaloneInputModule>();

            // Arquitetura da antiga sala de concertos (TripoSG Fab3D prefabs existentes)
            var hallRoot = new GameObject("Hub_ConcertHall_Architecture");
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Hall_Floor_Base";
            floor.transform.SetParent(hallRoot.transform, false);
            floor.transform.position = new Vector3(0f, -0.25f, 0f);
            floor.transform.localScale = new Vector3(24f, 0.5f, 24f);
            var floorMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Game/Materials/Fab3D/Mat_Fab3D_caldeiral_env_floor_tile.mat");
            if (floorMat != null) floor.GetComponent<MeshRenderer>().sharedMaterial = floorMat;

            // Pilares TripoSG em arco de sala de concertos
            Vector3[] pillarPositions =
            {
                new Vector3(-9f, 0f, -6f), new Vector3(9f, 0f, -6f),
                new Vector3(-9f, 0f,  2f), new Vector3(9f, 0f,  2f),
                new Vector3(-6f, 0f,  8f), new Vector3(6f, 0f,  8f),
            };
            for (int i = 0; i < pillarPositions.Length; i++)
                SpawnPrefab("Assets/Game/Prefabs/Fab3D/caldeiral_env_portal_pillar.prefab", $"Hall_Pillar_{i + 1}", hallRoot.transform, pillarPositions[i], Vector3.one * 1.4f);

            // Palco central — Orgao/Sirene da antiga sala de concertos (TripoSG)
            SpawnPrefab("Assets/Game/Prefabs/Fab3D/c5_prop_sirene_mestra.prefab", "Hall_Stage_Organ", hallRoot.transform, new Vector3(0f, 0f, 7.5f), Vector3.one * 1.3f);

            // Posto de Reparo (TripoSG bancada)
            SpawnPrefab("Assets/Game/Prefabs/Fab3D/c5_prop_bancada_relogio.prefab", "Hub_RepairStation", hallRoot.transform, new Vector3(-5.2f, 0f, 0.5f), Vector3.one * 1.2f);

            // Console da Tela de Fusos (TripoSG No de Re-Sincronia no centro da sala)
            SpawnPrefab("Assets/Game/Prefabs/Fab3D/c5_prop_no_resincronia.prefab", "Hub_TelaDeFusos_Console", hallRoot.transform, new Vector3(0f, 0f, 0.0f), Vector3.one * 1.25f);

            // Dez no hub (visual TripoSG)
            SpawnPrefab("Assets/Game/Prefabs/Fab3D/hero_dez_humanoid.prefab", "Hub_Dez_Stand", hallRoot.transform, new Vector3(-1.8f, 0f, -3.5f), Vector3.one);

            // 5 Slots de chegada de NPCs TripoSG (desativados inicialmente; chegam ao restaurar cada fuso!)
            var npcSpecs = new (string fusoId, string npcName, string prefabPath, Vector3 pos)[]
            {
                ("caldeiral",  "Tio Horário",     "Assets/Game/Prefabs/Fab3D/npc_tio_horario_humanoid.prefab",    new Vector3(-4.5f, 0f, -1.8f)),
                ("vitralha",   "Vó Segunda",      "Assets/Game/Prefabs/Fab3D/npc_vo_segunda_humanoid.prefab",     new Vector3(-2.4f, 0f,  3.2f)),
                ("maresia",    "Mecânico Brito",  "Assets/Game/Prefabs/Fab3D/npc_mecanico_brito_humanoid.prefab", new Vector3( 2.4f, 0f,  3.2f)),
                ("ecodia",     "Analista Luma",   "Assets/Game/Prefabs/Fab3D/npc_analista_luma_humanoid.prefab",  new Vector3( 4.8f, 0f, -0.5f)),
                ("retrogrado", "Engenheira Vera", "Assets/Game/Prefabs/Fab3D/npc_engenheira_vera_humanoid.prefab",new Vector3( 3.6f, 0f, -3.4f)),
            };

            var hubRoot = new GameObject("Hub_AOrquestra_System");
            var hubCtrl = hubRoot.AddComponent<HubOrquestraController>();
            hubCtrl.balanceDatabase = db;
            var telaUi = hubRoot.AddComponent<TelaDeFusosUI>();
            telaUi.hubController = hubCtrl;
            hubCtrl.telaDeFusos = telaUi;

            var npcsContainer = new GameObject("Hub_NPC_ArrivalSlots");
            foreach (var spec in npcSpecs)
            {
                var npcGo = SpawnPrefab(spec.prefabPath, $"NPC_{spec.fusoId}", npcsContainer.transform, spec.pos, Vector3.one);
                if (npcGo != null)
                {
                    npcGo.transform.LookAt(new Vector3(0f, spec.pos.y, 0f));
                    npcGo.SetActive(false);
                }
                hubCtrl.npcArrivalSlots.Add(new HubNpcArrivalSlot
                {
                    fusoId = spec.fusoId,
                    npcName = spec.npcName,
                    npcRoot = npcGo
                });
            }

            EditorSceneManager.SaveScene(sc, ScenePath);

            // Adiciona AOrquestra.unity aos EditorBuildSettings (se ainda nao estiver)
            var buildScenes = EditorBuildSettings.scenes.ToList();
            if (!buildScenes.Any(s => s.path == ScenePath))
            {
                buildScenes.Add(new EditorBuildSettingsScene(ScenePath, true));
                EditorBuildSettings.scenes = buildScenes.ToArray();
            }

            Debug.Log($"[HubBuilder] Cena '{ScenePath}' e '{DbAssetPath}' construidos com sucesso (5 slots NPC TripoSG).");
        }

        private static GameObject SpawnPrefab(string path, string name, Transform parent, Vector3 pos, Vector3 scale)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            GameObject go;
            if (prefab != null)
                go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            else
                go = GameObject.CreatePrimitive(PrimitiveType.Cube);

            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.transform.localScale = scale;
            return go;
        }
    }
}
#endif
