using System;

/// <summary>スキルが持ち得る属性。ダメージ軽減用のDamageTypeとは独立している。</summary>
[Flags]
public enum SkillAttribute : ulong
{
    None = 0,
    BasicAttack = 1UL << 0,
    SkillAttack = 1UL << 1,
    TrueDamage = 1UL << 2,
    Slow = 1UL << 3,
    Snare = 1UL << 4,
    Stun = 1UL << 5,
    Knockback = 1UL << 6,
    Chain = 1UL << 7,
    Dash = 1UL << 8,
    Blink = 1UL << 9,
    Heal = 1UL << 10,
    DamageReduction = 1UL << 11,
    Shield = 1UL << 12,
    Stealth = 1UL << 13,
    IncreaseMoveSpeed = 1UL << 14,
    IncreaseAttackSpeed = 1UL << 15,
    SkillsDisabled = 1UL << 16,
    StatSteal = 1UL << 17,
}

public enum CharacterAbilitySlot
{
    BasicAttack,
    P,
    Q,
    W,
    E,
    R,
}

/// <summary>スキル全体で起こり得る効果の定義。条件分岐するCCは同時適用を意味しない。</summary>
public readonly struct SkillAttributeDefinition
{
    public SkillTargetingType Targeting { get; }
    public SkillAttribute Attributes { get; }

    public SkillAttributeDefinition(SkillTargetingType targeting, SkillAttribute attributes)
    {
        Targeting = targeting;
        Attributes = attributes;
    }

    public bool Has(SkillAttribute attribute) => (Attributes & attribute) == attribute;
}

/// <summary>
/// キャラクター別の属性表。数値・命中判定・CC付与処理は既存の各コントローラーが担当する。
/// リーゼロッテQは資料の「指定無し」と説明が矛盾するため、実際の対象指定操作を採用する。
/// </summary>
public static class SkillAttributeCatalog
{
    private const SkillAttribute DamageMask = SkillAttribute.BasicAttack | SkillAttribute.SkillAttack |
        SkillAttribute.TrueDamage;

    private static readonly SkillAttributeDefinition[] Zelf =
    {
        new(SkillTargetingType.UnitTarget, SkillAttribute.BasicAttack),
        new(SkillTargetingType.NoTarget, SkillAttribute.Heal),
        new(SkillTargetingType.UnitTarget, SkillAttribute.SkillAttack | SkillAttribute.Dash),
        new(SkillTargetingType.DirectionTarget, SkillAttribute.SkillAttack | SkillAttribute.DamageReduction),
        new(SkillTargetingType.DirectionTarget, SkillAttribute.SkillAttack | SkillAttribute.Dash),
        new(SkillTargetingType.UnitTarget, SkillAttribute.IncreaseMoveSpeed | SkillAttribute.Knockback | SkillAttribute.Slow),
    };

    private static readonly SkillAttributeDefinition[] Volbraak =
    {
        new(SkillTargetingType.UnitTarget, SkillAttribute.BasicAttack),
        new(SkillTargetingType.NoTarget, SkillAttribute.Shield),
        new(SkillTargetingType.DirectionTarget, SkillAttribute.SkillAttack | SkillAttribute.Slow),
        new(SkillTargetingType.NoTarget, SkillAttribute.SkillAttack | SkillAttribute.Shield | SkillAttribute.Heal),
        new(SkillTargetingType.DirectionTarget, SkillAttribute.SkillAttack | SkillAttribute.Stun | SkillAttribute.Dash),
        new(SkillTargetingType.DirectionTarget, SkillAttribute.SkillAttack | SkillAttribute.TrueDamage | SkillAttribute.Chain),
    };

    private static readonly SkillAttributeDefinition[] Oboro =
    {
        new(SkillTargetingType.UnitTarget, SkillAttribute.BasicAttack),
        new(SkillTargetingType.UnitTarget, SkillAttribute.BasicAttack),
        new(SkillTargetingType.DirectionTarget, SkillAttribute.SkillAttack | SkillAttribute.Blink),
        new(SkillTargetingType.NoTarget, SkillAttribute.Stealth | SkillAttribute.IncreaseMoveSpeed),
        new(SkillTargetingType.UnitTarget, SkillAttribute.BasicAttack | SkillAttribute.Blink | SkillAttribute.SkillsDisabled),
        new(SkillTargetingType.UnitTarget, SkillAttribute.TrueDamage),
    };

    private static readonly SkillAttributeDefinition[] Rines =
    {
        new(SkillTargetingType.UnitTarget, SkillAttribute.BasicAttack),
        new(SkillTargetingType.NoTarget, SkillAttribute.SkillAttack),
        new(SkillTargetingType.PointTarget, SkillAttribute.SkillAttack | SkillAttribute.Snare),
        new(SkillTargetingType.NoTarget, SkillAttribute.SkillAttack | SkillAttribute.Slow | SkillAttribute.Snare),
        new(SkillTargetingType.NoTarget, SkillAttribute.SkillAttack | SkillAttribute.IncreaseMoveSpeed | SkillAttribute.Snare),
        new(SkillTargetingType.PointTarget, SkillAttribute.SkillAttack | SkillAttribute.Stun),
    };

    private static readonly SkillAttributeDefinition[] Lieselotte =
    {
        new(SkillTargetingType.UnitTarget, SkillAttribute.BasicAttack),
        new(SkillTargetingType.NoTarget, SkillAttribute.BasicAttack),
        new(SkillTargetingType.UnitTarget, SkillAttribute.BasicAttack | SkillAttribute.Heal),
        new(SkillTargetingType.NoTarget, SkillAttribute.IncreaseMoveSpeed | SkillAttribute.IncreaseAttackSpeed),
        new(SkillTargetingType.DirectionTarget, SkillAttribute.SkillAttack | SkillAttribute.Slow | SkillAttribute.Blink),
        new(SkillTargetingType.DirectionTarget, SkillAttribute.Dash | SkillAttribute.StatSteal),
    };

    public static bool TryGet(string characterId, CharacterAbilitySlot slot, out SkillAttributeDefinition definition)
    {
        SkillAttributeDefinition[] skills = characterId switch
        {
            "Zelf" => Zelf,
            "Volbraak" => Volbraak,
            "Oboro" => Oboro,
            "Rines" => Rines,
            "Lieselotte" => Lieselotte,
            _ => null,
        };
        int index = (int)slot;
        if (skills == null || index < 0 || index >= skills.Length)
        {
            definition = default;
            return false;
        }
        definition = skills[index];
        return true;
    }

    public static SkillAttributeDefinition Get(string characterId, CharacterAbilitySlot slot)
    {
        if (!TryGet(characterId, slot, out SkillAttributeDefinition definition))
            throw new ArgumentException($"Unknown character or ability: {characterId}/{slot}");
        return definition;
    }

    /// <summary>命中ダメージの分類だけを返す。条件付きCCや自己効果は命中ごとの情報へ混ぜない。</summary>
    public static SkillAttribute DamageOf(string characterId, CharacterAbilitySlot slot) =>
        Get(characterId, slot).Attributes & DamageMask;

    public static SkillAttribute DamageOf(string characterId, HeroSkillSlot slot) =>
        DamageOf(characterId, (CharacterAbilitySlot)((int)slot + (int)CharacterAbilitySlot.Q));
}
