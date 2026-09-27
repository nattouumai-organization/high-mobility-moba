# High Mobility MOBA スキル属性

## ゼルフ

### 通常攻撃

- ダメージ
  - 通常攻撃ダメージ（damage.basic_attack）
- 対象
  - 対象指定（target.target_selection）

### P

- 自己対象
  - ヒール（self.heal）
- 対象
  - 指定無し（target.no_target）

説明: 与ダメージの5%を回復（対敵ヒーロ）。ミニオンの場合は効果が半減。

### Q

- ダメージ
  - スキルダメージ（damage.skill）
- 移動
  - ダッシュ（movement.dash）
- 対象
  - 対象指定（target.target_selection）

説明: ミニオンまたは敵ヒーローへダッシュしてダメージ。同一対象へ短時間連続使用不可。異なる対象へは連続して発動可能。対象に他スキル命中で再使用可能。敵の周りにQを発動できるか表示。

### W

- ダメージ
  - スキルダメージ（damage.skill）
- 自己対象
  - ダメージ軽減（self.damage_reduction）
- 対象
  - 方向指定（target.direction_specification）

説明: 0.75秒間、前方120度から受ける通常ダメージを55%軽減。CCは防げない。持続中、周囲の敵へ合計AD×150%の連続ダメージを与える。

### E

- ダメージ
  - スキルダメージ（damage.skill）
- 移動
  - ダッシュ（movement.dash）
- 対象
  - 方向指定（target.direction_specification）

説明: マウスカーソル方向へダッシュし、ダッシュ経路とダッシュ後に前方へ飛ぶウェーブ(距離300)の経路にいる対象へ1回ずつダメージ。

### R

- 自己対象
  - 移動速度上昇（self.increase_ms）
- CC
  - ノックバック（cc.knockback）
  - スロウ（cc.slow）
- 対象
  - 対象指定（target.target_selection）

説明: 敵中心に決闘エリアを作る。ミニオン・自軍ミニオンを外へ押し出す。内側の敵はスロウ、ゼルフはMS上昇。外へ出た敵は大きくスロウ。共通Dで完全不発。

## ヴォルブラーク

### 通常攻撃

- ダメージ
  - 通常攻撃ダメージ（damage.basic_attack）
- 対象
  - 対象指定（target.target_selection）

### P

- 自己対象
  - シールド（self.shield）
- 対象
  - 指定無し（target.no_target）

説明: 一定時間無被弾で、次の攻撃を一度、完全無効化するシールド。消費まで永続。ミニオン攻撃では剥がれない。タワー攻撃は1回無効化するがPを消費。

### Q

- ダメージ
  - スキルダメージ（damage.skill）
- CC
  - スロウ（cc.slow）
- 対象
  - 方向指定（target.direction_specification）

説明: 地面を叩いて範囲攻撃。亀裂を残し、亀裂上の敵をスロウ。

### W

- ダメージ
  - スキルダメージ（damage.skill）
- 自己対象
  - シールド（self.shield）
  - ヒール（self.heal）
- 対象
  - 指定無し（target.no_target）

説明: シールドを得る。一定時間後に自動爆発し範囲ダメージ。与ダメージの5%を回復、ミニオン相手は半減。手動爆発なし。

### E

- ダメージ
  - スキルダメージ（damage.skill）
- CC
  - スタン（cc.stun）
- 移動
  - ダッシュ（movement.dash）
- 対象
  - 方向指定（target.direction_specification）

説明: 突進してダメージ＋スタン。共通Dで弾かれた場合は両方不発。

### R

- ダメージ
  - スキルダメージ（damage.skill）
- CC
  - 鎖（cc.chain）
- 対象
  - 方向指定（target.direction_specification）

説明: 鎖を飛ばし、敵と一定距離以上離れられない状態にする。同時間、敵ヒーローから受けたダメージを確定ダメージで自動反射。Dで鎖を弾かれても反射は付与。反射は再反射しない。タワー、ミニオン、設置物、自己ダメージは反射対象外。  


## 朧

### 通常攻撃

- ダメージ
  - 通常攻撃ダメージ（damage.basic_attack）
- 対象
  - 対象指定（target.target_selection）

### P

- ダメージ
  - 通常攻撃ダメージ（damage.basic_attack）
- 対象
  - 対象指定（target.target_selection）

説明: 背後からの通常攻撃に固定20 + ADの40%の追加通常ダメージ。対象は敵ヒーローのみ。背後判定は対象の後方を中心とした120度。クールダウンなし。

### Q

- ダメージ
  - スキルダメージ（damage.skill）
- 移動
  - ブリンク（movement.blink）
- 対象
  - 方向指定（target.direction_specification）

説明: 手裏剣を投げ、接触した各敵へ20 + ADの50%のダメージ。最後に当たった敵チャンピオンまたはミニオンへブリンク。2ストック。対象が消えた場合は最後の地点へ飛ぶ。

