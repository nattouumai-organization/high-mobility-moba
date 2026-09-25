using System.Collections.Generic;
using UnityEngine;

/// <summary>Rの一時ステータス移転。再命中・死亡・無効化で必ず差分を戻す。</summary>
[DisallowMultipleComponent]
public sealed class LieselotteStatSteal : MonoBehaviour
{
    private static readonly Dictionary<CharacterStats, LieselotteStatSteal> ActiveByTarget =
        new Dictionary<CharacterStats, LieselotteStatSteal>();
    private CharacterStats _owner;
    private HealthController _ownerHealth;
    private CharacterStats _target;
    private HealthController _targetHealth;
    private float _hp;
    private float _ad;
    private float _armor;
    private float _ownerAsPercent;
    private float _targetAsPercent;
    private float _moveSpeed;
    private float _endTime;
    private bool _active;

    public bool IsActive => _active;
    public float Remaining => IsActive ? Mathf.Max(0f, _endTime - Time.time) : 0f;

    private void Awake()
    {
        _owner = GetComponent<CharacterStats>();
        _ownerHealth = GetComponent<HealthController>();
    }

    private void Update()
    {
        if (!IsActive) return;
        if (Time.time >= _endTime || _ownerHealth == null || _ownerHealth.IsDead ||
            _targetHealth == null || _targetHealth.IsDead || !_target.gameObject.activeInHierarchy)
            End();
    }

    public bool Apply(CharacterStats target, HealthController targetHealth, float statPercent,
        float movePercent, float duration)
    {
        End();
        if (_owner == null || _ownerHealth == null || _ownerHealth.IsDead || target == null ||
            targetHealth == null || targetHealth.IsDead || target == _owner || duration <= 0f)
            return false;

        if (ActiveByTarget.TryGetValue(target, out LieselotteStatSteal previous) && previous != null)
            previous.End();

        _target = target;
        _targetHealth = targetHealth;
        _active = true;
        ActiveByTarget[target] = this;
        _ownerHealth.Died += End;
        _targetHealth.Died += End;
        float fraction = Mathf.Clamp01(statPercent / 100f);
        _hp = Mathf.Min(target.CurrentMaxHealth - 1f, target.CurrentMaxHealth * fraction);
        _ad = target.CurrentAttackDamage * fraction;
        _armor = target.CurrentArmor * fraction;
        float attackSpeed = target.CurrentAttackSpeed * fraction;
        _ownerAsPercent = attackSpeed / Mathf.Max(0.01f,
            _owner.Data != null ? _owner.Data.BaseAttackSpeed : _owner.CurrentAttackSpeed) * 100f;
        _targetAsPercent = attackSpeed / Mathf.Max(0.01f,
            target.Data != null ? target.Data.BaseAttackSpeed : target.CurrentAttackSpeed) * 100f;
        _moveSpeed = target.CurrentMoveSpeed * Mathf.Clamp01(movePercent / 100f);

        float ownerRatio = _ownerHealth.CurrentHealth / _ownerHealth.MaxHealth;
        float targetRatio = _targetHealth.CurrentHealth / _targetHealth.MaxHealth;
        _owner.AddMaxHealthBonus(_hp);
        target.AddMaxHealthBonus(-_hp);
        _owner.AddAttackDamageBonus(_ad);
        target.AddAttackDamageBonus(-_ad);
        _owner.AddArmorBonus(_armor);
        target.AddArmorBonus(-_armor);
        _owner.AddAttackSpeedPercentBonus(_ownerAsPercent);
        target.AddAttackSpeedPercentBonus(-_targetAsPercent);
        _owner.AddMoveSpeedBonus(_moveSpeed);
        target.AddMoveSpeedBonus(-_moveSpeed);
        _ownerHealth.PreserveHealthRatio(ownerRatio);
        _targetHealth.PreserveHealthRatio(targetRatio);
        _endTime = Time.time + duration;
        return true;
    }

    public void End()
    {
        if (!_active) return;
        _active = false;
        if (!ReferenceEquals(_target, null) && ActiveByTarget.TryGetValue(_target, out LieselotteStatSteal current) &&
            current == this) ActiveByTarget.Remove(_target);
        if (_ownerHealth != null) _ownerHealth.Died -= End;
        if (_targetHealth != null) _targetHealth.Died -= End;
        float ownerRatio = _ownerHealth != null && !_ownerHealth.IsDead
            ? _ownerHealth.CurrentHealth / _ownerHealth.MaxHealth : 0f;
        float targetRatio = _targetHealth != null && !_targetHealth.IsDead
            ? _targetHealth.CurrentHealth / _targetHealth.MaxHealth : 0f;
        _owner.RemoveMaxHealthBonus(_hp);
        _owner.RemoveAttackDamageBonus(_ad);
        _owner.RemoveArmorBonus(_armor);
        _owner.RemoveAttackSpeedPercentBonus(_ownerAsPercent);
        _owner.RemoveMoveSpeedBonus(_moveSpeed);
        if (_target != null)
        {
            _target.RemoveMaxHealthBonus(-_hp);
            _target.RemoveAttackDamageBonus(-_ad);
            _target.RemoveArmorBonus(-_armor);
            _target.RemoveAttackSpeedPercentBonus(-_targetAsPercent);
            _target.RemoveMoveSpeedBonus(-_moveSpeed);
        }
        if (_ownerHealth != null) _ownerHealth.PreserveHealthRatio(ownerRatio);
        if (_targetHealth != null) _targetHealth.PreserveHealthRatio(targetRatio);
        _target = null;
        _targetHealth = null;
        _endTime = 0f;
    }

    private void OnDisable() => End();
    private void OnDestroy() => End();
}
