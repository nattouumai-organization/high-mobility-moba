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

## 2026-08-04

### Added
- ミニオンの頭上にワールド空間のHPバーを表示するようにした(MinionController、既存のWorldHealthBarを再利用)。
- チーム毎のポイントを管理するPointsManagerを追加した(Scripts/Points/PointsManager.cs)。
- 近く(半径12・仕様未定義のため仮値)でミニオンが死亡した際に敵ヒーローのチームへ2ポイント、ラストヒット時は追加で3ポイントを付与するようにした。
- 画面左上に両チームの合計ポイントを表示するPointsHudを追加した(GameManagerが実行時に生成)。
- 通常キル(敵ヒーロー撃破)で25ポイントを獲得するようにした(Points/HeroKillRewards.cs、ミニオン・タワーによるとどめはキルポイントなし)。
- シャットダウン報酬を実装した(連続キル中の敵を撃破すると1連続+10pt/2連続+20pt/3連続以上+30pt、死亡で連続キルはリセット)。
- タワー段階報酬を実装した(タワーHPを1,000削る毎に攻撃側12pt・防衛側5pt、破壊した攻撃側に追加20pt)。

### Fixed
- ミニオン・タワーのHPバーが最大のまま減らない問題を修正した(スプライト未設定のUI ImageはFilledタイプが機能せず常に全面描画されるため、WorldHealthBarが白スプライトを自動補完するようにした)。

## 2026-08-03
### Fixed
- ミニオン同士がスタックして、攻撃も前進もしないミニオンが発生する問題を修正(fix14)
  - 進路上を塞ぐ他ミニオンを接線方向の横移動で回り込むようにした(標的自身・停止予定地点より先のミニオンは避けない)
  - それでも動けない状態が約0.4秒続いた場合は、真横へ0.5秒移動してスタックを解消(左右はスポーン順で分散)
  - これによりミニオンは常に「前進する」か「標的を攻撃する」のどちらかを行う

## 2026-08-02
### Fixed
- 障害物を迂回して移動する際に、視点(キャラクターの向き)が迂回方向を向かずカーソル方向のままになる問題を修正(fix13)
  - プレイヤーの移動中は、ObstacleAvoidanceで補正した実際の進行方向を向くようPlayerMouseFacingを調整
  - スタン中に回転しない挙動・スネア中の向き変更・ミニオンの向き(実装済み)は従来どおり


### Added
- 障害物回避(ObstacleAvoidance)を追加。ヒーローとミニオンの移動経路上に障害物(タワー・本拠地)がある場合、目的地への向きから外れる角度が小さい側(=最短側)の接線方向へ自動で迂回し、ぶつからず・減速せずに目的地へ移動する。
- 目的地が障害物と重なる位置を指定した場合は、到達できる障害物の縁まで目的地を自動補正する。

### Changed
- ヒーローの右クリック移動・自動接近(PlayerClickMovement)とミニオンの進軍・追跡(MinionController)が障害物回避を使用するように変更。攻撃対象の構造物そのものは障害物として扱わない(接近して攻撃できる)。ダッシュ系スキル(ゼルフE・フラッシュなど)は高機動アクションの仕様として従来どおり直進する。

## 2026-08-01

### Added
- タワーのアグロ仕様を追加。攻撃優先順位はアグロ中の敵ヒーロー(最優先) > 敵ミニオン > 敵ヒーロー(最も低い)。敵ヒーローがタワー下で味方ヒーローにダメージを与えるとアグロが発動し、死亡または射程外に出るまで最優先で狙われ続ける。
- タワーの頭上にワールド空間のHPバーを追加(既存のWorldHealthBarを実行時生成対応にして再利用。バーの色はチームカラー)。
- ミニオン同士が重ならないように分離処理を追加(近すぎたミニオンを毎フレーム押し離す。攻撃中・停止中も適用)。

### Fixed
- MinionControllerのコンパイルエラー(CS0619)を修正。Unity 6で廃止されたObject.GetInstanceID()の使用をやめ、スポーン順の連番で分離方向を決定する方式に変更。
- ミニオンがスポーン・進軍しない問題を修正。MapBuilderが起動時にGameManagerの存在を確認し、シーンに無い/無効なら自動生成・有効化するようにした(自己修復)。起動・ウェーブ出撃のConsoleログも追加。
- タワーがヒーローを攻撃しない問題を修正。GameManagerが開始直後(以陎5秒間隔)にヒーローへTeamMember(ブルー)を付与するようにした。

### Changed
- タワー・本拠地は「通常攻撃のみ」ダメージを受けるように変更(ゼルフW/Eなどのスキル・反射は無効)。DamageContextにIsBasicAttackフラグを追加し、通常攻撃/タワー/ミニオンの攻撃のみisBasicAttack: trueでTakeDamageを呼ぶ。
- 味方のタワー・本拠地は攻撃不可に変更。同一チームの対象は通常攻撃のターゲットにならず、構造物側でも同一チームからのダメージを0にする。
- 同一チームの対象(味方のタワー・本拠地・ミニオン)は右クリックで選択できないように変更(PlayerTargetSelectorにチーム判定を追加。味方構造物への右クリックは地面移動として扱われる)。

## 2026-07-31

### Fixed
- 1本目のタワーが生成されない問題を修正。マップレイアウトを設計書準拠(地面84x24・タワー±16・本拠地±33)に再構築した(fix5)。
- プレイヤーが画面に映らない問題を修正。カメラの追従オフセットを注視点基準で算出するようにした(fix6)。

### Changed
- レーンを左下(ブルー)→右上(レッド)の斜め配置に変更(MapBuilderの_laneYawDegrees、既定-45度)(fix7)。

## 2026-07-29

### Added