### W

- 自己対象
  - ステルス（self.stealth）
  - 移動速度上昇（self.increase_ms）
- 対象
  - 指定無し（target.no_target）

説明: 3秒間透明化。対象指定不可だが方向・地点指定スキルには当たる。MS上昇。攻撃、スキル、再発動で解除。敵の近くでは輪郭が見える。敵タワー内では通常どおり対象指定可能。

### E

- ダメージ
  - 通常攻撃ダメージ（damage.basic_attack）
- 自己対象
  - スキル使用不可（self.skills_disabled）
- 対象
  - 対象指定（target.target_selection）

説明: 指定した敵チャンピオンの真後ろへ移動し、通常攻撃＋追加ダメージ。その後、元の地点へ戻る。戻るまでスキル使用不可。CCを受けると帰還阻害され、その場に残る。開始地点は両者に表示。  


### R

- ダメージ
  - 確定ダメージ（damage.true_damage）
- 対象
  - 対象指定（target.target_selection）

説明: 最大HP10%以下の敵チャンピオンへ、最大HP10%分の確定ダメージで処刑。レンジ200。処刑圏はHPバーに明示。シールド・ダメージ軽減で耐えられる。  


## リネス

### 通常攻撃

- ダメージ
  - 通常攻撃ダメージ（damage.basic_attack）
- 対象
  - 対象指定（target.target_selection）

### P

- ダメージ
  - スキルダメージ（damage.skill）
- 対象
  - 指定無し（target.no_target）

説明: 敵がハードCC中に追加のハードCCを当てるとダメージ増加。スロウは対象外。発動回数制限・CC減衰なし。

### Q

- ダメージ
  - スキルダメージ（damage.skill）
- CC
  - スネア（cc.snare）
- 対象
  - 場所指定（target.specify_location）

説明: 1回目で地点指定、2回目で雷を落とす。ダメージ＋スネア。予兆があるため、命中難度が高い。

### W

- ダメージ
  - スキルダメージ（damage.skill）
- CC
  - スロウ（cc.slow）
  - スネア（cc.snare）
- 対象
  - 指定無し（target.no_target）

説明: 自分中心の雷。周囲にダメージ＋スロウ。対象がハードCC中ならスロウではなくスネア。

### E

- ダメージ
  - スキルダメージ（damage.skill）
- 自己対象
  - 移動速度上昇（self.increase_ms）
- CC
  - スネア（cc.snare）
- 対象
  - 指定無し（target.no_target）

説明: 雷化してMS上昇。敵に当たるとダメージ＋スネア。命中0.2秒後に雷化終了。

### R

- ダメージ
  - スキルダメージ（damage.skill）
- CC
  - スタン（cc.stun）
- 対象
  - 場所指定（target.specify_location）

説明: 指定地点に大雷。ダメージ。中心命中でスタン。共通Dで完全不発。

## リーゼロッテ・ヴァイス

### 通常攻撃

- ダメージ
  - 通常攻撃ダメージ（damage.basic_attack）
- 対象
  - 対象指定（target.target_selection）

### P

- ダメージ
  - 通常攻撃ダメージ（damage.basic_attack）
- 対象
  - 指定無し（target.no_target）

説明: 敵ヒーローへの通常攻撃ダメージを当てるとスタックがつく。３スタック溜まった状態の敵を攻撃することで追加ダメージ。発動後すぐ再スタック可能。スタックは数秒で消え、ミニオンでは蓄積しない。

### Q

- ダメージ
  - 通常攻撃ダメージ（damage.basic_attack）
- 自己対象
  - ヒール（self.heal）
- 対象
  - 指定無し（target.no_target）

説明: 対象指定噛みつき。ダメージ＋自己回復。敵ヒーロー相手の回復は高く、ミニオン相手は低い。Pの3スタックがあれば回復増加。３つなければスタックが１増える。

### W

- 自己対象
  - 移動速度上昇（self.increase_ms）
  - AS上昇（self.increase_as）
- 対象
  - 指定無し（target.no_target）

説明: P発動時、MSと少量のAS上昇。

### E

- ダメージ
  - スキルダメージ（damage.skill）
- CC
  - スロウ（cc.slow）
- 対象
  - 方向指定（target.direction_specification）

説明: 方向ブリンク。経路に血だまりを残す。踏んだ敵にダメージ＋短いスロウ。Q使用でEのクールダウン短縮。

### R

- 対象
  - 方向指定（target.direction_specification）

説明: 方向突進して噛みつく。直接ダメージなし。5秒間、敵のHP・AD・AS・ARを10%奪う。MSを8%奪う。効果は重複しない。HP変動時は双方の現在HP割合を維持。共通D成功時は突進のみで奪取なし。

補足: 属性は方向指定のみのスキルですが、敵のステータスを減らし、その分自分のステータスにするため注意。
