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
    /// GATE 6 — Corrida completa pelo Epicentro V2 (S1 Inicio -> Ilha Alivio S1B -> Estase ->
    /// Arena A -> Ponte Alivio S2B + Desvio Secreto Recompensado -> Arena B -> Portico Dourado S3 -> Arena Boss).
    /// </summary>
    public sealed class EpicenterV2FullRunCaptureTest
    {
        [Serializable]
        private sealed class FrameMeta
        {
            public int index;
            public string zone;
            public string pacing;
            public string badge;
        }

        [Serializable]
        private sealed class CaptureMeta
        {
            public List<FrameMeta> frames = new List<FrameMeta>();
            public int totalCheckpoints;
            public float maxGapMeters;
            public float maxRetraceSec;
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
        public IEnumerator EpicentroV2_CaptureFullLevelRun()
        {
            _outDir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "epicentro_v2_capture"));
            if (Directory.Exists(_outDir)) Directory.Delete(_outDir, true);
            Directory.CreateDirectory(_outDir);

            _meta = new CaptureMeta();
            _rt = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32);
            _readTex = new Texture2D(1920, 1080, TextureFormat.RGB24, false);

            var dirT  = GameType("Studio.Game.Epicenter.EpicenterV2LevelDesignDirector");
            var econT = GameType("Studio.Game.Parts.EconomyUpgradeSystem");

            try
            {
                PlayerPrefs.SetInt("CONCERTO_CS01_PLAYED", 1);
                PlayerPrefs.Save();
                var op = SceneManager.LoadSceneAsync("Epicentro", LoadSceneMode.Single);
                while (!op.isDone) yield return null;
                for (int i = 0; i < 6; i++) yield return null;

                var dir = UnityEngine.Object.FindAnyObjectByType(dirT);
                Assert.IsNotNull(dir);
                _meta.totalCheckpoints = (int)dirT.GetProperty("TotalCheckpoints").GetValue(dir);
                _meta.maxGapMeters     = (float)dirT.GetProperty("MaxCheckpointGapMeters").GetValue(dir);
                _meta.maxRetraceSec    = (float)dirT.GetProperty("MaxExpectedRetraceSeconds").GetValue(dir);

                var camGo = new GameObject("EpicenterV2_RunCam");
                var cam = camGo.AddComponent<Camera>();
                cam.fieldOfView = 56f;
                cam.farClipPlane = 400f;

                var dez = GameObject.Find("Player_Dez_Placeholder");
                var cc = dez != null ? dez.GetComponent<CharacterController>() : null;

                // Corrida completa passando por todos os marcos do Epicentro V2
                (Vector3 pos, string zone, string pacing, string badge, int secs)[] stops =
                {
                    (new Vector3(0f, 0.3f, 0f),     "S1 — INICIO & VISTA DA FENDA (CP 1/5)",        "Ritmo: Entrada calma | Leitura por balizas ciano (sem setas)", "CP 1/5", 4),
                    (new Vector3(19.5f, 0.7f, 0f),  "S1 — TRAVESSIA LINEAR (DIFICULDADE FACIL)",    "Desafio 1: Dash + Plataforma movel linear", "DESAFIO 1", 4),
                    (new Vector3(33.5f, 0.9f, 0f),  "S1B — ILHA DE ALIVIO PRE-ESTASE (CP 2/5)",     "NOVO V2: Alivio + Checkpoint antes do puzzle (<=30s retrabalho)", "CP 2/5", 4),
                    (new Vector3(38.0f, 1.1f, 0f),  "S1 — PUZZLE DE ESTASE (CALIBRADO 9.2 m/s)",    "Desafio 2: Plataforma rapida desacelerada por Estase (4.6 m/s)", "ESTASE", 4),
                    (new Vector3(50.0f, 1.1f, 0f),  "S2 — ENTRADA DA ARENA A (CP 3/5)",             "Alivio + Checkpoint antes do primeiro combate", "CP 3/5", 4),
                    (new Vector3(58.0f, 1.1f, 0f),  "S2 — ARENA DE COMBATE A (DIFICULDADE MEDIA)",  "Desafio 3: Encontro intermediario crescente", "COMBATE A", 4),
                    (new Vector3(71.0f, 1.1f, 0f),  "S2B — PONTE DE ALIVIO (CP 4/5)",               "NOVO V2: Checkpoint intermediario + pista dourada para desvio", "CP 4/5", 4),
                    (new Vector3(71.0f, 1.1f, 6.2f),"S2B — DESVIO LATERAL RECOMPENSADO!",           "GATE 4: Saida lateral recompensada com Reliquia Secreta (+1 peca)", "SECRETO", 5),
                    (new Vector3(84.0f, 1.1f, 0f),  "S2 — ARENA DE COMBATE B (ESCALADA)",           "Desafio 4: Escalada tatica antes do pico da fase", "COMBATE B", 4),
                    (new Vector3(102.0f, 1.1f, 0f), "S3 — PORTICO DOURADO DA ARENA FINAL (CP 5/5)", "NOVO V2: Leitura monumental + ultimo checkpoint pre-boss", "CP 5/5", 4),
                    (new Vector3(114.0f, 1.1f, 0f), "S3 — CORACAO DA FENDA (PICO: BOSS)",           "GATE 5: Pico de dificuldade — Guardiao da Fenda", "BOSS", 5)
                };

                for (int s = 0; s < stops.Length; s++)
                {
                    var st = stops[s];
                    if (dez != null)
                    {
                        if (cc != null) cc.enabled = false;
                        dez.transform.position = st.pos;
                        dez.transform.rotation = Quaternion.Euler(0f, st.badge == "SECRETO" ? 0f : 90f, 0f);
                        if (cc != null) cc.enabled = true;
                    }
                    if (st.badge == "SECRETO")
                        dirT.GetMethod("CollectSecretRewardForTest").Invoke(dir, null);

                    camGo.transform.position = st.pos + (st.badge == "SECRETO" ? new Vector3(-4.5f, 3.2f, -4.5f) : new Vector3(-6.2f, 3.6f, -4.8f));
                    camGo.transform.LookAt(st.pos + new Vector3(st.badge == "SECRETO" ? 0f : 4.0f, 0.9f, st.badge == "SECRETO" ? 0.8f : 0f));

                    yield return CaptureSec(cam, st.secs, st.zone, st.pacing, st.badge);
                }

                UnityEngine.Object.Destroy(camGo);
                var econ = UnityEngine.Object.FindAnyObjectByType(econT);
                if (econ != null) econT.GetMethod("ResetStateForNewGame").Invoke(econ, null);

                File.WriteAllText(Path.Combine(_outDir, "epicentro_v2_meta.json"), JsonUtility.ToJson(_meta, true));
            }
            finally
            {
                if (_rt != null) { _rt.Release(); UnityEngine.Object.Destroy(_rt); }
                if (_readTex != null) UnityEngine.Object.Destroy(_readTex);
            }
        }

        private IEnumerator CaptureSec(Camera cam, int seconds, string zone, string pacing, string badge)
        {
            yield return null;
            var prev = cam.targetTexture;
            cam.targetTexture = _rt;
            cam.Render();
            cam.targetTexture = prev;
            RenderTexture.active = _rt;
            _readTex.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0, false);
            _readTex.Apply(false);
            RenderTexture.active = null;

            byte[] jpg = _readTex.EncodeToJPG(86);
            for (int i = 0; i < seconds; i++)
            {
                int idx = _meta.frames.Count;
                File.WriteAllBytes(Path.Combine(_outDir, $"frame_{idx:D4}.jpg"), jpg);
                _meta.frames.Add(new FrameMeta { index = idx, zone = zone, pacing = pacing, badge = badge });
            }
        }
    }
}
