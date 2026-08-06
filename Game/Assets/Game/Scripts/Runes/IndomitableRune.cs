using System;
using System.Collections.Generic;
using UnityEngine;
/// <summary>不屈ルーン: 2s以内に最大HP15%以上受ける→最大HP8%シールド2.5s。CD40s。</summary>
public class IndomitableRune : MonoBehaviour
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
        if (_health != null) { _dmgH = (_, d) => OnDmg(d); _health.DamageTaken += _dmgH; }
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
    public float AbsorbWithShield(float incoming)
    {
        if (_shield <= 0f) return incoming;
        float abs = Mathf.Min(_shield, incoming); _shield -= abs;
        // 発動確認用ログ: シールドによる吸収量と残量。
        if (_shield <= 0f) Debug.Log($"[ルーン/不屈] シールドが {abs:F1} 吸収して破壊", this);
        else Debug.Log($"[ルーン/不屈] シールドが {abs:F1} 吸収 (残り {_shield:F1})", this);
        return incoming - abs;
    }
    public float ShieldAmount => _shield;
}
