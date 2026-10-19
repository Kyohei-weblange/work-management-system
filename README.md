# 社内勤務管理システム (Work Management System)

VB.NET (.NET 8) Minimal API と SQLite、および Vanilla JS (HTML/CSS) で構築された、社内勤務データ管理・工事番号別集計・出退勤打刻連携ウェブアプリケーションです。

---

## 📋 概要・開発目的

本システムは、日々の勤務実績（出退勤時刻、工事番号、残業時間、備考）の登録・管理、および工事番号ごとの月間集計を行う社内向けプロトタイプシステムです。
外部出退勤アプリからの自動打刻 API 連携機能や、Excel/CSV 形式での集計データ出力機能を備えています。

---

## ✨ 実装済み機能一覧

### 1. 認証機能
- ユーザーログイン・ログアウト処理 (`POST /api/login`)
- セッション擬似管理（フロントエンド側でのユーザー状態保持）

### 2. マスタ管理機能
- 工事番号マスタ一覧取得 (`GET /api/constructions`)
- 有効な工事番号のドロップダウン動的生成

### 3. 勤務データ管理機能
- 日次勤務実績登録 (`POST /api/work-records`)
  - 出退勤時間、残業時間、工事番号、備考の保持
- 前月データ一括コピー機能 (`POST /api/work-records/copy-previous-month`)

### 4. 月間集計・グラフ表示機能
- 月別勤務サマリー取得 (`GET /api/work-records/summary`)
  - 出勤日数、総残業時間の集計
  - 工事番号ごとの「総労働時間（定時実働＋残業）」および「内残業時間」の二重集計
  - フロントエンドでの工事別内訳テーブル動的描画

### 5. データ出力機能
- 勤務実績データの CSV 出力機能 (`GET /api/work-records/export-csv`)

### 6. 外部連携 API (DAY 6 実装)
- 外部出退勤アプリ/端末向け打刻 API (`POST /api/clock-in-out`)
  - リクエストパラメータ (`userId`, `clockType` [IN/OUT], `timestamp`) に基づく動的処理
  - `SELECT COUNT(*)` による当日の出勤データ存在チェック
  - 出勤 (`IN`) / 退勤 (`OUT`) に応じた `INSERT` / `UPDATE` 自動分岐処理

---

## 🛠 使用技術・動作環境

- **バックエンド:** VB.NET / .NET 8.0 (Minimal API)
- **データベース:** SQLite3 (Microsoft.Data.Sqlite)
- **フロントエンド:** HTML5 / CSS3 / JavaScript (ES6+, Vanilla JS)
- **開発・運用環境:** macOS / Git / GitHub

---

## 🚀 起動・使用方法

### 1. アプリケーションの起動
プロジェクトルートで以下のコマンドを実行します。

```bash
dotnet run
