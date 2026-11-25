using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Studio.Tests.PlayMode
{
    /// <summary>
    /// GATE 4 — Captura turntable em cena real (Caldeiral) dos 5 novos modelos TripoSG girando 360 graus.
    /// </summary>
    public sealed class MissingAssetsBatch1ShowcaseTest
    {
        private static readonly string[] PrefabPaths =
        {
            "Assets/Game/Prefabs/Fab3D/part_condensador_fluxo.prefab",
            "Assets/Game/Prefabs/Fab3D/part_estabilizador_orbital.prefab",
            "Assets/Game/Prefabs/Fab3D/prop_fragmento_nucleo.prefab",
            "Assets/Game/Prefabs/Fab3D/prop_celula_energia.prefab",
            "Assets/Game/Prefabs/Fab3D/prop_estacao_parry.prefab",
        };

        [UnityTest]
        public IEnumerator Capture5NewTripoSgModelsInCaldeiralShowcase()
        {
            PlayerPrefs.SetInt("CONCERTO_CS01_PLAYED", 1);
            PlayerPrefs.Save();

            yield return SceneManager.LoadSceneAsync("Caldeiral", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var cs01T = System.Type.GetType("Studio.Game.Cutscenes.CutsceneManager, Assembly-CSharp");
            var cs01 = cs01T != null ? Object.FindAnyObjectByType(cs01T) : null;
            if (cs01 != null && (bool)cs01T.GetProperty("IsPlaying").GetValue(cs01))
                cs01T.GetMethod("SkipCutscene").Invoke(cs01, null);
            yield return null;

            // Posiciona os 5 modelos em linha limpa bem na frente do spawn do Dez (piso 100% livre!)
            var dezGo = GameObject.Find("Player_Dez_Placeholder");
            Vector3 basePos = dezGo != null ? dezGo.transform.position : new Vector3(-28.5f, 0.08f, -18.5f);
            Vector3 fwd = dezGo != null ? dezGo.transform.forward : Vector3.forward;
            fwd.y = 0f;
            if (fwd.sqrMagnitude < 0.01f) fwd = Vector3.forward;
            fwd.Normalize();
            Vector3 right = Vector3.Cross(Vector3.up, fwd).normalized;

            Vector3 center = new Vector3(basePos.x, basePos.y + 0.05f, basePos.z) + fwd * 2.6f;
            var spawned = new GameObject[PrefabPaths.Length];
            for (int i = 0; i < PrefabPaths.Length; i++)
            {
                var pref = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPaths[i]);
                Assert.IsNotNull(pref, $"Prefab TripoSG ausente: {PrefabPaths[i]}");
                float offsetSide = (i - 2) * 0.88f;
                var go = Object.Instantiate(pref, center + right * offsetSide, Quaternion.identity);
                var rends = go.GetComponentsInChildren<Renderer>(true);
                Assert.Greater(rends.Length, 0, $"Prefab {PrefabPaths[i]} sem Renderer.");
                for (int r = 0; r < rends.Length; r++)
                {
                    Assert.IsNotNull(rends[r].sharedMaterial, $"Material nulo em {PrefabPaths[i]}");
                    StringAssert.DoesNotContain("Error", rends[r].sharedMaterial.shader.name);
                }
                spawned[i] = go;
            }

            var camGo = new GameObject("AssetsShowcaseCam");
            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 38f;
            camGo.transform.position = center - fwd * 2.5f + Vector3.up * 1.10f;
            camGo.transform.LookAt(center + Vector3.up * 0.42f);
            if (dezGo != null) dezGo.SetActive(false);

            string outDir = Path.Combine(Application.dataPath, "..", "Logs", "missing_assets_showcase");
            if (Directory.Exists(outDir)) Directory.Delete(outDir, true);
            Directory.CreateDirectory(outDir);

            var rt = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32);
            var tex = new Texture2D(1920, 1080, TextureFormat.RGB24, false);

            const int totalFrames = 150; // 10.0s @ 15fps (rotacao completa 360 graus)
            for (int f = 0; f < totalFrames; f++)
            {
                float yaw = (f / (float)totalFrames) * 360f;
                for (int i = 0; i < spawned.Length; i++)
                {
                    if (spawned[i] != null)
                        spawned[i].transform.rotation = Quaternion.Euler(0f, yaw, 0f);
                }
                yield return null;

                var pt = cam.targetTexture;
                var pa = RenderTexture.active;
                cam.targetTexture = rt;
                cam.Render();
                RenderTexture.active = rt;
                tex.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0, false);
                tex.Apply(false, false);
                cam.targetTexture = pt;
                RenderTexture.active = pa;
                File.WriteAllBytes(Path.Combine(outDir, $"frame_{f:D4}.jpg"), tex.EncodeToJPG(90));
            }

            for (int i = 0; i < spawned.Length; i++)
                if (spawned[i] != null) Object.Destroy(spawned[i]);
            Object.Destroy(tex);
            rt.Release();
            Object.Destroy(rt);
            Object.Destroy(camGo);
            Debug.Log("[MissingAssetsShowcase] 5 modelos TripoSG validados e capturados (150 frames 1080p)!");
        }
    }
}
