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
DamageSystem
DamageEvent
DamageType
StatusEffectController
CooldownController
```

- 通常ダメージはARで軽減する。
- 確定ダメージは朧Rの処刑とヴォルブラークRの反射のみ。
- ダメージ計算式：`FinalDamage = RawDamage * 100 / (100 + AR)`。

### Characters

```text
CharacterController
CharacterStats
CharacterData
BasicAttackController
CharacterSkillController
```

- `CharacterData` はScriptableObject。
- 基礎ステータスと成長値はデータに保存する。
- 現在HP、クールダウン、レベル、ポイントなどの実行時状態はComponent側で保持する。

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
