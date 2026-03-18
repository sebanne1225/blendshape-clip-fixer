BlendShape Clip Fixer
=====================

この zip は、BlendShape Clip Fixer の BOOTH 配布用案内ファイルです。

この zip の中に Unity package 本体は入っていません。
導入は VCC / VPM から行ってください。

最初に読む順番
--------------
1. この `00_README_FIRST.txt`
2. `01_VCC_INSTALL.txt`
3. `02_QUICKSTART.txt`

ツール概要
----------
AnimatorController 内の Missing BlendShape カーブを修正し、
修正版の AnimatorController と AnimationClip を非破壊で生成する
Unity Editor ツールです。

導入先
------
- VPM Repository URL
  https://sebanne1225.github.io/sebanne-listing/index.json

- Unity メニュー
  Tools/Sebanne/BlendShape Clip Fixer

主な注意点
----------
- 元の AnimatorController や AnimationClip は直接変更しません
- 対象 SkinnedMeshRenderer が未設定だと、一部の判定や置換候補確認が使えません
- まずスキャン結果を確認してから生成してください
- package 本体の導入は VCC / VPM 前提です

補足
----
- 手動 import 用の unitypackage や package zip は、この zip には同梱していません
- ライセンスは `LICENSE` を参照してください
