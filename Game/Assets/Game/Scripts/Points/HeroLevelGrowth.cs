using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ヒーローの基礎ステータスをレベルに応じて成長させるマネージャー(フェーズ7)。
/// GameManagerが実行時にAddComponentで生成する。
/// - HeroKillRewardsと同様にPlayerClickMovementを持つオブジェクトをヒーローとして定期スキャンする。
/// - チームレベル(LevelSystem)が上がるたびに、CharacterData(ZelfData/VolbraakData)の成長値を
///   CharacterStatsのボーナス(AddMaxHealthBonusなど)として1レベル分ずつ加算する。
///   成長式はGAME_DESIGN.md 10章に従う(HP/HPreg/AD/ARはフラット加算、ASは基礎値に対する%加算)。
/// - 最大HPの増加分はHealthControllerが検知して現在HPにも加算される。
/// - あわせてスキル強化の状態を保持するHeroSkillUpgradesをヒーローへ自動追加する。
/// </summary>
public class HeroLevelGrowth : MonoBehaviour
{
    private const float ScanInterval = 1f;

    private class HeroState
    {
        public CharacterStats Stats;
        public TeamMember Member;
        public int AppliedLevel = 1;
    }

    private readonly List<HeroState> _heroes = new List<HeroState>();
    private readonly HashSet<CharacterStats> _tracked = new HashSet<CharacterStats>();
    private float _scanTimer;

    private void Update()
    {
        _scanTimer -= Time.deltaTime;
        if (_scanTimer <= 0f)
        {
            _scanTimer = ScanInterval;
            ScanHeroes();
        }

        ApplyPendingGrowth();
    }

    private void ScanHeroes()
    {
        PlayerClickMovement[] movers = FindObjectsByType<PlayerClickMovement>(FindObjectsSortMode.None);
        foreach (PlayerClickMovement mover in movers)
        {
            CharacterStats stats = mover.GetComponent<CharacterStats>();
            TeamMember member = mover.GetComponent<TeamMember>();
            if (stats == null || member == null || _tracked.Contains(stats))
            {
                // TeamMemberはGameManagerが割り当てるため、未割当の間は次回スキャンで再確認する。
                continue;
            }

            _tracked.Add(stats);
            _heroes.Add(new HeroState { Stats = stats, Member = member });

            if (mover.GetComponent<HeroSkillUpgrades>() == null)
            {
                mover.gameObject.AddComponent<HeroSkillUpgrades>();
            }
        }
    }

    private void ApplyPendingGrowth()
    {
        foreach (HeroState hero in _heroes)
        {
            if (hero.Stats == null || hero.Member == null)
            {
                continue;
            }

            int level = LevelSystem.GetLevelForTeam(hero.Member.Team);
            while (hero.AppliedLevel < level)
            {
                hero.AppliedLevel++;
                ApplyOneLevelGrowth(hero);
            }
        }
    }

    private void ApplyOneLevelGrowth(HeroState hero)
    {
        CharacterData data = hero.Stats.Data;
        if (data == null)
        {
            Debug.LogWarning(
                $"[HeroLevelGrowth] {hero.Stats.name} はCharacterData未設定のため成長をスキップしました (Lv{hero.AppliedLevel})。",
                hero.Stats);
            return;
        }

        hero.Stats.AddMaxHealthBonus(data.HpGrowth);
        hero.Stats.AddHealthRegenBonus(data.HpRegenerationGrowth);
        hero.Stats.AddAttackDamageBonus(data.AttackDamageGrowth);
        hero.Stats.AddAttackSpeedPercentBonus(data.AttackSpeedGrowthPercent);
        hero.Stats.AddArmorBonus(data.ArmorGrowth);

        Debug.Log(
            $"[HeroLevelGrowth] {data.DisplayName} が Lv{hero.AppliedLevel} になりました。" +
            $"HP+{data.HpGrowth}, HPreg+{data.HpRegenerationGrowth}, AD+{data.AttackDamageGrowth}, " +
            $"AS+{data.AttackSpeedGrowthPercent}%, AR+{data.ArmorGrowth}",
            hero.Stats);
    }
}
