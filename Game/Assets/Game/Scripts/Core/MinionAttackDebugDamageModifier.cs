using UnityEngine;

/// <summary>
/// PrototypeMatchDebugControllerのInspector設定を各HealthControllerへ適用するデバッグ用被ダメージ補正。
/// 通常ルールで0になったダメージは変更しないため、第2タワー無敵・味方攻撃・スキル無効を迂回しない。
/// </summary>
[AddComponentMenu("")]
public sealed class MinionAttackDebugDamageModifier : MonoBehaviour, IIncomingDamageModifier
{
    private PrototypeMatchDebugController _settings;

    public void Initialize(PrototypeMatchDebugController settings)
    {
        _settings = settings;
    }

    public float ModifyIncomingDamage(DamageContext context, float currentAmount)
    {
        if (_settings == null || !_settings.OverrideMinionAttackDamage || currentAmount <= 0f)
        {
            return currentAmount;
        }

        if (context.Attacker == null || context.Attacker.GetComponentInParent<MinionController>() == null)
        {
            return currentAmount;
        }

        return _settings.MinionFinalDamagePerHit;
    }
}
