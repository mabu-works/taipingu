using System;
using Taipingu;

// 🧪 TypingEngine の簡易アサーションランナー（依存パッケージなし）
// 実行: dotnet run --project windows/tests
int passed = 0;
int failed = 0;

void Check(bool cond, string name)
{
    if (cond) { passed++; }
    else { failed++; Console.WriteLine($"FAIL: {name}"); }
}

void Eq<T>(T expected, T actual, string name)
{
    Check(Equals(expected, actual), $"{name} (expected={expected}, actual={actual})");
}

// 🔤 お題文データ
Eq(34, TypingEngine.Sentences.Length, "sentence count is 34");

// 🔤 tokenize: 拗音は1ユニット
var kyou = TypingEngine.Tokenize("きょう");
Eq(2, kyou.Count, "tokenize きょう unit count");
Eq("きょ", kyou[0].Kana, "tokenize きょう first unit");
Eq("kyo", kyou[0].Canonical, "tokenize きょ canonical");

// 🔤 tokenize: っ は次のユニットに合体し、重ね打ち候補を持つ
var gakkou = TypingEngine.Tokenize("がっこう");
Eq(3, gakkou.Count, "tokenize がっこう unit count");
Eq("っこ", gakkou[1].Kana, "tokenize っこ kana");
Check(Array.IndexOf(gakkou[1].Options, "kko") >= 0, "っこ accepts kko");
Check(Array.IndexOf(gakkou[1].Options, "xtuko") >= 0, "っこ accepts xtuko");

// 🔤 tokenize: 文末の っ は単体ユニット
var lastSokuon = TypingEngine.Tokenize("あっ");
Eq(2, lastSokuon.Count, "tokenize あっ unit count");
Eq("っ", lastSokuon[1].Kana, "trailing っ kana");
Eq("xtu", lastSokuon[1].Canonical, "trailing っ canonical");

// 🔍 判定: 標準表記と別表記
Check(TypingEngine.MatchProgress(TypingEngine.Tokenize("し"), "shi").Complete, "し = shi");
Check(TypingEngine.MatchProgress(TypingEngine.Tokenize("し"), "si").Complete, "し = si");
Check(TypingEngine.MatchProgress(TypingEngine.Tokenize("ち"), "ti").Complete, "ち = ti");
Check(TypingEngine.MatchProgress(TypingEngine.Tokenize("つ"), "tu").Complete, "つ = tu");
Check(TypingEngine.MatchProgress(TypingEngine.Tokenize("ふ"), "hu").Complete, "ふ = hu");
Check(TypingEngine.MatchProgress(TypingEngine.Tokenize("じ"), "zi").Complete, "じ = zi");
Check(TypingEngine.MatchProgress(TypingEngine.Tokenize("しゃ"), "sya").Complete, "しゃ = sya");
Check(TypingEngine.MatchProgress(TypingEngine.Tokenize("ちゃ"), "tya").Complete, "ちゃ = tya");
Check(TypingEngine.MatchProgress(TypingEngine.Tokenize("ちゃ"), "cya").Complete, "ちゃ = cya");

// 🔍 判定: 「ん」は n / nn 両対応（文脈依存）
Check(TypingEngine.MatchProgress(TypingEngine.Tokenize("ほんを"), "honwo").Complete, "ほんを = honwo (n before consonant)");
Check(TypingEngine.MatchProgress(TypingEngine.Tokenize("ほんを"), "honnwo").Complete, "ほんを = honnwo");
Check(!TypingEngine.MatchProgress(TypingEngine.Tokenize("ほんを"), "howo").Valid, "ほんを != howo");

// 🔍 判定: っ の重ね打ちと xtu 表記
Check(TypingEngine.MatchProgress(TypingEngine.Tokenize("ずっと"), "zutto").Complete, "ずっと = zutto");
Check(TypingEngine.MatchProgress(TypingEngine.Tokenize("ずっと"), "zuxtuto").Complete, "ずっと = zuxtuto");

// 🔍 判定: ミス検出と部分一致
var miss = TypingEngine.MatchProgress(TypingEngine.Tokenize("し"), "xa");
Check(!miss.Valid, "し != xa (invalid)");
var partial = TypingEngine.MatchProgress(TypingEngine.Tokenize("し"), "sh");
Check(partial.Valid && !partial.Complete && partial.Partial == 2, "し partial 'sh'");

// 🔍 判定: 全34文が標準表記で完走できる
foreach (var s in TypingEngine.Sentences)
{
    var units = TypingEngine.Tokenize(s);
    string canonical = "";
    foreach (var u in units) canonical += u.Canonical;
    var m = TypingEngine.MatchProgress(units, canonical);
    Check(m.Complete && m.Valid, $"canonical completes: {s}");
}

// 📊 スコア計算
Eq(100.0, TypingEngine.Accuracy(0, 0), "accuracy with no keys");
Eq(87.5, TypingEngine.Accuracy(7, 8), "accuracy 7/8");
Eq(300, TypingEngine.Cpm(300, 60), "cpm 300 chars in 60s");
Eq(60, TypingEngine.Wpm(300), "wpm from cpm 300");
Eq(263, TypingEngine.ScoreFor("60", 300, 87.5, 300), "score60 = correct * acc");
Eq(300, TypingEngine.ScoreFor("ta", 300, 87.5, 300), "scoreTA = cpm");

Console.WriteLine($"TypingEngine tests: {passed} passed, {failed} failed");
return failed == 0 ? 0 : 1;
