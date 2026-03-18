# CODEX_HANDOFF

> この文書は開発用のハンドオフ / 設計メモです。エンドユーザー向けの導入説明や使い方は `README.md` を参照してください。

## Goal

- `BlendShape Clip Fixer` を package repo として一本化し、root を唯一の source of truth にする。
- 既存機能を壊さず、他の sebanne 系 package repo と近い構成へ寄せる。

## Current State

- もともとの repo は Unity project と listing 用ファイルを内包していたが、現在は root package を本体として扱う。
- root 直下に `package.json`、`Editor/`、`Runtime/`、`Documentation~/`、`Samples~/` を持つ package repo 構成へ整理済み。
- Editor 実装は `UI`、`Core`、`Utility`、`Diagnostics` に寄せて配置している。
- `Runtime` は asmdef のみ置いた予約領域で、現状の実装中心は Editor 側にある。
- legacy embedded package、`Website/`、`.github/workflows/build-listing.yml`、`Assets/`、`Packages/`、`ProjectSettings/` は cleanup 済み。

## Intentionally Deferred

- BOOTH_PACKAGE の整備
- 実装ロジックの本格的な分割や API 整理
- Unity 上での最終コンパイル確認

## Current Buckets

- `Editor/UI`
- `BlendshapeClipFixerWindow.cs`

- `Editor/Core`
- `BlendshapeClipFixerGenerator.cs`
- `BlendshapeControllerScanner.cs`
- `AnimatorControllerUtil.cs`

- `Editor/Utility`
- `AssetPathUtil.cs`
- `BlendshapeNameUtil.cs`

- `Editor/Diagnostics`
- `BlendshapeClipFixerLog.cs`

## Follow-up Candidates

- 公開面をそろえる次段では、README / listing / release asset / BOOTH_PACKAGE の見え方をまとめて確認する
- `package.json` の公開向けメタデータや README 冒頭の温度感を他 repo にさらに寄せる
