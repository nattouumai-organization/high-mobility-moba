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
DamageSystem
DamageEvent
DamageType
StatusEffectController
CooldownController
```

- 通常ダメージはARで軽減する。
- 確定ダメージは朧Rの処刑とヴォルブラークRの反射のみ。
- ダメージ計算式：`FinalDamage = RawDamage * 100 / (100 + AR)`。
- 試作では `HealthController`(Scripts/Combat)が現在HP・被ダメージ・回復の土台・HP変化通知・死亡イベントを管理する。CharacterStatsを持つ対象はCurrent Max Healthを、持たない対象(TrainingDummy)はInspectorのMax Healthを最大HPとして使用する。ARによる軽減は未実装で、将来的にHealthComponent / DamageSystemへ発展させる。

### Characters

```text
CharacterController
CharacterStats
CharacterData
BasicAttackController
CharacterSkillController
PlayerTargetSelector
PlayerBasicAttackController
PlayerDeathHandler
Targetable
```

- `CharacterData` はScriptableObject。
- 基礎ステータスと成長値はデータに保存する。
- 現在HP、クールダウン、レベル、ポイントなどの実行時状態はComponent側で保持する。
- `PlayerTargetSelector` は右クリックによるターゲットの選択・切替・解除を管理し、`Targetable` は選択される側の見た目(選択リング、選択色、被弾フラッシュ)を管理する。
- 右クリック入力の優先順位は「TargetableLayerの対象選択 > GroundLayerへの移動」とする。
- 試作ではレイヤーを GroundLayer(6)、TargetableLayer(7) として使用する。
- `CharacterStats` は移動速度に加えて、攻撃速度(毎秒の攻撃回数)と攻撃射程(Unity units)の基礎値を管理する。Current Attack Speed = Base Attack Speed × (1 + Bonus Attack Speed Percent / 100)、Attack Interval = 1 / Current Attack Speed。最大HP(Current Max Health = Base + Bonus、1未満にならない)と攻撃力(Current Attack Damage = Base + Bonus、0未満にならない)の基礎値も管理する。現在HPはHealthControllerが保持する。
- `PlayerBasicAttackController` は選択中のターゲットへの通常攻撃を管理する。攻撃間隔ごとにCharacterStatsのCurrent Attack Damageを対象のHealthControllerへ即時に与え、被弾フラッシュを発生させる。射程判定はTargetableのColliderの最も近い点との水平距離(XZ平面)で行い、射程外のターゲットを選択した場合はPlayerClickMovementのMoveToPosition()で射程内まで自動接近してから攻撃する。ターゲットが死亡した場合は攻撃を停止し、PlayerTargetSelectorが選択を解除する。将来的にミニオンなども扱うBasicAttackControllerへ発展させる。
- `Targetable` は選択リングの色で射程内(明るい緑)/射程外(オレンジ)を表示する。死亡時はHealthControllerの死亡イベントを受けて選択不可(Collider無効化)となり、短時間死亡状態を表示した後にGameObjectを非表示化する。
- `PlayerDeathHandler` はPlayerの死亡イベントを受け取り、PlayerClickMovement / PlayerMouseFacing / PlayerBasicAttackController / CharacterControllerと見た目(Renderer)を無効化する。リスポーンは未実装。
- `WorldHealthBar`(Scripts/UI)はHealthControllerのHP変化・死亡イベントを購読し、World Space Canvas上のUI ImageのFill AmountでHPバーを表示する。バーは毎フレームMain Cameraの向きに揃え、対象の死亡時は非表示になる。

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

サーバーで必ず検証するもの：

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
