using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace VisualNovel
{
    /// <summary>
    /// 메모장으로 작성하는 txt 대본을 VNScript 로 변환한다.
    ///
    ///   # 주석
    ///   @변수 player = 모아
    ///   @캐릭터 유나 = images/yuna.png, left, #FFB3D9
    ///
    ///   [배경 images/bg_classroom.png]
    ///   방과 후, 노을이 비치는 교실.          ← 나레이션 (이름표 없음)
    ///   유나: 어, {player}! 아직 안 갔어?      ← 등록한 캐릭터 (이미지/색 자동)
    ///   하루(right): 나도 있는데…             ← 위치 지정
    ///   유나(center, images/yuna2.png): 응?   ← 이미지 직접 지정
    ///   ???: 누구지?                          ← 등록 안 된 이름은 이름표만 표시
    ///   [지우기]                              ← 인물 모두 지우기 ([지우기 left] 는 한 명만)
    ///   [배경 없음]
    /// </summary>
    public static class VNTextParser
    {
        const int MaxSpeakerLength = 20;

        /// <param name="presets">설정 화면에서 지정한 캐릭터 (대본의 @캐릭터 보다 우선)</param>
        /// <param name="presetPositions">캐릭터 id → 기본 위치</param>
        /// <param name="aliases">대사에서 쓰는 별칭 → 캐릭터 id (예: "A" → "유나")</param>
        public static VNScript Parse(string text, List<string> warnings = null,
            IList<VNCharacterDef> presets = null,
            IDictionary<string, string> presetPositions = null,
            IDictionary<string, string> aliases = null)
        {
            var script = new VNScript();
            var lastPos = new Dictionary<string, string>();
            var defaultPos = new Dictionary<string, string>();
            if (presetPositions != null) foreach (var kv in presetPositions) defaultPos[kv.Key] = kv.Value;
            VNLine pending = null; // 대사 앞의 [배경] [지우기] 명령을 모아 두는 곳

            VNLine Pending() => pending ?? (pending = new VNLine { position = "" });

            var rows = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            for (int n = 0; n < rows.Length; n++)
            {
                string raw = rows[n].Trim();
                if (raw.Length == 0 || raw.StartsWith("#") || raw.StartsWith("//")) continue;

                try
                {
                    // ── @설정 ──
                    if (raw.StartsWith("@"))
                    {
                        ParseDefinition(raw.Substring(1).Trim(), script, defaultPos, warnings, n + 1);
                        continue;
                    }

                    // ── [명령] ──
                    if (raw.StartsWith("[") && raw.EndsWith("]"))
                    {
                        var inner = raw.Substring(1, raw.Length - 2).Trim();
                        SplitFirst(inner, out var cmd, out var arg);
                        switch (cmd.ToLowerInvariant())
                        {
                            case "배경": case "bg": case "background":
                                Pending().background = IsNone(arg) ? "none" : arg;
                                break;
                            case "지우기": case "clear":
                                if (string.IsNullOrEmpty(arg)) Pending().clearCharacters = true;
                                else
                                {
                                    var p = Pending();
                                    var slot = NormalizePos(arg) ?? "center";
                                    p.clearSlots = string.IsNullOrEmpty(p.clearSlots) ? slot : p.clearSlots + "," + slot;
                                }
                                break;
                            default:
                                Warn(warnings, n + 1, $"알 수 없는 명령 [{cmd}]");
                                break;
                        }
                        continue;
                    }

                    // ── 대사 / 나레이션 ──
                    var line = TakePending(ref pending);
                    if (raw.StartsWith(">"))
                    {
                        line.nickname = "none";
                        line.text = Unescape(raw.Substring(1).Trim());
                    }
                    else if (TrySplitSpeaker(raw, out var speaker, out var options, out var body))
                    {
                        line.text = Unescape(body);
                        if (aliases != null && aliases.TryGetValue(speaker, out var aliased) && !string.IsNullOrEmpty(aliased))
                            speaker = aliased;
                        bool known = script.characters.Exists(c => c.id == speaker) ||
                                     (presets != null && FindPreset(presets, speaker) != null);
                        if (known) line.character = speaker;
                        else line.nickname = speaker;

                        string pos = null;
                        foreach (var opt in options)
                        {
                            var np = NormalizePos(opt);
                            if (np != null) pos = np;
                            else if (LooksLikePath(opt)) line.image = opt;
                            else if (opt.StartsWith("이름=") || opt.StartsWith("name=")) line.nickname = opt.Substring(opt.IndexOf('=') + 1).Trim();
                            else Warn(warnings, n + 1, $"알 수 없는 옵션 '{opt}'");
                        }
                        if (pos == null && !lastPos.TryGetValue(speaker, out pos) && !defaultPos.TryGetValue(speaker, out pos))
                            pos = "center";
                        line.position = pos;
                        lastPos[speaker] = pos;
                    }
                    else
                    {
                        line.nickname = "none";
                        line.text = Unescape(raw);
                    }
                    if (string.IsNullOrEmpty(line.position)) line.position = "center";
                    script.lines.Add(line);
                }
                catch (Exception e)
                {
                    Warn(warnings, n + 1, e.Message);
                }
            }

            // 마지막에 대사 없이 남은 [배경] 등은 빈 대사로 표시
            if (pending != null)
            {
                pending.nickname = "none";
                pending.text = "";
                if (string.IsNullOrEmpty(pending.position)) pending.position = "center";
                script.lines.Add(pending);
            }
            // 설정 화면 캐릭터가 대본의 같은 이름 @캐릭터 보다 우선
            if (presets != null)
                foreach (var p in presets)
                {
                    if (p == null || string.IsNullOrEmpty(p.id)) continue;
                    script.characters.RemoveAll(c => c.id == p.id);
                    script.characters.Add(p);
                }
            return script;
        }

        static VNCharacterDef FindPreset(IList<VNCharacterDef> presets, string id)
        {
            foreach (var p in presets) if (p != null && p.id == id) return p;
            return null;
        }

        static VNLine TakePending(ref VNLine pending)
        {
            var line = pending ?? new VNLine { position = "" };
            pending = null;
            return line;
        }

        static void ParseDefinition(string body, VNScript script, Dictionary<string, string> defaultPos, List<string> warnings, int lineNo)
        {
            SplitFirst(body, out var kind, out var rest);
            int eq = rest.IndexOf('=');
            if (eq < 0) { Warn(warnings, lineNo, "'=' 가 필요합니다. 예) @캐릭터 유나 = images/yuna.png"); return; }
            string key = rest.Substring(0, eq).Trim();
            string value = rest.Substring(eq + 1).Trim();

            switch (kind.ToLowerInvariant())
            {
                case "변수": case "var":
                    script.variables.RemoveAll(v => v.key == key);
                    script.variables.Add(new VNVariable { key = key, value = value });
                    break;
                case "캐릭터": case "인물": case "char": case "character":
                    var def = new VNCharacterDef { id = key, nickname = key, nameColor = "#FFFFFF" };
                    foreach (var tokRaw in value.Split(','))
                    {
                        var tok = tokRaw.Trim();
                        if (tok.Length == 0) continue;
                        var np = NormalizePos(tok);
                        if (tok.StartsWith("#")) def.nameColor = tok;
                        else if (tok == "초상화" || tok.Equals("portrait", StringComparison.OrdinalIgnoreCase)) def.portrait = true;
                        else if (tok == "전신" || tok.Equals("full", StringComparison.OrdinalIgnoreCase)) def.portrait = false;
                        else if (TryParseRatio(tok, out var aspect)) { def.portrait = true; def.portraitAspect = aspect; }
                        else if (TryParsePercent(tok, "크기=", "size=", out var sc)) def.scale = sc;
                        else if (TryParsePercent(tok, "높이=", "y=", out var oy)) def.offsetY = oy;
                        else if (np != null) defaultPos[key] = np;
                        else if (tok.StartsWith("이름=") || tok.StartsWith("name=")) def.nickname = tok.Substring(tok.IndexOf('=') + 1).Trim();
                        else def.image = tok;
                    }
                    script.characters.RemoveAll(c => c.id == key);
                    script.characters.Add(def);
                    break;
                default:
                    Warn(warnings, lineNo, $"알 수 없는 설정 @{kind}");
                    break;
            }
        }

        /// <summary>"이름(옵션, 옵션): 대사" 형태인지 판별</summary>
        static bool TrySplitSpeaker(string raw, out string speaker, out List<string> options, out string body)
        {
            speaker = body = null;
            options = new List<string>();
            int colon = IndexOfColon(raw);
            if (colon <= 0) return false;

            string head = raw.Substring(0, colon).Trim();
            body = raw.Substring(colon + 1).Trim();

            int paren = head.IndexOf('(');
            if (paren >= 0 && head.EndsWith(")"))
            {
                foreach (var o in head.Substring(paren + 1, head.Length - paren - 2).Split(','))
                    if (o.Trim().Length > 0) options.Add(o.Trim());
                head = head.Substring(0, paren).Trim();
            }
            if (head.Length == 0 || head.Length > MaxSpeakerLength) return false;
            speaker = head;
            return true;
        }

        static int IndexOfColon(string s)
        {
            // 괄호 안(이미지 경로의 C:\ 등)의 콜론은 무시
            int depth = 0;
            for (int i = 0; i < s.Length && i <= MaxSpeakerLength + 200; i++)
            {
                char c = s[i];
                if (c == '(') depth++;
                else if (c == ')') depth--;
                else if ((c == ':' || c == '：') && depth == 0) return i;
            }
            return -1;
        }

        static void SplitFirst(string s, out string first, out string rest)
        {
            s = s.Trim();
            int sp = s.IndexOfAny(new[] { ' ', '\t', ':' });
            if (sp < 0) { first = s; rest = ""; return; }
            first = s.Substring(0, sp).Trim();
            rest = s.Substring(sp + 1).Trim();
        }

        static string NormalizePos(string s)
        {
            switch (s.Trim().ToLowerInvariant())
            {
                case "left": case "l": case "왼쪽": case "좌": return "left";
                case "right": case "r": case "오른쪽": case "우": return "right";
                case "center": case "c": case "가운데": case "중앙": return "center";
                default: return null;
            }
        }

        /// <summary>"크기=120%" → 1.2, "높이=-10%" → -0.1 (% 없이 쓰면 그대로 비율)</summary>
        static bool TryParsePercent(string tok, string key1, string key2, out float value)
        {
            value = 0f;
            string v;
            if (tok.StartsWith(key1, StringComparison.OrdinalIgnoreCase)) v = tok.Substring(key1.Length);
            else if (tok.StartsWith(key2, StringComparison.OrdinalIgnoreCase)) v = tok.Substring(key2.Length);
            else return false;
            v = v.Trim();
            bool percent = v.EndsWith("%");
            if (percent) v = v.TrimEnd('%').Trim();
            if (!float.TryParse(v, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out value)) return false;
            if (percent) value /= 100f;
            return true;
        }

        /// <summary>"4:3" 같은 가로:세로 비율</summary>
        static bool TryParseRatio(string s, out float aspect)
        {
            aspect = 0f;
            var parts = s.Split(':');
            if (parts.Length != 2) return false;
            if (!float.TryParse(parts[0].Trim(), out var w) || !float.TryParse(parts[1].Trim(), out var h) || w <= 0 || h <= 0) return false;
            aspect = w / h;
            return true;
        }

        static bool LooksLikePath(string s) => s.Contains("/") || s.Contains("\\") || s.Contains(".") || IsNone(s);
        static bool IsNone(string s) => s != null && (s.Trim() == "없음" || s.Trim().Equals("none", StringComparison.OrdinalIgnoreCase));
        static string Unescape(string s) => s.Replace("\\n", "\n");

        static void Warn(List<string> warnings, int lineNo, string msg)
        {
            var w = $"{lineNo}번째 줄: {msg}";
            warnings?.Add(w);
            Debug.LogWarning("[VN] " + w);
        }

        /// <summary>UTF-8 우선, 실패하면 한국어 ANSI(CP949) 로 디코딩</summary>
        public static string DecodeText(byte[] bytes)
        {
            try { return new UTF8Encoding(false, true).GetString(bytes).TrimStart('\uFEFF'); }
            catch (DecoderFallbackException) { }
            try { return Encoding.GetEncoding(949).GetString(bytes); }
            catch { }
            return Encoding.UTF8.GetString(bytes).TrimStart('\uFEFF');
        }
    }
}