- カメラ操作を実装(TopDownCameraController、Scripts/Core。Main Cameraへ追加する)。ロックモード(既定)ではプレイヤーを中心にカメラが追従する。Yでフリーモードと切り替えられ、フリーモードでは追従せず、マウスカーソルを画面端(上下左右)へ持っていくとその方向へゆっくりスクロールする(スクロール速度と画面端の判定幅はInspector設定)。フリーモード中もSpaceを押している間は即座にプレイヤー中心になって追従し、離すとその場でフリーモードへ戻る(LOLデフォルトのSpace/Yと同じ配置)。
- PlayerInputHubへカメラ操作用のInputActionを追加(CameraCenter: Space / CameraLockToggle: Y)。
- キャラごとのPlayerプレハブ(Prefab Variant)方式を実装(フェーズ5前準備)。共通コンポーネントだけを持つPF_Player_Baseを親プレハブとし、各キャラクターは固有スキルコンポーネントを追加したPrefab Variant(PF_Player_Zelf / PF_Player_Volbraak、Prefabs/Characters/)として作成する。CharacterDataへPlayer Prefab参照を追加し、新規PlayerSpawner(Scripts/Characters)が試合シーン開始時に選択キャラクターのVariantをスポナーの位置・向きへ生成する(シーン直置きのPlayerは廃止。既存Playerがある場合は生成をスキップする安全網付き)。

### Fixed

- Prefab Variant移行後に全スキルが「マウスカーソルがGroundを指していないため発動しません」等で不発になる問題を修正。Variantへ追加し直したスキルコンポーネントのGround/Targetable LayerMaskが未設定(Nothing)のままになることが原因。新規PlayerLayerMaskFallback(Scripts/Characters)をPlayerCharacterApplierのAwakeから呼び出し、未設定の_groundLayer/_targetableLayerのみをレイヤー名(GroundLayer/TargetableLayer、無ければ6/7番)から自動補正する(Inspector設定済みの値は上書きしない。FlashControllerのWall Layerなど意図的な未設定は対象外)。

### Changed

- PlayerCharacterApplier: Prefab Variant方式に合わせて役割を更新。Playerプレハブ(PF_Player_Base)へアタッチして全Variantで共通使用し、CharacterDataの適用(ステータス・テーマカラー)を担当する。固有スキルコンポーネントの取り外しは、CharacterDataとVariantの誤設定に備えた安全網として維持(正しい組み合わせでは何も取り除かれない)。各VariantのFallback Character Dataにはそのキャラクター自身のCharacterDataを設定する。
- ヴォルブラークR(反射): 反射で与えるダメージに反射フラグを付け、反射フラグ付きのダメージ(再反射)は反射しないように更新(GAME_DESIGN 12章「反射は再反射しない」)。ミラー戦(ヴォルブラーク対ヴォルブラーク)などで両者の反射ウィンドウが有効な場合でも、反射同士が無限にループしない。
- HealthController / DamageContext: ダメージが反射によるものかを表すIsReflectedフラグをDamageContextへ追加。TakeDamageのisReflected引数(既定false)から、軽減判定(IIncomingDamageModifier)と被ダメージ通知(DamageTaken)の両方へ引き継がれる。

## 2026-07-26

### Added

- ヴォルブラークR(鎖): VolbraakRControllerを追加(Scripts/Characters)。Rキーでマウスカーソル方向へ鎖を飛ばし(射程6・先端速度18・命中半径0.6、Inspector設定)、最初に当たった敵ヒーロー(Character/TrainingDummy分類)を鎖で繋ぐ。繋がれた敵は持続時間(既定3秒)の間、ヴォルブラークから一定距離(既定4)以上離れられない(境界を越えた分だけ毎フレーム引き戻される)。鎖はミニオン・タワーには当たらずすり抜ける。対象が共通Dの無効化ウィンドウ中の場合、拘束は不発になる(クールダウンは消費。「Dで鎖を弾かれても反射は付与」の反射ダメージは後続タスクで実装)。移動を伴わないためスネア中も使用可能(スタン中・E突進中・死亡中は行動ロックにより使用不可)。自身の死亡時は鎖を即時終了し、デス時は残りクールダウンを60%短縮する。クールダウン既定90秒。
- ヴォルブラークR(反射): 鎖の命中時に反射ウィンドウ(持続時間は拘束と同じ・既定3秒)を開始するようVolbraakRControllerを更新。ウィンドウ中に敵ヒーロー(Character/TrainingDummy分類)から受けたダメージの実ダメージ量を、攻撃者へ確定ダメージ(True)で自動反射する(反射倍率はInspector設定・既定1)。ミニオン・タワー・設置物・自己ダメージ・攻撃者不明のダメージは反射しない。共通Dに鎖を弾かれて拘束が不発の場合も反射ウィンドウは付与する(GAME_DESIGN 12章「Dで鎖を弾かれても反射は付与」)。自身の死亡でウィンドウは即時終了する(死亡の瞬間の致死ダメージまでは反射)。反射の再反射防止は後続タスクで実装する。

### Changed
- HealthController: 実ダメージを受けたときに(ダメージ情報・実ダメージ量)を通知するDamageTakenイベントを追加(ヴォルブラークRの反射が購読)。死亡処理(Died)より前に通知するため、致死ダメージも通知対象になる。あわせてTECHNICAL_DESIGNのHealthController記述を現状の実装(AR軽減実装済み・確定ダメージの用途)に合わせて更新。
- ヴォルブラークE: 敵に当たると突進がそこで停止し、敵を突進方向へ少し押し出して(既定0.8、Inspector設定)ヴォルブラークが敵の手前に止まるように変更。Tower分類と共通Dに弾かれた相手は押し出さない(共通Dの場合は突進の停止のみ)。
- ヴォルブラークE: 突進をよりゆっくり・長く調整(距離4→5.5、移動時間0.25秒→0.6秒。Inspector設定)。
- VolbraakQController: GroundLayerMask/TargetableLayerMaskの公開プロパティを追加(FlashControllerがレイヤー未設定時に流用する)。
- PlayerCharacterApplier: ヴォルブラーク以外のキャラクターを選択した場合に、VolbraakRController(R)も取り除くように更新。

### Fixed
- ヴォルブラーク選択時にフラッシュ(F)が「マウスカーソルがGroundを指していないため発動しません」と表示されて発動できない問題を修正。FlashControllerのレイヤー流用先がZelfQControllerのみで、ヴォルブラーク選択時は起動時にZelfQControllerが削除されるためGround Layerが未設定のままになっていた。VolbraakQControllerからも流用するようにし、レイヤー未設定時は原因が分かる設定案内の警告を出すように改善。

