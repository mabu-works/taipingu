using System;
using System.Collections.Generic;

namespace Taipingu
{
    // 🔤 かなユニット（Web版 tokenize のユニットに対応）
    internal sealed class Unit
    {
        public string Kana = "";
        public string[] Options = Array.Empty<string>();
        public string Canonical => Options[0];
    }

    // 🔍 マッチ結果（Web版 matchProgress の戻り値に対応）
    internal sealed class MatchResult
    {
        public int UnitsDone;
        public int Partial;
        public int Consumed;
        public bool Complete;
        public bool Valid;
    }

    // ==========================================
    // ⌨️ ローマ字判定エンジン（Web版 js/typing-core.js の移植）
    // WinForms に依存しない純ロジックだけをここに置き、テスト可能にする。
    // ==========================================
    internal static class TypingEngine
    {
        // ------------------------------------------
        // 🔤 かな → ローマ字候補表（Web版から転記）
        // ------------------------------------------
        public static readonly Dictionary<string, string[]> KanaRomaji = new Dictionary<string, string[]>
        {
            ["あ"] = new[] { "a" }, ["い"] = new[] { "i" }, ["う"] = new[] { "u" }, ["え"] = new[] { "e" }, ["お"] = new[] { "o" },
            ["か"] = new[] { "ka" }, ["き"] = new[] { "ki" }, ["く"] = new[] { "ku" }, ["け"] = new[] { "ke" }, ["こ"] = new[] { "ko" },
            ["さ"] = new[] { "sa" }, ["し"] = new[] { "shi", "si" }, ["す"] = new[] { "su" }, ["せ"] = new[] { "se" }, ["そ"] = new[] { "so" },
            ["た"] = new[] { "ta" }, ["ち"] = new[] { "chi", "ti" }, ["つ"] = new[] { "tsu", "tu" }, ["て"] = new[] { "te" }, ["と"] = new[] { "to" },
            ["な"] = new[] { "na" }, ["に"] = new[] { "ni" }, ["ぬ"] = new[] { "nu" }, ["ね"] = new[] { "ne" }, ["の"] = new[] { "no" },
            ["は"] = new[] { "ha" }, ["ひ"] = new[] { "hi" }, ["ふ"] = new[] { "fu", "hu" }, ["へ"] = new[] { "he" }, ["ほ"] = new[] { "ho" },
            ["ま"] = new[] { "ma" }, ["み"] = new[] { "mi" }, ["む"] = new[] { "mu" }, ["め"] = new[] { "me" }, ["も"] = new[] { "mo" },
            ["や"] = new[] { "ya" }, ["ゆ"] = new[] { "yu" }, ["よ"] = new[] { "yo" },
            ["ら"] = new[] { "ra" }, ["り"] = new[] { "ri" }, ["る"] = new[] { "ru" }, ["れ"] = new[] { "re" }, ["ろ"] = new[] { "ro" },
            ["わ"] = new[] { "wa" }, ["を"] = new[] { "wo" }, ["ん"] = new[] { "nn", "n" },
            ["が"] = new[] { "ga" }, ["ぎ"] = new[] { "gi" }, ["ぐ"] = new[] { "gu" }, ["げ"] = new[] { "ge" }, ["ご"] = new[] { "go" },
            ["ざ"] = new[] { "za" }, ["じ"] = new[] { "ji", "zi" }, ["ず"] = new[] { "zu" }, ["ぜ"] = new[] { "ze" }, ["ぞ"] = new[] { "zo" },
            ["だ"] = new[] { "da" }, ["ぢ"] = new[] { "di", "ji", "zi" }, ["づ"] = new[] { "du", "zu" }, ["で"] = new[] { "de" }, ["ど"] = new[] { "do" },
            ["ば"] = new[] { "ba" }, ["び"] = new[] { "bi" }, ["ぶ"] = new[] { "bu" }, ["べ"] = new[] { "be" }, ["ぼ"] = new[] { "bo" },
            ["ぱ"] = new[] { "pa" }, ["ぴ"] = new[] { "pi" }, ["ぷ"] = new[] { "pu" }, ["ぺ"] = new[] { "pe" }, ["ぽ"] = new[] { "po" },
            ["ぁ"] = new[] { "xa", "la" }, ["ぃ"] = new[] { "xi", "li" }, ["ぅ"] = new[] { "xu", "lu" }, ["ぇ"] = new[] { "xe", "le" }, ["ぉ"] = new[] { "xo", "lo" },
            ["ゃ"] = new[] { "xya", "lya" }, ["ゅ"] = new[] { "xyu", "lyu" }, ["ょ"] = new[] { "xyo", "lyo" },
            ["ー"] = new[] { "-" }
        };

