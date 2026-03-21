# BlendShape Clip Fixer

## 概要

`BlendShape Clip Fixer` は、`AnimatorController` 内の Missing `BlendShape` カーブや renderer path のズレを見つけて、修正版の `AnimatorController` と `AnimationClip` を非破壊で生成する Unity Editor ツールです。

表情用 `AnimatorController` を別アバターへ移した時や、参照先メッシュの構成が変わって `BlendShape` カーブが missing になった時の修正を想定しています。

元の `AnimatorController` と `AnimationClip` は直接変更せず、生成物だけを出力する運用を前提にしています。

## 何ができるか

- `AnimatorController` 内で使われている `AnimationClip` をまとめて走査できます
- 選択した `SkinnedMeshRenderer` を基準に、missing `BlendShape` 名を一覧化できます
- `Missing / Replace Map` で旧名から新名への置換先を指定できます
- 必要なら `BlendShape` カーブの path を対象 renderer 側へ寄せて再生成できます
- 元 asset を壊さず、修正版 Controller と Clip を新規生成できます

## 現在の対応範囲

- Editor 拡張主体の package です
- 対象は `AnimatorController` が参照する `AnimationClip` の `BlendShape` カーブです
- 生成先は `Assets/BlendshapeClipFixer_Output/<ControllerName>_Fixed/` が既定です
- `Clean output before generate` を有効にすると、同じ出力先を整理してから再生成できます
- Runtime は現時点では予約領域で、実装の中心は `Editor/` にあります

## VCC / VPM での導入

1. VCC に追加する URL として `https://sebanne1225.github.io/sebanne-listing/index.json` を追加します。
2. package 一覧から `BlendShape Clip Fixer` (`com.sebanne.blendshape-clip-fixer`) を追加します。
3. Unity を開き、package が導入されていることを確認します。

参考ページ (`VCC` 追加先ではありません): `https://sebanne1225.github.io/sebanne-listing/`

## 使い方

1. Unity 上部メニューの `Tools/Sebanne/BlendShape Clip Fixer` を開きます。
2. `Source Controller` に修正したい `AnimatorController` を指定します。
3. 必要に応じて `Target Avatar Root` と `Target SkinnedMeshRenderer` を指定します。
4. `Scan Controller` を押して、missing blendShape 名や path mismatch を確認します。
5. `Missing / Replace Map` を埋めて、必要なら自動マップや候補絞り込みを使います。
6. 問題がなければ `Generate Fixed Assets` を押して修正版 asset を生成します。

## Scan / 診断

- `Scan Controller` では、対象 `AnimatorController` 内の clip 数、`BlendShape` バインディング数、missing 名、path mismatch を確認できます
- `Missing / Replace Map` では、旧名から新名への置換先を候補付きで指定できます
- 失敗や未解決項目がある場合は、生成結果ダイアログと Unity Console ログから追跡できます

## 出力

既定の出力先は次のとおりです。

`Assets/BlendshapeClipFixer_Output/<ControllerName>_Fixed/`

- `<ControllerName>_Fixed.controller`
- `Clips/*.anim`

## 制限事項

- 表情構成は、主に 1 つの `SkinnedMeshRenderer` に集約されている前提を想定しています
- missing が多いのに path mismatch が少ない場合は、命名規則のズレを手動で埋める必要があります
- Runtime API を提供する package ではなく、Unity Editor 内で使う補助ツール寄りの構成です

## Release Asset

GitHub Release には、VPM 配布確認や手動保管に使える package zip を添付する想定です。

- 例: `com.sebanne.blendshape-clip-fixer-1.0.2.zip`

## ライセンス

MIT License です。詳細は `LICENSE` を参照してください。
