using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 追撃ルーン。実際に成立した移動スキルの後1.25秒以内に敵ヒーローへ命中した場合に発動する。
/// 移動スキル自身の命中も含め、最初に成立した敵ヒーローへの命中で1回だけ発動する。
/// </summary>
public class PursuitRune : MonoBehaviour
{
    private CharacterStats _stats;
    private MovementSkillSignal _movementSkills;
    private float _cdEnd = -1f;
    private bool _win;
    private float _winEnd;
    private float _scanTimer;
    private readonly Dictionary<HealthController, Action<DamageContext, float>> _subs =
        new Dictionary<HealthController, Action<DamageContext, float>>();

    private void Awake()
    {
        _stats = GetComponent<CharacterStats>();
        _movementSkills = GetComponent<MovementSkillSignal>();
        if (_movementSkills == null) _movementSkills = gameObject.AddComponent<MovementSkillSignal>();
        _movementSkills.Used += OnMovementSkillUsed;
        Scan();
    }

    private void Update()
    {
        if (_win && Time.time >= _winEnd) _win = false;
        _scanTimer -= Time.deltaTime;
        if (_scanTimer <= 0f) { _scanTimer = 1f; Scan(); }
    }

    private void OnDestroy()
    {
        if (_movementSkills != null) _movementSkills.Used -= OnMovementSkillUsed;
        foreach (var pair in _subs) if (pair.Key) pair.Key.DamageTaken -= pair.Value;
    }

    private void OnMovementSkillUsed()
    {
        _win = true;
        _winEnd = Time.time + 1.25f;
        Scan();
    }

    private void Scan()
    {
        foreach (HealthController health in FindObjectsByType<HealthController>(FindObjectsSortMode.None))
        {
            if (!health || health.gameObject == gameObject || _subs.ContainsKey(health)) continue;
            Action<DamageContext, float> handler = (context, _) =>
            {
                if (IsMe(context.Attacker)) Hit(health);
            };
            health.DamageTaken += handler;
            _subs[health] = handler;
        }
    }

    private bool IsMe(Transform attacker)
    {
        return attacker && (attacker == transform || attacker.IsChildOf(transform));
    }

    private void Hit(HealthController target)
    {
        if (!_win || Time.time >= _winEnd || Time.time < _cdEnd) return;
        Targetable targetable = target != null ? target.GetComponentInParent<Targetable>() : null;
        if (!OboroCombatUtility.IsEnemyChampion(transform, targetable)) return;
        _win = false;
        _cdEnd = Time.time + 12f;
        if (_stats == null || !target) return;
        float damage = 40f + _stats.CurrentAttackDamage * 0.30f;
        target.TakeDamage(damage, transform, DamageType.Normal);
        CrowdControlController cc = target.GetComponent<CrowdControlController>();
        if (cc == null) cc = target.gameObject.AddComponent<CrowdControlController>();
        cc.ApplySlowToCurrentSpeed(0.85f, 0.5f);
        Debug.Log($"[ルーン/追撃] 発動！ {target.name} へ {damage:F1} ダメージ + 15%スロウ (0.5秒) / CD 12秒", this);
    }
}
