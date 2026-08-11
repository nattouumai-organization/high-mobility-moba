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
- ミニオン、各チーム2本のタワー、ポイント、レベル、第2タワー破壊による勝敗を追加する
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
MatchResultController
MatchResultUI
MatchState
TeamType
GameTick
TopDownCameraController
```

- 試合開始、勝敗、復活、ゲーム状態を管理する。
- `GameManager`は`IsMatchEnded`、`WinningTeam`、`LosingTeam`を保持し、第2タワー破壊時だけ`MatchEnded(Action<Team>)`を1試合につき1回発火する。第1タワー破壊では試合を終了しない。
- `GameManager`は起動時に`MatchResultController`をget-or-addし、SC_Prototypeへの手動アタッチなしで結果UIと停止処理を有効化する。
- `GameManager`はSC_Prototype用の`PrototypeMatchDebugController`もget-or-addする。旧シーンのMap本体に残る未初期化TowerControllerだけを無効化し、Blue第2タワーの前提判定へ混入させない。InspectorのPlayer Team DebugではPlayerチームを一時固定できる。Minion Attack Debugでは通常ルールで0になった攻撃を維持したまま、有効なミニオン攻撃の1ヒット最終ダメージを一時的に上書きできる。いずれも既定は無効で通常バランスへ影響しない。
- 状態は `Waiting`、`CharacterSelect`、`Playing`、`Finished` を持つ。
- `TopDownCameraController`(Scripts/Core) はMain Cameraへ追加するカメラモード管理。ロックモード(既定)ではプレイヤーを中心にカメラが追従し、フリーモードでは追従せずマウスカーソルが画面端(上下左右)にある間その方向へ水平にスクロールする(スクロール速度・画面端の判定幅はInspector設定)。フリーモード中もSpaceを押している間は即座にプレイヤー中心へ戻して追従し、Yでモードを切り替える(フリー→ロック切替時は即座にプレイヤー中心)。追従対象は未設定ならPlayerClickMovement/PlayerInputHubを持つオブジェクトを自動検出し、対象取得時のカメラ位置との相対オフセットを維持するため俰瞰角度・高さはシーン設定のまま。スクロール方向はカメラのY軸回転に合わせたXZ平面上の右・前方向を使用する。入力はPlayerInputHub(CameraCenterPressed / CameraLockTogglePressedThisFrame / MousePosition)を使用し、マップ境界によるスクロール範囲のクランプはマップ実装後に追加する。

### Combat

```text
HealthComponent
HealthController
RespawnController
DamageSystem
DamageEvent
DamageType
DamageContext
IIncomingDamageModifier
StatusEffectController
CooldownController
```

- 通常ダメージはARで軽減する。
- 確定ダメージは朧Rの処刑とヴォルブラークRの反射のみ。
- ダメージ計算式：`FinalDamage = RawDamage * 100 / (100 + AR)`。
- 試作では `HealthController`(Scripts/Combat)が現在HP・被ダメージ・回復の土台・HP変化通知・死亡イベントを管理する。CharacterStatsを持つ対象はCurrent Max Healthを、持たない対象(TrainingDummy)はInspectorのMax Healthを最大HPとして使用する。TakeDamage / Healは実際に適用したダメージ量・回復量(残りHP・最大HPを超えない値)を返し、ダメージを与えた側が実ダメージ量を取得できる。Revive()で死亡状態から現在HPを全快して復活でき、復活イベントで見た目・操作の復元を各コンポーネントへ通知する。TakeDamageは攻撃者のTransformとダメージ種別(DamageType.Normal / True。Scripts/Combat/DamageInfo.cs)も受け取れ、HPへ適用する直前に同じGameObject上のIIncomingDamageModifier(ゼルフWの前方ダメージ軽減など)がDamageContext(攻撃者・ダメージ種別・元ダメージ・反射フラグ)を使ってダメージ量を変更できる。通常ダメージ(Normal)はAR(防御力)による軽減式 FinalDamage = RawDamage × 100 / (100 + AR) で軽減され、確定ダメージ(True)はARでは軽減されない(ヴォルブラークRの反射ダメージが使用)。従来のTakeDamage(ダメージ量のみ)は攻撃者なしの通常ダメージとして互換動作する。実ダメージ(実際に減ったHP)が発生したときは(ダメージ情報・実ダメージ量)をDamageTakenイベントで通知する(ヴォルブラークRの反射が購読。死亡処理より前に通知するため致死ダメージも通知対象)。TakeDamageは反射ダメージかどうか(isReflected・既定false)も受け取れ、DamageContext.IsReflectedとして軽減判定と被ダメージ通知へ引き継がれる(反射ダメージの再反射防止に使用)。将来的にHealthComponent / DamageSystemへ発展させる。
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
ZelfWController
ZelfEController
CharacterSelectionManager
CharacterSelectionUI
PlayerCharacterApplier
PlayerLayerMaskFallback
PlayerSpawner
VolbraakPassiveShield
VolbraakQController
VolbraakWController
VolbraakEController
VolbraakRController
```

