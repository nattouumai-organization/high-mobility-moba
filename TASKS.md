# フェーズ5 タスクリスト

## タスク1: マップの実装
- [x] MapBuilder でランタイム生成（1レーン対称マップ）
- [x] 開始地点（GetSpawnPoint）・境界（BoundsMin/BoundsMax）を公開
- [x] TopDownCameraController にマップ境界クランプを追加

## タスク2: 開始地点の設定
- [x] PlayerSpawner に Team プロパティを追加
- [x] スポーン時に TeamMember コンポーネントを Player に付与

## タスク3〜4: タワーの実装・連続攻撃
- [x] TowerController (HP5000/AR60/AD130/AS0.80/射程8.0)
- [x] TeamMember で敵味方判定、近接する敵ヒーローを優先して攻撃
- [x] 連続攻撃ボーナス (+25%/発、上限+200%=3倍、2秒リセット)

## タスク5: タワーのミニオン不在時ダメージ軽減
- [x] IIncomingDamageModifier で AR 軽減を TowerController 自前適用
- [x] 射程内に味方 MinionController(Team一致) がいない場合、通常ダメージ ×0.1（90%軽減）
- [x] 確定ダメージ(True)は完全無効(0)
- [x] MinionController スタブ

## タスク6: 本拠地の実装
- [x] NexusController (HP6000/AR50)
- [x] タワー破壊後のみ被ダメージ、GameManager.OnNexusDestroyed で勝敗通知
- [x] TowerController 破壊時に敵陣NexusController.OnGuardTowerDestroyed()

## fix1: コンパイルエラー修正（Team/TeamMember 未定義）
- [x] Core/Team.cs 追加（Team enum が未定義で CS0246 全件発生していた）
- [x] Core/TeamMember.cs 追加
- [x] 冪等パッチ（二重適用防止）

## fix2: using ディレクティブ不足修正
- [x] TowerController / NexusController に `using Combat;` `using Characters;` を追加
- [x] → ただしこれらの namespace は存在しなかったため fix3 で再修正

## fix3: 正しい API 名に全面修正
- [x] `using Combat;` `using Characters;` を削除（namespace なし→直接参照）
- [x] HealthController.OnDeath  → .Died
- [x] DamageContext.DamageType  → .Type
- [x] Targetable.IsTargetable   → !t.IsDead
- [x] Targetable.TargetClassification → .Classification
- [x] InitializeRuntime パッチ: _targetClassification → _classification / _ringRenderer → _selectionRingRenderer
- [x] IIncomingDamageModifier シグネチャ: ModifyIncomingDamage(DamageContext context, float currentAmount)
- [x] DamageInfo.cs を ZIP に同梱

## タスク7: 勝敗UI・ゲームループリスタート（未実装）
- [ ] 勝敗 UI（リザルト画面）
- [ ] ゲームループのリスタート
