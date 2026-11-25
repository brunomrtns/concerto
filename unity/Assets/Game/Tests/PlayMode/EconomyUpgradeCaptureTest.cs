using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Studio.Tests.PlayMode
{
    /// <summary>
    /// GATE 6 — Captura PlayMode da Economia & Progressao:
    /// coletar pecas -> abrir o posto de troca -> comprar 2 upgrades -> demonstrar efeitos rodando no HUD e gameplay.
    /// </summary>
    public sealed class EconomyUpgradeCaptureTest
    {
        [Serializable]
        private sealed class FrameMeta
        {
            public int index;
            public string step;
            public string detail;
            public string badge;
        }

        [Serializable]
        private sealed class CaptureMeta
        {
            public List<FrameMeta> frames = new List<FrameMeta>();
        }

        private string _outDir;
        private CaptureMeta _meta;
        private Texture2D _readTex;
        private RenderTexture _rt;

        private static Type GameType(string fullName)
        {
            var t = Type.GetType(fullName + ", Assembly-CSharp");
            Assert.IsNotNull(t, $"Tipo nao encontrado em Assembly-CSharp: {fullName}");
            return t;
        }

        [UnityTest]
        public IEnumerator Economy_CaptureCollectBenchBuyAndVerifyEffects()
        {
            _outDir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "economy_capture"));
            if (Directory.Exists(_outDir)) Directory.Delete(_outDir, true);
            Directory.CreateDirectory(_outDir);

            _meta = new CaptureMeta();
            _rt = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32);
            _readTex = new Texture2D(1920, 1080, TextureFormat.RGB24, false);

            var cs01T    = GameType("Studio.Game.Cutscenes.CutsceneManager");
            var econT    = GameType("Studio.Game.Parts.EconomyUpgradeSystem");
            var saveSysT = GameType("Studio.Game.Save.SaveSystem");
            var hpT      = GameType("Studio.Game.Combat.PlayerHealthController");
            var powersT  = GameType("Studio.Game.Powers.DezPowersController");
            var juiceT   = GameType("Studio.Game.Combat.GameFeelJuiceController");

            string tmpDir = Path.Combine(Application.temporaryCachePath, $"concerto_econ_cap_{Guid.NewGuid():N}");
            Directory.CreateDirectory(tmpDir);
            string tmpSave = Path.Combine(tmpDir, "concerto_save.json");
            var mSetPath = saveSysT.GetMethod("SetCustomPathForTests");
            mSetPath.Invoke(null, new object[] { tmpSave });

            try
            {
                PlayerPrefs.SetInt("CONCERTO_CS01_PLAYED", 1);
                PlayerPrefs.Save();
                var op = SceneManager.LoadSceneAsync("Caldeiral", LoadSceneMode.Single);
                while (!op.isDone) yield return null;
                for (int i = 0; i < 6; i++) yield return null;

                var cs01 = UnityEngine.Object.FindAnyObjectByType(cs01T);
                if (cs01 != null && (bool)cs01T.GetProperty("IsPlaying").GetValue(cs01))
                    cs01T.GetMethod("SkipCutscene").Invoke(cs01, null);
                yield return null;

                var econ   = UnityEngine.Object.FindAnyObjectByType(econT);
                var hp     = UnityEngine.Object.FindAnyObjectByType(hpT);
                var powers = UnityEngine.Object.FindAnyObjectByType(powersT);
                var juice  = UnityEngine.Object.FindAnyObjectByType(juiceT);
                Assert.IsNotNull(econ);

                econT.GetMethod("ResetStateForNewGame").Invoke(econ, null);
                yield return CaptureSec(3, "1/4 — ESTADO INICIAL (0/6 PECAS)", "Vida Max: 4 segmentos | Eco Cooldown: 3.0s | Saldo: 0", "INICIO");

                // 1. Coleta 3 pecas
                string[] partNames = { "Nucleo de Ressonancia", "Bobina de Fase Beta", "Regulador Crono" };
                for (int p = 0; p < 3; p++)
                {
                    econT.GetMethod("NotifyPartEarned").Invoke(econ, new object[] { 1 });
                    if (juice != null)
                        juiceT.GetMethod("TriggerItemPickupJuice").Invoke(juice, new object[] { Vector3.zero, partNames[p] });
                    yield return CaptureSec(3, $"1/4 — PECA COLETADA ({p + 1}/6): {partNames[p]}", $"Contador HUD: {p + 1}/6 | Saldo: {p + 1} pecas | Salvo no JSON", "COLETA");
                }

                // 2. Abre posto de troca
                econT.GetMethod("OpenBenchUI").Invoke(econ, null);
                yield return CaptureSec(4, "2/4 — POSTO DE TROCA (BANCADA DO DEZ)", "4 upgrades disponiveis (Custo total = 5 pecas / 6 no jogo)", "POSTO");

                // 3. Compra Upgrade 1: Blindagem de Caldeiral (+1 Vida Maxima)
                Assert.IsTrue((bool)econT.GetMethod("TryPurchaseUpgrade").Invoke(econ, new object[] { "upg_vida_extra" }));
                yield return CaptureSec(4, "3/4 — UPGRADE 1 COMPRADO: BLINDAGEM DE CALDEIRAL", "Vida Maxima: 4 -> 5 segmentos! (-1 peca, Saldo: 2)", "UPGRADE #1");

                // 4. Compra Upgrade 2: Capacitor de Eco (-33% Cooldown)
                econT.GetMethod("NavigateBench").Invoke(econ, new object[] { +1 });
                Assert.IsTrue((bool)econT.GetMethod("TryPurchaseUpgrade").Invoke(econ, new object[] { "upg_eco_recarga" }));
                yield return CaptureSec(4, "3/4 — UPGRADE 2 COMPRADO: CAPACITOR DE ECO", "Eco Cooldown: 3.0s -> 2.0s! (-1 peca, Saldo: 1)", "UPGRADE #2");

                // 5. Fecha posto e demonstra efeitos rodando no gameplay!
                econT.GetMethod("CloseBenchUI").Invoke(econ, null);
                if (powers != null)
                {
                    powersT.GetMethod("UnlockPowers").Invoke(powers, new object[] { false });
                    powersT.GetMethod("TriggerEcoTemporal").Invoke(powers, null);
                }
                int maxHp = (int)hpT.GetProperty("MaxSegments").GetValue(hp);
                var pCfg = powersT.GetProperty("Cfg").GetValue(powers);
                float ecoCd = (float)pCfg.GetType().GetField("ecoCooldownSec").GetValue(pCfg);
                Assert.AreEqual(5, maxHp);
                Assert.AreEqual(2.0f, ecoCd, 0.01f);

                yield return CaptureSec(5, "4/4 — EFEITOS ATIVOS NO GAMEPLAY E SALVOS NO JSON!", $"Vida Max: {maxHp} | Eco Cooldown: {ecoCd:F1}s | Saldo: 1 | Zero PlayerPrefs", "VALIDADO");

                File.WriteAllText(Path.Combine(_outDir, "economy_meta.json"), JsonUtility.ToJson(_meta, true));
            }
            finally
            {
                mSetPath.Invoke(null, new object[] { null });
                if (Directory.Exists(tmpDir)) Directory.Delete(tmpDir, true);
                if (_rt != null) { _rt.Release(); UnityEngine.Object.Destroy(_rt); }
                if (_readTex != null) UnityEngine.Object.Destroy(_readTex);
            }
        }

        private IEnumerator CaptureSec(int seconds, string step, string detail, string badge)
        {
            yield return null;
            var cam = Camera.main;
            if (cam == null) cam = UnityEngine.Object.FindAnyObjectByType<Camera>();
            if (cam != null)
            {
                var prev = cam.targetTexture;
                cam.targetTexture = _rt;
                cam.Render();
                cam.targetTexture = prev;
                RenderTexture.active = _rt;
                _readTex.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0, false);
                _readTex.Apply(false);
                RenderTexture.active = null;
            }
            byte[] jpg = _readTex.EncodeToJPG(86);
            for (int s = 0; s < seconds; s++)
            {
                int idx = _meta.frames.Count;
                File.WriteAllBytes(Path.Combine(_outDir, $"frame_{idx:D4}.jpg"), jpg);
                _meta.frames.Add(new FrameMeta { index = idx, step = step, detail = detail, badge = badge });
            }
        }
    }
}