- `CharacterData` はScriptableObject。
- 基礎ステータスと成長値はデータに保存する。
- 現在HP、クールダウン、レベル、ポイントなどの実行時状態はComponent側で保持する。
- `PlayerTargetSelector` は右クリックによるターゲットの選択・切替・解除を管理し、`Targetable` は選択される側の見た目(選択リング、選択色、被弾フラッシュ)を管理する。
- 右クリック入力の優先順位は「TargetableLayerの対象選択 > GroundLayerへの移動」とする。
- 試作ではレイヤーを GroundLayer(6)、TargetableLayer(7) として使用する。レイヤー番号を固定値で扱うEditorスクリプトは使用せず、各コンポーネントのLayerMaskはシーンのInspector設定として保存する。LayerMaskが未設定(Nothing)の場合はPlayerLayerMaskFallbackが起動時にレイヤー名(無ければ6/7番)から自動補正するため、Prefab Variantへスキルコンポーネントを追加し直した直後でも動作する(Inspector設定があればそちらを優先する)。
- `PlayerMouseFacing` は右クリックしたGround地点の方向を、PlayerがY軸回転のみで向く処理を管理する(移動はPlayerClickMovementの責務)。外部スクリプト向けのpublic API SetLookTarget(ワールド座標指定) / SetLookDirection(方向ベクトル指定)で内部の目標回転を安全に更新でき、指定地点がPlayerとほぼ同じ位置の場合は何もしない。実際の回転は毎フレームInspectorのRotation Speed設定で行われ、目標回転などのprivateフィールドはPlayerMouseFacing内部だけで管理する(外部からのReflectionによる書き換えは使用しない)。
- 視点仕様: Playerは移動している方向へ視点が向き、ブリンクした場合はブリンクした方向を向くことを基本とする。視点方向は各移動・スキル処理がPlayerMouseFacingのpublic APIへ明示的に方向を渡して指定する(ブリンク方向と視点方向が異なる例外スキルも、渡す方向を変えるだけで実装できる)。スキル間の連携(ゼルフE→Qのクールダウンリセットなど)もReflectionではなく、各コンポーネントが公開するpublicメソッド・プロパティの直接呼び出しで行う。
- `CharacterStats` は移動速度に加えて、攻撃速度(毎秒の攻撃回数)と攻撃射程(Unity units)の基礎値を管理する。Current Attack Speed = Base Attack Speed × (1 + Bonus Attack Speed Percent / 100)、Attack Interval = 1 / Current Attack Speed。最大HP(Current Max Health = Base + Bonus、1未満にならない)と攻撃力(Current Attack Damage = Base + Bonus、0未満にならない)の基礎値も管理する。現在HPはHealthControllerが保持する。
- `PlayerBasicAttackController` は選択中のターゲットへの通常攻撃を管理する。攻撃間隔ごとにCharacterStatsのCurrent Attack Damageを対象のHealthControllerへ即時に与え(攻撃者としてPlayerのTransformを渡す通常ダメージ)、被弾フラッシュを発生させる。HealthControllerが返す実ダメージ量を使って、ダメージ表示(CombatTextManager)とゼルフPの与ダメージ回復(ZelfPassiveHeal)へ通知する。射程判定はTargetableのColliderの最も近い点との水平距離(XZ平面)で行い、射程外のターゲットを選択した場合はPlayerClickMovementのMoveToPosition()で射程内まで自動接近してから攻撃する。ターゲットが死亡した場合は攻撃を停止し、PlayerTargetSelectorが選択を解除する。将来的にミニオンなども扱うBasicAttackControllerへ発展させる。
- `Targetable` は選択リングの色で射程内(明るい緑)/射程外(オレンジ)を表示する。死亡時はHealthControllerの死亡イベントを受けて選択不可(Collider無効化)となり、短時間死亡状態を表示した後に本体Rendererのみを非表示化する(GameObjectは無効化せず、復活イベントを受けて本体・Colliderを元へ戻す)。また、ターゲット分類(TargetClassification: Character / Minion / Tower / TrainingDummy)をInspectorで保持し、攻撃側(ゼルフPなど)が効果量の判定に使用する。
- `PlayerDeathHandler` はPlayerの死亡イベントを受け取り、PlayerClickMovement / PlayerMouseFacing / PlayerBasicAttackController / CharacterControllerと見た目(Renderer)を無効化する。復活イベントを受け取った場合は、無効化したコンポーネントと見た目を元へ戻し、移動を停止した状態で復活する(復活までの時間と復活位置はRespawnControllerが管理)。
- `WorldHealthBar`(Scripts/UI)はHealthControllerのHP変化・死亡イベントを購読し、World Space Canvas上のUI ImageのFill AmountでHPバーを表示する。バーは毎フレームMain Cameraの向きに揃え、対象の死亡時はCanvasの無効化で非表示になり、復活時に再表示される。
- `ZelfPassiveHeal`(Scripts/Characters)はゼルフP(与ダメージ回復)を管理する。通常攻撃から実ダメージ量とターゲット分類を受け取り、Character 5% / Minion 2.5% / Tower 0%(テスト用のTrainingDummy分類はCharacterと同じ5%。いずれもInspector設定)で自身のHealthControllerを回復する。死亡中は回復せず、最大HPを超えない。実際にHPが増えた場合のみ緑色の回復表示を要求する。
- `ZelfQController`(Scripts/Characters)はゼルフQを管理する。Qの対象はマウス下の有効なTargetableのみで、PlayerTargetSelectorの選択対象は対象決定に使用しない(マウス下に有効な対象がいない場合、またはTower分類の対象にはQを発動しない)。対象がQ射程外の場合は自動接近してQ射程内に入った時点で自動発動し、自動接近は右クリック入力・対象の死亡・無効化・破棄・Tower分類への変化で中止する。射程内ならブリンクして `Base Damage + Current Attack Damage × AD Ratio` のダメージを与え(攻撃者としてPlayerのTransformを渡す通常ダメージ)、Q成功対象へ同一対象ロック(Same Target Lockout)を設定し、分類別クールダウン処理(Character / TrainingDummy: 即時リセット、Minion: 残り50%短縮)を行う。視点は自動接近中は移動方向へ、ブリンク後はブリンクした方向へPlayerMouseFacing.SetLookDirection()で明示的に向ける(ブリンク移動量がほぼゼロの場合のみ対象方向へフォールバック)。与ダメージ表示はCombatTextManager.ShowDamageDealt()、ゼルフP回復はZelfPassiveHeal.NotifyDamageDealt()の直接呼び出しで行い、Reflectionは使用しない。スキル間連携用のpublic APIとして、ResetCooldown()(Qの残りクールダウンだけを即時0にする。Same Target Lockout・自動接近状態は変更しない)、CancelPendingApproach()(自動接近中であれば中止)、GroundLayerMask / TargetableLayerMask(LayerMask設定の共有用読み取り専用プロパティ)を公開する。参照・レイヤー・数値はSC_PrototypeシーンのPlayerのInspector設定として保存する。
- `ZelfWController`(Scripts/Characters)はゼルフW(前方ダメージ軽減)を管理する。Wキー(Input System)で発動し、Duration 0.75秒 / Cooldown 10秒 / Front Angle 120度 / Damage Reduction 55%(いずれもInspector設定)。IIncomingDamageModifierとしてHealthControllerからHP適用直前に呼び出され、W持続中に受けたダメージごとに、ダメージを受けた瞬間のtransform.forwardと攻撃者への水平方向(Y軸高さは含めない)で前方判定して通常ダメージだけを軽減する(背後・側面・攻撃者不明・確定ダメージは軽減しない)。攻撃・CC・CC無効化・無敵・対象指定不可の機能は持たず、W中も移動・回転・通常攻撃・Q・Eを制限しない。持続中は前方に青い扇形のLineRenderer防御エフェクトを実行時生成で表示する(子オブジェクトのローカル座標描画で回転に追従、終了時に非表示)。
- `ZelfEController`(Scripts/Characters)はゼルフE(方向ダッシュ)を管理する。Eキー(Input System)でマウス下のGround地点の方向へDash Distance 4.0をDash Duration 0.18秒でダッシュする(Hit Radius 0.60 / End Extension 0.75 / Base Damage 20 / AD Ratio 50% / Cooldown 8秒、いずれもInspector設定。Groundを指していない・近すぎる・CD中は不発動)。発動時にPlayerClickMovementを停止してZelfQController.CancelPendingApproach()でQ自動接近を中止し、ダッシュ中はCharacterControllerを無効化して位置を直接更新する(GroundレイキャストでY座標維持、終了時に対象と重なっていればダッシュ方向へ押し出し補正。NavMesh不使用)。命中判定は経路+終点先End ExtensionをHit RadiusのカプセルでTargetableLayerのみ判定し、同一TargetableにはE 1回につき1回だけ `Base Damage + Current Attack Damage × AD Ratio` の通常ダメージをHealthController経由・攻撃者情報付きで与える(Tower分類にも与える。被弾フラッシュ・ダメージ表示・ゼルフP回復は既存経路)。Character分類(TrainingDummy含む)へ1体以上命中した場合のみZelfQController.ResetCooldown()を呼ぶ。ダッシュ中は青いTrailRendererの残像を表示し、終了後短時間で消える。LayerMask未設定時はZelfQControllerのGroundLayerMask / TargetableLayerMaskを自動使用する。
- `DummyAutoAttack`(Scripts/Characters)は攻撃ダミー(AttackDummy)用の自動攻撃。Inspectorで設定した攻撃対象(PlayerのHealthController)が攻撃射程内の場合のみ、攻撃間隔ごとに即時ダメージを与え(攻撃者として自身のTransformを渡す通常ダメージ。PlayerのゼルフWの前方判定対象になる)、実ダメージ量を受けた側の頭上に黄色で表示する。攻撃力・攻撃速度・射程はInspector設定(試作は10 / 1 / 2)。射程判定はPlayerの通常攻撃と同じく対象Colliderの最も近い点との水平距離で行い、自身または対象の死亡中は攻撃しない。
- `FloatingCombatText` / `CombatTextManager`(Scripts/UI)は再利用可能なフローティング戦闘テキスト。CombatTextManagerがShowDamageDealt(赤・攻撃対象の頭上・例: 60) / ShowDamageTaken(黄・受けた側の頭上・例: -10) / ShowHeal(緑・例: +3)のstatic APIで表示要求を受け取り(プレイヤー視点で1回のダメージにつき表示は1つ)、対象の頭上のワールド空間にWorld Space Canvas+標準Text(LegacyRuntimeフォント)の整数テキストを生成する(重なり軽減のランダム横方向オフセット付き)。FloatingCombatTextは上方向移動・フェードアウト・Main Cameraへの向き揃え(裏返らない)を行い、表示終了後に自身を安全に削除する。プール処理は未実装だが生成箇所を集約してあり、将来プールへ置き換えやすい。将来のキャラクター・ミニオン・タワーからも共通利用できる。
- `CharacterData`(Scripts/Characters)はキャラクター固有の固定情報(ID・表示名・役割・説明・テーマカラー・Character Status)、基礎ステータス・成長値、P/Q/W/E/Rのスキル説明を保持するScriptableObject。Data/Characters/へZelfData.asset・VolbraakData.asset・OboroData.assetを作成済み。SC_Prototype開始時はPlayerCharacterApplierが選択中のCharacterDataをPlayerへ適用する(フェーズ4前準備)。各キャラクターのPlayerプレハブ(Prefab Variant)への参照(Player Prefab)も保持し、PlayerSpawnerが試合シーン開始時の生成に使用する(フェーズ5前準備)。
- `CharacterSelectionManager` は選択中のCharacterDataを保持する常駐マネージャー。DontDestroyOnLoadでシーン遷移後も参照でき、二重生成時は後から生成された方を破棄する(セーブデータ化はしない)。
- `CharacterSelectionUI` はSC_CharacterSelectのキャラクターカード・詳細パネル・開始ボタンを制御する。UIはInspectorで設定したキャラクター一覧(CharacterData参照+Coming Soon用フォールバック表示)から実行時にUnity UI Canvas上へ構築し、Availableのキャラクターのみ選択可能にする。フォントはUnity組み込みのLegacyRuntimeを使用し、New Input System対応のEventSystemも実行時に生成する。詳細パネルのスキル一覧はInspectorの短い一覧を優先し、未設定の場合はCharacterDataのP〜Rスキル説明から自動生成する。
- `PlayerCharacterApplier` はPlayerプレハブ(PF_Player_Base)へアタッチして全Prefab Variantで共通使用し、シーン開始時にCharacterSelectionManagerが保持する選択中CharacterDataをCharacterStats.SetCharacterData()へ適用する(未選択でSC_Prototypeを直接起動した場合はInspectorのFallback Character Data(ZelfData想定)を使用)。選択キャラクターがゼルフ以外の場合はゼルフ固有スキルコンポーネント(ZelfPassiveHeal / ZelfQ/W/E/RController)をDestroyImmediateで取り除き、移動・通常攻撃・共通D・Fなどの共通コンポーネントだけで動作させる(各キャラクターの固有スキルは実装後にこのクラスへ登録する)。同様に、ヴォルブラーク以外を選択した場合はヴォルブラーク固有のVolbraakPassiveShield(P)・VolbraakQController(Q)・VolbraakWController(W)・VolbraakEController(E)・VolbraakRController(R)を取り除く。DefaultExecutionOrder(-100)で他コンポーネントのAwakeより先に実行し、PlayerのRendererへテーマカラーも適用する(Inspectorで無効化可能)。Prefab Variant方式では各Variantは自分のスキルコンポーネントしか持たないため取り外しは通常何も行われず、CharacterDataとVariantの組み合わせをInspectorで誤設定した場合の安全網として機能する。各VariantのFallback Character Dataにはそのキャラクター自身のCharacterDataを設定する。
- `OboroData`はGAME_DESIGN.mdの朧基礎値を一元管理する(HP590/+90、HPreg3.0/+0.25、AD63/+5.5、AS0.78/+1.5%、AR22/+3.5、MS370、AA射程125)。Character StatusはAvailableで、SC_CharacterSelectからPF_Player_Oboroを選択・生成できる。P/Q/W/E/RはPF_Player_OboroのOboroSkillInstallerが実行時に重複なく構成する。
- `PlayerSpawner`(Scripts/Characters)は試合シーン(SC_Prototype)開始時に、キャラクター選択結果に応じたPlayerプレハブ(Prefab Variant)をスポナーの位置・向きへ生成する。選択中CharacterDataのPlayer Prefabを生成し、未選択で直接起動した場合はInspectorのFallback Character Data(ZelfData想定)を使用する。シーンへPlayerが直接配置されている場合は生成をスキップする(移行前シーン用の安全網)。朧選択時だけ生成ワールド座標のYを1へ固定し、スポナー側の高いYを継承しない。DefaultExecutionOrder(-200)により、Playerを自動検出する他コンポーネント(TopDownCameraController / SkillRangePreviewなど)のAwakeより先に生成する。
- `PlayerLayerMaskFallback` はPlayerCharacterApplierのAwakeから呼ばれる静的ヘルパー。Player配下の全コンポーネントの `_groundLayer` / `_targetableLayer` フィールドを調べ、未設定(Nothing)のものだけをレイヤー名(GroundLayer / TargetableLayer、無ければ6 / 7番)から自動補正する。Inspector設定済みの値は上書きせず、FlashControllerのWall Layerのような意図的な未設定フィールドは対象外。Prefab Variantへスキルコンポーネントを追加し直した際のLayerMask未設定によるスキル不発を防ぐ安全網。
- Playerプレハブ構成(Prefabs/Characters/): `PF_Player_Base` がすべてのキャラクター共通のコンポーネント(移動・視点・ターゲット選択・通常攻撃・HP/復活・共通D・Fフラッシュ・PlayerInputHub・PlayerCharacterApplierなど)だけを持つ親プレハブ。各キャラクターは `PF_Player_Zelf`(ZelfPassiveHeal / ZelfQ/W/E/RControllerを追加)・`PF_Player_Volbraak`(VolbraakPassiveShield / VolbraakQ/W/E/RControllerを追加)・`PF_Player_Oboro`(OboroSkillInstallerからP/Q/W/E/Rを構成)のようにPrefab Variantとして作成し、CharacterData(Data/Characters/)のPlayer Prefabへ設定する。新キャラクターの追加手順: CharacterData作成 → PF_Player_BaseからPrefab Variant作成 → 固有スキルコンポーネントを追加(LayerMaskなどのInspector設定も忘れずに) → CharacterDataへVariantとFallbackを設定 → キャラクター選択画面の一覧へ登録。
- `VolbraakPassiveShield`(Scripts/Characters)はヴォルブラークP(初撃無効化)を管理する。IIncomingDamageModifierとしてHealthControllerからHPへ適用する直前に呼び出され、一定時間(Recharge Duration、既定10秒)被弾しないとシールドが展開され、次に受ける攻撃1回をダメージ種別(Normal / True)を問わず完全無効化する(ダメージ0)。シールドは消費まで永続し、ミニオン(TargetClassification.Minion)の攻撃では剥がれない(無効化もされず通常どおり受ける)。タワー(Tower分類)の攻撃も1回無効化するがPを消費する(タワー本体はフェーズ5実装予定。攻撃者のTargetable分類で判定するため実装後そのまま機能する)。攻撃者不明(null)のダメージは無効化の対象。被弾(実際にHPが減るダメージ)があるたびに無被弾タイマーをリセットする(ミニオンからの被弾も含む)。シールド展開中はPlayerの周囲へLineRendererのリングを実行時生成で表示し(Inspectorで無効化可能)、死亡中は再展開せず復活時は展開済みで復活する。
- `VolbraakQController`(Scripts/Characters)はヴォルブラークQ(地面叩きと亀裂)を管理する。Qキーでマウスカーソル方向へ地面を叩き、前方の帯状範囲(長さ4×幅1.6、Inspector設定)へ範囲ダメージ(基礎25+AD×0.8)を与える。叩いた場所には亀裂が残り(既定4秒)、亀裂上の敵(Tower分類を除く)へCrowdControlController.ApplySlow経由でスロウ(既定35%)を短い持続で掛け直しながら継続付与する(複数スロウは最も強い1つだけが有効になるLoL方式)。同時に複数の亀裂は存在せず、再発動時は古い亀裂が即時消滅する。移動を伴わないためスネア中も使用でき、スタン中・死亡中などは行動ロックにより使用不可。自身の死亡時は展開中の亀裂を即時終了する。亀裂はLineRendererの枠+ジグザグ線をシーン直下へ実行時生成して表示し(地面に固定)、NormalCastではQキー押下中に方向線のみを表示する。GroundとTargetableのLayerMaskはInspectorで設定し(ZelfQControllerと同じ設定)、FlashControllerがレイヤー未設定時に流用できるようGroundLayerMask/TargetableLayerMaskを公開プロパティとして提供する。
- `VolbraakWController`(Scripts/Characters)はヴォルブラークW(シールドと時限爆発)を管理する。Wキーで即時発動(対象・方向指定なしの自己バフのためプレビューなし)し、HPシールド(基礎80+AD×0.8、発動時のADでスナップショット)を獲得する。IIncomingDamageModifierとしてダメージ種別(Normal / True)を問わず吸収し、通常ダメージはAR軽減式(×100/(100+AR))を適用したHP換算値でシールドを消費する(吸収しきれない分だけHPへ通す)。ヴォルブラークPのシールド展開中にミニオン以外から攻撃を受けた場合はWでは吸収せずPの初撃無効化を優先する(コンポーネントの適用順に依存しない)。発動から一定時間後(既定3秒)に自動爆発し、周囲(半径2.5)の対象へ範囲ダメージ(基礎40+AD×0.9)を与える(手動爆発なし。シールドが途中で割れても爆発は発生する)。爆発で実際に与えたダメージ×回復率(Character/Tower/TrainingDummy 5%・Minion 2.5%、Inspector設定)を自身へ回復する。移動を伴わないためスネア中も使用でき、スタン中・死亡中などは行動ロックにより使用不可(展開済みシールド・爆発の進行はロック中も継続)。自身の死亡時はシールド・爆発を中止する(爆発しない)。シールド中はPlayerの周囲へ青系リングを、爆発時は爆発半径のリングを短時間表示する(LineRenderer実行時生成)。TargetableのLayerMaskはInspectorで設定する(ZelfQControllerと同じ設定)。クールダウンは既定12秒でTime.timeAsDouble基準。
- `VolbraakEController`(Scripts/Characters)はヴォルブラークE(突進とスタン)を管理する。NormalCastではEキー押下中に方向線(長さ=突進距離)のみを表示し、離した瞬間にマウスカーソル方向へ突進する(距離5.5・0.6秒。CharacterControllerを一時無効化して直接移動・地面追従・終了時のめり込み解消はZelfEControllerと同じ方式)。当たったTargetable(自身を除く)へダメージ(基礎40+AD×0.7)とスタン(既定1秒)を与え、突進はそこで停止する(敵を突進方向へ少し押し出してヴォルブラークは敵の手前に止まる。Tower分類と共通Dに弾かれた相手は押し出さない)。スタンはCrowdControlController.ApplyStun経由で適用し、戻り値がtrue(共通Dによる無効化)の場合はダメージも適用しない(「共通Dで弾かれた場合は両方不発」)。Tower分類にはスタンを掛けずダメージのみ与える。移動スキルのためスネア中・スタン中は使用不可。突進中はAbilityLockControllerへロック(理由: VolbraakEDash)を追加して通常攻撃・他スキルの入力を禁止し、死亡時は突進を即時中断してロックを解除する。突進の軌跡はTrailRendererで表示する。GroundとTargetableのLayerMaskはInspectorで設定する(ZelfQControllerと同じ設定)。クールダウンは既定12秒でTime.timeAsDouble基準。
- `VolbraakRController`(Scripts/Characters)はヴォルブラークR(鎖)を管理する。NormalCastではRキー押下中に方向線(長さ=鎖の射程)のみを表示し、離した瞬間にマウスカーソル方向へ鎖を飛ばす(射程6・先端速度18・命中半径0.6、Inspector設定)。鎖は最初に当たった敵ヒーロー(Character/TrainingDummy分類)だけを判定し、ミニオン・タワーはすり抜ける。命中した敵は拘束(既定3秒)され、ヴォルブラークから一定距離(既定4)以上離れられない(境界を越えた分だけ毎フレーム引き戻す。相手のCharacterControllerが有効ならMove、無効ならTransform直接移動)。対象が共通Dの無効化ウィンドウ中の場合は拘束が不発になる(クールダウンは消費。「Dで鎖を弾かれても反射は付与」のため、反射ウィンドウは共通Dブロック時にも付与する)。移動を伴わないためスネア中も使用でき、スタン中・E突進中・死亡中は行動ロックにより使用不可。鎖の命中時に反射ウィンドウ(持続時間は拘束と同じ)を開始し、ウィンドウ中に敵ヒーロー(Character/TrainingDummy分類)から受けたダメージの実ダメージ量を、HealthControllerのDamageTaken通知経由で攻撃者へ確定ダメージ(True)として自動反射する(反射倍率はInspector設定・既定1)。ミニオン・タワー・設置物・自己ダメージ・攻撃者不明のダメージは反射しない。反射で与えるダメージには反射フラグ(DamageContext.IsReflected)を付け、反射フラグ付きのダメージは再反射しない(GAME_DESIGN 12章「反射は再反射しない」。ミラー戦などで反射同士が無限にループするのを防ぐ)。自身の死亡時は鎖・拘束・反射ウィンドウを即時終了し(死亡の瞬間の致死ダメージまでは反射)、デス時は残りクールダウンを60%短縮する(GAME_DESIGN 7章)。鎖はLineRendererを実行時生成して表示する(飛行中は本体→先端、拘束中は本体→対象)。IsTetherActive / TetherTarget / TetherRemainingDuration / IsReflectActiveをpublic APIとして公開する。GroundとTargetableのLayerMaskはInspectorで設定する(ZelfQControllerと同じ設定)。クールダウンは既定90秒でTime.timeAsDouble基準。
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
NexusController (legacy compatibility only)
```

- GameManagerが20秒ごとに5体のミニオンを出現させる。IsMatchEnded後は新規ウェーブを生成しない。
- TowerControllerが優先順位に従って敵を攻撃する。第2タワーは同チームの第1タワー破壊まで無敵。
- 第2タワー破壊だけを正式な勝利条件とする。NexusControllerは旧互換用で、勝敗処理には使用しない。

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
第1タワー・第2タワーHP
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

## フェーズ5: マップ生成・構造物・ミニオン(実行時生成)

### MapBuilder(実行順序-300)

- シーンの空オブジェクトにアタッチするだけで1レーンマップ(地面84x24・第1タワー±16・第2タワー±33)を実行時生成する。本拠地は生成しない。
- `_laneYawDegrees`(既定-45度)で全体を回転し、ブルー左下→レッド右上の斜めレーンにする。
- Targetable/Groundレイヤーはレイヤー名(既定"Targetable"/"Ground")から実行時に解決し、PlayerTargetSelectorのLayerMask設定と整合させる。
- StartでGameManagerの存在を確認し、シーンに無い/無効なら自動生成・有効化する(ミニオン不出撃の自己修復)。
- カメラ用に回転後マップの外接矩形+マージンの移動限界(CameraMinXなど)を公開する。

### GameManager(実行順序-250)

- ウェーブ管理: 開始15秒後に初回、以陀20秒間隔で両チームに近接3体+遠隔2体を出撃。ウェーブレベル=floor((n-1)/2)。
- ヒーロー(PlayerClickMovement)へのTeamMember(ブルー)付与を担当(開始直後+5秒間隔)。タワーの索敵はTeamMember前提。
- 第2タワー破壊通知でIsMatchEndedをtrueにし、WinningTeam/LosingTeamを保持してMatchEnded(Action<Team>)を1回だけ発火する。第1タワー破壊では終了しない。
- 起動時にMatchResultControllerをget-or-addし、結果UIとゲーム停止処理をシーン手動設定なしで有効化する。

### 構造物のダメージルール(DamageContext.IsBasicAttack)

- DamageContextにIsBasicAttackフラグを追加。通常攻撃(ヒーローAA・タワー・ミニオン)のみisBasicAttack: trueでTakeDamageを呼ぶ。
- タワー: 同一チームからのダメージ0 / 通常攻撃以外は0 / 攻撃者周囲8以内に味方ミニオン不在なら確定無効・通常90%軽減 / AR60。
- 第2タワー: 自チームの第1タワー破壊まで完全無敵 / 同一チームダメージ0 / 通常攻撃のみ / AR60。
- NexusControllerは旧シーン・Prefab互換用として残すが、MapBuilderから生成せず勝敗処理にも使用しない。
- ヒーローの通常攻撃は同一チームの対象をターゲットにしない(PlayerBasicAttackController.GetValidTarget)。さらに右クリック選択段階でも同一チームの対象を除外する(PlayerTargetSelector.IsSameTeam)。
- 構造物・ミニオンはCharacterStatsを持たないため、HealthController.SetMaxHealth(実行時HP設定)と各ControllerのIIncomingDamageModifierでARを自前適用する。AddComponent後はHealthController.RefreshDamageModifiers()を呼ぶ。

### タワーの攻撃優先順位とアグロ

- 優先順位: アグロ中の敵ヒーロー(最優先) > 敵ミニオン > 敵ヒーロー(最も低い。ミニオンが射程内に1体もいない場合のみ)。
- アグロ発動条件: 敵ヒーローが味方ヒーローに実ダメージを与え、かつ攻撃者または被弾者がタワー射程内にいること。ミニオン・構造物からの被弾では発動しない。
- アグロ解除条件: アグロ対象の死亡、またはタワー射程外への離脱。解除後は通常の優先順位に戻る(再度攻撃すれば再発動)。
- 検知方法: タワーが味方ヒーローのHealthController.DamageTakenイベントを購読(1秒間隔で購読先を見直し)し、DamageContext.Attackerから攻撃者を判定する。HealthControllerの変更は不要。
- アグロ中も連続攻撃ボーナス(+25%/発・最大+200%)は通常通り適用される。

### タワーのHPバー(WorldHealthBar再利用)

- WorldHealthBarにInitializeRuntime(HealthController, Image)を追加し、実行時生成に対応。
- TowerController.Initializeがタワー頭上(中心+3.2m)にWorld Space Canvas(240x28px、1px=0.01m)+Background/Fill Imageを生成する。Fillの色はチームカラー。
- タワー本体は非一様スケールのためHPバーは子にせず、ワールド位置だけ合わせる(タワーは不動)。破壊時は非表示→タワーOnDestroyで破棄。

### ミニオン(MinionController)

- 近接: HP420/AD18/AS0.85/射程1.75、遠隔: HP290/AD22/AS0.70/射程5。移動速度3.3。ウェーブレベル成長: 近接HP+20/AD+1.5/AR+1、遠隔HP+14/AD+1.5/AR+0.5。
- 索敵範囲7以内の最近敵を狙い、敵不在の間はレーン進行方向へ進軍(中心線引き寄せ付き)。無敵状態の第2タワーは狙わない。
- 分離処理: 半径の合計+余白(0.1m)より近いミニオン同士を最大2m/秒で押し離し、重なりを防ぐ。完全に重なった場合はスポーン順の連番から決定的な方向へ離れる(Unity 6で廃止のGetInstanceIDは使用しない)。
- 攻撃はisBasicAttack: trueの通常攻撃扱いで構造物にも有効。ActiveMinions(static)をタワーのミニオン同伴判定が参照する。

### 移動と障害物回避(ObstacleAvoidance)

- 静的ユーティリティObstacleAvoidanceが障害物(タワー・旧互換本拠地)をXZ平面上の円として扱う。半径はコライダー形状(タワー=CapsuleColliderの水平半径、本拠地=BoxColliderの水平対角半径)から算出し、移動体半径+余白0.15mを加える。
- SteerDirection: 直進経路が円と交差する場合、目的地への向きから外れる角度が小さい側(=最短側)の接線角+余裕4度へ進行方向を回転する。接触中は接線+外向き成分で縁を回り込む。この距離より先の障害物は無視する上限(残り移動距離・先読み距離)を持つ。
- ClampDestination: 目的地が円の内側の場合、円の外周(中心ちょうどを指した場合は現在位置側の縁)へ目的地を押し出す。
- 障害物一覧はTowerController/NexusControllerから1秒間隔で自動収集する(タワー破壊・生成などのシーン変化に追従)。
- 使用箇所: PlayerClickMovement(CharacterController.Moveの方向補正+目的地補正。半径はCharacterControllerから取得)、MinionController(進軍・追跡の方向補正。先読み3m)。攻撃対象の構造物はignore指定で障害物から除外し、接近攻撃を妨げない。ダッシュ系スキル(ゼルフE・フラッシュなど)は直進のまま。

### 迂回時の視点(向き)の調整(fix13)
- PlayerClickMovementは移動中、ObstacleAvoidanceで補正した実際の進行方向をPlayerMouseFacing.SetMovementLookDirection()へ毎フレーム通知する
- PlayerMouseFacingは通知を受けたフレーム(Updateの実行順の差を吸収するため直前フレームも含む)の間、右クリックのカーソル方向による目標回転の上書きを行わず、進行方向を優先する
- 回転自体は従来どおりRotation Speed(毎秒度数)によるRotateTowardsの滑らかな回転で、スタン中は回転しない
- ミニオンは迂回後の進行方向をFaceDirectionで向く実装が既にあるため変更なし

### ミニオンの回り込みとスタック解消(fix14)
- MinionControllerは移動時、進路上(先読み1.6m・停止予定地点まで)を塞ぐ最も近い他ミニオンを円(半径の合計+余白)として扱い、外れる角度が小さい側の接線方向へ進行方向を曲げて回り込む(AvoidOtherMinions)
- 攻撃対象自身と接触済みの相手は回り込みの対象外(接触済みの相手は分離処理が押し離す)
- 毎フレーム実移動量を監視し、移動を試みているのに実移動量が期待値の35%未満の状態が0.4秒続いたら、進行方向の真横へ0.5秒移動してスタックを解消する(左右はスポーン順の偶奇で分散)
- 攻撃射程内では足を止めて攻撃するため、ミニオンは常に「前進」か「攻撃」のどちらかを行い、何もしない時間は発生しない

### ミニオンHPバーとポイント獲得(フェーズ6)

- MinionControllerが頭上にワールド空間CanvasのHPバーを実行時生成する(既存のWorldHealthBarを再利用。ミニオンは一様スケールのため子オブジェクトとして生成)。
- PointsManager(静的クラス)がチーム毎のポイントを保持し、PointsChangedイベントで変化を通知する。SubsystemRegistrationで再生開始毎にリセットする。
- ミニオン死亡時、半径ProximityPointRange(=12f・仕様未定義のため仮値)以内の敵ヒーローに2pt、最後にダメージを与えたヒーローに追加3ptを付与する(MinionController.AwardDeathPoints)。
- GameManagerが起動時にPointsHudを生成し、画面左上に合計ポイントを表示する(内蔵フォントに日本語グリフが無いため英語表記)。
- UI Imageはスプライト未設定だとFilledタイプが機能せず常に全面描画されるため、WorldHealthBarがFill Imageに白スプライト(Texture2D.whiteTexture)を自動補完する。
- HeroKillRewards(GameManagerが実行時生成)がヒーローのDamageTaken/Diedを購読し、最後に攻撃した敵ヒーローのチームへ通常キル25ptを付与する。ミニオン・タワーにとどめを刺された場合はキルポイントなし(連続キルのみリセット)。
- 連続キル数をヒーロー毎に記録し、撃破された側が1/2/3以上の連続キル中なら撃破側へ追加10/20/30ptのシャットダウン報酬を付与する。
- TowerControllerがHealthChangedを監視し、与ダメージ累計1,000毎に攻撃側12pt・防衛側5pt、破壊時に攻撃側へ追加20ptを付与する(HP回復があっても最大到達段階で判定し二重付与しない)。
- ヒーローのチームはGameManagerがレーンのローカルX座標で割り当てる(ブルー陣側=負がブルー、正がレッド)。従来の全員ブルー割当では敵ヒーローが同一チーム扱いになり、キルポイント・シャットダウン報酬が発生しなかった。
- 本拠地(NexusController)は生成せず、各チームの第1タワーと第2タワーを生成する(1本目X=±16・2本目X=±33、いずれもHP5,000で段階報酬対象)。TowerControllerはtier(何本目か)を持ち、2本目は1本目が破壊されるまで無敵でミニオンの索敵からも除外される。2本目の破壊でGameManagerがマッチ終了(そのチームの負け)として扱う。NexusController.csは旧シーン・Prefab互換用として残るが、破壊されても勝敗処理を行わない。
- タワーのHPバーには段階報酬(1,000ダメージ毎)の境界を縦の区切り線で表示する。
- テストプレイ用: HeroKillRewardsはRespawnControllerを持つ非ヒーロー(トレーニングダミー・攻撃ダミー)もキル対象として追跡する。ヒーローがとどめを刺すとキル25ptを付与し(ダミーがTeamMemberを持たない場合はキル者のチームへ付与)、ダミーは倒されるたびに連続キル数が1増える扱いにして2回目以降の撃破でシャットダウン報酬(+10/+20/+30pt)を順に確認できる。ミニオン・タワーはRespawnControllerを持たないため対象外で、実際のヒーロー同士のキル判定にも影響しない。

### 試合終了と結果UI(MatchResultController / MatchResultUI)

- `MatchResultController`は`GameManager.MatchEnded`を購読し、試合終了処理を1回だけ実行する。
- 終了直後に結果UIを表示し、PlayerInputHub、移動、視点、通常攻撃、ターゲット選択、Q/W/E/R、共通D、F、復活、ルーン、報酬監視を停止する。
- 既存MinionControllerを無効化して移動・索敵・通常攻撃を停止し、TowerControllerを無効化して索敵・アグロ更新・通常攻撃を停止する。GameObjectとHPバーはDestroyしない。
- GameManagerはIsMatchEnded後にウェーブ生成を行わない。
- UI表示後はWaitForSecondsRealtimeでInspector設定可能な待機時間(既定1.0秒)を待ち、Time.timeScale=0にする。シーン再読込時は1へ復元する。
- `MatchResultUI`はScreen Space OverlayのCanvasを実行時生成する。ローカルPlayerのTeamMemberから勝敗を判定し、VICTORY/勝利またはDEFEAT/敗北、勝利・敗北チーム、第2タワー破壊説明、両チームのポイントを表示する。Player未生成・Team未設定時は勝利チーム表示へ安全にフォールバックする。
- 外部画像・外部UIアセットは使用しない。日本語フォントはWindows標準フォントを実行時に選択し、取得できない場合はUnity組み込みフォントへフォールバックする。
- `NexusController`は旧シーン・Prefab互換用。MapBuilderはNexusを生成せず、Nexus破壊では勝敗処理を行わない。
- 共通D失敗時はクールダウンのみ消費し、0.30秒硬直は採用しない。

### フェーズ7-1〜7-4: レベルとスキル強化

- `LevelSystem`(静的クラス): チームポイントからLv1〜Lv6を算出する。閾値は0/40/90/150/225/310。状態を持たず毎回PointsManagerから算出する。
- `HeroLevelGrowth`: GameManagerが実行時に生成。ヒーロー(PlayerClickMovement+CharacterStats+TeamMember)を定期スキャンし、チームレベル上昇時にCharacterDataの成長値を1レベル分ずつボーナスAPIで加算する。HeroSkillUpgradesの自動追加も担当。
- `CharacterStats`: `Data`アクセサとAddAttackDamageBonus / AddAttackSpeedPercentBonus / AddHealthRegenBonus(及び解除API)を追加。最大HP増加分は従来どおりHealthControllerが現在HPへも加算する。
- `HeroSkillUpgrades`: Q/W/E/Rのランク(最大2)とLv6追加強化の使用状況を保持。GAME_DESIGN 6章の順序(Lv2〜4でQ/W/E各1回、Lv5でR、Lv6で任意スキルの追加強化)に従う。ランクによるスキル性能変化は今後のタスクで実装(GetRank参照想定)。
- `PlayerInputHub`: 強化用修飾キー(左右Ctrl)を追加。Ctrl押下中はQ/W/E/RのPressed/PressedThisFrameを抑制し(ReleasedThisFrameは抑制しない)、Upgrade*PressedThisFrameを公開する。
- `SkillUpgradeHud`: GameManagerが実行時に生成。画面下部中央にQ/W/E/Rスロット(仮アイコン)とランクピップを表示し、強化可能時に上向き矢印(通常=緑、Lv6追加強化=金色)を表示する。本格的なスキルアイコンはフェーズ8で差し替え想定。

### phase7-runes: ルーンシステム + 選択画面

- `RuneType` enum: None/Relentless/Indomitable/Pursuit/Siege。
- `RuneSelectionManager`: DontDestroyOnLoadシングルトン。CharacterSelectionManagerと同様の設計。
- `RelentlessRune`: HealthController.DamageTakenサブスクリプション。3秒リングバッファ。
- `IndomitableRune`: 追加シールドAbsorbWithShield公開メソッド(将来HealthController御山用)。
- `PursuitRune`: PlayerInputHub.EPressedThisFrame/FPressedThisFrameでE/Fを検出。
- `SiegeRune`: TowerController向けstatic GetMultiplierメソッド。
- `RuneSelectionUI/RuneHoverHandler`: Unity UGUIでプロシージャル生成。


## 朧スキル実装（Phase 8）

### 共通構成

- `PF_Player_Oboro`へ`OboroSkillInstaller`を追加し、スポーン直後のワールドY座標を1へ補正してからP/Q/W/E/Rを重複なく実行時構成する。`PlayerCharacterApplier`もOboroの固有コンポーネントを誤Prefab時に除去し、Oboro選択時にInstallerが無い場合は補完する。
- `OboroCombatUtility`へ敵味方判定、Ground/Targetable LayerMaskのフォールバック、安全なCharacterController瞬間移動、対象の生存判定を集約した。TeamMemberのないテスト対象はClassificationがCharacterの場合だけ、E/R用の敵チャンピオン代用として許可する。
- 全コントローラーは`PlayerInputHub`、`AbilityLockController`、`CrowdControlController`、`HealthController`、`SkillRangeIndicator`、`Time.timeAsDouble`という既存キャラクターと同じ経路を使用する。
- `MatchResultController`の停止対象へ朧の全コンポーネントを追加し、発動中の投射物・透明化・E帰還待機・自動接近も試合終了時に停止する。
- `SkillCooldownHud`は朧Q/W/E/Rを検出し、Qは現在ストック/最大ストックと次ストック回復時間を表示する。

### P：背後通常攻撃

- `OboroPassiveBackstab`はユーザー確定値どおり、**敵ヒーロー(Character分類かつ敵TeamMember)だけ**を対象にする。対象の後方を中心とする全角120度以内からの通常攻撃へ`20 + Current AD × 0.40`の通常ダメージを加算し、内部クールダウンは設けない。
- `PlayerBasicAttackController`で基礎通常攻撃とP追加分を合算してから`HealthController.TakeDamage`を1回だけ呼ぶ。戦闘テキスト、ルーン命中回数、被ダメージイベントを二重化しない。
- 朧Eの「通常攻撃」部分も同じP判定を通す。

### Q：手裏剣・2ストック・テレポート

- カーソル方向へ射程7.0、速度14.0、接触半径0.60の貫通手裏剣を投げる。投射高さはPlayerルートと同じ0にし、Y=1のヒーローから近接・遠隔ミニオン双方へ当たる高さにする。接触対象は敵Character、テスト用TrainingDummy、敵Minionで、Towerは対象外。
- 接触した各対象へ1発につき1回、`20 + Current AD × 0.50`の通常ダメージを与える。Zelf Qの`30 + AD×0.60`、Volbraak Qの`25 + AD×0.80`に対し、貫通・2ストック・テレポートを持つため1ストック当たりの基礎値と係数を低めにした。
- 飛翔中に接触した最後の対象を記録し、飛翔終了時にその対象へテレポートする。対象が死亡・無効化・破棄された場合は接触時の最後の地点へ飛ぶ。未命中時はテレポートしない。
- 既定Cast ModeはQuickCastとし、Q押下フレームで即時発射する。Groundレイを取得できない場合はPlayer高さの水平面との交点、さらに取得できない場合は正面方向を使い、Ground設定だけを理由に不発にしない。
- 最大2ストック、1ストックの回復時間は既定8秒。残りストックがあれば、先の手裏剣の飛翔中にも次を使用できる。

### W：透明化

- 持続時間は確定値3秒。3秒経過前でも、攻撃、Q/E/R、D/F、W再発動、死亡、試合終了のいずれかで即時解除する。既定CD12秒、MS上昇20%。
- 本体Rendererだけを非表示にし、ColliderとTargetableは残すため、方向・地点指定スキルの命中判定は維持する。
- `PlayerTargetSelector`と`PlayerBasicAttackController`は透明中の朧を対象指定から除外する。AIが透明化前の対象参照を保持した場合にも、敵タワー射程外の通常攻撃は`IIncomingDamageModifier`で0にする。方向・地点指定スキル(`IsBasicAttack=false`)は無効化しない。
- 敵が既定3.0以内にいる場合は紫色の輪郭リングを表示する。敵タワーの既定射程8.0以内では輪郭を表示し、通常どおり対象指定・通常攻撃可能に戻す。

### E：背後攻撃・帰還

- 敵チャンピオン(Character分類)だけを対象指定し、既定射程4.0外では既存Q/Rと同じ方式で自動接近する。TeamMemberのないTrainingDummyもClassificationをCharacterへ変更した場合はテスト用敵チャンピオンとして許可する。右クリック、死亡、対象無効化、CCで接近を中止する。
- 発動地点へ両者から見える帰還リングを生成し、対象の後方0.8へ移動する。`通常攻撃 + 20 + AD×0.40`を1回の通常ダメージとして与え、背後条件を満たす敵ヒーローならPも同じダメージへ合算する。
- 0.65秒の帰還待機中は`AbilityLockController`へ`OboroEReturn`ロックを追加する。待機中にスタンまたはスネアを受けた場合は開始地点へ戻らず現在地点に残る。既定CD10秒。
- `PursuitRune`へ`OboroE#`のSourceId除外を追加し、E自身のダメージでは追撃を発動せず、E後の次の別命中までウィンドウを維持する。

