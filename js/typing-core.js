"use strict";
// ==========================================
// ⌨️ タイピング道場 typing-core.js
// ローマ字判定・お題文・スコア計算の純ロジック（DOM 非依存）。
// ブラウザでは window.TypingCore、Node では module.exports として使う。
// 判定は「かなユニットごとの候補表」を使った DFS マッチで、
// 表記ゆれ（shi/si 等）と「ん」の n/nn 両対応を許容する。
// ==========================================

// ------------------------------------------
// 🔤 かな → ローマ字候補表
// 先頭が標準表記（canonical）で、残りは許容する別表記。
// ------------------------------------------
const KANA_ROMAJI = {
  "あ": ["a"], "い": ["i"], "う": ["u"], "え": ["e"], "お": ["o"],
  "か": ["ka"], "き": ["ki"], "く": ["ku"], "け": ["ke"], "こ": ["ko"],
  "さ": ["sa"], "し": ["shi", "si"], "す": ["su"], "せ": ["se"], "そ": ["so"],
  "た": ["ta"], "ち": ["chi", "ti"], "つ": ["tsu", "tu"], "て": ["te"], "と": ["to"],
  "な": ["na"], "に": ["ni"], "ぬ": ["nu"], "ね": ["ne"], "の": ["no"],
  "は": ["ha"], "ひ": ["hi"], "ふ": ["fu", "hu"], "へ": ["he"], "ほ": ["ho"],
  "ま": ["ma"], "み": ["mi"], "む": ["mu"], "め": ["me"], "も": ["mo"],
  "や": ["ya"], "ゆ": ["yu"], "よ": ["yo"],
  "ら": ["ra"], "り": ["ri"], "る": ["ru"], "れ": ["re"], "ろ": ["ro"],
  "わ": ["wa"], "を": ["wo"], "ん": ["nn", "n"],
  "が": ["ga"], "ぎ": ["gi"], "ぐ": ["gu"], "げ": ["ge"], "ご": ["go"],
  "ざ": ["za"], "じ": ["ji", "zi"], "ず": ["zu"], "ぜ": ["ze"], "ぞ": ["zo"],
  "だ": ["da"], "ぢ": ["di", "ji", "zi"], "づ": ["du", "zu"], "で": ["de"], "ど": ["do"],
  "ば": ["ba"], "び": ["bi"], "ぶ": ["bu"], "べ": ["be"], "ぼ": ["bo"],
  "ぱ": ["pa"], "ぴ": ["pi"], "ぷ": ["pu"], "ぺ": ["pe"], "ぽ": ["po"],
  "ぁ": ["xa", "la"], "ぃ": ["xi", "li"], "ぅ": ["xu", "lu"], "ぇ": ["xe", "le"], "ぉ": ["xo", "lo"],
  "ゃ": ["xya", "lya"], "ゅ": ["xyu", "lyu"], "ょ": ["xyo", "lyo"],
  "ー": ["-"]
};

// 🔤 拗音など（2文字で1ユニット）
const DIGRAPH_ROMAJI = {
  "きゃ": ["kya"], "きゅ": ["kyu"], "きょ": ["kyo"],
  "しゃ": ["sha", "sya"], "しゅ": ["shu", "syu"], "しょ": ["sho", "syo"],
  "ちゃ": ["cha", "tya", "cya"], "ちゅ": ["chu", "tyu", "cyu"], "ちょ": ["cho", "tyo", "cyo"],
  "にゃ": ["nya"], "にゅ": ["nyu"], "にょ": ["nyo"],
  "ひゃ": ["hya"], "ひゅ": ["hyu"], "ひょ": ["hyo"],
  "みゃ": ["mya"], "みゅ": ["myu"], "みょ": ["myo"],
  "りゃ": ["rya"], "りゅ": ["ryu"], "りょ": ["ryo"],
  "ぎゃ": ["gya"], "ぎゅ": ["gyu"], "ぎょ": ["gyo"],
  "じゃ": ["ja", "jya", "zya"], "じゅ": ["ju", "jyu", "zyu"], "じょ": ["jo", "jyo", "zyo"],
  "ぢゃ": ["dya", "ja", "jya"], "ぢゅ": ["dyu", "ju", "jyu"], "ぢょ": ["dyo", "jo", "jyo"],
  "びゃ": ["bya"], "びゅ": ["byu"], "びょ": ["byo"],
  "ぴゃ": ["pya"], "ぴゅ": ["pyu"], "ぴょ": ["pyo"],
  "ふぁ": ["fa"], "ふぃ": ["fi"], "ふぇ": ["fe"], "ふぉ": ["fo"],
  "うぃ": ["wi"], "うぇ": ["we"], "うぉ": ["wo"],
  "てぃ": ["thi"], "でぃ": ["dhi"]
};

