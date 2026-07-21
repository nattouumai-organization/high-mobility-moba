# Technical Design

## 1. 技術スタック

```text
Game Engine: Unity 6
Render Pipeline: Universal Render Pipeline (URP)
Language: C#
Version Control: Git / GitHub
IDE: Visual Studio または VS Code
3D: Blender
Steam Integration: Steamworks.NET
Networking: Unity Transport + Server-authoritative dedicated server
```

## 2. 開発フェーズ

### Phase 1: ローカル試作

- シングルプレイヤーで移動、通常攻撃、スキル、HP、ダメージを実装
- ネットワークコードは入れない
- ゼルフとヴォルブラークで戦闘の気持ちよさを確認する

### Phase 2: ローカル対戦

- 同一PC上で2キャラクターを操作可能にする
- ミニオン、タワー、本拠地、ポイント、レベル、勝敗を追加する
- スキル判定とゲームバランスを検証する

### Phase 3: オンライン1v1

- 専用サーバーによるサーバー権威型へ移行
- クライアントは入力を送る
- サーバーが位置、HP、ダメージ、スキル命中、クールダウン、ポイント、勝敗を決定する
- クライアントは予測と補間で操作を滑らかに見せる

## 3. Unityフォルダ構成

```text
Assets/
  Game/
    Art/
      Materials/
      Models/
      VFX/
    Audio/
    Data/
      Characters/
      Skills/
      Minions/
      Structures/
      Runes/
    Prefabs/
      Characters/
      Minions/
      Structures/
      UI/
    Scenes/
      Prototype/
      Gameplay/
    Scripts/
      Core/
      Combat/
      Characters/
      Skills/
      Minions/
      Structures/
      UI/
      Networking/
    Settings/
```

## 4. コード設計

### Core

```text
GameManager
MatchState
TeamType
GameTick
```

- 試合開始、勝敗、復活、ゲーム状態を管理する。
- 状態は `Waiting`、`CharacterSelect`、`Playing`、`Finished` を持つ。

### Combat

```text
HealthComponent
HealthController
RespawnController
DamageSystem
DamageEvent
DamageType
StatusEffectController
CooldownController
```

- 通常ダメージはARで軽減する。
- 確定ダメージは朧Rの処刑とヴォルブラークRの反射のみ。
- ダメージ計算式：`FinalDamage = RawDamage * 100 / (100 + AR)`。
- 試作では `HealthController`(Scripts/Combat)が現在HP・被ダメージ・回復の土台・HP変化通知・死亡イベントを管理する。CharacterStatsを持つ対象はCurrent Max Healthを、持たない対象(TrainingDummy)はInspectorのMax Healthを最大HPとして使用する。TakeDamage / Healは実際に適用したダメージ量・回復量(残りHP・最大HPを超えない値)を返し、ダメージを与えた側が実ダメージ量を取得できる。Revive()で死亡状態から現在HPを全快して復活でき、復活イベントで見た目・操作の復元を各コンポーネントへ通知する。ARによる軽減は未実装で、将来的にHealthComponent / DamageSystemへ発展させる。
- 試作では `RespawnController`(Scripts/Combat)が死亡した対象の復活を管理する。死亡イベントを受けてRespawn Delay秒(SC_Prototypeでは1秒、Inspector設定)後に初期位置・初期向きへ戻し、HealthController.Revive()で全快する。Player・TrainingDummy・AttackDummyで共通利用し、将来のキャラクター・ミニオンにも再利用できる。

### Characters

```text
CharacterController
CharacterStats
CharacterData
BasicAttackController
CharacterSkillController
PlayerTargetSelector
PlayerMouseFacing
PlayerBasicAttackController
PlayerDeathHandler
Targetable
DummyAutoAttack
ZelfPassiveHeal
ZelfQController
CharacterSelectionManager
CharacterSelectionUI
```