## 2026-07-25

### Added

- フェーズ4前準備: ヴォルブラークのCharacterData(Data/Characters/VolbraakData.asset)を追加。キャラクター選択画面でヴォルブラークが選択可能になる(HP760/HPreg5.0/AD54/AS0.68/AR42/MS335/AA射程175)。
- フェーズ4前準備: PlayerCharacterApplierを追加(Scripts/Characters)。SC_Prototype開始時に、キャラクター選択画面で選択したCharacterDataをPlayerのCharacterStatsへ適用し、ゼルフ以外を選択した場合はゼルフ固有スキル(P/Q/W/E/R)を取り除く(移動・通常攻撃・共通D・Fは全キャラクター共通で動作)。Playerの見た目には選択キャラクターのテーマカラーを適用する。
- ヴォルブラークP(初撃無効化): VolbraakPassiveShieldを追加(Scripts/Characters)。一定時間(既定10秒、Inspector設定)被弾しないとシールドが展開され、次に受ける攻撃1回をダメージ種別(通常/確定)を問わず完全無効化する(消費まで永続)。ミニオンの攻撃ではシールドは剥がれない(無効化もされず通常どおり受ける)。被弾があるたびに無被弾タイマーはリセットされる。シールド展開中はPlayerの周囲へリングを表示する。
- ヴォルブラークP: タワー攻撃の1回無効化とP消費に対応。攻撃者のTargetable分類(Tower)で判定するため、フェーズ5のタワー実装後もそのまま機能する(ミニオン以外の攻撃は全てPを消費して無効化)。
- ヴォルブラークQ(亀裂): VolbraakQControllerを追加(Scripts/Characters)。Qキーでマウスカーソル方向へ地面を叩き、前方の帯状範囲(長さ4×幅1.6、Inspector設定)へ範囲ダメージ(基礎25+AD×0.8)を与える。叩いた場所には亀裂が残り(既定4秒)、亀裂上の敵へスロウ(既定35%)を継続付与する。同時に複数の亀裂は存在せず、再発動時は古い亀裂が即時消滅する。スロウはCrowdControlController.ApplySlow経由で適用(複数スロウは最も強い1つだけが有効)。クールダウン既定8秒。
- ヴォルブラークW(シールドと時限爆発): VolbraakWControllerを追加(Scripts/Characters)。Wキーで即時にHPシールド(基礎80+AD×0.8、Inspector設定)を獲得し、一定時間後(既定3秒)に自動爆発して周囲(半径2.5)へ範囲ダメージ(基礎40+AD×0.9)を与える(手動爆発なし)。爆発で実際に与えたダメージの5%を回復し、ミニオン相手は半減(2.5%)。シールドはダメージ種別(通常/確定)を問わず吸収し、通常ダメージはAR軽減後のHP換算値で消費される。シールドが途中で割れても爆発は予定どおり発生する。ヴォルブラークP展開中はPの初撃無効化が優先され、Wシールドは消費されない。クールダウン既定12秒。
- ヴォルブラークE(突進とスタン): VolbraakEControllerを追加(Scripts/Characters)。Eキーでマウスカーソル方向へ突進し(距離4・0.25秒、Inspector設定)、経路上の敵へダメージ(基礎40+AD×0.7)とスタン(既定1秒)を与える(各対象1回まで)。対象が共通Dの無効化ウィンドウ中の場合はダメージとスタンの両方が不発になる(GAME_DESIGN 12章)。Tower分類にはスタンを掛けずダメージのみ与える。移動スキルのためスネア中・スタン中は使用不可。突進中は他スキル・通常攻撃の入力をロックし、死亡時は突進を即時中断する。クールダウン既定12秒。

### Changed

- PlayerCharacterApplier: ヴォルブラーク以外のキャラクターを選択した場合に、ヴォルブラーク固有のVolbraakPassiveShield(P)を取り除くように更新。
- PlayerCharacterApplier: ヴォルブラーク以外のキャラクターを選択した場合に、VolbraakQController(Q)も取り除くように更新。
- PlayerCharacterApplier: ヴォルブラーク以外のキャラクターを選択した場合に、VolbraakWController(W)も取り除くように更新。
- PlayerCharacterApplier: ヴォルブラーク以外のキャラクターを選択した場合に、VolbraakEController(E)も取り除くように更新。
- キャラクター選択画面: 詳細パネルのスキル一覧がInspector未設定の場合、CharacterDataのP〜Rスキル説明から自動生成するように変更(ヴォルブラーク追加用)。
- フェーズ1〜3見直し(重要度:低): MS%バフの基準を基礎MS(BaseMoveSpeed)へ統一。共通Dの成功時MS上昇が「発動時点の現在MS」基準だったものを、ゼルフRのMS上昇と同じ基礎MS基準へ変更。
- フェーズ1〜3見直し(重要度:低): スロウ適用後の移動速度にLoL準拠の下限(MS110)を追加(CharacterStats.CurrentMoveSpeed)。基礎MSが110未満の対象(練習用ダミーなど)は基礎MSがそのまま下限。
- フェーズ1〜3見直し(重要度:低): PlayerMouseFacingの右クリック・マウス座標の入力をPlayerInputHub経由へ一元化(Mouse.currentの直接参照を廃止)。
- フェーズ1〜3見直し(重要度:低): 各スキル(Q/W/E/R/共通D/F)のクールダウン終了時刻をTime.timeAsDouble基準のdoubleへ変更(長時間起動時のfloat精度劣化対策)。SkillCooldownHudのリフレクション読み取りもfloat/double両対応へ更新。

### Fixed

- フェーズ1〜3見直し(重要度:低): ゼルフEのダッシュ後ウェーブが、自身の死亡後も命中判定を続けていた問題を修正(死亡時に即中断)。
- フェーズ1〜3見直し(重要度:低): ドキュメント・コメントの誤字を修正(「攻撃傄」→「攻撃側」、「行動妃害耐性」→「行動妨害耐性」、「攻撃者だ45」→「攻撃者へ45」、ゼルフWの説明を「合計AD×1.5分のダメージを毎ティック均等に与える」へ修正)。

## 2026-07-24

