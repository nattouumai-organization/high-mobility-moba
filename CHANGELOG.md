# Changelog

このファイルには、ゲーム仕様・実装・バランスに関する重要な変更を記録する。

形式：

```text
## 2026-07-21

### Added

- ゼルフQの対象ブリンク、対象指定ダメージ、同一対象ロック、分類別クールダウン処理を実装。
- ZelfQControllerを追加。Qキーで選択中の有効なCharacter / Minion / TrainingDummyへ、Collider最寄り点を基準に安全な停止距離でブリンクする。
- Qダメージを `Base Damage + Current Attack Damage × AD Ratio` としてHealthController経由で適用する。
- Q成功対象へSame Target Lockoutを設定し、ロック中の同一対象にはブリンク・ダメージ・クールダウン消費を発生させない。
- Character / TrainingDummy分類へのQ命中時はQクールダウンを即時リセットし、Minion分類への命中時は残りクールダウンを50%短縮する。Tower分類には発動しない。
- Qダメージでも既存の被弾フラッシュ、ダメージ表示、ゼルフP回復の通知経路を利用する。

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
- CharacterData ScriptableObjectを追加(Scripts/Characters)。キャラクター固有の固定情報(ID・表示名・役割・説明・テーマカラー・Character Status)、基礎ステータスと成長値、P/Q/W/E/Rのスキル説明を保持する。実行中の現在ステータスは従来どおりCharacterStats / HealthControllerが扱い、SC_PrototypeのPlayerへはまだ適用しない。
- ゼルフのCharacterData(Data/Characters/ZelfData.asset)を追加。基礎ステータス(HP650・HP成長105・HP自動回復3.5/+0.35・AD60/+4.5・AS0.80/+3.0%・AR28/+4.0・MS360・射程200)とP/Q/W/E/Rのスキル説明を設定。
- キャラクター選択画面SC_CharacterSelectを追加。タイトル・サブタイトルと、5キャラクター分のカード(名前・イメージカラー・役割・利用可能状態)を表示する。Availableのゼルフのみ選択可能で、朧・ヴォルブラーク・リネス・リーゼロッテ・ヴァイスはComing Soon表示の半透明・選択不可。選択中のカードは明るい枠線とカードの明度上昇で表示し、画面下部に選択中キャラクター名を表示する。
- ゼルフ詳細パネルを追加。名前・役割・Short Description・イメージカラーの大きな仮プレースホルダー・基礎ステータス(HP/AD/AS/AR/MS/AA Range)・P〜Rの短いスキル一覧を表示する。スキルの詳細説明はCharacterDataが保持し、将来ツールチップや別パネルとして表示できる(今回は未実装)。
- 「プロトタイプを開始」ボタンを追加。選択したCharacterDataをCharacterSelectionManager(DontDestroyOnLoad・二重生成防止)が保持したままSC_Prototypeを読み込む。SC_CharacterSelectとSC_PrototypeをBuild SettingsのScene Listへ登録し、起動時はSC_CharacterSelectから始まる。
- ゼルフの通常攻撃を実装。選択中のTargetableが攻撃射程内の場合のみ、攻撃間隔(Current Attack Speed)ごとにCharacterStatsのCurrent Attack Damageを対象のHealthControllerへ即時に与える(弾丸・投射物・攻撃アニメーションなし)。射程外では攻撃せず、ターゲット死亡時は攻撃を停止してターゲット選択を安全に解除する(既存の射程判定・自動接近・被弾フラッシュは維持)。
- ターゲット分類を追加。TargetableにTarget Classification(Character / Minion / Tower / TrainingDummy)をInspectorで設定でき、将来のキャラクター・ミニオン・タワーでも再利用できる。TrainingDummyは初期状態でCharacter分類とする。
- ゼルフP(与ダメージ回復)を実装(ZelfPassiveHeal、Scripts/Characters)。実際に与えたダメージ量(実ダメージ。残りHPを超えた過剰ダメージ分は含まない)を基準に、Character分類は5%、Minion分類は2.5%、Tower分類は0%を回復する(回復率はInspector設定。テスト用のTrainingDummy分類はCharacterと同じ5%)。最大HPを超えず、死亡中は回復しない。回復のクールダウン・追加回復・回復阻害・ライフスティールは未実装。
- ダメージ表示システムを追加(FloatingCombatText / CombatTextManager、Scripts/UI)。攻撃した側の頭上に与ダメージを赤(例: 60)、受けた側の頭上に被ダメージを青(例: -60)、ゼルフPで実際にHPが増えた場合のみ回復量を緑(例: +3)で表示する。ワールド空間のWorld Space Canvas+標準Text(LegacyRuntimeフォント)による整数表示で、短時間上方向へ移動しながらフェードアウトし、常にMain Cameraの方向を向いて裏返らず、ランダムな横方向オフセットで重なりを軽減する。表示終了後は安全に削除され、プール処理は未実装だが将来プールへ置き換えやすい構造。将来のキャラクター・ミニオン・タワーからも共通利用できる。
- 攻撃ダミー(AttackDummy)をSC_Prototypeへ1体追加(DummyAutoAttack、Scripts/Characters)。攻撃射程内のPlayerへ攻撃間隔ごとに即時ダメージを与え(攻撃力10・攻撃速度1・射程2、いずれもInspector設定)、実際に与えたダメージ量をPlayerの頭上に黄色で表示する。自身または対象の死亡中は攻撃しない。本体はTrainingDummyと同構成(HP300・Character分類・被弾フラッシュ・選択リング・HPバー付き)。移動・追跡・弾丸・攻撃アニメーション・敵AIは未実装。
- 復活処理を追加(RespawnController、Scripts/Combat)。Player・TrainingDummy・AttackDummyが死亡から1秒後(Inspector設定)に初期位置・初期向きでHP全快で復活する。HealthControllerにRevive(全快)と復活イベントを追加し、Targetable(本体・Collider)、PlayerDeathHandler(操作系・見た目)、WorldHealthBar(HPバー)がそれぞれ復元する。復活したダミーの再選択は右クリックで行う。

### Changed

- 疑似通常攻撃(被弾フラッシュのみ)を、実ダメージを与える通常攻撃へ更新。
- PlayerMouseFacingの回転速度を毎秒720度から毎秒1440度へ変更(2倍)。
- 右クリック移動を「クリックした地点へ移動する」から「長押し中は常にカーソル下のGround地点へ向かって移動し続ける」仕様へ変更。長押し中はPlayerがカーソル方向を向き続ける。長押し中にカーソルがTargetableを指した場合はターゲット選択を優先して選択・切替し、その後ターゲット以外(Ground)を右クリック(長押し含む)すると解除されて移動する。
- PlayerのBase Attack Damageを20から60へ変更(ゼルフのテスト用初期値。Bonus Attack Damageは0)。
- TrainingDummyのMax Health / Current Healthを100から300へ変更(通常攻撃60ダメージ×5回で死亡)。
- HealthControllerのTakeDamage / Healを、実際に適用したダメージ量・回復量(過剰ダメージ・過剰回復分は含まない)を返すよう更新。ダメージを与えた側が実ダメージ量を取得できる。
- ダメージ表示をプレイヤー視点へ変更。与えたダメージは攻撃対象の頭上に赤色で1つだけ表示し(従来の「攻撃側頭上の赤+受けた側頭上の青」の二重表示を廃止)、Playerが受けたダメージはPlayerの頭上に黄色(例: -10)で表示する。回復(緑)の表示は変更なし。
- TrainingDummyの死亡時の非表示化を、GameObject全体の無効化から本体Rendererのみの非表示へ変更(復活イベントを受け取れるようにするため)。WorldHealthBarの死亡時非表示も、HPバーGameObjectの無効化からCanvasの無効化へ変更。見た目の挙動は従来どおり。

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
- ���本拠地破壊を勝利条件として決定。
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