- `CharacterData` はScriptableObject。
- 基礎ステータスと成長値はデータに保存する。
- 現在HP、クールダウン、レベル、ポイントなどの実行時状態はComponent側で保持する。
- `PlayerTargetSelector` は右クリックによるターゲットの選択・切替・解除を管理し、`Targetable` は選択される側の見た目(選択リング、選択色、被弾フラッシュ)を管理する。
- 右クリック入力の優先順位は「TargetableLayerの対象選択 > GroundLayerへの移動」とする。
- 試作ではレイヤーを GroundLayer(6)、TargetableLayer(7) として使用する。レイヤー番号を固定値で扱うEditorスクリプトは使用せず、各コンポーネントのLayerMaskはシーンのInspector設定として保存する。
- `PlayerMouseFacing` は右クリックしたGround地点の方向を、PlayerがY軸回転のみで向く処理を管理する(移動はPlayerClickMovementの責務)。外部スクリプト向けのpublic API SetLookTarget(ワールド座標指定) / SetLookDirection(方向ベクトル指定)で内部の目標回転を安全に更新でき、指定地点がPlayerとほぼ同じ位置の場合は何もしない。実際の回転は毎フレームInspectorのRotation Speed設定で行われ、目標回転などのprivateフィールドはPlayerMouseFacing内部だけで管理する(外部からのReflectionによる書き換えは使用しない)。
- 視点仕様: Playerは移動している方向へ視点が向き、ブリンクした場合はブリンクした方向を向くことを基本とする。視点方向は各移動・スキル処理がPlayerMouseFacingのpublic APIへ明示的に方向を渡して指定する(ブリンク方向と視点方向が異なる例外スキルも、渡す方向を変えるだけで実装できる)。
- `CharacterStats` は移動速度に加えて、攻撃速度(毎秒の攻撃回数)と攻撃射程(Unity units)の基礎値を管理する。Current Attack Speed = Base Attack Speed × (1 + Bonus Attack Speed Percent / 100)、Attack Interval = 1 / Current Attack Speed。最大HP(Current Max Health = Base + Bonus、1未満にならない)と攻撃力(Current Attack Damage = Base + Bonus、0未満にならない)の基礎値も管理する。現在HPはHealthControllerが保持する。
- `PlayerBasicAttackController` は選択中のターゲットへの通常攻撃を管理する。攻撃間隔ごとにCharacterStatsのCurrent Attack Damageを対象のHealthControllerへ即時に与え、被弾フラッシュを発生させる。HealthControllerが返す実ダメージ量を使って、ダメージ表示(CombatTextManager)とゼルフPの与ダメージ回復(ZelfPassiveHeal)へ通知する。射程判定はTargetableのColliderの最も近い点との水平距離(XZ平面)で行い、射程外のターゲットを選択した場合はPlayerClickMovementのMoveToPosition()で射程内まで自動接近してから攻撃する。ターゲットが死亡した場合は攻撃を停止し、PlayerTargetSelectorが選択を解除する。将来的にミニオンなども扱うBasicAttackControllerへ発展させる。
- `Targetable` は選択リングの色で射程内(明るい緑)/射程外(オレンジ)を表示する。死亡時はHealthControllerの死亡イベントを受けて選択不可(Collider無効化)となり、短時間死亡状態を表示した後に本体Rendererのみを非表示化する(GameObjectは無効化せず、復活イベントを受けて本体・Colliderを元へ戻す)。また、ターゲット分類(TargetClassification: Character / Minion / Tower / TrainingDummy)をInspectorで保持し、攻撃側(ゼルフPなど)が効果量の判定に使用する。
- `PlayerDeathHandler` はPlayerの死亡イベントを受け取り、PlayerClickMovement / PlayerMouseFacing / PlayerBasicAttackController / CharacterControllerと見た目(Renderer)を無効化する。復活イベントを受け取った場合は、無効化したコンポーネントと見た目を元へ戻し、移動を停止した状態で復活する(復活までの時間と復活位置はRespawnControllerが管理)。
- `WorldHealthBar`(Scripts/UI)はHealthControllerのHP変化・死亡イベントを購読し、World Space Canvas上のUI ImageのFill AmountでHPバーを表示する。バーは毎フレームMain Cameraの向きに揃え、対象の死亡時はCanvasの無効化で非表示になり、復活時に再表示される。
- `ZelfPassiveHeal`(Scripts/Characters)はゼルフP(与ダメージ回復)を管理する。通常攻撃から実ダメージ量とターゲット分類を受け取り、Character 5% / Minion 2.5% / Tower 0%(テスト用のTrainingDummy分類はCharacterと同じ5%。いずれもInspector設定)で自身のHealthControllerを回復する。死亡中は回復せず、最大HPを超えない。実際にHPが増えた場合のみ緑色の回復表示を要求する。
- `ZelfQController`(Scripts/Characters)はゼルフQを管理する。Qの対象はマウス下の有効なTargetableのみで、PlayerTargetSelectorの選択対象は対象決定に使用しない(マウス下に有効な対象がいない場合、またはTower分類の対象にはQを発動しない)。対象がQ射程外の場合は自動接近してQ射程内に入った時点で自動発動し、自動接近は右クリック入力・対象の死亡・無効化・破棄・Tower分類への変化で中止する。射程内ならブリンクして `Base Damage + Current Attack Damage × AD Ratio` のダメージを与え、Q成功対象へ同一対象ロック(Same Target Lockout)を設定し、分類別クールダウン処理(Character / TrainingDummy: 即時リセット、Minion: 残り50%短縮)を行う。視点は自動接近中は移動方向へ、ブリンク後はブリンクした方向へPlayerMouseFacing.SetLookDirection()で明示的に向ける(ブリンク移動量がほぼゼロの場合のみ対象方向へフォールバック)。与ダメージ表示はCombatTextManager.ShowDamageDealt()、ゼルフP回復はZelfPassiveHeal.NotifyDamageDealt()の直接呼び出しで行い、Reflectionは使用しない。参照・レイヤー・数値はSC_PrototypeシーンのPlayerのInspector設定として保存する。
- `DummyAutoAttack`(Scripts/Characters)は攻撃ダミー(AttackDummy)用の自動攻撃。Inspectorで設定した攻撃対象(PlayerのHealthController)が攻撃射程内の場合のみ、攻撃間隔ごとに即時ダメージを与え、実ダメージ量を受けた側の頭上に黄色で表示する。攻撃力・攻撃速度・射程はInspector設定(試作は10 / 1 / 2)。射程判定はPlayerの通常攻撃と同じく対象Colliderの最も近い点との水平距離で行い、自身または対象の死亡中は攻撃しない。
- `FloatingCombatText` / `CombatTextManager`(Scripts/UI)は再利用可能なフローティング戦闘テキスト。CombatTextManagerがShowDamageDealt(赤・攻撃対象の頭上・例: 60) / ShowDamageTaken(黄・受けた側の頭上・例: -10) / ShowHeal(緑・例: +3)のstatic APIで表示要求を受け取り(プレイヤー視点で1回のダメージにつき表示は1つ)、対象の頭上のワールド空間にWorld Space Canvas+標準Text(LegacyRuntimeフォント)の整数テキストを生成する(重なり軽減のランダム横方向オフセット付き)。FloatingCombatTextは上方向移動・フェードアウト・Main Cameraへの向き揃え(裏返らない)を行い、表示終了後に自身を安全に削除する。プール処理は未実装だが生成箇所を集約してあり、将来プールへ置き換えやすい。将来のキャラクター・ミニオン・タワーからも共通利用できる。
- `CharacterData`(Scripts/Characters)はキャラクター固有の固定情報(ID・表示名・役割・説明・テーマカラー・Character Status)、基礎ステータス・成長値、P/Q/W/E/Rのスキル説明を保持するScriptableObject。第1弾としてData/Characters/ZelfData.assetを作成済み。SC_PrototypeのPlayerへはまだ適用しない。
- `CharacterSelectionManager` は選択中のCharacterDataを保持する常駐マネージャー。DontDestroyOnLoadでシーン遷移後も参照でき、二重生成時は後から生成された方を破棄する(セーブデータ化はしない)。
- `CharacterSelectionUI` はSC_CharacterSelectのキャラクターカード・詳細パネル・開始ボタンを制御する。UIはInspectorで設定したキャラクター一覧(CharacterData参照+Coming Soon用フォールバック表示)から実行時にUnity UI Canvas上へ構築し、Availableのキャラクターのみ選択可能にする。フォントはUnity組み込みのLegacyRuntimeを使用し、New Input System対応のEventSystemも実行時に生成する。
- 起動シーンはSC_CharacterSelectとし、「プロトタイプを開始」ボタンでSC_Prototypeを読み込む(Build SettingsのScene Listへ両シーンを登録)。

