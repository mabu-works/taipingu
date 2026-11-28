"use strict";
// ==========================================
// ⌨️ タイピング道場 app.js
// 画面遷移・入力イベント・タイマー・結果表示・スコアカードを担当する。
// ローマ字判定とスコア計算の純ロジックは js/typing-core.js にある。
// ==========================================

const Core = window.TypingCore;
const { tokenize, matchProgress } = Core;

// ------------------------------------------
// 🖥️ 画面・状態管理
// ------------------------------------------
const $ = (id) => document.getElementById(id);
const screens = { home: $("screen-home"), game: $("screen-game"), result: $("screen-result") };
function showScreen(name) {
  for (const key of Object.keys(screens)) screens[key].classList.toggle("hidden", key !== name);
}

// 🏆 ベストスコアの読み込み（保存形式は typing-core.js の定義どおり）
function loadBest() {
  try {
    return {
      s60: Core.parseBest60(localStorage.getItem(Core.BEST_KEY_60)),
      ta: Core.parseBestTa(localStorage.getItem(Core.BEST_KEY_TA))
    };
  } catch {
    return { s60: null, ta: null };
  }
}
function renderBest() {
  const b = loadBest();
  $("best-60").textContent = Core.formatBest60(b.s60) || "まだ記録なし";
  $("best-ta").textContent = Core.formatBestTa(b.ta) || "まだ記録なし";
}

// 🎮 ゲーム状態
const G = {
  mode: null,          // "60" | "ta"
  units: [],           // 現在の文のユニット
  typed: "",           // 現在の文で受け付けたローマ字
  queue: [],           // 残りの文インデックス
  started: false,
  finished: false,
  startAt: 0,
  timerId: null,
  totalCorrect: 0,     // 正しく打てたローマ字数（全モード累計）
  totalKeys: 0,        // 総打鍵数
  miss: 0,
  sentencesDone: 0,
  lastResult: null
};

// 🎲 お題文の順序をシャッフルする（Fisher-Yates）
function shuffledSentences() {
  const idx = Core.SENTENCES.map((_, i) => i);
  for (let i = idx.length - 1; i > 0; i--) {
    const j = Math.floor(Math.random() * (i + 1));
    [idx[i], idx[j]] = [idx[j], idx[i]];
  }
  return idx;
}

function startGame(mode) {
  G.mode = mode;
  G.queue = shuffledSentences();
  G.typed = "";
  G.started = false;
  G.finished = false;
  G.totalCorrect = 0;
  G.totalKeys = 0;
  G.miss = 0;
  G.sentencesDone = 0;
  if (G.timerId) { clearInterval(G.timerId); G.timerId = null; }
  nextSentence();
  showScreen("game");
  $("game-note").textContent = mode === "60"
    ? "最初のキーを押すと60秒カウントが始まります。"
    : "最初のキーを押すと計測が始まります。1文を打ち切ろう。";
  $("typer").value = "";
  $("typer").focus();
  updateHud(0);
}

function nextSentence() {
  if (G.queue.length === 0) G.queue = shuffledSentences();
  const s = Core.SENTENCES[G.queue.shift()];
  G.units = tokenize(s);
  G.typed = "";
  $("typer").value = "";
  renderSentence();
}

// 🖍️ お題文を進み具合で色分け表示する（かな行＋ローマ字行）
function renderSentence() {
  const m = matchProgress(G.units, G.typed);
  // 🔤 かな表示
  let kanaHtml = "";
  G.units.forEach((u, i) => {
    const cls = i < m.unitsDone ? "done" : (i === m.unitsDone ? "current" : "rest");
    kanaHtml += `<span class="${cls}">${u.kana}</span>`;
  });
  $("sentence-kana").innerHTML = kanaHtml;
  // 🔤 ローマ字表示（標準表記ベース）
  let romaHtml = "";
  G.units.forEach((u, i) => {
    if (i < m.unitsDone) {
      romaHtml += `<span class="done">${u.canonical}</span>`;
    } else if (i === m.unitsDone) {
      const donePart = G.typed.length ? `<span class="partial">${escapeHtml(currentPartialText(m))}</span>` : "";
      romaHtml += `<span class="current">${donePart}${u.canonical.slice(m.partial)}</span>`;
    } else {
      romaHtml += `<span class="rest">${u.canonical}</span>`;
    }
  });
  $("sentence-romaji").innerHTML = romaHtml;
}

// 🔍 現在ユニットで既に打った部分（標準表記の先頭 partial 文字で近似表示）
function currentPartialText(m) {
  if (m.unitsDone >= G.units.length) return "";
  const unit = G.units[m.unitsDone];
  return unit.canonical.slice(0, m.partial);
}
function escapeHtml(s) {
  return s.replace(/[&<>"']/g, (c) => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" }[c]));
}

function currentAccuracy() {
  return Core.accuracy(G.totalCorrect, G.totalKeys);
}

