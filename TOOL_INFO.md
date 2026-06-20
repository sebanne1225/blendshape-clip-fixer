# TOOL_INFO

このファイルは内部向けの整理メモです。公開向けの説明は `README.md` を優先します。

## 基本情報

- ツール名: `BlendShape Clip Fixer`
- package名: `com.sebanne.blendshape-clip-fixer`
- 表示名: `BlendShape Clip Fixer`
- Runtime asmdef: `Sebanne.BlendshapeClipFixer`
- Editor asmdef: `Sebanne.BlendshapeClipFixer.Editor`
- 現在 version: `1.0.2`

## 想定用途

- 表情用 `AnimatorController` が参照している `AnimationClip` の `BlendShape` カーブを走査し、missing 名や path mismatch を確認して、修正版 asset を非破壊で生成する。

## 現状の構成方針

- package 本体は repo root を使い、`Editor/Core`、`Editor/UI`、`Editor/Diagnostics`、`Editor/Utility` の受け皿で整理する。
- `Editor` 配下は `UI`、`Core`、`Utility`、`Diagnostics` の責務に分ける。
- `Runtime` は現時点では予約領域として置き、再利用型や共通モデルが必要になるまで最小構成にする。

## 現在対応していること

- `AnimatorController` 単位の scan
- `Missing / Replace Map` による置換先の指定
- 対象 renderer path への再バインド
- 修正版 Controller / Clip の非破壊生成

## 非対応

- Runtime API の提供
- 複数 renderer にまたがる複雑な表情構成への最適化
- BOOTH 向け配布物の整備

## 今後やりたいこと

- Diagnostics の拡張とログ粒度の整理
- 公開向け説明と配布導線の最終確認
- `documentationUrl` / `changelogUrl` など公開面の細部調整