### R：低HP処刑

- 敵チャンピオン(Character分類)だけを対象指定し、固定射程200ステータス=`2.0 Unity units`で発動する。TeamMemberのないTrainingDummyもClassificationをCharacterへ変更した場合はテスト対象として許可する。処刑閾値は確定値の最大HP10%、既定CD100秒。
- 発動時は対象最大HPの10%を`DamageType.True`で1回だけ与える。ARは無視するが、HealthControllerへ通常経路で渡すためシールドや`IIncomingDamageModifier`で吸収・軽減・無効化された場合は生存できる。
- 対象の`CommonDController.TryBlockHardCC`が成功した場合は完全不発とし、クールダウンだけ消費する。
- `WorldHealthBar`は敵チャンピオンのHPバーへ10%位置の縦マーカーを表示し、処刑圏内では赤、圏外では黄にする。
- デス時はGAME_DESIGN.mdどおり残りRクールダウンを60%短縮する。

### 仕様未記載数値の扱い

- Qストック回復時間、WのCD/MS/輪郭距離、Eの射程/追加ダメージ/帰還時間/CD、RのCDは、各コンポーネントの`SerializeField`としてInspector変更可能な初期調整値にした。Qダメージ、W持続3秒、R処刑閾値10%・最大HP10%ダメージは今回の確定値としてGAME_DESIGN.mdへ反映した。