        public static readonly Dictionary<string, string[]> DigraphRomaji = new Dictionary<string, string[]>
        {
            ["きゃ"] = new[] { "kya" }, ["きゅ"] = new[] { "kyu" }, ["きょ"] = new[] { "kyo" },
            ["しゃ"] = new[] { "sha", "sya" }, ["しゅ"] = new[] { "shu", "syu" }, ["しょ"] = new[] { "sho", "syo" },
            ["ちゃ"] = new[] { "cha", "tya", "cya" }, ["ちゅ"] = new[] { "chu", "tyu", "cyu" }, ["ちょ"] = new[] { "cho", "tyo", "cyo" },
            ["にゃ"] = new[] { "nya" }, ["にゅ"] = new[] { "nyu" }, ["にょ"] = new[] { "nyo" },
            ["ひゃ"] = new[] { "hya" }, ["ひゅ"] = new[] { "hyu" }, ["ひょ"] = new[] { "hyo" },
            ["みゃ"] = new[] { "mya" }, ["みゅ"] = new[] { "myu" }, ["みょ"] = new[] { "myo" },
            ["りゃ"] = new[] { "rya" }, ["りゅ"] = new[] { "ryu" }, ["りょ"] = new[] { "ryo" },
            ["ぎゃ"] = new[] { "gya" }, ["ぎゅ"] = new[] { "gyu" }, ["ぎょ"] = new[] { "gyo" },
            ["じゃ"] = new[] { "ja", "jya", "zya" }, ["じゅ"] = new[] { "ju", "jyu", "zyu" }, ["じょ"] = new[] { "jo", "jyo", "zyo" },
            ["ぢゃ"] = new[] { "dya", "ja", "jya" }, ["ぢゅ"] = new[] { "dyu", "ju", "jyu" }, ["ぢょ"] = new[] { "dyo", "jo", "jyo" },
            ["びゃ"] = new[] { "bya" }, ["びゅ"] = new[] { "byu" }, ["びょ"] = new[] { "byo" },
            ["ぴゃ"] = new[] { "pya" }, ["ぴゅ"] = new[] { "pyu" }, ["ぴょ"] = new[] { "pyo" },
            ["ふぁ"] = new[] { "fa" }, ["ふぃ"] = new[] { "fi" }, ["ふぇ"] = new[] { "fe" }, ["ふぉ"] = new[] { "fo" },
            ["うぃ"] = new[] { "wi" }, ["うぇ"] = new[] { "we" }, ["うぉ"] = new[] { "wo" },
            ["てぃ"] = new[] { "thi" }, ["でぃ"] = new[] { "dhi" }
        };

        public static readonly string[] SokuonSolo = { "xtu", "ltu", "xtsu", "ltsu" };

        // ------------------------------------------
        // 📝 お題文（ひらがなのみ・34文、Web版から転記）
        // ------------------------------------------
        public static readonly string[] Sentences =
        {
            "あさごはんはたまごとぱん",
            "きょうはいいてんきですね",
            "ねこがにわであそんでいる",
            "でんしゃにのってまちへいく",
            "あたらしいくつをかいました",
            "としょかんでほんをよむ",
            "ともだちとこうえんであった",
            "あめがふってきたのでかさをさす",
            "すずしいかぜがふいている",
            "やまのうえからうみがみえる",
            "おんがくをききながらべんきょうする",
            "かぞくとりょこうにいきたい",
            "じてんしゃでかわぞいをはしる",
            "ゆうごはんはおむらいすにしよう",
            "ほしぞらがとてもきれいだ",
            "あしたははやおきをがんばる",
            "がっこうのちかくにぱんやがある",
            "いぬとさんぽするのがすきです",
            "なつやすみにうみへいった",
            "ふゆはこたつでみかんをたべる",
            "さくらのきがまんかいになった",
            "ぴあののれんしゅうをまいにちする",
            "しゅくだいをおわらせてからあそぶ",
            "おかしづくりにちょうせんした",
            "かぜをひかないようにきをつける",
            "ゆうやけぞらがあかくそまる",
            "あたらしいあそびをはじめた",
            "まちのとしょかんはしずかだ",
            "おじいちゃんのいえにあそびにいく",
            "きんぎょすくいがじょうずだ",
            "まほうのことばをとなえた",
            "ずっとまっていたばすがきた",
            "しっかりてあらいをしよう",
            "にっぽんのしきはうつくしい"
        };

