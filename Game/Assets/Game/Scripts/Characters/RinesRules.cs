using UnityEngine;

/// <summary>Rines prototype values and decisions shared by the skill controllers.</summary>
public static class RinesRules
{
    public static bool IsHardControlled(CrowdControlController cc) =>
        cc != null && (cc.IsStunned || cc.IsSnared);

    public static float Damage(float baseDamage, float adRatio, float attackDamage, int rank,
        bool passive, float passiveMultiplier, float rankDamageStep)
    {
        return (baseDamage + adRatio * attackDamage) * (1f + rankDamageStep * Mathf.Clamp(rank, 0, 2)) *
               (passive ? passiveMultiplier : 1f);
    }

    public static bool CanDetonateQ(double elapsed, float delay, float window) =>
        elapsed >= delay && elapsed <= window;

    public static double EEndAfterHit(double now, float tail) => now + tail;

    public static bool IsValidTarget(Transform owner, Targetable target)
    {
        if (target == null || !OboroCombatUtility.IsEnemy(owner, target)) return false;
        return target.Classification == TargetClassification.Character ||
               target.Classification == TargetClassification.TrainingDummy ||
               target.Classification == TargetClassification.Minion;
    }

    public static bool IsValidWTarget(Transform owner, Targetable target)
    {
        if (target == null || target.Classification != TargetClassification.Character ||
            !OboroCombatUtility.IsAlive(target)) return false;
        if (target.IsTrainingDummyPlayerProxy)
            return !OboroCombatUtility.IsOwner(owner, target);
        if (!target.CompareTag("Player")) return false;
        TeamMember ownTeam = owner != null ? owner.GetComponentInParent<TeamMember>() : null;
        TeamMember targetTeam = target.GetComponentInParent<TeamMember>();
        return ownTeam != null && targetTeam != null && ownTeam.Team != targetTeam.Team;
    }
}
