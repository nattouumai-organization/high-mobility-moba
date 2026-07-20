# Changelog

このファイルには、ゲーム仕様・実装・バランスに関する重要な変更を記録する。

形式：

```text
## YYYY-MM-DD

### Added
- 新規追加

### Changed
- 仕様・数値の変更

### Fixed
- 不具合修正

### Removed
- 削除した要素
```

## 2026-07-21

### Added

- HPと通常攻撃ダメージの基本ステータスを実装。CharacterStatsにBase Max Health / Bonus Max Health(合計がCurrent Max Health、1未満にならない)とBase Attack Damage / Bonus Attack Damage(合計がCurrent Attack Damage、0未満にならない)を追加。
- HealthControllerを追加(Scripts/Combat)。現在HPの管理、被ダメージ、回復用の土台(今回未使用)、HP変化通知、死亡イベントを持つ。CharacterStatsを持つ対象はCurrent Max Healthを、持たない対象はInspectorのMax Healthを最大HPとして使用する。
- WorldHealthBarを追加(Scripts/UI)。PlayerとTrainingDummyの頭上にWorld Space CanvasのHPバーを表示し、HP割合に応じてUI ImageのFill Amountを更新する。バーは常にMain Cameraの方向を向き、裏返らない。背景色でPlayer(暗い青系)とTrainingDummy(暗い赤系)を区別する。
- 通常攻撃のダメージ処理を実装。射程内のターゲットへ攻撃間隔ごとにCurrent Attack Damage(Player初期値20)を即時に与え、既存の被弾フラッシュを発生させる。
- TrainingDummyの死亡処理を実装。HP 0でCollider無効化・選択不可・ターゲット解除・攻撃停止・HPバー非表示となり、短時間死亡状態を表示した後にGameObjectを非表示化する(Destroy不使用でMissing Referenceを防止)。
- Playerの死亡処理を実装(PlayerDeathHandler)。HP 0でPlayerClickMovement / PlayerMouseFacing / PlayerBasicAttackController / CharacterControllerを無効化し、見た目とHPバーを非表示にする。リスポーンは未実装。

### Changed

- 疑似通常攻撃(被弾フラッシュのみ)を、実ダメージを与える通常攻撃へ更新。
- PlayerMouseFacingの回転速度を毎秒720度から毎秒1440度へ変更(2倍)。

## 2026-07-20

### Added

- 通常攻撃のターゲット選択を実装。右クリックでTargetableLayerの対象を選択し、Groundの右クリックで解除する。
- 右クリック入力の優先順位を「ターゲット選択 > Ground移動」とし、対象を右クリックした場合はGroundへ移動しない。
- SC_Prototypeシーンにテスト用ダミーTrainingDummyを1体設置(TargetableLayer / 赤系仮マテリアル)。
- 選択中のダミーに、足元の黄色い選択リングと本体色を明るくする視覚フィードバックを追加。
- 将来の通常攻撃から呼び出す、被弾時に短時間白く点滅する処理をTargetableに用意。
- 通常攻撃の射程判定を実装。TargetableのColliderの最も近い点との水平距離(XZ平面、高さは含めない)がCurrent Attack Range以下なら射程内とする。
- 攻撃速度と攻撃間隔を実装。CharacterStatsにBase Attack Speed(毎秒の攻撃回数)、Bonus Attack Speed Percent、Base Attack Rangeを追加し、Attack Interval = 1 / Current Attack Speedとする。
- PlayerBasicAttackControllerを追加。選択中のターゲットが射程内の場合のみ、攻撃間隔ごとに疑似通常攻撃(被弾フラッシュのみ、ダメージ・HP減少なし)を実行する。
- 選択リングの色で射程内外を表示するよう更新(射程内: 明るい緑 / 射程外: オレンジ)。

### Changed

- 射程外のターゲットを右クリックで選択した場合、その場に留まる仕様から、射程内に入るまでターゲットへ自動接近し、射程内に入ったら停止して疑似通常攻撃を開始する仕様へ変更。
- 攻撃対象を右クリックで選択した際、Playerが進行中の移動を中断してその場で停止するよう変更(通常攻撃時にその場で止まる挙動の先行実装)。
- プレイヤーの向き制御を「常にマウスカーソル方向を向く」から「最後に右クリックした方向を向く」へ変更。

## 2026-07-19

### Added

- 1v1高機動アクションMOBAの初期ゲーム設計を作成。
- 初期キャラクターを5人に決定。
  - ゼルフ
  - 朧
  - ヴォルブラーク
  - リネス
  - リーゼロッテ・ヴァイス
- Windows / Steam向け、基本プレイ無料、CGアニメ調の方針を決定。
- 敵本拠地破壊を勝利条件として決定。
- 1レーン、各陣営タワー1本・本拠地1つのマップ構成を決定。
- ゴールドと経験値を統合したポイント制を決定。
- レベル上限を6とする案を決定。
- 共通Dを、高難度のCCカウンター技として決定。
- Fを初期版ではフラッシュ固定とする案を決定。
- ルーンを4種類の事前選択式とする方針を決定。
- Unity 6 + C# + URPを主な開発環境として決定。
- GitHubでソース管理を行う方針を決定。

### Changed

- 試合時間は約5分を目標とするが、制限時間は設けない方針へ変更。
- ダメージタイプと防御タイプを1種類に統一。
- 通常ダメージはARで軽減する。
- 朧Rの処刑とヴォルブラークRの反射のみを確定ダメージとして扱う。
- リーゼロッテRはHP、AD、AS、AR、MSを一時的に奪う仕様へ決定。
- リーゼロッテRでHPを奪う際、双方の現在HP割合を維持する仕様へ決定。

### Notes

- 数値は初期テスト用であり、プレイテスト後に調整する。
- オンライン正式実装はローカル試作とローカル対戦の後に行う。
