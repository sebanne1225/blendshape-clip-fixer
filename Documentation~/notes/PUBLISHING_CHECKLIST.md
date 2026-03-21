# PUBLISHING_CHECKLIST

## Release 前の確認

- `package.json` の version を更新したか
- `CHANGELOG.md` に今回の差分を追記したか
- `README.md` の導入手順と制限事項が現状と合っているか
- root 直下の `Editor/` `Runtime/` `Documentation~/` `Samples~/` が package zip に入る前提で問題ないか

## Release workflow

- `.github/workflows/release.yml` は root package を zip 化する前提
- release tag と `package.json.version` は一致させる
- `workflow_dispatch` で zip だけ先に確認してもよい

## 配布面の確認

- GitHub Release に version 付き package zip が付いているか
- listing repo 側の見え方は別 repo で確認する
- repo 内の `BOOTH_PACKAGE/` は作業用の固定名で運用する
- BOOTH に貼る最終フォルダ名 / zip 名は `package.json.version` ベースで `BlendShapeClipFixer_Booth_Package_v1.0.2` を推奨する
