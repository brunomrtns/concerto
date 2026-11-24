using System;
using System.Collections.Generic;
using UnityEngine;

namespace Studio.Game.Cutscenes
{
    [Serializable]
    public class CutsceneSubtitleLine
    {
        public string id;
        public float startSec;
        public float endSec;
        public string speaker;
        public string style; // "dialogue" | "radio" | "memory"
        public string text;
    }

    [Serializable]
    public class CutsceneSubtitleConfig
    {
        public string cutsceneId;
        public string locale;
        public float totalDurationSec = 72f;
        public string titleCardText = "CONCERTO";
        public float titleCardStartSec = 62.5f;
        public float titleCardEndSec = 69.5f;
        public float skipHintDelaySec = 3.0f;
        public string skipHintText = "Segure [Espaço] ou [Esc] para pular";
        public string postObjectiveText = "Fale com Juno";
        public List<CutsceneSubtitleLine> lines = new List<CutsceneSubtitleLine>();

        public static CutsceneSubtitleConfig FromJson(TextAsset jsonAsset)
        {
            if (jsonAsset == null || string.IsNullOrEmpty(jsonAsset.text))
                return new CutsceneSubtitleConfig();
            return JsonUtility.FromJson<CutsceneSubtitleConfig>(jsonAsset.text) ?? new CutsceneSubtitleConfig();
        }

        public CutsceneSubtitleLine GetActiveLine(float timeSec)
        {
            if (lines == null) return null;
            for (int i = 0; i < lines.Count; i++)
            {
                var l = lines[i];
                if (timeSec >= l.startSec && timeSec <= l.endSec)
                    return l;
            }
            return null;
        }
    }
}
