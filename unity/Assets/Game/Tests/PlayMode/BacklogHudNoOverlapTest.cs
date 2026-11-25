using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Studio.Tests.PlayMode
{
    public sealed class BacklogHudNoOverlapTest
    {
        private static Rect GetScreenRect(RectTransform rt)
        {
            var corners = new Vector3[4];
            rt.GetWorldCorners(corners);
            float minX = Mathf.Min(corners[0].x, corners[1].x, corners[2].x, corners[3].x);
            float maxX = Mathf.Max(corners[0].x, corners[1].x, corners[2].x, corners[3].x);
            float minY = Mathf.Min(corners[0].y, corners[1].y, corners[2].y, corners[3].y);
            float maxY = Mathf.Max(corners[0].y, corners[1].y, corners[2].y, corners[3].y);
            return Rect.MinMaxRect(minX, minY, maxX, maxY);
        }

        [UnityTest]
        public IEnumerator BL002_TopLeftHpAndObjectiveHudHaveZeroOverlap()
        {
            PlayerPrefs.SetInt("CONCERTO_CS01_PLAYED", 1);
            PlayerPrefs.Save();

            yield return SceneManager.LoadSceneAsync("Caldeiral", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var cs01T = Type.GetType("Studio.Game.Cutscenes.CutsceneManager, Assembly-CSharp");
            var cs01 = cs01T != null ? UnityEngine.Object.FindAnyObjectByType(cs01T) : null;
            if (cs01 != null && (bool)cs01T.GetProperty("IsPlaying").GetValue(cs01))
                cs01T.GetMethod("SkipCutscene").Invoke(cs01, null);
            yield return null;
            Canvas.ForceUpdateCanvases();

            var hpGo = GameObject.Find("HP_SegmentsRoot");
            Assert.IsNotNull(hpGo, "HP_SegmentsRoot obrigatorio.");
            var hpRt = hpGo.GetComponent<RectTransform>();
            Assert.LessOrEqual(hpRt.anchoredPosition.y, -100f, "BL-002: HP_SegmentsRoot deve ficar abaixo do banner de Objetivo (y <= -100).");

            var objBanner = GameObject.Find("ObjectivePanel") ?? GameObject.Find("ObjectiveHUD");
            if (objBanner != null)
            {
                var objRt = objBanner.GetComponent<RectTransform>();
                if (objRt != null)
                {
                    Rect rHp = GetScreenRect(hpRt);
                    Rect rObj = GetScreenRect(objRt);
                    Assert.IsFalse(rHp.Overlaps(rObj), $"BL-002: HP ({rHp}) nao pode sobrepor Objetivo ({rObj}).");
                }
            }

            string repoRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
            string outPath = Path.Combine(repoRoot, "captures", "backlog", "bl002_hud_top_left_depois.png");
            Directory.CreateDirectory(Path.GetDirectoryName(outPath));
            ScreenCapture.CaptureScreenshot(outPath);
            yield return null;
            yield return null;
            Debug.Log($"[BL-002] PASSOU: HP_SegmentsRoot.anchoredPosition={hpRt.anchoredPosition}, screenshot={outPath}");
        }
    }
}
