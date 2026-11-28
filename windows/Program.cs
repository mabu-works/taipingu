using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Text.Json;
using System.Windows.Forms;

namespace Taipingu
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            ApplicationConfiguration.Initialize();
            Application.Run(new MainForm());
        }
    }

    // 🏆 ベスト記録の保存形式（%AppData%\Taipingu\best.json、Web版の localStorage 相当）
    internal sealed class BestEntry
    {
        public int Score { get; set; }
        public double Time { get; set; }
        public int Cpm { get; set; }
        public double Acc { get; set; }
    }

    internal sealed class BestData
    {
        public BestEntry? S60 { get; set; }
        public BestEntry? Ta { get; set; }
    }

    internal sealed class MainForm : Form
    {
        // ==========================================
        // 🎨 Web版と同じ配色
        // ==========================================
        private static readonly Color Bg = Color.FromArgb(10, 15, 28);
        private static readonly Color Surface = Color.FromArgb(17, 26, 46);
        private static readonly Color Panel2 = Color.FromArgb(24, 38, 66);
        private static readonly Color TextColor = Color.FromArgb(232, 238, 252);
        private static readonly Color Muted = Color.FromArgb(157, 177, 216);
        private static readonly Color Accent = Color.FromArgb(106, 141, 255);
        private static readonly Color Accent2 = Color.FromArgb(45, 212, 191);
        private static readonly Color DoneColor = Color.FromArgb(74, 222, 128);
        private static readonly Color CurrentBg = Color.FromArgb(29, 51, 85);
        private static readonly Color RestColor = Color.FromArgb(113, 134, 160);
        private static readonly Color PartialColor = Color.FromArgb(45, 212, 191);

        // ==========================================
        // 🖥️ 画面部品
        // ==========================================
        private readonly Panel homePanel;
        private readonly Panel gamePanel;
        private readonly Panel resultPanel;
        private readonly Label best60Label;
        private readonly Label bestTaLabel;
        private readonly Label hudLabel;
        private readonly Label gameNote;
        private readonly Panel sentencePanel;
        private readonly Label resultTitle;
        private readonly Label newBestLabel;
        private readonly Label resScore;
        private readonly Label resChars;
        private readonly Label resCpm;
        private readonly Label resWpm;
        private readonly Label resAcc;
        private readonly Label resMiss;
        private readonly Label resTime;
        private readonly System.Windows.Forms.Timer timer;
        private readonly Font kanaFont = new Font("Yu Gothic UI", 24F);
        private readonly Font romaFont = new Font("Consolas", 15F);

        // ==========================================
        // 🎮 ゲーム状態（Web版の G に対応）
        // ==========================================
        private string mode = ""; // "60" | "ta"
        private List<Unit> units = new List<Unit>();
        private string typed = "";
        private readonly Queue<int> queue = new Queue<int>();
        private bool started;
        private bool finished;
        private readonly Stopwatch stopwatch = new Stopwatch();
        private int totalCorrect;
        private int totalKeys;
        private int miss;
        private int sentencesDone;
        private long missFlashUntil;
        private (string mode, int score, int chars, int cpm, int wpm, double acc, int miss, double time)? lastResult;

        public MainForm()
        {
            Text = "タイピング道場 for Windows";
            ClientSize = new Size(880, 560);
            MinimumSize = new Size(896, 599);
            BackColor = Bg;
            ForeColor = TextColor;
            Font = new Font("Yu Gothic UI", 10F);
            KeyPreview = true;
            KeyPress += OnFormKeyPress;

            // ------------------------------------------
            // 🏠 ホーム画面
            // ------------------------------------------
            homePanel = MakeScreenPanel();
            var homeTitle = new Label
            {
                Text = "タイピング道場",
                Font = new Font("Yu Gothic UI", 26F, FontStyle.Bold),
                ForeColor = TextColor,
                Location = new Point(40, 32),
                AutoSize = true
            };
            homePanel.Controls.Add(homeTitle);
            var homeSub = new Label
            {
                Text = "ひらがなのお題文をローマ字で打つタイピングゲームです。",
                ForeColor = Muted,
                Location = new Point(40, 92),
                AutoSize = true
            };
            homePanel.Controls.Add(homeSub);
            var modeLabel = new Label { Text = "モードを選ぶ", ForeColor = Muted, Location = new Point(40, 136), AutoSize = true };
            homePanel.Controls.Add(modeLabel);

            var btn60 = MakeAccentButton("60秒チャレンジ", 40, 166, 320, 52);
            btn60.Click += (s, e) => StartGame("60");
            homePanel.Controls.Add(btn60);
            var note60 = new Label
            {
                Text = "制限時間60秒でお題文を次々に打ちます。スコア = 正しく打てたローマ字数 × 正確率。",
                ForeColor = Muted,
                Location = new Point(40, 226),
                AutoSize = true
            };
            homePanel.Controls.Add(note60);

            var btnTa = MakeAccentButton("1文タイムアタック", 40, 262, 320, 52);
            btnTa.Click += (s, e) => StartGame("ta");
            homePanel.Controls.Add(btnTa);
            var noteTa = new Label
            {
                Text = "ランダムな1文を打ち切るまでのタイムを計測します。",
                ForeColor = Muted,
                Location = new Point(40, 322),
                AutoSize = true
            };
            homePanel.Controls.Add(noteTa);

            var bestLabel = new Label { Text = "自己ベスト", ForeColor = Muted, Location = new Point(40, 376), AutoSize = true };
            homePanel.Controls.Add(bestLabel);
            best60Label = new Label { Text = "", ForeColor = TextColor, Location = new Point(40, 404), AutoSize = true };
            bestTaLabel = new Label { Text = "", ForeColor = TextColor, Location = new Point(40, 434), AutoSize = true };
            homePanel.Controls.Add(best60Label);
            homePanel.Controls.Add(bestTaLabel);
            Controls.Add(homePanel);

            // ------------------------------------------
            // 🎮 ゲーム画面
            // ------------------------------------------
            gamePanel = MakeScreenPanel();
            hudLabel = new Label
            {
                Text = "",
                Font = new Font("Yu Gothic UI", 13F, FontStyle.Bold),
                ForeColor = TextColor,
                Location = new Point(40, 24),
                AutoSize = true
            };
            gamePanel.Controls.Add(hudLabel);

            sentencePanel = new Panel
            {
                Location = new Point(40, 76),
                Size = new Size(800, 150),
                BackColor = Surface,
                BorderStyle = BorderStyle.FixedSingle
            };
            sentencePanel.Paint += OnSentencePaint;
            sentencePanel.Click += (s, e) => Focus();
            gamePanel.Controls.Add(sentencePanel);

            gameNote = new Label { Text = "", ForeColor = Muted, Location = new Point(40, 242), AutoSize = true };
            gamePanel.Controls.Add(gameNote);

            var quitBtn = new Button
            {
                Text = "やめる",
                BackColor = Surface,
                ForeColor = TextColor,
                FlatStyle = FlatStyle.Flat,
                Location = new Point(40, 282),
                Size = new Size(120, 34),
                TabStop = false
            };
            quitBtn.Click += (s, e) =>
            {
                timer.Stop();
                stopwatch.Stop();
                finished = true;
                RenderBest();
                ShowScreen(homePanel);
            };
            gamePanel.Controls.Add(quitBtn);
            Controls.Add(gamePanel);

            // ------------------------------------------
            // 🏆 結果画面
            // ------------------------------------------
            resultPanel = MakeScreenPanel();
            resultTitle = new Label
            {
                Text = "",
                Font = new Font("Yu Gothic UI", 20F, FontStyle.Bold),
                ForeColor = TextColor,
                Location = new Point(40, 32),
                AutoSize = true
            };
            resultPanel.Controls.Add(resultTitle);
            newBestLabel = new Label
            {
                Text = "自己ベスト更新！",
                Font = new Font("Yu Gothic UI", 12F, FontStyle.Bold),
                ForeColor = Accent2,
                Location = new Point(40, 82),
                AutoSize = true,
                Visible = false
            };
            resultPanel.Controls.Add(newBestLabel);

            resScore = AddResultRow(resultPanel, "スコア", 130);
            resChars = AddResultRow(resultPanel, "正しく打った文字", 168);
            resCpm = AddResultRow(resultPanel, "CPM（1分あたり文字数）", 206);
            resWpm = AddResultRow(resultPanel, "WPM（1分あたり単語数）", 244);
            resAcc = AddResultRow(resultPanel, "正確率", 282);
            resMiss = AddResultRow(resultPanel, "ミス", 320);
            resTime = AddResultRow(resultPanel, "タイム", 358);

            var cardBtn = MakeAccentButton("スコアカードを保存", 40, 424, 220, 40);
            cardBtn.Click += (s, e) => SaveScoreCard();
            resultPanel.Controls.Add(cardBtn);
            var retryBtn = new Button
            {
                Text = "もう一度",
                BackColor = Surface,
                ForeColor = TextColor,
                FlatStyle = FlatStyle.Flat,
                Location = new Point(276, 424),
                Size = new Size(140, 40)
            };
            retryBtn.Click += (s, e) => StartGame(mode);
            resultPanel.Controls.Add(retryBtn);
            var homeBtn = new Button
            {
                Text = "モード選択へ",
                BackColor = Surface,
                ForeColor = TextColor,
                FlatStyle = FlatStyle.Flat,
                Location = new Point(432, 424),
                Size = new Size(160, 40)
            };
            homeBtn.Click += (s, e) => { RenderBest(); ShowScreen(homePanel); };
            resultPanel.Controls.Add(homeBtn);
            Controls.Add(resultPanel);

            timer = new System.Windows.Forms.Timer { Interval = 100 };
            timer.Tick += OnTimerTick;

            RenderBest();
            ShowScreen(homePanel);
        }

        private Panel MakeScreenPanel()
        {
            return new Panel { Dock = DockStyle.Fill, BackColor = Bg, Visible = false };
        }

        private Button MakeAccentButton(string text, int x, int y, int width, int height)
        {
            var btn = new Button
            {
                Text = text,
                Font = new Font("Yu Gothic UI", 12F, FontStyle.Bold),
                BackColor = Accent,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Location = new Point(x, y),
                Size = new Size(width, height)
            };
            btn.FlatAppearance.BorderSize = 0;
            return btn;
        }

        private Label AddResultRow(Panel parent, string label, int y)
        {
            var name = new Label { Text = label, ForeColor = Muted, Location = new Point(40, y), AutoSize = true };
            parent.Controls.Add(name);
            var value = new Label
            {
                Text = "",
                Font = new Font("Yu Gothic UI", 12F, FontStyle.Bold),
                ForeColor = TextColor,
                Location = new Point(320, y - 2),
                AutoSize = true
            };
            parent.Controls.Add(value);
            return value;
        }

        private void ShowScreen(Panel target)
        {
            homePanel.Visible = target == homePanel;
            gamePanel.Visible = target == gamePanel;
            resultPanel.Visible = target == resultPanel;
        }

        // ==========================================
        // 🏆 ベスト記録の読み書き
        // ==========================================
        private static string BestFilePath()
        {
            string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Taipingu");
            return Path.Combine(dir, "best.json");
        }

        private static BestData LoadBest()
        {
            try
            {
                string path = BestFilePath();
                if (File.Exists(path))
                {
                    var data = JsonSerializer.Deserialize<BestData>(File.ReadAllText(path));
                    if (data != null) return data;
                }
            }
            catch (Exception) { /* 読めない場合は記録なしとして扱う */ }
            return new BestData();
        }

        private static void SaveBest(BestData data)
        {
            try
            {
                string path = BestFilePath();
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllText(path, JsonSerializer.Serialize(data));
            }
            catch (Exception) { /* 保存できなくてもゲームは続行する */ }
        }

        private static string FormatAcc(double acc)
        {
            return acc == Math.Floor(acc) ? ((int)acc).ToString() : acc.ToString("0.0");
        }

        private void RenderBest()
        {
            var b = LoadBest();
            best60Label.Text = "60秒チャレンジ: " + (b.S60 != null
                ? $"{b.S60.Score} 点（{b.S60.Cpm} CPM / 正確率 {FormatAcc(b.S60.Acc)}%）"
                : "まだ記録なし");
            bestTaLabel.Text = "タイムアタック: " + (b.Ta != null
                ? $"{b.Ta.Time:F1} 秒（{b.Ta.Cpm} CPM / 正確率 {FormatAcc(b.Ta.Acc)}%）"
                : "まだ記録なし");
        }

        // ==========================================
        // 🎮 ゲーム進行（Web版の移植）
        // ==========================================
        private double Accuracy() => TypingEngine.Accuracy(totalCorrect, totalKeys);

        private void ShuffleQueue()
        {
            var idx = new List<int>();
            for (int i = 0; i < TypingEngine.Sentences.Length; i++) idx.Add(i);
            var rng = new Random();
            for (int i = idx.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (idx[i], idx[j]) = (idx[j], idx[i]);
            }
            foreach (var i in idx) queue.Enqueue(i);
        }

        private void StartGame(string newMode)
        {
            mode = newMode;
            queue.Clear();
            ShuffleQueue();
            typed = "";
            started = false;
            finished = false;
            stopwatch.Reset();
            timer.Stop();
            totalCorrect = 0;
            totalKeys = 0;
            miss = 0;
            sentencesDone = 0;
            NextSentence();
            ShowScreen(gamePanel);
            gameNote.Text = mode == "60"
                ? "最初のキーを押すと60秒カウントが始まります。"
                : "最初のキーを押すと計測が始まります。1文を打ち切ろう。";
            UpdateHud(0);
            Focus();
        }

        private void NextSentence()
        {
            if (queue.Count == 0) ShuffleQueue();
            string s = TypingEngine.Sentences[queue.Dequeue()];
            units = TypingEngine.Tokenize(s);
            typed = "";
            sentencePanel.Invalidate();
        }

        private void UpdateHud(double elapsedSec)
        {
            string timeText = mode == "60"
                ? $"残り時間 {Math.Max(0, 60 - elapsedSec):F1} 秒"
                : $"タイム {elapsedSec:F1} 秒";
            hudLabel.Text = $"{timeText}　正解 {totalCorrect}　ミス {miss}　正確率 {FormatAcc(Accuracy())}%";
        }

        private void StartTimerIfNeeded()
        {
            if (started) return;
            started = true;
            stopwatch.Restart();
            timer.Start();
        }

        private void OnTimerTick(object? sender, EventArgs e)
        {
            if (!started || finished) return;
            double elapsed = stopwatch.Elapsed.TotalSeconds;
            UpdateHud(elapsed);
            if (missFlashUntil != 0 && Environment.TickCount64 > missFlashUntil)
            {
                missFlashUntil = 0;
                sentencePanel.Invalidate();
            }
            if (mode == "60" && elapsed >= 60) FinishGame(60);
        }

        private void OnFormKeyPress(object? sender, KeyPressEventArgs e)
        {
            if (!gamePanel.Visible || finished) return;
            char c = char.ToLowerInvariant(e.KeyChar);
            if ((c >= 'a' && c <= 'z') || c == '-')
            {
                HandleChar(c);
                e.Handled = true;
            }
        }

        private void HandleChar(char ch)
        {
            if (finished) return;
            StartTimerIfNeeded();
            totalKeys += 1;
            string candidate = typed + ch;
            var m = TypingEngine.MatchProgress(units, candidate);
            if (m.Valid)
            {
                typed = candidate;
                totalCorrect += 1;
                sentencePanel.Invalidate();
                if (m.Complete)
                {
                    sentencesDone += 1;
                    if (mode == "ta")
                    {
                        FinishGame(stopwatch.Elapsed.TotalSeconds);
                        return;
                    }
                    NextSentence();
                }
            }
            else
            {
                miss += 1;
                missFlashUntil = Environment.TickCount64 + 150;
                sentencePanel.Invalidate();
            }
            UpdateHud(started ? stopwatch.Elapsed.TotalSeconds : 0);
        }

        private void FinishGame(double elapsedSec)
        {
            if (finished) return;
            finished = true;
            timer.Stop();
            stopwatch.Stop();
            double acc = Accuracy();
            int cpm = TypingEngine.Cpm(totalCorrect, elapsedSec);
            int wpm = TypingEngine.Wpm(cpm);
            int score = TypingEngine.ScoreFor(mode, totalCorrect, acc, cpm);

            bool isNewBest = false;
            var best = LoadBest();
            if (mode == "60")
            {
                if (best.S60 == null || score > best.S60.Score)
                {
                    best.S60 = new BestEntry { Score = score, Cpm = cpm, Acc = acc };
                    isNewBest = true;
                }
            }
            else
            {
                if (best.Ta == null || elapsedSec < best.Ta.Time)
                {
                    best.Ta = new BestEntry { Time = elapsedSec, Cpm = cpm, Acc = acc };
                    isNewBest = true;
                }
            }
            if (isNewBest) SaveBest(best);

            lastResult = (mode, score, totalCorrect, cpm, wpm, acc, miss, elapsedSec);

            resultTitle.Text = mode == "60" ? "60秒チャレンジ 結果" : "タイムアタック 結果";
            newBestLabel.Visible = isNewBest;
            resScore.Text = mode == "60" ? $"{score} 点" : $"―（参考CPM {cpm}）";
            resChars.Text = $"{totalCorrect} 文字";
            resCpm.Text = $"{cpm}";
            resWpm.Text = $"{wpm}";
            resAcc.Text = $"{FormatAcc(acc)}%";
            resMiss.Text = $"{miss} 回";
            resTime.Text = mode == "60" ? $"60.0 秒（{sentencesDone} 文完走）" : $"{elapsedSec:F1} 秒";
            RenderBest();
            ShowScreen(resultPanel);
        }

        // ==========================================
        // 📝 お題文の描画（かな＋ローマ字を色分け）
        // ==========================================
        private void OnSentencePaint(object? sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            if (units.Count == 0) return;
            var m = TypingEngine.MatchProgress(units, typed);
            var flags = TextFormatFlags.NoPadding | TextFormatFlags.SingleLine;

            // 🔤 かな行
            int x = 14;
            int yKana = 16;
            for (int i = 0; i < units.Count; i++)
            {
                var u = units[i];
                var size = TextRenderer.MeasureText(g, u.Kana, kanaFont, Size.Empty, flags);
                Color color = i < m.UnitsDone ? DoneColor : (i == m.UnitsDone ? TextColor : RestColor);
                if (i == m.UnitsDone)
                {
                    using var bg = new SolidBrush(CurrentBg);
                    g.FillRectangle(bg, x - 2, yKana, size.Width + 4, size.Height);
                }
                TextRenderer.DrawText(g, u.Kana, kanaFont, new Point(x, yKana), color, flags);
                x += size.Width;
            }

            // 🔤 ローマ字行（標準表記ベース）
            x = 14;
            int yRoma = 84;
            for (int i = 0; i < units.Count; i++)
            {
                var u = units[i];
                if (i < m.UnitsDone)
                {
                    var size = TextRenderer.MeasureText(g, u.Canonical, romaFont, Size.Empty, flags);
                    TextRenderer.DrawText(g, u.Canonical, romaFont, new Point(x, yRoma), DoneColor, flags);
                    x += size.Width;
                }
                else if (i == m.UnitsDone)
                {
                    var full = TextRenderer.MeasureText(g, u.Canonical, romaFont, Size.Empty, flags);
                    using (var bg = new SolidBrush(CurrentBg))
                    {
                        g.FillRectangle(bg, x - 2, yRoma, full.Width + 4, full.Height);
                    }
                    if (m.Partial > 0)
                    {
                        string donePart = u.Canonical.Substring(0, m.Partial);
                        var psize = TextRenderer.MeasureText(g, donePart, romaFont, Size.Empty, flags);
                        TextRenderer.DrawText(g, donePart, romaFont, new Point(x, yRoma), PartialColor, flags);
                        string restPart = u.Canonical.Substring(m.Partial);
                        TextRenderer.DrawText(g, restPart, romaFont, new Point(x + psize.Width, yRoma), TextColor, flags);
                    }
                    else
                    {
                        TextRenderer.DrawText(g, u.Canonical, romaFont, new Point(x, yRoma), TextColor, flags);
                    }
                    x += full.Width;
                }
                else
                {
                    var size = TextRenderer.MeasureText(g, u.Canonical, romaFont, Size.Empty, flags);
                    TextRenderer.DrawText(g, u.Canonical, romaFont, new Point(x, yRoma), RestColor, flags);
                    x += size.Width;
                }
            }

            // 🔴 ミス時の赤フラッシュ（Web版の入力欄フラッシュ相当）
            if (missFlashUntil != 0 && Environment.TickCount64 <= missFlashUntil)
            {
                using var overlay = new SolidBrush(Color.FromArgb(50, 255, 70, 70));
                g.FillRectangle(overlay, sentencePanel.ClientRectangle);
            }
        }

        // ==========================================
        // 🖼️ スコアカード（Web版と同じ 1200×630 PNG）
        // ==========================================
        private void SaveScoreCard()
        {
            if (lastResult == null) return;
            var r = lastResult.Value;
            using var bmp = new Bitmap(1200, 630);
            using (var g = Graphics.FromImage(bmp))
            {
                using (var grad = new LinearGradientBrush(new Point(0, 0), new Point(1200, 630),
                    Color.FromArgb(79, 70, 229), Color.FromArgb(124, 58, 237)))
                {
                    g.FillRectangle(grad, 0, 0, 1200, 630);
                }
                using (var circle = new SolidBrush(Color.FromArgb(31, 255, 255, 255)))
                {
                    g.FillEllipse(circle, 1080 - 190, 90 - 190, 380, 380);
                    g.FillEllipse(circle, 90 - 140, 570 - 140, 280, 280);
                }
                using var white = new SolidBrush(Color.White);
                using var whiteDim = new SolidBrush(Color.FromArgb(217, 255, 255, 255));
                using (var fTitle = new Font("Yu Gothic UI", 54F, FontStyle.Bold, GraphicsUnit.Pixel))
                using (var fMode = new Font("Yu Gothic UI", 34F, FontStyle.Regular, GraphicsUnit.Pixel))
                using (var fMain = new Font("Yu Gothic UI", 150F, FontStyle.Bold, GraphicsUnit.Pixel))
                using (var fUnit = new Font("Yu Gothic UI", 56F, FontStyle.Bold, GraphicsUnit.Pixel))
                using (var fSub = new Font("Yu Gothic UI", 40F, FontStyle.Regular, GraphicsUnit.Pixel))
                using (var fDate = new Font("Yu Gothic UI", 30F, FontStyle.Regular, GraphicsUnit.Pixel))
                {
                    g.DrawString("タイピング道場", fTitle, white, 70, 56);
                    g.DrawString(r.mode == "60" ? "60秒チャレンジ" : "1文タイムアタック", fMode, white, 70, 136);
                    string mainVal = r.mode == "60" ? $"{r.score}" : $"{r.time:F1}";
                    string mainUnit = r.mode == "60" ? " 点" : " 秒";
                    g.DrawString(mainVal, fMain, white, 70, 200);
                    float w = g.MeasureString(mainVal, fMain).Width;
                    g.DrawString(mainUnit, fUnit, white, 70 + w + 10, 294);
                    g.DrawString($"CPM {r.cpm} / WPM {r.wpm}", fSub, white, 70, 410);
                    g.DrawString($"正確率 {FormatAcc(r.acc)}% ・ ミス {r.miss} 回 ・ {r.chars} 文字", fSub, white, 70, 472);
                    g.DrawString(DateTime.Now.ToString("yyyy/MM/dd"), fDate, whiteDim, 70, 550);
                }
            }
            using var dialog = new SaveFileDialog
            {
                Filter = "PNG 画像|*.png",
                FileName = "taipingu-score.png",
                DefaultExt = "png"
            };
            if (dialog.ShowDialog(this) != DialogResult.OK) return;
            bmp.Save(dialog.FileName, ImageFormat.Png);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                timer.Dispose();
                kanaFont.Dispose();
                romaFont.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
