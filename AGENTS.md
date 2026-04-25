# AGENTS.md

この repo は `BlendShape Clip Fixer` の Unity package です。作業時は以下を守ってください。

## 開発ルール

- 元の `AnimatorController` や `AnimationClip` を直接壊さず、修正版 asset の新規生成を前提にする。
- missing 名の解決と path 修正は、利用者が途中状態を把握できる UI とログを優先する。
- `Editor` 実装は `UI / Core / Utility / Diagnostics` の責務分離を崩しすぎない。
- Runtime に Editor 依存コードを混ぜない。
- repo の公開向け文書は `README.md`、補助情報は `TOOL_INFO.md`、開発メモは `CLAUDE.md` を使い分ける。
- repo root を package 本体として扱い、実装や文書の source of truth を分散させない。