### Added

- フェーズ1〜3見直し: ゼルフRの「共通Dで完全不発」を実装(対象が共通Dの無効化ウィンドウ中にRを発動した場合、決闘エリアは展開されず何も起こらない。Rのクールダウンは消費し、対象側では共通D成功時のカウンター攻撃45+AD30%とMS上昇が発生する。射程外からの自動接近後の発動も同様に判定する)
- フェーズ1〜3見直し: デス時のR・共通D・Fの残りクールダウン60%短縮を実装(GAME_DESIGN.md 7章準拠。ZelfRController・CommonDController・FlashControllerの各自がHealthController.Diedを購読し、死亡時に残りクールダウンを(1 - 0.6)倍へ短縮する。短縮割合はInspectorのDeath Cooldown Reductionで調整可能。Q/W/Eは仕様どおり短縮しない)
- フェーズ1〜3見直し: AR(防御力)による通常ダメージ軽減とHPreg(毎秒HP自動回復)を実装(CharacterStatsにCurrentArmor/CurrentHealthRegenとAddArmorBonus/RemoveArmorBonusを追加し、CharacterDataの既存フィールドBaseArmor/ArmorGrowth/BaseHpRegeneration/HpRegenerationGrowthから読み込む。HealthControllerは通常ダメージ(Normal)にFinalDamage = RawDamage × 100 / (100 + AR)の軽減式を適用し(ゼルフWなどのIIncomingDamageModifier適用後)、生存中は毎フレームHPregで回復する。CharacterStatsを持たないTrainingDummyはAR 0・HPregなしの従来動作。ゼルフはAR28/ARUP4.0/HPreg3.5/HPregUP0.35。ZelfData.assetのBase Armor・Base Hp Regenerationに値が入っているかInspectorで要確認)

### Changed

- フェーズ1〜3見直し: ゼルフRのエリア内外スロウをCrowdControlController.ApplySlow経由へ一本化(従来はCharacterStatsを直接操作していたため、他のスロウと加算されて「最も強い1つだけ適用」のLoL方式に反していた。エリア内スロウは短い持続0.4秒を0.25秒間隔で掛け直して維持し(掛け直しはログなし)、退出スロウもApplySlow経由へ変更。ApplySlowにwithLog引数を追加した。死亡時のスロウ解除はCrowdControlControllerの既存処理に集約)
- フェーズ1〜3見直し: 復活時間の既定値を1秒→4秒へ変更(GAME_DESIGN.md 7章: Lv1〜2=4秒/Lv3〜4=6秒/Lv5〜6=8秒。レベル連動はレベルシステム実装後の後続タスク。既存シーンのPlayerは旧値1秒が保存されているためInspectorでRespawn Delay=4へ手動変更が必要。練習用ダミーは1秒のままでよい)
- フェーズ1〜3見直し: GAME_DESIGN.mdのゼルフWの説明を実装に合わせて更新(「攻撃判定なし。」を削除し、持続中の周囲への合計AD×150%連続ダメージと、敵ヒーロー初回命中時のQ即時再使用・同一対象ロック解除を追記。コード変更なし)

### Fixed

- フェーズ1〜3見直し: スタン中でも共通Dが発動できてしまう問題を修正(CommonDControllerがAbilityLockControllerの行動ロックを確認するようにした。スタン中・ゼルフW発動中・Eダッシュ中・死亡中はDを発動できない。共通DはCCを受ける前に予測して押す技のため、スタンを受けてからの後出しは不可。スネア中は仕様どおりDを使用できる)
- フェーズ1〜3見直し: 復活時にゼルフWのクールダウンが全回復してしまう問題を修正(OnHealthRevivedでのクールダウンリセットを削除。デス時のCD短縮は仕様上R・共通D・Fの60%短縮のみで、これは後続タスクで実装予定)

## 2026-07-23

### Added

- フェーズ3: ハードCC(スタン・スネア)とスロウを実装(スタン: 移動・通常攻撃・全スキルを禁止。スネア: 移動と移動スキル(ゼルフQ/E)を禁止し、通常攻撃・W・R・D・Fは使用可能(FはLoL準拠)。スロウ: 基礎移動速度を割合で減少させ、複数同時は最も強い1つのみ適用(LoL方式)・共通Dでは防げない。同種ハードCCの重ねがけは残り時間が長い方を採用。ハードCC中は右クリックの移動先予約のみ受け付け、CC終了後に移動を再開。死亡時は全CCを解除。HardCcTestEmitterをスタン/スネア/スロウ切替式に拡張)
- フェーズ3: スキル射程・範囲のプレビューを実装(SkillRangePreviewを追加。Q: マウス下の対象に発動対象マーカー(射程内は白・射程外はオレンジ=自動接近)、R: 発動した場合の決闘エリア円をキー長押し中に表示。W/Eは従来どおり方向線のみ。Fは押した瞬間に発動する仕様のためプレビューなし。既存のスキルスクリプトは変更せず、設定値をリフレクションで参照する)
- フェーズ3: クールダウンUIをEternal Return風のステータスHUDへ拡張(下部HUDパネルの枠組みを追加し、攻撃力・攻撃速度・移動速度・攻撃射程のリアルタイム表示、HPバー(現在HP / 最大HP)、ポートレート+レベルバッジを表示。レベル表記はレベルシステム実装までのプレースホルダー。移動速度・攻撃射程はステータス単位(MS360・射程200など)で表示する)
- フェーズ3: Fフラッシュを実装(FlashController新規・Fキー・移動距離400=4.0 Unity units・クールダウン55秒。全キャラクター共通)。
  マウスカーソルが指すGround地点へ即座にブリンクし、カーソル地点が最大距離より遠い場合はカーソル方向へ最大距離ぶん移動する。
  壁(Wall Layer)は越えられず、経路上に壁がある場合は壁の手前で停止する(Wall Layer未設定時は壁判定なし。現在のプロトタイプマップに壁はない)。
  Fは押した瞬間に発動する。着地地点のプレビュー表示は行わない。
  W発動中・Eダッシュ中・死亡中などの行動ロック中は発動できない。発動時は進行中の移動・Q/R自動接近を中止し、ブリンクした方向を向く。
  デス時のR・共通D・Fクールダウン60%短縮は後続タスクで実装する。