        // ------------------------------------------
        // 🔤 ひらがな文を「かなユニット」の配列に分解する
        // ------------------------------------------
        public static List<Unit> Tokenize(string sentence)
        {
            var units = new List<Unit>();
            int i = 0;
            bool pendingSokuon = false;
            while (i < sentence.Length)
            {
                string? kana = null;
                string[]? options = null;
                if (i + 1 < sentence.Length && DigraphRomaji.TryGetValue(sentence.Substring(i, 2), out var dg))
                {
                    kana = sentence.Substring(i, 2);
                    options = dg;
                    i += 2;
                }
                else if (sentence[i] == 'っ')
                {
                    pendingSokuon = true;
                    i += 1;
                    continue;
                }
                else if (KanaRomaji.TryGetValue(sentence[i].ToString(), out var kr))
                {
                    kana = sentence[i].ToString();
                    options = kr;
                    i += 1;
                }
                else
                {
                    i += 1;
                    continue; // 未対応文字はスキップ
                }

                if (pendingSokuon)
                {
                    var merged = new List<string>();
                    foreach (var opt in options!)
                    {
                        char c = opt[0];
                        if ("bcdfghjklmpqrstvwxyz".Contains(c)) merged.Add(c + opt);
                    }
                    if (merged.Count > 0)
                    {
                        foreach (var s in SokuonSolo)
                            foreach (var o in options!)
                                merged.Add(s + o);
                        options = merged.ToArray();
                    }
                    kana = "っ" + kana;
                    pendingSokuon = false;
                }
                units.Add(new Unit { Kana = kana!, Options = options! });
            }
            if (pendingSokuon)
            {
                units.Add(new Unit { Kana = "っ", Options = (string[])SokuonSolo.Clone() });
            }
            return units;
        }

        private static bool IsBetter(MatchResult a, MatchResult b)
        {
            if (a.UnitsDone != b.UnitsDone) return a.UnitsDone > b.UnitsDone;
            return a.Partial > b.Partial;
        }

        // ------------------------------------------
        // 🔍 入力済みローマ字がユニット列のどこまで進んだかを DFS で判定する
        // ------------------------------------------
        public static MatchResult MatchProgress(List<Unit> units, string typed)
        {
            var best = new MatchResult { UnitsDone = 0, Partial = 0, Consumed = 0, Complete = false };
            var stack = new Stack<(int ui, int ti)>();
            stack.Push((0, 0));
            while (stack.Count > 0)
            {
                var (ui, ti) = stack.Pop();
                if (ui >= units.Count)
                {
                    if (ti == typed.Length)
                    {
                        var cand = new MatchResult { UnitsDone = ui, Partial = 0, Consumed = ti, Complete = true };
                        if (IsBetter(cand, best)) best = cand;
                    }
                    continue;
                }
                var unit = units[ui];
                string rest = typed.Substring(ti);
                foreach (var opt in unit.Options)
                {
                    if (rest.StartsWith(opt, StringComparison.Ordinal))
                    {
                        stack.Push((ui + 1, ti + opt.Length));
                    }
                    else if (rest.Length > 0 && opt.StartsWith(rest, StringComparison.Ordinal))
                    {
                        var cand = new MatchResult { UnitsDone = ui, Partial = rest.Length, Consumed = ti + rest.Length, Complete = false };
                        if (IsBetter(cand, best)) best = cand;
                    }
                }
                if (ti == typed.Length)
                {
                    var cand = new MatchResult { UnitsDone = ui, Partial = 0, Consumed = ti, Complete = false };
                    if (IsBetter(cand, best)) best = cand;
                }
            }
            best.Valid = best.Consumed == typed.Length;
            return best;
        }

        // ------------------------------------------
        // 📊 スコア計算（Web版と同一式）
        // ------------------------------------------
        public static int Jr(double v) => (int)Math.Floor(v + 0.5); // JS の Math.round（正数）

        public static double Accuracy(int correct, int keys)
        {
            return keys == 0 ? 100.0 : Math.Floor(correct * 1000.0 / keys + 0.5) / 10.0;
        }

        public static int Cpm(int correct, double elapsedSec)
        {
            double minutes = elapsedSec / 60.0;
            return minutes > 0 ? Jr(correct / minutes) : 0;
        }

        public static int Wpm(int cpmValue) => Jr(cpmValue / 5.0);

        public static int ScoreFor(string mode, int correct, double acc, int cpmValue)
        {
            return mode == "60" ? Jr(correct * (acc / 100.0)) : cpmValue;
        }
    }
}
