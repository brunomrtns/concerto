using System;
using System.Collections.Generic;
using UnityEngine;

namespace Studio.Game.Dialogue
{
    [Serializable]
    public class LocalizedEntry
    {
        public string key;
        public string value;
    }

    [Serializable]
    public class LocalizationFile
    {
        public string locale = "pt-BR";
        public List<LocalizedEntry> entries = new List<LocalizedEntry>();
    }

    /// <summary>
    /// Tabela data-driven chave -> texto (pronta para traducao futura sem hardcode em codigo).
    /// </summary>
    public sealed class LocalizationTable
    {
        private readonly Dictionary<string, string> _map = new Dictionary<string, string>(StringComparer.Ordinal);
        public string Locale { get; private set; } = "pt-BR";

        public void Load(TextAsset jsonAsset)
        {
            _map.Clear();
            if (jsonAsset == null || string.IsNullOrEmpty(jsonAsset.text)) return;
            var file = JsonUtility.FromJson<LocalizationFile>(jsonAsset.text);
            if (file == null) return;
            Locale = string.IsNullOrEmpty(file.locale) ? "pt-BR" : file.locale;
            if (file.entries == null) return;
            for (int i = 0; i < file.entries.Count; i++)
            {
                var e = file.entries[i];
                if (!string.IsNullOrEmpty(e.key))
                    _map[e.key] = e.value ?? string.Empty;
            }
        }

        public string Get(string key)
        {
            if (string.IsNullOrEmpty(key)) return string.Empty;
            return _map.TryGetValue(key, out string val) ? val : key;
        }

        public string Format(string key, params object[] args)
        {
            string raw = Get(key);
            if (args == null || args.Length == 0) return raw;
            try { return string.Format(raw, args); }
            catch { return raw; }
        }
    }
}