function updateHud(elapsedSec) {
  if (G.mode === "60") {
    const remain = Math.max(0, 60 - elapsedSec);
    $("hud-time").textContent = remain.toFixed(1);
  } else {
    $("hud-time").textContent = elapsedSec.toFixed(1);
  }
  $("hud-correct").textContent = G.totalCorrect;
  $("hud-miss").textContent = G.miss;
  $("hud-acc").textContent = Core.formatAccuracy(currentAccuracy()) + "%";
}

// ⏱ 最初のキー入力でタイマーを開始する
function startTimerIfNeeded() {
  if (G.started) return;
  G.started = true;
  G.startAt = performance.now();
  G.timerId = setInterval(() => {
    const elapsed = (performance.now() - G.startAt) / 1000;
    updateHud(elapsed);
    if (G.mode === "60" && elapsed >= 60) finishGame(60);
  }, 100);
}

// ⌨️ 1文字の入力を受け付ける（正しければ取り込み、違えばミス）
function handleChar(ch) {
  if (G.finished) return;
  if (!/^[a-z\-]$/.test(ch)) return;
  startTimerIfNeeded();
  G.totalKeys += 1;
  const candidate = G.typed + ch;
  const m = matchProgress(G.units, candidate);
  if (m.valid) {
    G.typed = candidate;
    G.totalCorrect += 1;
    renderSentence();
    if (m.complete) {
      G.sentencesDone += 1;
      if (G.mode === "ta") {
        const elapsed = (performance.now() - G.startAt) / 1000;
        finishGame(elapsed);
        return;
      }
      nextSentence();
    }
  } else {
    G.miss += 1;
    const t = $("typer");
    t.classList.add("miss-flash");
    setTimeout(() => t.classList.remove("miss-flash"), 150);
  }
  const elapsed = G.started ? (performance.now() - G.startAt) / 1000 : 0;
  updateHud(elapsed);
}

// 🏁 ゲーム終了：スコア計算とベスト保存、結果画面の表示
function finishGame(elapsedSec) {
  if (G.finished) return;
  G.finished = true;
  if (G.timerId) { clearInterval(G.timerId); G.timerId = null; }
  const acc = currentAccuracy();
  const cpmValue = Core.cpm(G.totalCorrect, elapsedSec);
  const wpmValue = Core.wpm(cpmValue);
  const score = Core.scoreFor(G.mode, G.totalCorrect, acc, cpmValue);

  let isNewBest = false;
  const best = loadBest();
  try {
    if (G.mode === "60") {
      if (!best.s60 || score > best.s60.score) {
        localStorage.setItem(Core.BEST_KEY_60, JSON.stringify({ score, cpm: cpmValue, acc, date: Date.now() }));
        isNewBest = true;
      }
    } else {
      if (!best.ta || elapsedSec < best.ta.time) {
        localStorage.setItem(Core.BEST_KEY_TA, JSON.stringify({ time: elapsedSec, cpm: cpmValue, acc, date: Date.now() }));
        isNewBest = true;
      }
    }
  } catch {
    // 保存できない環境でも結果表示は続ける
  }

  G.lastResult = {
    mode: G.mode, score, chars: G.totalCorrect, cpm: cpmValue, wpm: wpmValue, acc,
    miss: G.miss, time: elapsedSec, sentences: G.sentencesDone
  };

  $("result-title").textContent = G.mode === "60" ? "⏱ 60秒チャレンジ 結果" : "🏃 タイムアタック 結果";
  $("result-newbest").classList.toggle("hidden", !isNewBest);
  $("res-score").textContent = G.mode === "60" ? `${score} 点` : `―（参考CPM ${cpmValue}）`;
  $("res-chars").textContent = `${G.totalCorrect} 文字`;
  $("res-cpm").textContent = `${cpmValue}`;
  $("res-wpm").textContent = `${wpmValue}`;
  $("res-acc").textContent = `${acc}%`;
  $("res-miss").textContent = `${G.miss} 回`;
  $("res-time").textContent = G.mode === "60" ? `60.0 秒（${G.sentencesDone} 文完走）` : `${elapsedSec.toFixed(1)} 秒`;
  renderBest();
  showScreen("result");
}

