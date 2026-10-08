# Effectシーンのエフェクト分離

EffectシーンのMain Cameraは、品質設定のRenderer一覧の1番にある
`PC_Effect_Renderer` / `Mobile_Effect_Renderer`を使用します。
ほかのシーンが使う既定Rendererは変更しません。

描画順は「通常オブジェクト → Retro Pixel → Effect Opaque → Effect Transparent」です。
この3パスはBefore Rendering Post Processingに、上記の順序で並べます。
専用Rendererの通常描画からEffectレイヤーを除き、ドット化後に元のマテリアルで描画します。
深度判定は維持するため、遮蔽物の奥にある演出は隠れます。

## エフェクトを追加する場合

- ParticleSystem、TrailRenderer、LineRendererは自動でEffectレイヤーに入ります。
  実行中に生成したもの、非表示のもの、プールから再利用したものも対象です。
- メッシュやSpriteRendererで作った演出は、Hierarchyの「Effect Layer Setup」にある
  「追加のエフェクト」へ親オブジェクトを登録してください。
  強攻撃の表示は登録済みです。
- 新しい演出Prefabの描画オブジェクトに、直接Effectレイヤーを設定する方法も使えます。
- プレイヤー・敵・キュー本体・背景は追加対象に登録しないでください。
  水風船の紐は自動で対象になりますが、別GameObjectの本体はドット化を維持します。

レイヤーはGameObject単位です。モデル本体と演出を同じGameObjectに置かず、
演出用の子オブジェクトを分けてください。
レイヤー番号を限定した独自の当たり判定を追加する場合は、対象マスクも確認してください。

## 再設定と検証

`Tools > Prototype > Setup Effect Layers`でEffectシーンに設定できます。
追加エフェクト欄に手作業で登録した対象は保持します。

検証用コピーで`Prototype.Editor.EffectLayerValidation.BuildAndValidateBatch`を
Unityの`-executeMethod`へ指定すると、PC・Mobileの描画と深度判定を確認します。
Windowsで両方を検証する場合は、検証用コピーのQuality設定だけで
MobileのStandalone除外を解除してください。本体の対象プラットフォーム設定は変更しません。
検証画像は左が通常、右がEffectレイヤー、上段が不透明、下段が透明です。
画像比較中だけドット解像度を64へ下げ、元のマテリアルは変更しません。
