# Changelog

このファイルは `BlendShape Clip Fixer` の変更履歴を管理します。

## [Unreleased]

## [1.0.2] - 2026-03-21

### Changed

- README の VCC / VPM 導入手順を `source.json` ベースの案内に整理
- BOOTH 同梱テキストの案内文を公開向けに調整
- release asset や公開導線の文言を現在の運用に合わせて更新

## [1.0.1] - 2026-03-18

### Changed

- 公開名を `BlendShape Clip Fixer` に統一し、`README.md`、`TOOL_INFO.md`、package metadata の公開向け表現を整理した
- UI 文言、HelpBox、ログ prefix などの表示を見直し、修正対象や途中状態を把握しやすい形に調整した
- repo root を package 本体として扱う構成に整理し、公開向け文書と metadata の source of truth を root に統一した

### Notes

- 実装機能の追加は行わず、公開面と使い勝手の改善をまとめた patch release とした

## [1.0.0] - 2026-03-18

### Added

- `AnimatorController` 内の `AnimationClip` を走査し、missing blendShape 名や path mismatch を確認できる EditorWindow を追加
- `Missing / Replace Map` による置換指定と、修正版 Controller / Clip の非破壊生成を追加
- 既定出力先 `Assets/BlendshapeClipFixer_Output/<ControllerName>_Fixed/` への生成フローを追加
