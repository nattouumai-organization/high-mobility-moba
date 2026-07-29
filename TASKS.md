# フェーズ5 タスクリスト

## タスク1: マップの実装
- [x] MapBuilder でランタイム生成（1レーン対称マップ）
- [x] 地面 Plane のレイヤーを GroundLayer に自動設定
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

## fix1〜fix3: コンパイルエラー修正
- [x] Team.cs / TeamMember.cs 追加
- [x] using Combat; / using Characters; 削除（namespace なしのトップレベルクラスのため）
- [x] API名全修正: Died / .Type / !IsDead / .Classification
- [x] InitializeRuntimeパッチフィールド名修正
- [x] DamageInfo.cs 同梱

## fix4: フィールド未生成 / Ground レイヤー未設定 修正
- [x] MapBuilder: 地面のレイヤーを GroundLayer へ自動設定 → 右クリック移動・スキル発動修正
- [x] MapBuilder: Inspector 未割り当て時はタワー・ネクサスのビジュアルプリミティブを自動生成
   • Tower: Cylinder, チームカラー (Blue=青/Red=赤)
   • Nexus: Cube, チームカラー (タワーより暗め)
- [x] MapBuilder: GroundLayer が見つからない場合はフォールバック番号 6 を使用して警告表示
- [x] NexusController: _crystalRenderer 未設定時に GetComponentInChildren<Renderer>() で代替

## タスク7: 勝敗UI・ゲームループリスタート（未実装）
- [ ] 勝敗 UI（リザルト画面）
- [ ] ゲームループのリスタート