// 🔤 促音「っ」単体（文末など）の候補
const SOKUON_SOLO = ["xtu", "ltu", "xtsu", "ltsu"];

// 🔤 子音として重ね打ちできる文字（促音の判定に使う）
const DOUBLABLE = /[bcdfghjklmpqrstvwxyz]/;

// ------------------------------------------
// 📝 お題文（ひらがなのみ・34文）
// 内容データとして旧版から引き継ぎ、件数と内容はテストで固定する。
// ------------------------------------------
const SENTENCES = [
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
];

// ------------------------------------------
// 🔤 ひらがな文を「かなユニット」の配列に分解する
// ユニット: { kana: "きゃ", options: ["kya"], canonical: "kya" }
// 「っ」は次のユニットと合体させ、子音を重ねた候補を作る。
// 対応表にない文字はスキップする。
// ------------------------------------------
function tokenize(sentence) {
  const units = [];
  let i = 0;
  let pendingSokuon = false;
  while (i < sentence.length) {
    const two = sentence.slice(i, i + 2);
    let kana = null;
    let options = null;
    if (two.length === 2 && DIGRAPH_ROMAJI[two]) {
      kana = two;
      options = DIGRAPH_ROMAJI[two];
      i += 2;
    } else if (sentence[i] === "っ") {
      pendingSokuon = true;
      i += 1;
      continue;
    } else if (KANA_ROMAJI[sentence[i]]) {
      kana = sentence[i];
      options = KANA_ROMAJI[sentence[i]];
      i += 1;
    } else {
      i += 1;
      continue; // 未対応文字はスキップ
    }
    if (pendingSokuon) {
      const doubled = [];
      for (const opt of options) {
        const c = opt[0];
        if (DOUBLABLE.test(c)) doubled.push(c + opt);
      }
      if (doubled.length > 0) {
        options = doubled.concat(SOKUON_SOLO.flatMap((s) => options.map((o) => s + o)));
      }
      kana = "っ" + kana;
      pendingSokuon = false;
    }
    units.push({ kana, options, canonical: options[0] });
  }
  if (pendingSokuon) {
    units.push({ kana: "っ", options: SOKUON_SOLO.slice(), canonical: "xtu" });
  }
  return units;
}

// 🔍 進み具合の比較：完了ユニット数が多い方、次に部分一致が長い方が良い
function isBetterProgress(a, b) {
  if (a.unitsDone !== b.unitsDone) return a.unitsDone > b.unitsDone;
  return a.partial > b.partial;
}

// ------------------------------------------
// 🔍 入力済みローマ字 typed が units のどこまで進んだかを DFS で判定する
// 戻り値: { unitsDone, partial, consumed, complete, valid }
// valid=false は「どの候補でもこの typed にはなり得ない」＝タイプミス。
// ------------------------------------------
function matchProgress(units, typed) {
  let best = { unitsDone: 0, partial: 0, consumed: 0, complete: false };
  // 🧮 スタック：[unitIndex, typedIndex]
  const stack = [[0, 0]];
  while (stack.length) {
    const [ui, ti] = stack.pop();
    if (ui >= units.length) {
      if (ti === typed.length) {
        const cand = { unitsDone: ui, partial: 0, consumed: ti, complete: true };
        if (isBetterProgress(cand, best)) best = cand;
      }
      continue;
    }
    const unit = units[ui];
    const rest = typed.slice(ti);
    for (const opt of unit.options) {
      if (rest.startsWith(opt)) {
        stack.push([ui + 1, ti + opt.length]);
      } else if (rest.length > 0 && opt.startsWith(rest)) {
        const cand = { unitsDone: ui, partial: rest.length, consumed: ti + rest.length, complete: false };
        if (isBetterProgress(cand, best)) best = cand;
      }
    }
    // 🔍 typed をすべて消費しきった位置（部分一致なし）も候補
    if (ti === typed.length) {
      const cand = { unitsDone: ui, partial: 0, consumed: ti, complete: false };
      if (isBetterProgress(cand, best)) best = cand;
    }
  }
  best.valid = best.consumed === typed.length;
  return best;
}