### Skills

```text
SkillData
SkillController
SkillTargeting
Projectile
AreaEffect
DashMovement
CrowdControlEffect
```

- Q/W/E/R/P/D/Fを共通のスキルインターフェースで扱う。
- キャラクター固有の挙動は個別SkillControllerまたはStrategyで実装する。
- 初期段階では、過度な汎用化を避ける。

### Minions / Structures

```text
MinionController
MinionSpawner
WaveController
TowerController
NexusController
```

- WaveControllerが20秒ごとに5体のミニオンを出現させる。
- TowerControllerが敵ヒーローを優先して攻撃する。
- NexusControllerはタワー破壊後にのみダメージを受ける。

## 5. データ設計

数値はコードに直書きしない。すべてScriptableObjectで編集可能にする。

```text
CharacterData
- characterName
- hpBase / hpUp
- hpRegBase / hpRegUp
- adBase / adUp
- asBase / asUp
- arBase / arUp
- msBase
- aaRangeBase
- skillDataList

MinionData
- minionType
- hpBase / hpUp
- adBase / adUp
- asBase
- arBase / arUp
- msBase
- aaRangeBase

RuneData
- runeName
- description
- cooldown
- effectValues
```

## 6. ネットワーク設計

正式版の対戦はP2PではなくDedicated Serverを使う。

