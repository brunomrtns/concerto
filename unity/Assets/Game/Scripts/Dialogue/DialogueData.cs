using System;
using System.Collections.Generic;
using UnityEngine;

namespace Studio.Game.Dialogue
{
    [Serializable]
    public class DialogueEntry
    {
        public string speakerKey;
        public string textKey;
    }

    [Serializable]
    public class DialogueConversationData
    {
        public string dialogueId;
        public string promptKey = "prompt_talk";
        public string postObjectiveKey;
        public int partsTotal = 3;
        public List<DialogueEntry> lines = new List<DialogueEntry>();

        public static DialogueConversationData FromJson(TextAsset jsonAsset)
        {
            if (jsonAsset == null || string.IsNullOrEmpty(jsonAsset.text))
                return null;
            return JsonUtility.FromJson<DialogueConversationData>(jsonAsset.text);
        }
    }
}
