using System;
using System.Collections.Generic;
using UnityEngine;
/// <summary>
/// 不屈ルーン: 2s以内に最大HP15%以上受ける→最大HP8%シールド2.5s。CD40s。
/// シールドはIIncomingDamageModifierとしてHealthControllerへ接続し、実際にダメージを吸収する(phase7-runes-fix4)。
/// 通常ダメージ(Normal)はAR軽減後のHP換算値でシールドを消費する(VolbraakWと同じ方式のため二重軽減なし)。
/// シールド残量はShieldAmountで公開し、WorldHealthBarがHPバーの白いゲージとして表示する。
/// </summary>
public class IndomitableRune : MonoBehaviour, IIncomingDamageModifier
{
    private CharacterStats _stats;
    private HealthController _health;
    private float _cdEnd = -1f;
    private float _shield, _shieldEnd;
    private readonly List<(float t, float d)> _recent = new List<(float, float)>();
    private Action<DamageContext, float> _dmgH;
    private void Awake()
    {
        _stats = GetComponent<CharacterStats>();
        _health = GetComponent<HealthController>();
        if (_health != null)
        {
            _dmgH = (_, d) => OnDmg(d); _health.DamageTaken += _dmgH;
            // ルーンは実行時にAddComponentされるため、HealthControllerのIIncomingDamageModifier
            // キャッシュ(Awakeで取得済み)を再取得させ、シールドが実際にダメージを吸収できるようにする(phase7-runes-fix4)。
            _health.RefreshDamageModifiers();
        }
    }
    private void OnDestroy() { if (_health && _dmgH != null) _health.DamageTaken -= _dmgH; }
    private void Update()
    {
        if (_shield > 0f && Time.time >= _shieldEnd)
        {
            // 発動確認用ログ: シールドが時間切れで消滅。
            Debug.Log($"[ルーン/不屈] シールド終了 (残り {_shield:F1} を破棄)", this);
            _shield = 0f;
        }
    }
    private void OnDmg(float d)
    {
        if (d <= 0f || Time.time < _cdEnd) return;
        float now = Time.time;
        _recent.RemoveAll(e => now - e.t > 2f);
        _recent.Add((now, d));
        if (_stats == null) return;
        float total = 0f; foreach (var e in _recent) total += e.d;
        if (total >= _stats.CurrentMaxHealth * 0.15f)
        {
            _recent.Clear(); _cdEnd = now + 40f;
            _shield = _stats.CurrentMaxHealth * 0.08f;
            _shieldEnd = now + 2.5f;
            // 発動確認用ログ: 発動条件(2秒以内に最大HP15%以上被弾)成立。
            Debug.Log($"[ルーン/不屈] 発動！ 被弾合計 {total:F1} → シールド {_shield:F1} (2.5秒) / CD 40秒", this);
        }
    }
    /// <summary>
    /// HealthControllerがHPへ適用する直前に呼び出すダメージ変更処理(IIncomingDamageModifier)。
    /// シールド残量がある間、ダメージ種別(Normal / True)を問わず吸収する。
    /// 通常ダメージ(Normal)はAR軽減後のHP換算値でシールドを消費し、吸収しきれない分だけを元ダメージ換算へ戻して返す
    /// (返した値にはこの後HealthControllerがARによる軽減を適用するため、二重軽減にはならない。VolbraakWと同じ方式)。
    /// </summary>
    public float ModifyIncomingDamage(DamageContext context, float currentAmount)
    {
        if (_shield <= 0f || currentAmount <= 0f) return currentAmount;
        float mitigationFactor = 1f;
        if (context.Type == DamageType.Normal && _stats != null)
        {
            float armor = _stats.CurrentArmor;
            if (armor > 0f) mitigationFactor = 100f / (100f + armor);
        }
        float hpEquivalent = currentAmount * mitigationFactor;
        float abs = Mathf.Min(_shield, hpEquivalent);
        _shield -= abs;
        // 発動確認用ログ: シールドによる吸収量と残量。
        if (_shield <= 0f) { _shield = 0f; Debug.Log($"[ルーン/不屈] シールドが {abs:F1} 吸収して破壊", this); }
        else Debug.Log($"[ルーン/不屈] シールドが {abs:F1} 吸収 (残り {_shield:F1})", this);
        return (hpEquivalent - abs) / mitigationFactor;
    }
    /// <summary>現在のシールド残量(HP換算)。WorldHealthBarの白いゲージ表示に使用する。</summary>
    public float ShieldAmount => _shield;
}
