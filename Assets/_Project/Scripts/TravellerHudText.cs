using System;
using System.Collections.Generic;
using System.Text;

namespace ThreeBodyProblem
{
    public static class TravellerHudText
    {
        private static readonly string[] Targets = { "岩下阴影", "周文王", "姬昌", "旅人", "朝歌", "纣王", "沙漏", "天窗", "复苏池", "两颗飞星", "三个太阳", "绿色箭头", "传奏官", "表章" };
        public static string Objective(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            var text = new StringBuilder(value.Length + 64);
            for (int i = 0; i < value.Length;)
            {
                string match = null;
                foreach (var target in Targets)
                    if (i + target.Length <= value.Length && string.CompareOrdinal(value, i, target, 0, target.Length) == 0) { match = target; break; }
                if (match != null) { text.Append("<color=#F2D35E>").Append(match).Append("</color>"); i += match.Length; }
                else { char c = value[i++]; text.Append(c == '<' ? "＜" : c == '>' ? "＞" : c.ToString()); }
            }
            return text.ToString();
        }

        // Budget is in approximate full-width glyphs, keeping Chinese and Latin
        // subtitles readable without splitting surrogate pairs or Latin words.
        public static List<string> SubtitlePages(string value, int fullWidthBudget)
        {
            var pages = new List<string>(); if (string.IsNullOrEmpty(value)) return pages;
            int start = 0, width = 0, breakAt = -1; int limit = Math.Max(16, fullWidthBudget) * 2;
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i]; width += c <= 127 ? 1 : 2;
                if (char.IsWhiteSpace(c) || "，。！？；,.!?;".IndexOf(c) >= 0) breakAt = i + 1;
                if (width < limit || char.IsHighSurrogate(c)) continue;
                int end = breakAt > start + Math.Max(8, fullWidthBudget / 3) ? breakAt : i + 1;
                if (end < value.Length && char.IsLowSurrogate(value[end])) end++;
                pages.Add(value.Substring(start, end - start).Trim()); start = end;
                i = end - 1; width = 0; breakAt = -1;
            }
            if (start < value.Length) pages.Add(value.Substring(start).Trim());
            return pages;
        }
    }
}
