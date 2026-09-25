using UnityEngine;

[CreateAssetMenu(menuName = "Game/Characters/Lieselotte Skills")]
public sealed class LieselotteSkillData : ScriptableObject
{
    [SerializeField] private float _passiveBaseDamage = 20f;
    [SerializeField] private float _passiveAdRatio = 0.4f;
    [SerializeField] private float _passiveStackSeconds = 4f;
    [SerializeField] private float _passiveEmpowerSeconds = 4f;
    [SerializeField] private float _qBaseDamage = 35f;
    [SerializeField] private float _qAdRatio = 0.6f;
    [SerializeField] private float _qCooldown = 8f;
    [SerializeField] private float _qRange = 1.75f;
    [SerializeField] private float _qHeroHealPercent = 30f;
    [SerializeField] private float _qMinionHealPercent = 10f;
    [SerializeField] private float _qEmpoweredHealMultiplier = 1.5f;
    [SerializeField] private float _wMoveSpeedPercent = 15f;
    [SerializeField] private float _wAttackSpeedPercent = 10f;
    [SerializeField] private float _wDuration = 2f;
    [SerializeField] private float _eCooldown = 12f;
    [SerializeField] private float _eDistance = 4f;
    [SerializeField] private float _ePoolRadius = 0.65f;
    [SerializeField] private float _ePoolDuration = 3f;
    [SerializeField] private float _eBaseDamage = 25f;
    [SerializeField] private float _eAdRatio = 0.4f;
    [SerializeField] private float _eSlowPercent = 25f;
    [SerializeField] private float _eSlowDuration = 1f;
    [SerializeField] private float _eCooldownReductionOnQ = 2f;
    [SerializeField] private float _rCooldown = 90f;
    [SerializeField] private float _rDistance = 5f;
    [SerializeField] private float _rHitRadius = 0.7f;
    [SerializeField] private float _rDuration = 5f;
    [SerializeField] private float _rStatStealPercent = 10f;
    [SerializeField] private float _rMoveSpeedStealPercent = 8f;
    [SerializeField] private float _rankDamageStep = 0.1f;

    public float PassiveBaseDamage => _passiveBaseDamage;
    public float PassiveAdRatio => _passiveAdRatio;
    public float PassiveStackSeconds => _passiveStackSeconds;
    public float PassiveEmpowerSeconds => _passiveEmpowerSeconds;
    public float QBaseDamage => _qBaseDamage;
    public float QAdRatio => _qAdRatio;
    public float QCooldown => _qCooldown;
    public float QRange => _qRange;
    public float QHeroHealPercent => _qHeroHealPercent;
    public float QMinionHealPercent => _qMinionHealPercent;
    public float QEmpoweredHealMultiplier => _qEmpoweredHealMultiplier;
    public float WMoveSpeedPercent => _wMoveSpeedPercent;
    public float WAttackSpeedPercent => _wAttackSpeedPercent;
    public float WDuration => _wDuration;
    public float ECooldown => _eCooldown;
    public float EDistance => _eDistance;
    public float EPoolRadius => _ePoolRadius;
    public float EPoolDuration => _ePoolDuration;
    public float EBaseDamage => _eBaseDamage;
    public float EAdRatio => _eAdRatio;
    public float ESlowPercent => _eSlowPercent;
    public float ESlowDuration => _eSlowDuration;
    public float ECooldownReductionOnQ => _eCooldownReductionOnQ;
    public float RCooldown => _rCooldown;
    public float RDistance => _rDistance;
    public float RHitRadius => _rHitRadius;
    public float RDuration => _rDuration;
    public float RStatStealPercent => _rStatStealPercent;
    public float RMoveSpeedStealPercent => _rMoveSpeedStealPercent;
    public float RankDamageStep => _rankDamageStep;
}
