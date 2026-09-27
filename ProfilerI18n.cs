using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace KSPPerformanceProfiler
{
    public class LanguagePack
    {
        public string Code { get; set; } = "";
        public string Name { get; set; } = "";
        public string Author { get; set; } = "";
        public string Description { get; set; } = "";
        public string FilePath { get; set; } = "";
        public Dictionary<string, string> Translations { get; set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public override string ToString() => $"{Name} ({Code})";
    }

    /// <summary>
    /// Lightweight, zero-dependency, crash-proof JSON parser specifically designed for
    /// deserializing language packs in Unity/Mono without relying on external DLLs.
    /// </summary>
    public static class SimpleJsonParser
    {
        public static object Parse(string json)
        {
            if (string.IsNullOrEmpty(json)) return null;
            int index = 0;
            return ParseValue(json, ref index);
        }

        private static void SkipWhitespace(string s, ref int index)
        {
            while (index < s.Length && char.IsWhiteSpace(s[index]))
            {
                index++;
            }
        }

        private static object ParseValue(string s, ref int index)
        {
            SkipWhitespace(s, ref index);
            if (index >= s.Length) return null;

            char c = s[index];
            if (c == '{') return ParseObject(s, ref index);
            if (c == '[') return ParseArray(s, ref index);
            if (c == '"') return ParseString(s, ref index);
            if (c == 't' || c == 'f') return ParseBool(s, ref index);
            if (c == 'n') return ParseNull(s, ref index);
            if (char.IsDigit(c) || c == '-') return ParseNumber(s, ref index);

            index++;
            return null;
        }

        private static Dictionary<string, object> ParseObject(string s, ref int index)
        {
            var dict = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            index++; // skip '{'

            while (index < s.Length)
            {
                SkipWhitespace(s, ref index);
                if (index >= s.Length) break;
                if (s[index] == '}')
                {
                    index++;
                    break;
                }

                if (s[index] == ',')
                {
                    index++;
                    continue;
                }

                string key = ParseString(s, ref index);
                if (key == null) break;

                SkipWhitespace(s, ref index);
                if (index < s.Length && s[index] == ':')
                {
                    index++;
                }

                object val = ParseValue(s, ref index);
                dict[key] = val;

                SkipWhitespace(s, ref index);
                if (index < s.Length && s[index] == ',')
                {
                    index++;
                }
                else if (index < s.Length && s[index] == '}')
                {
                    index++;
                    break;
                }
            }
            return dict;
        }

        private static List<object> ParseArray(string s, ref int index)
        {
            var list = new List<object>();
            index++; // skip '['

            while (index < s.Length)
            {
                SkipWhitespace(s, ref index);
                if (index >= s.Length) break;
                if (s[index] == ']')
                {
                    index++;
                    break;
                }
                if (s[index] == ',')
                {
                    index++;
                    continue;
                }

                object val = ParseValue(s, ref index);
                list.Add(val);

                SkipWhitespace(s, ref index);
                if (index < s.Length && s[index] == ',')
                {
                    index++;
                }
                else if (index < s.Length && s[index] == ']')
                {
                    index++;
                    break;
                }
            }
            return list;
        }

        private static string ParseString(string s, ref int index)
        {
            SkipWhitespace(s, ref index);
            if (index >= s.Length || s[index] != '"') return null;
            index++; // skip opening '"'

            var sb = new StringBuilder();
            while (index < s.Length)
            {
                char c = s[index++];
                if (c == '"')
                {
                    return sb.ToString();
                }
                if (c == '\\' && index < s.Length)
                {
                    char esc = s[index++];
                    switch (esc)
                    {
                        case '"': sb.Append('"'); break;
                        case '\\': sb.Append('\\'); break;
                        case '/': sb.Append('/'); break;
                        case 'b': sb.Append('\b'); break;
                        case 'f': sb.Append('\f'); break;
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        case 'u':
                            if (index + 4 <= s.Length)
                            {
                                string hex = s.Substring(index, 4);
                                index += 4;
                                if (int.TryParse(hex, System.Globalization.NumberStyles.HexNumber, null, out int code))
                                {
                                    sb.Append((char)code);
                                }
                            }
                            break;
                        default:
                            sb.Append(esc);
                            break;
                    }
                }
                else
                {
                    sb.Append(c);
                }
            }
            return sb.ToString();
        }

        private static bool? ParseBool(string s, ref int index)
        {
            if (s.Substring(index).StartsWith("true", StringComparison.OrdinalIgnoreCase))
            {
                index += 4;
                return true;
            }
            if (s.Substring(index).StartsWith("false", StringComparison.OrdinalIgnoreCase))
            {
                index += 5;
                return false;
            }
            return null;
        }

        private static object ParseNull(string s, ref int index)
        {
            if (s.Substring(index).StartsWith("null", StringComparison.OrdinalIgnoreCase))
            {
                index += 4;
            }
            return null;
        }

        private static object ParseNumber(string s, ref int index)
        {
            int start = index;
            if (s[index] == '-') index++;
            while (index < s.Length && (char.IsDigit(s[index]) || s[index] == '.' || s[index] == 'e' || s[index] == 'E' || s[index] == '+' || s[index] == '-'))
            {
                index++;
            }
            string numStr = s.Substring(start, index - start);
            if (double.TryParse(numStr, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double dVal))
            {
                return dVal;
            }
            return 0;
        }
    }

    public static class ProfilerI18n
    {
        public static readonly List<LanguagePack> AvailablePacks = new List<LanguagePack>();
        public static int CurrentPackIndex = -1; // -1 = Auto, >= 0 = manual pack index

        private static LanguagePack baseEnglishPack = null;
        private static bool isInitialized = false;

        public static void ReloadLanguagePacks()
        {
            AvailablePacks.Clear();
            baseEnglishPack = null;

            List<string> searchDirs = new List<string>();
            try
            {
                string rootDir = Path.Combine(KSPUtil.ApplicationRootPath, "GameData", "KSPPerformanceProfiler", "Localization");
                if (Directory.Exists(rootDir)) searchDirs.Add(rootDir);
            }
            catch { }

            try
            {
                string asmDir = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "..", "Localization");
                if (Directory.Exists(asmDir) && !searchDirs.Contains(asmDir)) searchDirs.Add(asmDir);
            }
            catch { }

            try
            {
                string localDir = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "Localization");
                if (Directory.Exists(localDir) && !searchDirs.Contains(localDir)) searchDirs.Add(localDir);
            }
            catch { }

            HashSet<string> loadedCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var dir in searchDirs)
            {
                try
                {
                    string[] files = Directory.GetFiles(dir, "*.json");
                    foreach (var file in files)
                    {
                        try
                        {
                            string json = File.ReadAllText(file, Encoding.UTF8);
                            var pack = ParseLanguagePack(file, json);
                            if (pack != null && !loadedCodes.Contains(pack.Code))
                            {
                                AvailablePacks.Add(pack);
                                loadedCodes.Add(pack.Code);

                                if (pack.Code.Equals("en-us", StringComparison.OrdinalIgnoreCase) ||
                                    (baseEnglishPack == null && pack.Code.StartsWith("en", StringComparison.OrdinalIgnoreCase)))
                                {
                                    baseEnglishPack = pack;
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            UnityEngine.Debug.LogError($"[KSPPerformanceProfiler] Failed to load language pack from {file}: {ex.Message}");
                        }
                    }
                }
                catch { }
            }

            // Sort order: zh-cn first, en-us second, then alphabetical
            AvailablePacks.Sort((a, b) =>
            {
                if (a.Code.Equals("zh-cn", StringComparison.OrdinalIgnoreCase)) return -1;
                if (b.Code.Equals("zh-cn", StringComparison.OrdinalIgnoreCase)) return 1;
                if (a.Code.Equals("en-us", StringComparison.OrdinalIgnoreCase)) return -1;
                if (b.Code.Equals("en-us", StringComparison.OrdinalIgnoreCase)) return 1;
                return string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
            });

            isInitialized = true;
            UnityEngine.Debug.Log($"[KSPPerformanceProfiler] Loaded {AvailablePacks.Count} external language packs from disk.");
        }

        private static LanguagePack ParseLanguagePack(string filePath, string json)
        {
            object obj = SimpleJsonParser.Parse(json);
            if (!(obj is Dictionary<string, object> dict)) return null;

            string code = "";
            string name = "";
            string author = "";
            string desc = "";

            if (dict.TryGetValue("LanguageCode", out object cObj) && cObj is string cStr) code = cStr.Trim();
            if (dict.TryGetValue("LanguageName", out object nObj) && nObj is string nStr) name = nStr.Trim();
            if (dict.TryGetValue("Author", out object aObj) && aObj is string aStr) author = aStr.Trim();
            if (dict.TryGetValue("Description", out object dObj) && dObj is string dStr) desc = dStr.Trim();

            if (string.IsNullOrEmpty(code))
            {
                code = Path.GetFileNameWithoutExtension(filePath).ToLowerInvariant();
            }
            if (string.IsNullOrEmpty(name))
            {
                name = code;
            }

            var pack = new LanguagePack
            {
                Code = code,
                Name = name,
                Author = author,
                Description = desc,
                FilePath = filePath
            };

            Dictionary<string, object> transMap = null;
            if (dict.TryGetValue("Translations", out object tObj) && tObj is Dictionary<string, object> subDict)
            {
                transMap = subDict;
            }
            else
            {
                transMap = dict;
            }

            foreach (var kvp in transMap)
            {
                if (kvp.Value is string sVal)
                {
                    pack.Translations[kvp.Key] = sVal;
                }
            }

            return pack;
        }

        public static LanguagePack GetActiveLanguagePack()
        {
            if (!isInitialized || AvailablePacks.Count == 0)
            {
                ReloadLanguagePacks();
            }

            if (CurrentPackIndex >= 0 && CurrentPackIndex < AvailablePacks.Count)
            {
                return AvailablePacks[CurrentPackIndex];
            }

            // Auto-detect based on KSP language / OS culture
            string detected = GetDetectedLanguageCode();
            for (int i = 0; i < AvailablePacks.Count; i++)
            {
                if (AvailablePacks[i].Code.Equals(detected, StringComparison.OrdinalIgnoreCase))
                    return AvailablePacks[i];
            }
            for (int i = 0; i < AvailablePacks.Count; i++)
            {
                if (AvailablePacks[i].Code.StartsWith(detected, StringComparison.OrdinalIgnoreCase) ||
                    detected.StartsWith(AvailablePacks[i].Code, StringComparison.OrdinalIgnoreCase))
                    return AvailablePacks[i];
            }

            return AvailablePacks.Count > 0 ? AvailablePacks[0] : null;
        }

        private static string GetDetectedLanguageCode()
        {
            try
            {
                if (KSP.Localization.Localizer.CurrentLanguage != null)
                {
                    string lang = KSP.Localization.Localizer.CurrentLanguage.ToLowerInvariant();
                    if (lang.Contains("zh") || lang.Contains("cn") || lang.Contains("chinese")) return "zh-cn";
                    if (lang.Contains("ru") || lang.Contains("russian")) return "ru";
                    if (lang.Contains("es") || lang.Contains("spanish")) return "es";
                    if (lang.Contains("de") || lang.Contains("german")) return "de";
                    if (lang.Contains("fr") || lang.Contains("french")) return "fr";
                    if (lang.Contains("ja") || lang.Contains("japanese")) return "ja";
                    if (lang.Contains("en") || lang.Contains("english")) return "en-us";
                }
            }
            catch { }

            try
            {
                string sysLang = System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.ToLowerInvariant();
                if (sysLang == "zh") return "zh-cn";
                if (sysLang == "ru") return "ru";
                if (sysLang == "es") return "es";
                if (sysLang == "de") return "de";
                if (sysLang == "fr") return "fr";
                if (sysLang == "ja") return "ja";
                if (sysLang == "en") return "en-us";
            }
            catch { }

            return "en-us";
        }

        public static void SetLanguageByCode(string code)
        {
            if (!isInitialized || AvailablePacks.Count == 0)
            {
                ReloadLanguagePacks();
            }

            for (int i = 0; i < AvailablePacks.Count; i++)
            {
                if (AvailablePacks[i].Code.Equals(code, StringComparison.OrdinalIgnoreCase) ||
                    AvailablePacks[i].Code.StartsWith(code, StringComparison.OrdinalIgnoreCase))
                {
                    CurrentPackIndex = i;
                    return;
                }
            }
        }

        public static bool IsChinese
        {
            get
            {
                var pack = GetActiveLanguagePack();
                return pack != null && pack.Code.StartsWith("zh", StringComparison.OrdinalIgnoreCase);
            }
        }

        public static string Get(string key)
        {
            var pack = GetActiveLanguagePack();
            if (pack != null && pack.Translations.TryGetValue(key, out string val))
            {
                return val;
            }

            // Fallback to base English JSON pack if translation missing in selected pack
            if (baseEnglishPack != null && baseEnglishPack.Translations.TryGetValue(key, out string enVal))
            {
                return enVal;
            }

            return key;
        }

        public static string Format(string key, params object[] args)
        {
            string template = Get(key);
            try
            {
                return string.Format(template, args);
            }
            catch
            {
                return template;
            }
        }

        public static void ToggleNextLanguage()
        {
            if (!isInitialized || AvailablePacks.Count == 0)
            {
                ReloadLanguagePacks();
            }

            if (AvailablePacks.Count == 0) return;

            if (CurrentPackIndex == -1)
            {
                CurrentPackIndex = 0;
            }
            else if (CurrentPackIndex >= AvailablePacks.Count - 1)
            {
                CurrentPackIndex = -1; // Return to Auto
            }
            else
            {
                CurrentPackIndex++;
            }
        }

        public static string GetCurrentLanguageButtonText()
        {
            if (!isInitialized || AvailablePacks.Count == 0)
            {
                ReloadLanguagePacks();
            }

            if (CurrentPackIndex == -1)
            {
                var pack = GetActiveLanguagePack();
                string activeName = pack != null ? pack.Name : "Auto";
                return $"🌐 {Get("lang_auto")} ({activeName})";
            }

            if (CurrentPackIndex >= 0 && CurrentPackIndex < AvailablePacks.Count)
            {
                return $"🌐 {AvailablePacks[CurrentPackIndex].Name}";
            }

            return $"🌐 {Get("lang_label")}";
        }
    }
}
