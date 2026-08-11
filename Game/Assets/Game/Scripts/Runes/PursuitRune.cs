using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 追撃ルーン。E/F後1.25秒以内の次の敵命中で追加ダメージとスロウを与える。
/// ZelfE / VolbraakE / OboroE自身のダメージでは発動せず、E後の次の別命中までウィンドウを維持する。
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
            _win = true;
            _winEnd = Time.time + 1.25f;
            if (Time.time < _cdEnd) Debug.Log($"[ルーン/追撃] E/F押下 (CD中あと {_cdEnd - Time.time:F1}秒)", this);
            else Debug.Log("[ルーン/追撃] 発動ウィンドウ開始 (1.25秒以内の命中で発動)", this);
        }
        if (_win && Time.time >= _winEnd) _win = false;
        _scanTimer -= Time.deltaTime;
        if (_scanTimer <= 0f) { _scanTimer = 1f; Scan(); }
    }

    private void OnDestroy()
    {
        foreach (var pair in _subs) if (pair.Key) pair.Key.DamageTaken -= pair.Value;
    }

    private void Scan()
    {
        foreach (HealthController health in FindObjectsByType<HealthController>(FindObjectsSortMode.None))
        {
            if (!health || health.gameObject == gameObject || _subs.ContainsKey(health)) continue;
            Action<DamageContext, float> handler = (context, _) =>
            {
                if (IsMe(context.Attacker)) Hit(health, context);
            };
            health.DamageTaken += handler;
            _subs[health] = handler;
        }
    }

    private bool IsMe(Transform attacker)
    {
        return attacker && (attacker == transform || attacker.IsChildOf(transform));
    }

    private static bool IsESkillDamage(string sourceId)
    {
        return !string.IsNullOrEmpty(sourceId) &&
               (sourceId.StartsWith("ZelfE#", StringComparison.Ordinal) ||
                sourceId.StartsWith("VolbraakE#", StringComparison.Ordinal) ||
                sourceId.StartsWith("OboroE#", StringComparison.Ordinal));
    }

    private void Hit(HealthController target, DamageContext context)
    {
        if (!_win || Time.time < _cdEnd) return;
        if (IsESkillDamage(context.SourceId))
        {
            Debug.Log("[ルーン/追撃] E自身のダメージのため発動しません (E使用後の次の命中で発動)", this);
            return;
        }

        _win = false;
        _cdEnd = Time.time + 12f;
        if (_stats == null || !target) return;
        float damage = 40f + _stats.CurrentAttackDamage * 0.30f;
        target.TakeDamage(damage, transform, DamageType.Normal);
        CharacterStats targetStats = target.GetComponent<CharacterStats>();
        CrowdControlController cc = target.GetComponent<CrowdControlController>();
        if (cc != null && targetStats != null) cc.ApplySlow(-(targetStats.BaseMoveSpeed * 0.15f), 0.5f);
        Debug.Log($"[ルーン/追撃] 発動！ {target.name} へ {damage:F1} ダメージ + 15%スロウ (0.5秒) / CD 12秒", this);
    }
}
