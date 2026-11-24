using System;
using System.Collections.Generic;
using UnityEngine;

namespace Studio.Game.Parts
{
    [Serializable]
    public class PartPlacementData
    {
        public string id;
        public string displayName;
        public string prefabSlug;
        public string locationHint;
        public Vector3 position;
    }

    [Serializable]
    public class PartsLoopConfigData
    {
        public int requiredCount = 5;
        public List<PartPlacementData> parts = new List<PartPlacementData>();

        public static PartsLoopConfigData FromJson(TextAsset jsonAsset)
        {
            if (jsonAsset == null || string.IsNullOrEmpty(jsonAsset.text))
                return new PartsLoopConfigData();
            return JsonUtility.FromJson<PartsLoopConfigData>(jsonAsset.text) ?? new PartsLoopConfigData();
        }
    }
}
