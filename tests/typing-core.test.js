"use strict";
// 🧪 typing-core のユニットテスト（node:test、依存なし）
const test = require("node:test");
const assert = require("node:assert/strict");
const Core = require("../js/typing-core.js");

const { tokenize, matchProgress } = Core;

function completes(sentence, typed) {
  const m = matchProgress(tokenize(sentence), typed);
  return m.valid && m.complete;
}

// ------------------------------------------
// 📝 お題文データの完全性
// ------------------------------------------
test("sentences: exactly 34 sentences are shipped", () => {
  assert.equal(Core.SENTENCES.length, 34);
});

test("sentences: every sentence tokenizes with no skipped characters", () => {
  for (const s of Core.SENTENCES) {
    const joined = tokenize(s).map((u) => u.kana).join("");
    assert.equal(joined, s, `sentence fully covered: ${s}`);
  }
});

test("sentences: every sentence can be completed with its canonical romaji", () => {
  for (const s of Core.SENTENCES) {
    const units = tokenize(s);
    const canonical = units.map((u) => u.canonical).join("");
    assert.ok(completes(s, canonical), `canonical completes: ${s}`);
  }
});

test("tables: kana and digraph table sizes are pinned", () => {
  assert.equal(Object.keys(Core.KANA_ROMAJI).length, 80);
  assert.equal(Object.keys(Core.DIGRAPH_ROMAJI).length, 45);
  assert.deepEqual(Core.SOKUON_SOLO, ["xtu", "ltu", "xtsu", "ltsu"]);
});

// ------------------------------------------
// 🔤 tokenize
// ------------------------------------------
test("tokenize: digraphs become single units", () => {
  const units = tokenize("きょう");
  assert.equal(units.length, 2);
  assert.equal(units[0].kana, "きょ");
  assert.equal(units[0].canonical, "kyo");
});

test("tokenize: sokuon merges into the next unit with doubled options", () => {
  const units = tokenize("がっこう");
  assert.equal(units.length, 3);
  assert.equal(units[1].kana, "っこ");
  assert.ok(units[1].options.includes("kko"));
  assert.ok(units[1].options.includes("xtuko"));
  assert.equal(units[1].canonical, "kko");
});

test("tokenize: trailing sokuon becomes a solo unit", () => {
  const units = tokenize("あっ");
  assert.equal(units.length, 2);
  assert.equal(units[1].kana, "っ");
  assert.deepEqual(units[1].options, ["xtu", "ltu", "xtsu", "ltsu"]);
  assert.equal(units[1].canonical, "xtu");
});

test("tokenize: unknown characters are skipped", () => {
  const units = tokenize("あ漢字い");
  assert.deepEqual(units.map((u) => u.kana), ["あ", "い"]);
});

// ------------------------------------------
// 🔍 matchProgress: 表記ゆれ
// ------------------------------------------
test("judgement: major spelling variants are accepted", () => {
  const cases = [
    ["し", "shi"], ["し", "si"],
    ["ち", "chi"], ["ち", "ti"],
    ["つ", "tsu"], ["つ", "tu"],
    ["ふ", "fu"], ["ふ", "hu"],
    ["じ", "ji"], ["じ", "zi"],
    ["ぢ", "di"], ["ぢ", "ji"],
    ["づ", "du"], ["づ", "zu"],
    ["しゃ", "sha"], ["しゃ", "sya"],
    ["ちゃ", "cha"], ["ちゃ", "tya"], ["ちゃ", "cya"],
    ["じゃ", "ja"], ["じゃ", "jya"], ["じゃ", "zya"],
    ["しゅくだい", "shukudai"], ["しゅくだい", "syukudai"]
  ];
  for (const [kana, typed] of cases) {
    assert.ok(completes(kana, typed), `${kana} = ${typed}`);
  }
});

test("judgement: full sentence typed with variant spellings completes", () => {
  assert.ok(completes(
    "しゅくだいをおわらせてからあそぶ",
    "syukudaiwoowarasetekaraasobu"
  ));
});

test("judgement: wrong keys are invalid", () => {
  const m = matchProgress(tokenize("し"), "xa");
  assert.equal(m.valid, false);
  assert.equal(m.complete, false);
});

test("judgement: partial input reports progress within the current unit", () => {
  const m = matchProgress(tokenize("しか"), "sh");
  assert.equal(m.valid, true);
  assert.equal(m.complete, false);
  assert.equal(m.unitsDone, 0);
  assert.equal(m.partial, 2);
  const m2 = matchProgress(tokenize("しか"), "shik");
  assert.equal(m2.unitsDone, 1);
  assert.equal(m2.partial, 1);
});

