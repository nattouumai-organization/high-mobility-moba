using System;
using System.Collections.Generic;
using UnityEngine;
/// <summary>連撃ルーン: 3秒以内に敵3回命中→45+AD*35%ダメージ+MS12% 1.5s。CD8s。</summary>
public class RelentlessRune : MonoBehaviour
{
    private CharacterStats _stats;
    private float _cdEnd = -1f;
    private bool _msActive;
    private float _msFlat, _msEnd;
    private float _scanTimer;
    private readonly List<float> _hits = new List<float>();
    private readonly Dictionary<HealthController, Action<DamageContext, float>> _subs =
        new Dictionary<HealthController, Action<DamageContext, float>>();
    private void Awake() { _stats = GetComponent<CharacterStats>(); }
    private void Update()
    {
        _scanTimer -= Time.deltaTime;
        if (_scanTimer <= 0f) { _scanTimer = 1f; Scan(); }
        if (_msActive && Time.time >= _msEnd) ClearMs();
    }
    private void OnDestroy() { foreach (var kv in _subs) if (kv.Key) kv.Key.DamageTaken -= kv.Value; ClearMs(); }
    private void Scan()
    {
        foreach (HealthController hc in FindObjectsByType<HealthController>(FindObjectsSortMode.None))
        {
            if (!hc || hc.gameObject == gameObject || _subs.ContainsKey(hc)) continue;
            Action<DamageContext, float> h = (ctx, _) => { if (IsMe(ctx.Attacker)) Hit(hc); };
            hc.DamageTaken += h;
            _subs[hc] = h;
        }
    }
    private bool IsMe(Transform a) => a && (a == transform || a.IsChildOf(transform));
    private void Hit(HealthController target)
    {
        if (Time.time < _cdEnd) return;
        float now = Time.time;
        _hits.RemoveAll(t => now - t > 3f);
        _hits.Add(now);
        if (_hits.Count >= 3)
        {
            _hits.Clear(); _cdEnd = now + 8f;
            if (_stats == null || !target) return;
            target.TakeDamage(45f + _stats.CurrentAttackDamage * 0.35f, transform, DamageType.Normal);
            if (!_msActive) { _msFlat = _stats.BaseMoveSpeed * 0.12f; _stats.AddMoveSpeedBonus(_msFlat); _msActive = true; }
            _msEnd = Time.time + 1.5f;
            Debug.Log("[Relentless] activated", this);
        }
    }
    private void ClearMs() { if (!_msActive) return; _stats?.RemoveMoveSpeedBonus(_msFlat); _msActive = false; _msFlat = 0f; }
}
