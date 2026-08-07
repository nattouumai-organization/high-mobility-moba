using System;
using System.Collections.Generic;
using UnityEngine;
/// <summary>
/// 追撃ルーン: E/F後1.25s以内に敵命中→40+AD*30%+15%スロウ0.5s。CD12s。
/// E自身のダメージ(SourceIdがゼルフE/ヴォルブラークE)では発動しない。その際発動ウィンドウは消費せず、
/// E使用後の次の命中(通常攻撃・Q・Wなど)で発動する(phase7-runes-fix4)。
/// </summary>
public class PursuitRune : MonoBehaviour
{
    private CharacterStats _stats;
    private PlayerInputHub _input;
    private float _cdEnd = -1f;
    private bool _win;
    private float _winEnd;
    private float _scanTimer;
    private readonly Dictionary<HealthController, Action<DamageContext, float>> _subs =
        new Dictionary<HealthController, Action<DamageContext, float>>();
    private void Awake()
    {
        _stats = GetComponent<CharacterStats>();
        _input = GetComponent<PlayerInputHub>();
    }
    private void Update()
    {
        if (_input != null && (_input.EPressedThisFrame || _input.FPressedThisFrame))
        {
            _win = true; _winEnd = Time.time + 1.25f;
            // 発動確認用ログ: E/F押下による発動ウィンドウの開始(CD中は命中しても発動しない)。
            if (Time.time < _cdEnd) Debug.Log($"[ルーン/追撃] E/F押下 (CD中あと {_cdEnd - Time.time:F1}秒)", this);
            else Debug.Log("[ルーン/追撃] 発動ウィンドウ開始 (1.25秒以内の命中で発動)", this);
        }
        if (_win && Time.time >= _winEnd) _win = false;
        _scanTimer -= Time.deltaTime;
        if (_scanTimer <= 0f) { _scanTimer = 1f; Scan(); }
    }
    private void OnDestroy() { foreach (var kv in _subs) if (kv.Key) kv.Key.DamageTaken -= kv.Value; }
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
    // ダメージのSourceIdがE自身(ゼルフE/ヴォルブラークE)のものかどうか(phase7-runes-fix4)。
    private static bool IsESkillDamage(string sourceId) =>
        !string.IsNullOrEmpty(sourceId) &&
        (sourceId.StartsWith("ZelfE#", StringComparison.Ordinal) ||
         sourceId.StartsWith("VolbraakE#", StringComparison.Ordinal));
    private void Hit(HealthController target, DamageContext ctx)
    {
        if (!_win || Time.time < _cdEnd) return;
        // E自身のダメージでは発動しない。発動ウィンドウは消費せず、E使用後の次の命中で発動する(phase7-runes-fix4)。
        if (IsESkillDamage(ctx.SourceId))
        {
            Debug.Log("[ルーン/追撃] E自身のダメージのため発動しません (E使用後の次の命中で発動)", this);
            return;
        }
        _win = false; _cdEnd = Time.time + 12f;
        if (_stats == null || !target) return;
        float dmg = 40f + _stats.CurrentAttackDamage * 0.30f;
        target.TakeDamage(dmg, transform, DamageType.Normal);
        CharacterStats ts = target.GetComponent<CharacterStats>();
        CrowdControlController cc = target.GetComponent<CrowdControlController>();
        if (cc != null && ts != null) cc.ApplySlow(-(ts.BaseMoveSpeed * 0.15f), 0.5f);
        // 発動確認用ログ: 追加ダメージとスロウの発動。
        Debug.Log($"[ルーン/追撃] 発動！ {target.name} へ {dmg:F1} ダメージ + 15%スロウ (0.5秒) / CD 12秒", this);
    }
}