```text
Server Tick: 60Hz
Client Role: 入力送信、予測、補間、描画
Server Role: 移動、スキル、ダメージ、CC、ポイント、勝敗の最終判定
```

サーバーで必ず検証するもの��

```text
移動速度
ダッシュ距離
スキル射程
クールダウン
マナは存在しないため検証不要
ダメージ
HP
ポイント
レベル
タワー・本拠地HP
```

## 7. 命名規則

```text
C# class: PascalCase
field: _camelCase
property: PascalCase
method: PascalCase
boolean: Is / Has / Can で始める
ScriptableObject: XxxData
Prefab: PF_Xxx
Material: M_Xxx
Scene: SC_Xxx
```

例：

```text
ZelfData
PF_Zelf
ZelfSkillQController
TowerController
IsDead
CanCastSkill
```

## 8. Git運用

```text
main: 常に動作する安定版
feature/xxx: 機能追加用
fix/xxx: バグ修正用
```

コミット例：

```text
feat: add basic right-click movement
feat: add Zelf Q target dash
feat: add minion wave spawner
fix: prevent tower damage without allied minions
balance: reduce Zelf E dash damage
```

Unityの `Library`、`Temp`、`Logs`、`obj`、`Build` はGit管理しない。

TASKS.md / CHANGELOG.mdなどのMarkdown文書は手動で更新する。Unity EditorスクリプトによるMarkdownの自動編集(メニュー操作によるセットアップスクリプトを含む)は使用しない。
