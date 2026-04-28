# CLAUDE

> この文書は開発用のハンドオフ / 設計メモです。エンドユーザー向けの導入説明や使い方は `README.md` を参照してください。

## Goal

- `BlendShape Clip Fixer` を package repo として維持し、`AnimatorController` 内の missing `BlendShape` カーブ修正を非破壊で行える状態を保つ。
- handoff には、初期の repo 整理メモではなく、今どこまで実装済みかと次回再開の判断材料だけを残す。

## Current State

- repo root が package source of truth です。`package.json`、`Editor/`、`Runtime/`、`Documentation~/`、`Samples~/`、`README.md`、`CHANGELOG.md`、release workflow が root に揃っています。
- `package.json` は `1.0.2`、ローカル HEAD は `684f34f` (`main`) です。tag は `1.0.2` まであります。
- 実装の中心は `Editor/UI/BlendshapeClipFixerWindow.cs` です。menu は `Tools/Sebanne/BlendShape Clip Fixer` です。
- Window では `AnimatorController` 走査、target renderer 指定、missing / replace map 編集、scan summary、binding preview、generate まで一通り行えます。
- mapping UI は手動編集に加えて、完全一致 ignore case、自動正規化 / fuzzy、keyword 一括マップ、候補絞り込みを持っています。
- `clipFocusMode` と `generateFocusedClipOnly` があり、1 つの clip に集中して確認しつつ、Generate 時の修正対象も絞れます。
- UI state と mapping は `EditorPrefs` に保存されます。再度 window を開いた時に選択状況へ寄せて復元する実装です。
- Generate は出力サブフォルダを必要ならクリーンし、controller を再コピーしつつ clip を作成 / 再利用して rewiring します。元 asset は直接変更しません。
- `Runtime` は asmdef だけの予約領域で、実装の中心は Editor 側です。
- 公開面は `README.md`、`CHANGELOG.md`、`BOOTH_PACKAGE/`、`.github/workflows/release.yml` に寄せてあります。`CHANGELOG.md` には空の `[Unreleased]` セクションが残っています。

## Current Direction

- repo 内に hard blocker はありません。
- 次に触る時は、公開面 / サンプル整備を進める回か、コード整理や API 分離を進める回かを最初に決めると作業がぶれにくいです。
- 生成フローを触る時は、`cleanOutputBeforeGenerate` と clip 再利用の挙動、focused clip だけ修正する挙動を壊さないのが大事です。
- UI 改修では、スキャン -> マッピング -> 生成の流れと、再起動後の復元導線を維持したいです。

## Current Blocker

- 明確な blocker はありません。
- 依然として Editor ツール主体で、Runtime API は実質ありません。
- ambiguous な mapping は自動で決め打ちせず、人が候補を確定する前提です。

## Rules

- 非破壊
- 元の `AnimatorController` / `AnimationClip` を直接上書きしない
- 出力先は `Assets/` 配下
- まず scan してから map を詰める
- focused clip モードの挙動を壊さない
- まず短い plan を出してから作業

## Key Files

- `Editor/UI/BlendshapeClipFixerWindow.cs`
- `Editor/Core/BlendshapeControllerScanner.cs`
- `Editor/Core/BlendshapeClipFixerGenerator.cs`
- `Editor/Core/AnimatorControllerUtil.cs`
- `Editor/Utility/AssetPathUtil.cs`
- `Editor/Utility/BlendshapeNameUtil.cs`
- `Editor/Diagnostics/BlendshapeClipFixerLog.cs`
- `README.md`
- `.github/workflows/release.yml`

## Resume Notes

- package: `com.sebanne.blendshape-clip-fixer`
- version: `1.0.2`
- latest tag: `1.0.2`
- HEAD: `684f34f` (`main`)
- release asset 名: `com.sebanne.blendshape-clip-fixer-1.0.2.zip`
- 既定出力の親フォルダ: `Assets/BlendshapeClipFixer_Output/`
