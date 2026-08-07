using System;
using System.Collections.Generic;
using UnityEngine;
/// <summary>
/// 連撃ルーン: 3秒以内に敵3回命中→45+AD*35%ダメージ+MS12% 1.5s。CD8s。
/// SourceId付きのダメージ(スキル)は1回の発動で1カウント(多段ヒット・複数対象命中でも1回。例: ゼルフWは1発動で1カウント)。
/// SourceIdを持たないダメージ(通常攻撃など)は従来どおり毎ヒットカウントする(phase7-runes-fix4)。
/// </summary>
public class RelentlessRune : MonoBehaviour
{
    private CharacterStats _stats;
    private float _cdEnd = -1f;
    private bool _msActive;
    private float _msFlat, _msEnd;
    private float _scanTimer;
    private readonly List<float> _hits = new List<float>();
    // カウント済みSourceIdと記録時刻。1スキル1カウント判定に使用し、5秒を超えた記録は破棄する(phase7-runes-fix4)。
    private readonly Dictionary<string, float> _countedSources = new Dictionary<string, float>();
    private readonly List<string> _expiredSources = new List<string>();
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
            Action<DamageContext, float> h = (ctx, _) => { if (IsMe(ctx.Attacker)) Hit(hc, ctx); };
            hc.DamageTaken += h;
            _subs[hc] = h;
        }
    }
    private bool IsMe(Transform a) => a && (a == transform || a.IsChildOf(transform));
    private void Hit(HealthController target, DamageContext ctx)
    {
        if (Time.time < _cdEnd) return;
        float now = Time.time;
        // SourceId付きのダメージ(スキル)は1回の発動(同一SourceId)で1カウント(多段ヒット・複数対象命中でも1回)。
        // SourceIdを持たないダメージ(通常攻撃など)は従来どおり毎ヒットカウントする(phase7-runes-fix4)。
        if (!string.IsNullOrEmpty(ctx.SourceId))
        {
            PruneCountedSources(now);
            if (_countedSources.ContainsKey(ctx.SourceId)) return;
            _countedSources[ctx.SourceId] = now;
        }
        _hits.RemoveAll(t => now - t > 3f);
        _hits.Add(now);
        if (_hits.Count < 3)
        {
            // 発動確認用ログ: 命中カウントの進捗(3秒以内の命中数)。
            Debug.Log($"[ルーン/連撃] 命中 {_hits.Count}/3 (3秒以内)", this);
            return;
        }
        _hits.Clear(); _cdEnd = now + 8f;
        if (_stats == null || !target) return;
        float dmg = 45f + _stats.CurrentAttackDamage * 0.35f;
        target.TakeDamage(dmg, transform, DamageType.Normal);
        if (!_msActive) { _msFlat = _stats.BaseMoveSpeed * 0.12f; _stats.AddMoveSpeedBonus(_msFlat); _msActive = true; }
        _msEnd = Time.time + 1.5f;
        // 発動確認用ログ: 追加ダメージとMSバフの発動。
        Debug.Log($"[ルーン/連撃] 発動！ {target.name} へ {dmg:F1} ダメージ + MS12% (1.5秒) / CD 8秒", this);
    }
    // 記録から5秒を超えた古いSourceIdを破棄する(辞書の際限ない肥大化を防ぐ。カウント有効期間3秒より長いため判定へ影響しない)。
    private void PruneCountedSources(float now)
    {
        _expiredSources.Clear();
        foreach (var kv in _countedSources) if (now - kv.Value > 5f) _expiredSources.Add(kv.Key);
        foreach (string key in _expiredSources) _countedSources.Remove(key);
    }
    private void ClearMs()
    {
        if (!_msActive) return;
        _stats?.RemoveMoveSpeedBonus(_msFlat); _msActive = false; _msFlat = 0f;
        // 発動確認用ログ: MSバフの終了。
        Debug.Log("[ルーン/連撃] MSバフ終了", this);
    }
}