- PlayerInputHubにFフラッシュのInputActionを追加(FPressedThisFrame / FPressed / FReleasedThisFrame)。
- フェーズ3: クールダウンUIを実装(SkillCooldownHud新規)。LoL / Eternal Return風に画面下中央へQ/W/E/R(大)と共通D/F(小)のスキルスロットを並べる。
  クールダウン中は時計回りのラジアルワイプと残り秒数(10秒未満は小数点1桁)を表示し、完了時は白いフラッシュで通知する。
  W持続中・Eダッシュ中・R決闘エリア中・共通Dウィンドウ中はスロット枠をハイライトする。
  見た目はCGアニメ調に合わせた濃紺ベース+青アクセント(D=橙/F=黄のアクセント付き)で、色・サイズはInspectorで調整可能。
  UIはすべてコード生成(Screen Space Overlay)で、既存スクリプトの変更なし(各コントローラーの_cooldownEndTime/_cooldownをリフレクションで参照するため、該当フィールドを改名する場合はSkillCooldownHudも更新する)。

### Changed

- 仕様変更: 共通D失敗時の0.30秒硬直を廃止した。失敗しても何も起きない(クールダウンのみ消費する)。
  GAME_DESIGN.mdの共通D仕様を更新し、TASKS.mdから「共通D失敗時の0.30秒硬直を実装する」タスクを削除した。
  CommonDControllerの失敗時硬直に関するコメントを整理した(WindowExpiredイベントはUIなどの拡張用に残す)。

### Fixed

- フェーズ3: スタン中に右クリックでキャラクターの向き(視点)を変えられてしまう問題を修正(PlayerMouseFacingにCrowdControlControllerのスタン判定を追加。スタン中は回転を停止し、右クリックによる目標方向の予約のみ受け付けてスタン終了後に回転を再開する。スネア中は通常攻撃などが可能なため、向きの変更は従来どおり許可する)

## 2026-07-22

### Added

- ZelfQControllerにW/E連携用のpublic APIを追加した(GroundLayerMask / TargetableLayerMask / ResetCooldown / ClearLockout / CancelPendingApproach)。ZelfWControllerとZelfEControllerがReflectionを使わずにQの状態を安全に操作できるようにした。
- WとEからQクールダウンリセット、Same Target Lockout解除、自動接近中止を安全に呼べるようにした。WとEがCharacterまたはCharacter扱いTrainingDummyへ命中した際、(1)ResetCooldown (2)ClearLockout(target) の順で即時実行する。
- TASKS.mdでゼルフWの前方ダメージ軽減・ゼルフEの方向ダッシュ・ゼルフE命中時のQ即時再使用の3項目を完了([x])へ更新した。
- PlayerMouseFacingへ、外部スクリプトから安全に目標回転を更新できるpublicメソッドを追加(SetLookTarget: ワールド座標指定 / SetLookDirection: 方向ベクトル指定)。Y軸回転のみを使い、指定地点がPlayerとほぼ同じ位置の場合は安全に何もしない。実際の回転は従来どおり毎フレームInspectorのRotation Speed設定で行われ、右クリックによる回転仕様は変更しない。
- ゼルフWの前方ダメージ軽減を実装(ZelfWController、Scripts/Characters)。Wキー(Input System)で0.75秒間、前方120度から受ける通常ダメージだけを55%軽減する(Duration / Cooldown 10秒 / Front Angle / Damage ReductionはいずれもInspector設定)。前方判定はダメージを受けた瞬間のPlayerのtransform.forwardと攻撃者への水平方向(Y軸高さは含めない)で被ダメージごとに行い、背後・側面からのダメージ、攻撃者情報が取得できないダメージ、確定ダメージ(将来用)は軽減しない。Wは攻撃技ではなくダメージ・ノックバック・スロウ・スタン・スネアを与えず、CC無効化・無敵・対象指定不可・シールドも持たない。持続中はPlayer前方に青い扇形のLineRenderer防御エフェクトを表示し(Playerの回転に追従)、W終了時に非表示になる。軽減発生時はDebug.Logで確認でき、軽減後の実ダメージは既存の被ダメージ表示で表示される。
- ゼルフWへ周囲ダメージを追加した(W Damage Radius 2.0 / Total AD Ratio 1.5 / Tick Interval 0.1秒、いずれもInspector設定)。Duration 0.75秒間に合計AD×1.5分のダメージを_wDamageRadius以内の全Targetableへ毎ティック均等に与える。Character/TrainingDummyへの初回命中時にQのCDを即時リセットし同一対象ロックを解除する。
- ゼルフW発動中は通常攻撃・Q・Eを無効化し、W終了後に元の状態へ復元する(死亡時はPlayerDeathHandlerが管理するため復元しない)。
- ゼルフEの方向ダッシュを実装(ZelfEController、Scripts/Characters)。Eキー(Input System)でマウスカーソルが指すGround上の地点の方向へ、Dash Distance 4.0 Unity unitsをDash Duration 0.18秒かけてダッシュする(Cooldown 8秒、いずれもInspector設定)。マウスがGroundを指していない場合・マウス地点が近すぎる場合・クールダウン中は発動しない。ダッシュ終了後にPost-Dash Wave(3.0 Unity units、Speed 10)を前方へ飛ばし、経路とウェーブ経路で命中したTargetableへBase Damage 20 + AD×50%の通常ダメージを与える。ダッシュ中は青いTrailRendererの残像を表示する。
- ゼルフEのダッシュ中はQ・W・通常攻撃の入力を無効化し、ダッシュ終了(移動完了)後に元の状態へ復元する。ウェーブ中は無効化しない。
- ゼルフE命中時のQ即時再使用を実装。EがCharacter分類(Character扱いTrainingDummy含む)へ命中した瞬間、ZelfQController.ResetCooldown()とClearLockout(target)をその場で即時実行する(PostDashWave終了を待たない)。Minion・Tower分類だけへの命中ではリセットしない。
- ダメージ処理へ攻撃者情報と通常ダメージ分類を追加(Scripts/Combat/DamageInfo.cs: DamageType / DamageContext / IIncomingDamageModifier)。HealthController.TakeDamageが攻撃者のTransformとダメージ種別(Normal / True)を受け取れるようになり、HPへ適用する直前に同じGameObject上のIIncomingDamageModifier(ゼルフWなど)がDamageContext(攻撃者・ダメージ種別・元ダメージ)を使ってダメージ量を変更できる。通常ダメージ(Normal)だけが軽減対象で、確定ダメージ(True)は将来の朧R処刑・ヴォルブラークR反射用の分類のみ用意し今回は使用しない。通常攻撃(PlayerBasicAttackController)・ゼルフQ・ゼルフE・DummyAutoAttackは攻撃者情報を渡すよう更新し、従来のTakeDamage(ダメージ量のみ)も攻撃者なしの通常ダメージとして互換動作する。与ダメージ表示・被ダメージ表示・ゼルフP回復・HPバー・死亡処理の既存経路は変更しない。