// ------------------------------------------
// 🔍 matchProgress: 「ん」と「っ」
// ------------------------------------------
test("judgement: ん accepts both n and nn before a consonant unit", () => {
  assert.ok(completes("ほんを", "honwo"));
  assert.ok(completes("ほんを", "honnwo"));
  assert.ok(completes("でんしゃ", "densha"));
  assert.ok(completes("でんしゃ", "dennsha"));
});

test("judgement: ん as a single n cannot swallow a following na-row unit", () => {
  assert.equal(matchProgress(tokenize("こんな"), "kona").valid, false);
  assert.ok(completes("こんな", "konna"));
});

test("judgement: sokuon accepts doubled consonants and xtu forms", () => {
  assert.ok(completes("ずっと", "zutto"));
  assert.ok(completes("ずっと", "zuxtuto"));
  assert.ok(completes("がっこう", "gakkou"));
  assert.ok(completes("がっこう", "gaxtukou"));
  assert.equal(matchProgress(tokenize("ずっと"), "zuto").valid, false);
});

test("judgement: long-vowel mark is typed with a hyphen", () => {
  assert.ok(completes("らーめん", "ra-men"));
  assert.ok(completes("らーめん", "ra-menn"));
});

// ------------------------------------------
// 📊 スコア計算
// ------------------------------------------
test("scoring: accuracy is rounded to one decimal, 100 with no keys", () => {
  assert.equal(Core.accuracy(0, 0), 100);
  assert.equal(Core.accuracy(7, 8), 87.5);
  assert.equal(Core.accuracy(1, 3), 33.3);
  assert.equal(Core.accuracy(50, 50), 100);
});

test("scoring: cpm and wpm follow the 5-chars-per-word convention", () => {
  assert.equal(Core.cpm(300, 60), 300);
  assert.equal(Core.cpm(150, 30), 300);
  assert.equal(Core.cpm(0, 60), 0);
  assert.equal(Core.cpm(10, 0), 0);
  assert.equal(Core.wpm(300), 60);
  assert.equal(Core.wpm(302), 60);
});

test("scoring: 60s score is correct chars times accuracy, TA score is cpm", () => {
  assert.equal(Core.scoreFor("60", 300, 87.5, 300), 263);
  assert.equal(Core.scoreFor("60", 100, 100, 100), 100);
  assert.equal(Core.scoreFor("ta", 300, 87.5, 300), 300);
});

test("scoring: HUD accuracy format drops a trailing .0", () => {
  assert.equal(Core.formatAccuracy(100), "100");
  assert.equal(Core.formatAccuracy(90), "90");
  assert.equal(Core.formatAccuracy(87.5), "87.5");
});

// ------------------------------------------
// 🏆 ベストスコアの保存形式
// ------------------------------------------
test("storage: localStorage keys are unchanged", () => {
  assert.equal(Core.BEST_KEY_60, "taipingu_best_60");
  assert.equal(Core.BEST_KEY_TA, "taipingu_best_ta");
  assert.equal(Core.THEME_KEY, "taipingu-theme");
});

test("storage: saved best records round-trip in the stored JSON shape", () => {
  const saved60 = JSON.stringify({ score: 263, cpm: 300, acc: 87.5, date: 1764330000000 });
  assert.deepEqual(Core.parseBest60(saved60), { score: 263, cpm: 300, acc: 87.5, date: 1764330000000 });
  const savedTa = JSON.stringify({ time: 12.34, cpm: 280, acc: 95.5, date: 1764330000000 });
  assert.deepEqual(Core.parseBestTa(savedTa), { time: 12.34, cpm: 280, acc: 95.5, date: 1764330000000 });
});

test("storage: broken or shape-mismatched records parse as null", () => {
  assert.equal(Core.parseBest60(null), null);
  assert.equal(Core.parseBest60(""), null);
  assert.equal(Core.parseBest60("not json"), null);
  assert.equal(Core.parseBest60('{"score":"263","cpm":300,"acc":87.5}'), null);
  assert.equal(Core.parseBestTa('{"cpm":280,"acc":95.5}'), null);
});

test("storage: best display strings match the home screen format", () => {
  assert.equal(
    Core.formatBest60({ score: 263, cpm: 300, acc: 87.5 }),
    "263 点（300 CPM / 正確率 87.5%）"
  );
  assert.equal(
    Core.formatBestTa({ time: 12.34, cpm: 280, acc: 95.5 }),
    "12.3 秒（280 CPM / 正確率 95.5%）"
  );
  assert.equal(Core.formatBest60(null), null);
  assert.equal(Core.formatBestTa(null), null);
});
