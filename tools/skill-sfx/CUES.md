# Skill SE cue sheet

`GAME_DESIGN.md`と`TASKS.md`の現行スキルをもとにした、初期試聴用のオリジナルSE。短時間1v1で、移動開始・命中・CC・Rの決定機を聞き分けられるようにしている。ゼルフは短い風切りと鋼、朧は乾いた「ザクッ」という斬撃、ヴォルブラークは重低音の打撃、リネスは短い合成放電、リーゼロッテは高音域の鋭い輝きで音色を分ける。丸い音程下降や跳ねる低音は使わない。通常攻撃、共通D/F、ミニオン、タワーのSEはこのセットに含めない。

本体は `Game/Assets/Game/Audio/SFX/Skills/<Character>/` にある48 kHz・16-bit PCM・mono WAV。全38本。ゼルフP、ヴォルブラークP消費・Q亀裂・R反射にはSEを付けない。各キャラの連続試聴ファイルは `previews/`。`generate.py` で同じ音を再生成でき、`validate_and_preview.py` で音声形式・無音・ピーク・末尾を検査できる。外部サンプルやAPIは使用していない。

これらは **音声素材のみ**。発動条件への再生コード、音量・定位・同時発音数、ミキサーへの接続はまだ行っていない。将来接続する際は、発動不可・命中なし・共通D成功・死亡中などに応じて適切なキューを選び、連続ヒットで過剰に鳴らさないこと。試聴と実戦で音量を調整する。

## ゼルフ（鋼・風）

| ファイル | 再生する段階 |
|---|---|
| `Q_BlinkHit.wav` | Qの対象ブリンクと命中時 |
| `W_GuardWhirl.wav` | Wの防御・旋回開始時 |
| `E_Dash.wav` | Eのダッシュ開始時 |
| `E_WaveHit.wav` | Eの前方ウェーブ放出・命中時 |
| `R_DuelField.wav` | Rの決闘エリア生成時 |
| `R_BoundarySlow.wav` | Rエリア外へ出た敵への大スロウ成立時。繰り返し再生を制限する |

## 朧（刃・影）

| ファイル | 再生する段階 |
|---|---|
| `P_Backstab.wav` | Pの背後通常攻撃追加ダメージ成立時 |
| `Q_ShurikenThrow.wav` | Qの手裏剣投擲時 |
| `Q_Teleport.wav` | Qの最後の接触対象または地点への転移時 |
| `W_StealthEnter.wav` | Wの透明化開始時 |
| `W_StealthExit.wav` | Wの透明化解除時 |
| `E_BackAttack.wav` | Eの背後移動と攻撃成立時 |
| `E_Return.wav` | Eで開始地点へ帰還した時。CCで帰還阻害された時は鳴らさない |
| `R_Execute.wav` | Rの処刑ダメージ成立時。Dなどで不発なら鳴らさない |

## ヴォルブラーク（岩・鎖・低音金属）

| ファイル | 再生する段階 |
|---|---|
| `P_ShieldReady.wav` | Pのシールド獲得時 |
| `Q_GroundSlam.wav` | Qの地面打撃時 |
| `W_ShieldStart.wav` | Wのシールド獲得時 |
| `W_Detonation.wav` | Wの時限爆発時 |
| `E_Charge.wav` | Eの突進開始時 |
| `E_StunHit.wav` | Eのダメージとスタン成立時。Dで防がれたら鳴らさない |
| `R_ChainLaunch.wav` | Rの鎖を飛ばす時 |
| `R_ChainLatch.wav` | Rの鎖が対象に接続された時 |

## リネス（短い合成放電）

| ファイル | 再生する段階 |
|---|---|
| `P_Overcharge.wav` | Pの重雷追加ダメージが成立した時 |
| `Q_Mark.wav` | Qの1段目、地点指定時 |
| `Q_LightningStrike.wav` | Qの2段目、実際の落雷時。時間切れの取り消しでは鳴らさない |
| `W_RingPulse.wav` | Wの自分中心の雷が発動した時 |
| `E_ThunderForm.wav` | Eの雷化開始時 |
| `E_Contact.wav` | Eの最初の有効な接触とスネア成立時 |
| `R_Warning.wav` | Rの予告を置いた時 |
| `R_ThunderImpact.wav` | Rの予告終了後に大雷が落ちた時。中心・外周共通 |

## リーゼロッテ・ヴァイス（高音域の鋭い輝き）

| ファイル | 再生する段階 |
|---|---|
| `P_FourthShot.wav` | Pの4打目の追加ダメージ成立時 |
| `Q_Bite.wav` | Qの噛みつき命中と回復成立時 |
| `W_BloodRush.wav` | P発動に伴うWのMS・AS上昇開始時。Wキー入力には紐付けない |
| `E_Blink.wav` | Eの方向ブリンク開始時 |
| `E_BloodPool.wav` | Eの血だまり生成時。対象ごとのスロウで重複再生しない |
| `R_Dash.wav` | Rの方向突進開始時 |
| `R_StatSteal.wav` | Rのステータス奪取成立時。共通Dで防がれたら鳴らさない |
| `R_Release.wav` | Rの奪取効果が終了・解除された時 |