- ゼルフRを実装した(ZelfRController.cs / Scripts/Characters)。
  Rキーでマウス下のCharacter/TrainingDummy分類の敵を中心に半径_arenaRadius(初期値5.0)の決闘エリアを展開する。
  エリアは対象の位置に固定され、LineRendererで紫色の輪として可視化される(Duration / Cooldown はInspector設定)。
  発動時にエリア内の全Targetable(自分以外)をエリア外縁0.6m外へ即座に押し出す(ミニオン押し出しタスク対応)。
  Character/TrainingDummy分類の押し出し対象にはエリア外スロウを即時付与する。
  エリア発動中、エリア内のCharacter/TrainingDummy分類の敵にはInner Slow Percentのスロウを毎フレーム維持する。
  エリア内からエリア外へ退出した敵にはOuter Slow PercentのスロウをOuter Slow Duration秒間付与する(エリア内外スロウタスク対応)。
  ゼルフ自身にはSelf MS Boost PercentのMS上昇バフをエリア持続中付与する。
  スロウ/MS上昇はCharacterStats.AddMoveSpeedBonus / RemoveMoveSpeedBonusで管理し、
  エリア終了・対象死亡時に確実に解除する。
  共通Dによる完全不発は共通D未実装のため今回は対象外(将来追加予定)。
- CharacterStatsにBaseMoveSpeedプロパティ・AddMoveSpeedBonusメソッド・RemoveMoveSpeedBonusメソッドを追加した。
  ZelfRControllerのスロウ/MS上昇計算から利用する。既存のCurrentMoveSpeed計算は変更しない。
- ZelfEControllerのダッシュ開始時にZelfRControllerもenabled=falseへ無効化し、ダッシュ終了後に元の状態へ復元するようにした。
  「Eダッシュ中は全スキルを無効化する」仕様に合わせてRも対象とした。
- ZelfWControllerのW発動時にZelfRControllerもenabled=falseへ無効化し、W終了後に元の状態へ復元するようにした。
  「W発動中はスキルを無効化する」仕様に合わせてRも対象とした。
- TASKS.mdでゼルフR関連の3タスク(決闘エリア・ミニオン押し出し・エリア内外スロウ)を完了([x])へ更新した。

### Changed

- フェーズ3: 共通D成功時のカウンター攻撃を実装(無効化成功時、攻撃者へ45+ADの30%の通常ダメージ。追加スタン・スネアは与えない)。
- フェーズ3: 共通D成功時に移動速度が10%上昇する効果を実装(持続は既定1.5秒・Inspector調整可。効果中の再成功は掛け直しで重複加算しない。死亡時は即解除)。
- フェーズ3: 共通Dの0.20秒CC無効化を実装(CommonDController新規・Dキー・クールダウン34秒)。ウィンドウ中に受けた最初のハードCCを1回だけ無効化し、CounterSucceeded/WindowExpiredイベントで後続タスク(成功時カウンター・失敗時硬直)へ拡張できる。
- フェーズ3: CCを受け取る共通の入口CrowdControlControllerを新設(ApplyHardCC。ハードCC専用でスロウは対象外)。行動制限の実体は後続タスク「ハードCC、スネア、スタン、スロウを実装する」で実装する。
- フェーズ3: テスト用HardCcTestEmitterを追加(一定間隔+予告ログでハードCCを発射。TrainingDummyにアタッチして使用)。
- PlayerInputHubに共通DのInputActionを追加(DPressedThisFrame)。
- フェーズ3準備(phase3-prep2): スキルインジケーターを指定方式ごとに統一。
  対象指定(Q/R)=攻撃範囲円、方向指定(W/E)=方向線のみ(W=前方軽減の向き、E=ダッシュ方向)、
  場所指定=発動地点マーカー(SkillRangeIndicator.ShowPointMarkerを新設。フェーズ3の場所指定スキルで使用)。
  Wの分類を無指定→方向指定に訂正(SkillTargetingTypeのコメント更新)。
- フェーズ3準備(phase3-prep1): スキル発動方式を「キーを押すと範囲表示・離すと発動」(NormalCast)にQ/W/E/Rで統一。
  SkillCastMode enumを新設し、各スキルのInspectorでQuickCast(押した瞬間に発動)へ個別に切替可能。
- フェーズ3準備(phase3-prep1): 汎用範囲インジケーターSkillRangeIndicator(円+方向線)を新設。
  Wは効果範囲円、Eはダッシュ射程円+本体→カーソル方向線を押下中に表示(Q/Rは既存の射程円を継続使用)。
- フェーズ3準備(phase3-prep1): 停止コマンド(Sキー)を追加。移動中断・ターゲット解除・Q/R自動接近の中止を行いその場で停止する。
- フェーズ3準備(phase3-prep1): スキル指定方式の共通語彙SkillTargetingType enum(対象指定/地点指定/方向指定/無指定)を新設。
  現行スキルの分類: Q=対象指定 / W=無指定 / E=方向指定 / R=対象指定。
- フェーズ1・2総合改善(phase1-2-fix4): 通常攻撃の被弾フラッシュを実ダメージが通った時のみ発生させるようにした
  (ゼルフWの軽減などでダメージ0の場合は光らない。Q/W/Eと同じ基準に統一)。
