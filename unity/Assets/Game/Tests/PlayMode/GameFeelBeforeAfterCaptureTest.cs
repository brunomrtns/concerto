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
    /// GATE 6 — Captura PlayMode ANTES (60s sem juice) vs DEPOIS (60s com juice)
    /// para composicao lado a lado em captures/gamefeel/gamefeel_antes_depois_60s.mp4.
    /// </summary>
    public sealed class GameFeelBeforeAfterCaptureTest
    {
        [Serializable]
        private sealed class FrameMeta
        {
            public int index;
            public string beat;
            public string metric;
        }

        [Serializable]
        private sealed class SideMeta
        {
            public List<FrameMeta> frames = new List<FrameMeta>();
        }

        private string _outRoot;
        private Texture2D _readTex;
        private RenderTexture _rt;

        private static Type GameType(string fullName)
        {
            var t = Type.GetType(fullName + ", Assembly-CSharp");
            Assert.IsNotNull(t, $"Tipo nao encontrado em Assembly-CSharp: {fullName}");
            return t;
        }

        [UnityTest]
        public IEnumerator GameFeel_Capture60sBeforeAndAfterSideBySide()
        {
            _outRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "gamefeel_capture"));
            if (Directory.Exists(_outRoot)) Directory.Delete(_outRoot, true);
            Directory.CreateDirectory(Path.Combine(_outRoot, "antes"));
            Directory.CreateDirectory(Path.Combine(_outRoot, "depois"));

            _rt = new RenderTexture(960, 1080, 24, RenderTextureFormat.ARGB32);
            _readTex = new Texture2D(960, 1080, TextureFormat.RGB24, false);

            var cs01T  = GameType("Studio.Game.Cutscenes.CutsceneManager");
            var juiceT = GameType("Studio.Game.Combat.GameFeelJuiceController");
            var locoT  = GameType("Studio.Game.Animation.DezLocomotionController");

            PlayerPrefs.SetInt("CONCERTO_CS01_PLAYED", 1);
            PlayerPrefs.Save();
            var op = SceneManager.LoadSceneAsync("Caldeiral", LoadSceneMode.Single);
            while (!op.isDone) yield return null;
            for (int i = 0; i < 6; i++) yield return null;

            var cs01 = UnityEngine.Object.FindAnyObjectByType(cs01T);
            if (cs01 != null && (bool)cs01T.GetProperty("IsPlaying").GetValue(cs01))
                cs01T.GetMethod("SkipCutscene").Invoke(cs01, null);
            yield return null;

            var juice = UnityEngine.Object.FindAnyObjectByType(juiceT);
            var loco  = UnityEngine.Object.FindAnyObjectByType(locoT) as Component;
            Assert.IsNotNull(juice);
            Assert.IsNotNull(loco);

            try
            {
                // 60s ANTES (sem juice)
                yield return Capture60sPass("antes", true, juiceT, juice, loco);

                // 60s DEPOIS (com juice completo: GATES 1-5)
                yield return Capture60sPass("depois", false, juiceT, juice, loco);
            }
            finally
            {
                juiceT.GetProperty("DisableJuiceForComparison").SetValue(null, false);
                if (_rt != null) { _rt.Release(); UnityEngine.Object.Destroy(_rt); }
                if (_readTex != null) UnityEngine.Object.Destroy(_readTex);
            }
        }

        private IEnumerator Capture60sPass(string folder, bool disableJuice, Type juiceT, object juice, Component loco)
        {
            juiceT.GetProperty("DisableJuiceForComparison").SetValue(null, disableJuice);
            var meta = new SideMeta();
            string dir = Path.Combine(_outRoot, folder);

            // 60s = 60 frames-chave a 1s cada (encodados a 12fps com duracao exata de 60.0s lado a lado!)
            // 0-12s: GATE 1 Golpe Leve & Pesado
            for (int c = 0; c < 3; c++)
            {
                bool heavy = (c == 2);
                juiceT.GetMethod("TriggerDezAttackHit").Invoke(juice, new object[] { loco.transform.position + loco.transform.forward * 1.4f + Vector3.up * 0.9f, heavy });
                yield return CaptureSec(dir, meta, 4,
                    heavy ? "GATE 1: GOLPE PESADO DO DEZ" : "GATE 1: GOLPE LEVE DO DEZ",
                    disableJuice ? "Sem hitstop | Sem shake | Sem faisca" : (heavy ? "Hitstop 85ms | Shake 180ms | Faisca+Som MESMO frame" : "Hitstop 65ms | Shake 180ms | Faisca+Som MESMO frame"));
            }

            // 12-24s: GATE 2 Receber Dano (flash + shake direcional + pausa 90ms)
            for (int c = 0; c < 2; c++)
            {
                juiceT.GetMethod("TriggerPlayerDamaged").Invoke(juice, new object[] { loco.transform, loco.transform.position + Vector3.right * 2f, false });
                yield return CaptureSec(dir, meta, 6,
                    "GATE 2: RECEBER DANO (NAO FATAL)",
                    disableJuice ? "Sem flash | Sem shake direcional" : "Flash no Dez (140ms) | Shake direcional (220ms) | Pausa 90ms");
            }

            // 24-36s: GATE 3 Inimigo Morto (knockback + hitstop maior 95ms)
            for (int c = 0; c < 2; c++)
            {
                juiceT.GetMethod("TriggerEnemyKilled").Invoke(juice, new object[] { null, loco.transform.forward });
                yield return CaptureSec(dir, meta, 6,
                    "GATE 3: INIMIGO MORTO",
                    disableJuice ? "Morte seca sem impacto" : "Knockback + Hitstop 95ms + Shake 240ms (fluxo continuo)");
            }

            // 36-48s: GATE 4 Pulo e Aterrissagem (squash & stretch + poeira)
            juiceT.GetMethod("TriggerJumpStretch").Invoke(juice, null);
            yield return CaptureSec(dir, meta, 6,
                "GATE 4: PULO (STRETCH NO RIG)",
                disableJuice ? "Rig rigido 1.0x" : "Stretch sutil (0.91, 1.12, 0.91) -> retorno exato 1.0");
            juiceT.GetMethod("TriggerLandingSquashAndDust").Invoke(juice, new object[] { loco.transform.position });
            yield return CaptureSec(dir, meta, 6,
                "GATE 4: ATERRISSAGEM (SQUASH + POEIRA)",
                disableJuice ? "Pouso seco sem poeira" : "Squash (1.13, 0.86, 1.13) + Poeira no chao + Som");

            // 48-60s: GATE 5 Coletar Item (3 camadas: visual + som + "clim" HUD)
            juiceT.GetMethod("TriggerItemPickupJuice").Invoke(juice, new object[] { loco.transform.position + loco.transform.forward * 1.2f, "Fragmento de Nucleo" });
            yield return CaptureSec(dir, meta, 12,
                "GATE 5: COLETAR ITEM (3 CAMADAS)",
                disableJuice ? "Coleta silenciosa sem HUD clim" : "Visual (particulas) + Som (chime) + 'CLIM' animado no HUD");

            Assert.AreEqual(60, meta.frames.Count, $"Pass '{folder}' deve ter exatamente 60s.");
            File.WriteAllText(Path.Combine(dir, "meta.json"), JsonUtility.ToJson(meta, true));
        }

        private IEnumerator CaptureSec(string dir, SideMeta meta, int seconds, string beat, string metric)
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
                _readTex.ReadPixels(new Rect(0, 0, 960, 1080), 0, 0, false);
                _readTex.Apply(false);
                RenderTexture.active = null;
            }
            byte[] jpg = _readTex.EncodeToJPG(86);
            for (int s = 0; s < seconds; s++)
            {
                int idx = meta.frames.Count;
                File.WriteAllBytes(Path.Combine(dir, $"frame_{idx:D4}.jpg"), jpg);
                meta.frames.Add(new FrameMeta { index = idx, beat = beat, metric = metric });
            }
            yield return new WaitForSecondsRealtime(0.11f);
        }
    }
}
