using System;
using System.Collections.Generic;
using System.Text;

namespace AutoCrafterLimits
{
    /// <summary>
    /// JSON deserialization for config load. (Save uses JsonUtility workaround in AutoCrafterConfigStore.)
    /// </summary>
    internal static class JsonHelper
    {
        public static PersistedStore Deserialize(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return new PersistedStore { AutoCrafters = new PersistedCrafterConfig[0] };
            var store = new PersistedStore { AutoCrafters = new PersistedCrafterConfig[0] };

            int start = json.IndexOf("\"AutoCrafters\"", StringComparison.OrdinalIgnoreCase);
            if (start < 0) return store;

            int arrStart = json.IndexOf('[', start);
            if (arrStart < 0) return store;

            var crafters = new List<PersistedCrafterConfig>();
            int i = arrStart + 1;
            while (i < json.Length)
            {
                int objStart = FindNextObjectStart(json, i);
                if (objStart < 0) break;
                int objEnd = FindMatchingBrace(json, objStart);
                if (objEnd < 0) break;
                var crafter = ParseCrafter(json.Substring(objStart, objEnd - objStart + 1));
                if (crafter != null) crafters.Add(crafter);
                i = objEnd + 1;
            }

            store.AutoCrafters = crafters.ToArray();
            return store;
        }

        private static int FindNextObjectStart(string json, int start)
        {
            for (int i = start; i < json.Length; i++)
            {
                if (json[i] == '{') return i;
                if (json[i] == ']') return -1;
            }
            return -1;
        }

        private static int FindMatchingBrace(string json, int openIdx)
        {
            int depth = 0;
            for (int i = openIdx; i < json.Length; i++)
            {
                char c = json[i];
                if (c == '{') depth++;
                else if (c == '}')
                {
                    depth--;
                    if (depth == 0) return i;
                }
            }
            return -1;
        }

        private static PersistedCrafterConfig ParseCrafter(string objJson)
        {
            var c = new PersistedCrafterConfig();
            c.Id = GetInt(objJson, "Id");
            c.LastOutputGroupId = GetString(objJson, "LastOutputGroupId");
            c.EnableOutputLimit = GetBool(objJson, "EnableOutputLimit");
            c.OutputLimitCountsPlanetWide = GetBool(objJson, "OutputLimitCountsPlanetWide");
            c.TargetOutputAmount = GetInt(objJson, "TargetOutputAmount");
            c.EnableInputThreshold = GetBool(objJson, "EnableInputThreshold");
            c.InputThresholdCountsPlanetWide = GetBool(objJson, "InputThresholdCountsPlanetWide");
            c.InputThresholds = ParseThresholdsArray(objJson, "InputThresholds");
            return c;
        }

        private static PersistedThreshold[] ParseThresholdsArray(string objJson, string key)
        {
            int keyStart = objJson.IndexOf("\"" + key + "\"", StringComparison.OrdinalIgnoreCase);
            if (keyStart < 0) return new PersistedThreshold[0];
            int arrStart = objJson.IndexOf('[', keyStart);
            if (arrStart < 0) return new PersistedThreshold[0];

            var list = new List<PersistedThreshold>();
            int i = arrStart + 1;
            while (i < objJson.Length)
            {
                if (objJson[i] == ']') break;
                int objStart = objJson.IndexOf('{', i);
                if (objStart < 0) break;
                int objEnd = FindMatchingBrace(objJson, objStart);
                if (objEnd < 0) break;
                var t = ParseThreshold(objJson.Substring(objStart, objEnd - objStart + 1));
                if (t != null && !string.IsNullOrEmpty(t.ItemId)) list.Add(t);
                i = objEnd + 1;
            }
            return list.ToArray();
        }

        private static PersistedThreshold ParseThreshold(string objJson)
        {
            var t = new PersistedThreshold();
            t.ItemId = GetString(objJson, "ItemId");
            t.Amount = GetInt(objJson, "Amount");
            return t;
        }

        private static int GetInt(string json, string key)
        {
            string val = GetValueAfterKey(json, key);
            if (string.IsNullOrEmpty(val)) return 0;
            return int.TryParse(val, out int n) ? n : 0;
        }

        private static bool GetBool(string json, string key)
        {
            string val = GetValueAfterKey(json, key);
            return val == "true";
        }

        private static string GetString(string json, string key)
        {
            string val = GetValueAfterKey(json, key);
            if (string.IsNullOrEmpty(val) || val.Length < 2) return "";
            if (val.StartsWith("\"") && val.EndsWith("\""))
            {
                return Unescape(val.Substring(1, val.Length - 2));
            }
            return val;
        }

        private static string GetValueAfterKey(string json, string key)
        {
            string search = "\"" + key + "\"";
            int idx = json.IndexOf(search, StringComparison.OrdinalIgnoreCase);
            if (idx < 0) return "";
            idx += search.Length;
            while (idx < json.Length && (json[idx] == ' ' || json[idx] == ':' || json[idx] == '\t')) idx++;
            if (idx >= json.Length) return "";
            if (json[idx] == '"')
            {
                int end = idx + 1;
                while (end < json.Length)
                {
                    if (json[end] == '\\') end += 2;
                    else if (json[end] == '"') { end++; break; }
                    else end++;
                }
                return end <= json.Length ? json.Substring(idx, end - idx) : "";
            }
            int start = idx;
            while (idx < json.Length && json[idx] != ',' && json[idx] != '}' && json[idx] != ']') idx++;
            return json.Substring(start, idx - start).Trim();
        }

        private static string Unescape(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            var sb = new StringBuilder(s.Length);
            for (int i = 0; i < s.Length; i++)
            {
                if (s[i] == '\\' && i + 1 < s.Length)
                {
                    switch (s[i + 1])
                    {
                        case '"': sb.Append('"'); i++; break;
                        case '\\': sb.Append('\\'); i++; break;
                        case 'n': sb.Append('\n'); i++; break;
                        case 'r': sb.Append('\r'); i++; break;
                        case 't': sb.Append('\t'); i++; break;
                        default: sb.Append(s[i]); break;
                    }
                }
                else sb.Append(s[i]);
            }
            return sb.ToString();
        }
    }
}