- フェーズ1・2総合改善(phase1-2-fix4): Camera.mainの毎フレーム検索を廃止し、Awakeでキャッシュ・破棄時のみ再取得する方式へ統一した
  (ZelfQController・PlayerTargetSelector)。
- フェーズ1・2総合改善(phase1-2-fix4): 死亡中はターゲット選択を停止し、選択中のターゲットも解除するようにした。
  あわせてPlayerTargetSelectorの入力をPlayerInputHub(InputAction)経由へ移行した。
- フェーズ1・2総合改善(phase1-2-fix3): 最大HPの動的変化に対応した。HealthControllerが最大HPの変化を毎フレーム検知し、
  増加分は現在HPへ加算(LoL方式)・減少時は現在HPをクランプする。CharacterStatsにAddMaxHealthBonus/RemoveMaxHealthBonusを追加。
- 数値のSO一元管理(phase1-2-fix3): CharacterStatsにCharacter Data参照を追加し、AwakeでZelfData.assetの基礎値を適用するようにした。
  ステータス単位→Unity単位の換算定数(MS60=1unit/s、射程100=1unit)をCharacterStatsで一元管理。未設定時は従来どおりInspector値を使用(後方互換)。
- 入力のInputAction移行(phase1-2-fix3): PlayerInputHubを新規追加し、Q/W/E/R・右クリック・マウス座標の取得を
  Keyboard.current/Mouse.currentの直接ポーリングからInputActionへ置き換えた。Awakeで自動追加されるためInspector設定不要。
- 行動ロックの診断ログを強化(phase1-2-fix2): ロック中にQ/E/Rを入力した際に必ず理由をConsoleへ出力するようにした。
  Eはロック判定をクールダウン判定より先に移動し、ダッシュ中・死亡中の押下もログを出す。
  AbilityLockControllerはロックの追加/解除をログに出す。
- フェーズ1・2総合改善(phase1-2-fix1): 参照カウント式の行動ロック `AbilityLockController` を新規追加。
  W発動・Eダッシュ・死亡による通常攻撃/Q/W/E/Rの禁止を、コンポーネントのenabled切り替えからロック方式へ一本化し、
  復元漏れ・二重復元を構造的に防止(各コントローラーがAwakeで自動追加するためInspector設定不要)。
  Phase 3のCC(スタン・スネア・共通D硬直)はロック理由を追加するだけで実装可能。
- ゼルフR: W/E発動中にRコンポーネントが無効化されなくなり、発動済みの決闘エリアがW/E中も正しく進行・終了するようになった
  (従来は持続時間が凍結し実質延長されていた)。
- ゼルフR: ゼルフ自身の死亡時に決闘エリアを即時終了し、自身MSブースト・スロウを全解除するようにした。
- 自動接近の二重制御を防止: Q/Rの射程外自動接近中は通常攻撃の自動接近を停止し、QとRの自動接近も相互排他にした。
- ゼルフRのCast RangeをInspector設定不要にした。
  旧バージョンのシーンで0のまま保存されていても、OnValidate(エディタ)と
  Awake(実行時)で既定値7へ自動補正する。初期化ログにCastRange値を表示する。
- ゼルフRの押し出し対象からCharacter・TrainingDummy・Tower分類を除外した。
  押し出されるのはミニオン等のみで、キャラクターとタワーはエリア内に留まる。
- ゼルフRを「R長押しで射程円表示 → キーを離して発動」方式へ変更した。
  Cast Range(初期値7)の射程円をRキー押下中に自分中心へ表示する(ZelfQと同方式)。
- ゼルフRを射程外の対象に発動した場合、射程内まで自動接近してから発動するようにした。
  右クリック・自身死亡・対象消失・E/WによるR無効化で自動接近は中止される。
- ZelfQControllerからReflection依存(System.Reflectionのusing / FieldInfo / BindingFlags / privateフィールド名・メソッド名を文字列で参照する処理)を全て削除。ブリンク後の向き更新はPlayerMouseFacingのpublicメソッド、ゼルフP回復への通知はZelfPassiveHeal.NotifyDamageDealt()の直接呼び出しへ変更。PlayerMouseFacingのprivateフィールドはPlayerMouseFacing内部だけで管理する。
- ゼルフQの対象決定を正しい仕様へ修正。Qの対象はマウス下の有効なTargetableのみとし、PlayerTargetSelectorで選択中の対象はQの対象決定に使用しない(従来はマウス下に対象がいない場合、選択中の対象へフォールバックしていた)。マウス下に有効な対象がいない場合、またはマウス下の対象がTower分類の場合、Qは発動しない。
- ゼルフQの射程外処理の正しい仕様を記録: マウス下の対象がQ射程外の場合、Playerは対象へ自動接近し、Q射程内に入った時点でQを自動発動する。自動接近中に右クリック入力があった場合、または対象が死亡・無効化・破棄・Tower分類へ変化した場合は自動接近を中止する(挙動は従来どおり)。
- QダメージをQ命中時に攻撃対象の頭上へ赤色で表示するよう修正(通常攻撃と同じCombatTextManager.ShowDamageDealtのプレイヤー視点表示経路。従来のZelfQControllerは与ダメージ表示を呼び出していなかった)。
- 視点仕様を整理: Playerは移動している方向へ視点が向き、ブリンクした場合はブリンクした方向を向くことを基本とする。Q射程外の自動接近中に視点が移動方向へ向かない問題を修正(接近中は毎フレームPlayerMouseFacing.SetLookDirection()へ移動方向を渡し、回転自体は従来どおりRotation Speed設定で行う)。
- Qブリンク後の向きを「対象の方向」から「ブリンクした方向」へ変更(通常は同じ方向。ブリンク移動量がほぼゼロの場合のみ対象の方向へフォールバック)。視点方向は各スキル・移動処理がPlayerMouseFacingのpublic APIへ明示的に方向を渡して指定する構成のため、将来の「ブリンク方向と視点方向が異なるスキル」は別の方向を渡すだけで実装できる。
- ゼルフQ実装時にCHANGELOG.md先頭の形式例(コードブロック)内へ自動挿入されていた2026-07-21のQ実装記録を、正しい2026-07-21のAdded節へ移動(形式例は元のYYYY-MM-DDテンプレートへ復元)。