// ------------------------------------------
// 🖼️ スコアカード（Canvas → PNG）
// ------------------------------------------
function drawScoreCard() {
  const r = G.lastResult;
  if (!r) return;
  const canvas = document.createElement("canvas");
  canvas.width = 1200; canvas.height = 630;
  const ctx = canvas.getContext("2d");
  // 🎨 背景
  const grad = ctx.createLinearGradient(0, 0, 1200, 630);
  grad.addColorStop(0, "#4f46e5");
  grad.addColorStop(1, "#7c3aed");
  ctx.fillStyle = grad;
  ctx.fillRect(0, 0, 1200, 630);
  ctx.fillStyle = "rgba(255,255,255,0.12)";
  ctx.beginPath(); ctx.arc(1080, 90, 190, 0, Math.PI * 2); ctx.fill();
  ctx.beginPath(); ctx.arc(90, 570, 140, 0, Math.PI * 2); ctx.fill();
  const fontStack = '"Hiragino Kaku Gothic ProN","Noto Sans CJK JP","Yu Gothic",sans-serif';
  ctx.fillStyle = "#ffffff";
  ctx.textAlign = "left";
  ctx.font = `700 54px ${fontStack}`;
  ctx.fillText("⌨️ タイピング道場", 70, 110);
  ctx.font = `400 34px ${fontStack}`;
  ctx.fillText(r.mode === "60" ? "60秒チャレンジ" : "1文タイムアタック", 70, 170);
  // 🔢 メイン数値
  ctx.font = `700 150px ${fontStack}`;
  const mainVal = r.mode === "60" ? `${r.score}` : `${r.time.toFixed(1)}`;
  const mainUnit = r.mode === "60" ? " 点" : " 秒";
  ctx.fillText(mainVal, 70, 350);
  const w = ctx.measureText(mainVal).width;
  ctx.font = `700 56px ${fontStack}`;
  ctx.fillText(mainUnit, 70 + w + 10, 350);
  // 📊 サブ指標
  ctx.font = `400 40px ${fontStack}`;
  const lines = [
    `CPM ${r.cpm} / WPM ${r.wpm}`,
    `正確率 ${r.acc}% ・ ミス ${r.miss} 回 ・ ${r.chars} 文字`
  ];
  lines.forEach((line, i) => ctx.fillText(line, 70, 450 + i * 62));
  const d = new Date();
  const dateStr = `${d.getFullYear()}/${String(d.getMonth() + 1).padStart(2, "0")}/${String(d.getDate()).padStart(2, "0")}`;
  ctx.font = `400 30px ${fontStack}`;
  ctx.fillStyle = "rgba(255,255,255,0.85)";
  ctx.fillText(dateStr, 70, 580);
  const a = document.createElement("a");
  a.href = canvas.toDataURL("image/png");
  a.download = "taipingu-score.png";
  a.click();
}

// ------------------------------------------
// 🎯 イベント
// ------------------------------------------
$("btn-mode-60").addEventListener("click", () => startGame("60"));
$("btn-mode-ta").addEventListener("click", () => startGame("ta"));
$("btn-quit").addEventListener("click", () => {
  if (G.timerId) { clearInterval(G.timerId); G.timerId = null; }
  G.finished = true;
  renderBest();
  showScreen("home");
});
$("btn-retry").addEventListener("click", () => startGame(G.mode));
$("btn-home").addEventListener("click", () => { renderBest(); showScreen("home"); });
$("btn-card").addEventListener("click", drawScoreCard);

// ⌨️ 入力：物理キーボード（keydown）とモバイル（inputイベント差分）の両対応
let lastKeyWasPhysical = false;
document.addEventListener("keydown", (e) => {
  if (screens.game.classList.contains("hidden")) return;
  if (e.metaKey || e.ctrlKey || e.altKey) return;
  if (e.key && e.key.length === 1) {
    e.preventDefault();
    handleChar(e.key.toLowerCase());
  }
});
$("typer").addEventListener("input", (e) => {
  // 📱 ソフトキーボード等で keydown が来ない環境向け：値の末尾1文字を取り込む
  const v = e.target.value;
  if (v.length > 0) {
    const ch = v.slice(-1).toLowerCase();
    e.target.value = "";
    if (document.activeElement === e.target && !lastKeyWasPhysical) handleChar(ch);
  }
  lastKeyWasPhysical = false;
});
$("typer").addEventListener("keydown", (e) => {
  if (e.key && e.key.length === 1) lastKeyWasPhysical = true;
});
$("screen-game").addEventListener("click", () => $("typer").focus());

// ------------------------------------------
// 🌙 テーマ切り替え（ダーク既定＋ライト切替）
// ------------------------------------------
(function initTheme() {
  var root = document.documentElement;
  if (!root || !root.getAttribute || !root.setAttribute) return;
  function apply(theme) {
    root.setAttribute("data-theme", theme);
    var btn = document.getElementById("themeToggle");
    if (btn) btn.textContent = theme === "light" ? "☀️" : "🌙";
  }
  var current = root.getAttribute("data-theme");
  if (current !== "light" && current !== "dark") {
    try { current = localStorage.getItem(Core.THEME_KEY); } catch (e) { current = null; }
  }
  apply(current === "light" ? "light" : "dark");
  var btn = document.getElementById("themeToggle");
  if (btn && btn.addEventListener) {
    btn.addEventListener("click", function () {
      var next = root.getAttribute("data-theme") === "light" ? "dark" : "light";
      apply(next);
      try { localStorage.setItem(Core.THEME_KEY, next); } catch (e) { /* 保存できなくても切り替え自体は有効 */ }
    });
  }
})();

renderBest();
showScreen("home");
