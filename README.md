# Blendshape Clip Fixer

Unity Editorツールです。選択した AnimatorController が参照している AnimationClip 内の **blendShape カーブ**をスキャンし、  
Missing になっている blendShape 名を **置換して修正版の Controller / Clip を生成**します。

- 元の AnimatorController / AnimationClip は編集しません（非破壊）
- 生成物だけを出力します（冪等運用向け）

## Requirements

- Unity 2022.3 以降（想定）
- Editor拡張（実行はUnity Editor内のみ）

## Install (VCC / VPM)

VCCで Repository を追加してインストールします。

- VCC: Settings -> Packages -> Add Repository
- index.json のURLを貼り付け

Repository URL:
https://sebanne1225.github.io/sebanne-listing/index.json

## How to use

1. Unityメニューから開く  
   Tools -> Blendshape Clip Fixer

2. 入力を設定する  
   - Source Controller: 修正したい AnimatorController
   - Target Avatar Root: アバターのルート（path修正を使うなら必須）
   - Target SkinnedMeshRenderer: 表情BlendShapeが入っているメッシュ（例: Face/Body）

3. Scan Controller を押す  
   - コントローラー内の全クリップを走査して Missing を一覧表示します

4. Missing / Replace Map を埋める  
   - Missingになっている旧BlendShape名 -> 正しいBlendShape名 に割り当てます

5. Generate Fixed Assets を押す  
   - 修正版の Controller と Clip を生成します

## Output

生成物の既定出力先:

Assets/BlendshapeClipFixer_Output/<ControllerName>_Fixed/

- <ControllerName>_Fixed.controller
- Clips/*.anim

※ Clean output を有効にすると、同じ場所に毎回作り直すのでゴミが増えません。

## Notes

- Path mismatches が 0 なのに Missing が多い場合は、BlendShape名の命名規則が一致していない可能性が高いです  
  -> Replace Map で名前を割り当てて修正してください
- SkinnedMeshRenderer が1つに統一されている表情構成を想定しています

## License

MIT License. See LICENSE.
This tool is provided "as is" without warranty. Use at your own risk.