### Removed

- ZelfQProjectSetup.cs(Scripts/Editor)を削除。Unity Editorのメニュー操作でSC_Prototypeを設定し、TASKS.md / CHANGELOG.mdを自動書き換える仕組みを廃止(ゲーム実装とMarkdown文書更新の分離、存在しないprivateフィールドを文字列で設定する不安定な処理と、Layer番号6・7を固定値で扱う処理の排除)。ZelfQControllerに必要な参照・レイヤー・数値と、TrainingDummyの分類・HPはSC_PrototypeシーンのInspector設定として保存済みのため、削除後もゼルフQは動作する。

### Fixed

- ゼルフRが発動できなくなる場合がある問題を修正した。
  - ZelfWController: W未使用のまま復活すると、初期値falseの「WasEnabled」で
    通常攻撃・Q・E・Rが上書きされて永久に無効化されるバグを修正。
    _skillsDisabledByWフラグで「Wが実際に無効化した場合のみ復元」するようにした。
  - ZelfEController: ダッシュ中に死亡するとQ・W・Rが無効化されたまま復元されない
    バグを修正。AbortDashOnDeathでも_skillsDisabledByDashフラグに基づき復元する。
  - ZelfRController: 診断用に初期化・有効化・無効化のログを追加。
    Play開始時に「初期化しました」ログが出ない場合はコンポーネント未アタッチと判別できる。
    自身死亡中のR発動も防止した。


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
- ゼルフ詳細パネルを追加。名前・役割・Short Description・イメージカラー・大きな仮プレースホルダー・基礎ステータス(HP/AD/AS/AR/MS/AA Range)・P〜Rの短いスキル一覧を表示する。スキルの詳細説明はCharacterDataが保持し、将来ツールチップや別パネルとして表示できる(今回は未実装)。
- 「プロトタイプを開始」ボタンを追加。選択したCharacterDataをCharacterSelectionManager(DontDestroyOnLoad・二重生成防止)が保持したままSC_Prototypeを読み込む。SC_CharacterSelectとSC_PrototypeをBuild SettingsのScene Listへ登録し、起動時はSC_CharacterSelectから始まる。
- ゼルフの通常攻撃を実装。選択中のTargetableが攻撃射程内の場合のみ、攻撃間隔(Current Attack Speed)ごとにCharacterStatsのCurrent Attack Damageを対象のHealthControllerへ即時に与える(弾丸・投射物・攻撃アニメーションなし)。射程外では攻撃せず、ターゲット死亡時は攻撃を停止してターゲット選択を安全に解除する(既存の射程判定・自動接近・被弾フラッシュは維持)。
- ターゲット分類を追加。TargetableにTarget Classification(Character / Minion / Tower / TrainingDummy)をInspectorで設定でき、将来のキャラクター・ミニオン・タワーでも再利用できる。TrainingDummyは初期状態でCharacter分類とする。
- ゼルフP(与ダメージ回復)を実装(ZelfPassiveHeal、Scripts/Characters)。実際に与えたダメージ量(実ダメージ。残りHPを超えた過剰ダメージ分は含まない)を基準に、Character分類は5%、Minion分類は2.5%、Tower分類は0%を回復する(回復率はInspector設定。テスト用のTrainingDummy分類はCharacterと同じ5%)。最大HPを超えず、死亡中は回復しない。回復のクールダウン・追加回復・回復阻害・ライフスティールは未実装。
- ダメージ表示システムを追加(FloatingCombatText / CombatTextManager、Scripts/UI)。攻撃した側の頭上に与ダメージを赤(例: 60)、受けた側の頭上に被ダメージを青(例: -60)、ゼルフPで実際にHPが増えた場合のみ回復量を緑(例: +3)で表示する。ワールド空間のWorld Space Canvas+標準Text(LegacyRuntimeフォント)による整数表示で、短時間上方向へ移動しながらフェードアウトし、常にMain Cameraの方向を向いて裏返らず、ランダムな横方向オフセットで重なりを軽減する。表示終了後は安全に削除され、プール処理は未実装だが将来プールへ置き換えやすい構造。将来のキャラクター・ミニオン・タワーからも共通利用できる。
- 攻撃ダミー(AttackDummy)をSC_Prototypeへ1体追加(DummyAutoAttack、Scripts/Characters)。攻撃射程内のPlayerへ攻撃間隔ごとに即時ダメージを与え(攻撃力10・攻撃速度1・射程2、いずれもInspector設定)、実際に与えたダメージ量をPlayerの頭上に黄色で表示する。自身または対象の死亡中は攻撃しない。本体はTrainingDummyと同構成(HP300・Character分類・被弾フラッシュ・選択リング・HPバー付き)。移動・追跡・弾丸・攻撃アニメーション・敵AIは未実装。
- 復活処理を追加(RespawnController、Scripts/Combat)。Player・TrainingDummy・AttackDummyが死亡から1秒後(Inspector設定)に初期位置・初期向きでHP全快で復活する。HealthControllerにRevive(全快)と復活イベントを追加し、Targetable(本体・Collider)、PlayerDeathHandler(操作系・見た目)、WorldHealthBar(HPバー)がそれぞれ復元する。復活したダミーの再選択は右クリックで行う。
- ゼルフQの対象ブリンク、対象指定ダメージ、同一対象ロック、分類別クールダウン処理を実装。
- ZelfQControllerを追加。Qキーで選択中の有効なCharacter / Minion / TrainingDummyへ、Collider最寄り点を基準に安全な停止距離でブリンクする。
- Qダメージを `Base Damage + Current Attack Damage × AD Ratio` としてHealthController経由で適用する。
- Q成功対象へSame Target Lockoutを設定し、ロック中の同一対象にはブリンク・ダメージ・クールダウン消費を発生させない。
- Character / TrainingDummy分類へのQ命中時はQクールダウンを即時リセットし、Minion分類への命中時は残りクールダウンを50%短縮する。Tower分類には発動しない。
- Qダメージでも既存の被弾フラッシュ、ダメージ表示、ゼルフP回復の通知経路を利用する。

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