// ------------------------------------------
// 📊 スコア計算
// ------------------------------------------

// 正確率（小数第1位まで）。打鍵数が 0 のときは 100。
function accuracy(correct, keys) {
  return keys === 0 ? 100 : Math.round((correct / keys) * 1000) / 10;
}

// CPM（1分あたり文字数）
function cpm(correct, elapsedSec) {
  const minutes = elapsedSec / 60;
  return minutes > 0 ? Math.round(correct / minutes) : 0;
}

// WPM（1単語 = 5文字の慣例で CPM から換算）
function wpm(cpmValue) {
  return Math.round(cpmValue / 5);
}

// モード別スコア：60秒は「正打数 × 正確率」、タイムアタックは CPM
function scoreFor(mode, correct, acc, cpmValue) {
  return mode === "60" ? Math.round(correct * (acc / 100)) : cpmValue;
}

// HUD 用の正確率表示（末尾の .0 を落とす）
function formatAccuracy(acc) {
  return acc.toFixed(1).replace(/\.0$/, "");
}

// ------------------------------------------
// 🏆 ベストスコアの保存形式
// localStorage のキーと JSON 形式は旧版と同一（既存ユーザーの記録を引き継ぐ）。
// 60秒: { score, cpm, acc, date } / タイムアタック: { time, cpm, acc, date }
// ------------------------------------------
const BEST_KEY_60 = "taipingu_best_60";
const BEST_KEY_TA = "taipingu_best_ta";
const THEME_KEY = "taipingu-theme";

function isFiniteNumber(v) {
  return typeof v === "number" && Number.isFinite(v);
}

// 保存済み JSON 文字列を検証して取り出す（壊れていたら null）
function parseBest60(json) {
  const e = parseJsonOrNull(json);
  if (!e || !isFiniteNumber(e.score) || !isFiniteNumber(e.cpm) || !isFiniteNumber(e.acc)) return null;
  return e;
}
function parseBestTa(json) {
  const e = parseJsonOrNull(json);
  if (!e || !isFiniteNumber(e.time) || !isFiniteNumber(e.cpm) || !isFiniteNumber(e.acc)) return null;
  return e;
}
function parseJsonOrNull(json) {
  if (typeof json !== "string" || json === "") return null;
  try {
    const v = JSON.parse(json);
    return v && typeof v === "object" ? v : null;
  } catch {
    return null;
  }
}

// ベスト表示の文字列（記録なしは呼び出し側で出し分けるため null を返す）
function formatBest60(entry) {
  return entry ? `${entry.score} 点（${entry.cpm} CPM / 正確率 ${entry.acc}%）` : null;
}
function formatBestTa(entry) {
  return entry ? `${entry.time.toFixed(1)} 秒（${entry.cpm} CPM / 正確率 ${entry.acc}%）` : null;
}

// ------------------------------------------
// 📦 公開
// ------------------------------------------
const TypingCore = {
  KANA_ROMAJI,
  DIGRAPH_ROMAJI,
  SOKUON_SOLO,
  SENTENCES,
  tokenize,
  matchProgress,
  accuracy,
  cpm,
  wpm,
  scoreFor,
  formatAccuracy,
  BEST_KEY_60,
  BEST_KEY_TA,
  THEME_KEY,
  parseBest60,
  parseBestTa,
  formatBest60,
  formatBestTa
};

if (typeof module !== "undefined" && module.exports) {
  module.exports = TypingCore;
}
if (typeof window !== "undefined") {
  window.TypingCore = TypingCore;
}
