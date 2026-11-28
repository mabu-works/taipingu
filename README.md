# タイピング道場（taipingu）

ひらがなのお題文をローマ字で打つ、日本語タイピングゲームです。ビルド不要の静的サイトで、ブラウザだけで動きます。

| 項目 | 内容 |
|---|---|
| バージョン | 1.0.0 |
| 状態 | 公開中 |
| 公開先 | [taipingu.vercel.app](https://taipingu.vercel.app) |
| 実装 | 静的 Web サイト（素の HTML / CSS / JavaScript、ビルド不要）＋ Windows 版（.NET 8 / WinForms） |
| 更新履歴 | [CHANGELOG.md](CHANGELOG.md) |

## ✨ 機能

- **60秒チャレンジ**: 制限時間60秒でお題文を次々に打ちます。スコア = 正しく打てたローマ字数 × 正確率。
- **1文タイムアタック**: ランダムな1文を打ち切るまでのタイムを計測します。
- **表記ゆれ対応**: し=shi/si、ち=chi/ti、つ=tsu/tu、ふ=fu/hu、じ=ji/zi、しゃ=sya/sha、ちゃ=cha/tya など主要な別表記を許容。「ん」は nn / n（次の文字が子音の場合）両対応、「っ」は子音の重ね打ち（zutto）や xtu 表記に対応。
- **色分け表示**: 入力済み（緑）／現在（ハイライト）／残り（グレー）を、かな表示とローマ字表示の両方で色分け。
- **結果表示**: スコア、打てた文字数、CPM（1分あたり文字数）、WPM（1分あたり単語数、1単語=5文字換算）、正確率、ミス数、タイム。
- **ベストスコア保存**: モード別に localStorage へ保存し、トップ画面に表示。
- **スコアカード**: 結果を Canvas で 1200×630 の PNG 画像にしてダウンロード可能。
- お題文はひらがなのみ34文。

## 📝 使い方

1. `index.html` をブラウザで開く（または静的ホスティングに配置する）。
2. モードを選ぶとゲーム画面になります。入力欄にフォーカスが当たるので、そのままキーボードで打ち始めます（最初のキーで計測開始）。
3. スマートフォンでは入力欄をタップしてソフトウェアキーボードを表示し、英字モードで入力してください。
4. 結果画面の「スコアカードを保存」で結果画像をダウンロードできます。

## 🪟 Windows 版

`windows/` フォルダに、同じ仕様の Windows アプリ（.NET 8 / WinForms）があります。お題文34文、ローマ字判定（表記ゆれ・「ん」「っ」の処理）、2つのモードと計測方式（CPM / WPM / 正確率、スコア計算）は Web 版と同一で、スコアカード（1200×630 PNG）の保存もできます。

- ビルド: `windows/build.bat`（Windows）または `windows/build.sh` で `windows/dist/Taipingu.exe` を生成（exe は Git 管理外）
- 自己ベストは Web 版の localStorage の代わりに、ユーザーごとのアプリデータフォルダー（`%AppData%\Taipingu\best.json`）へ保存します
- ローマ字判定エンジン（`windows/TypingEngine.cs`）は `windows/tests/` のコンソールテストで検証できます（`dotnet run --project windows/tests`）

## 📋 技術メモ

- 素の HTML/CSS/JS のみ。外部リソース・ライブラリ不使用、フォントはシステムの日本語フォントスタック。
- ローマ字判定は「かなユニットごとの候補表 + 深さ優先マッチ」で実装。入力文字列全体を再評価し、最も進んだ解釈を採用するため、「ん」の曖昧さ（n か nn か）も文脈で吸収します。純ロジックは `js/typing-core.js` に分離し、`npm test`（Node 標準のテストランナー）で検証します。
- PWA 対応: `manifest.webmanifest` と Service Worker（`sw.js`、キャッシュ名 `taipingu-v1`、同一オリジンGETをキャッシュ）でオフラインでも動作し、ホーム画面に追加できます。
- テーマはダーク既定で、右上のボタンからライトへ手動切り替え（設定は localStorage の `taipingu-theme` に保存）。
- スコアは端末の localStorage にのみ保存され、外部送信はしません。

## 📁 ファイル構成

```
index.html            画面（モード選択／ゲーム／結果）
style.css             スタイル（モバイルファースト、テーマ切替対応）
js/typing-core.js     ローマ字判定・お題文・スコア計算の純ロジック（DOM 非依存）
app.js                画面遷移・入力・タイマー・結果表示・スコアカード
sw.js                 Service Worker
manifest.webmanifest  PWAマニフェスト
icons/                アイコン（192 / 512）
tests/                typing-core のユニットテスト（node:test）
windows/              Windows 版（.NET 8 / WinForms、判定エンジン＋テスト付き）
```
