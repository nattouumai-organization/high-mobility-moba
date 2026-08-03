using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ヒーローのキルポイントとシャットダウン報酬を管理する(GAME_DESIGN.md 6章・フェーズ6)。
/// GameManagerが実行時にAddComponentで生成する。
/// - 通常キル: 敵ヒーローを撃破したチームに25ポイント。
/// - シャットダウン: 撃破された側が1連続キル中なら追加10pt、2連続なら20pt、3連続以上なら30pt。
/// - 最後にダメージを与えた敵ヒーローをキル者とする。ミニオン・タワーにとどめを刺された場合は
///   キルポイントなし(死亡した側の連続キルのみリセットする)。
/// ヒーロー(PlayerClickMovement)とTeamMemberは実行時に増えるため定期走査で購読する。
/// </summary>
public class HeroKillRewards : MonoBehaviour
{
    private const int KillPoints = 25;
    private const float ScanInterval = 1f;

    // ヒーロー毎の追跡状態(購読解除用にハンドラも保持する)。
    private class HeroState
    {
        public PlayerClickMovement Hero;
        public HealthController Health;
        public TeamMember Member;
        public Transform LastAttacker;
        public int KillStreak;
        public Action<DamageContext, float> DamageHandler;
        public Action DiedHandler;
    }

    private readonly Dictionary<HealthController, HeroState> _heroes =
        new Dictionary<HealthController, HeroState>();
    private float _scanTimer;

    private void Update()
    {
        _scanTimer -= Time.deltaTime;
        if (_scanTimer <= 0f)
        {
            _scanTimer = ScanInterval;
            ScanHeroes();
        }
    }

    private void OnDestroy()
    {
        foreach (HeroState state in _heroes.Values)
        {
            Unsubscribe(state);
        }

        _heroes.Clear();
    }

    private void ScanHeroes()
    {
        PlayerClickMovement[] heroes = FindObjectsByType<PlayerClickMovement>(FindObjectsSortMode.None);
        foreach (PlayerClickMovement hero in heroes)
        {
            if (hero == null)
            {
                continue;
            }

            HealthController health = hero.GetComponent<HealthController>();
            if (health == null || _heroes.ContainsKey(health))
            {
                continue;
            }

            HeroState state = new HeroState
            {
                Hero = hero,
                Health = health,
                Member = hero.GetComponent<TeamMember>(),
            };
            state.DamageHandler = (context, damage) => HandleDamageTaken(state, context);
            state.DiedHandler = () => HandleHeroDied(state);
            health.DamageTaken += state.DamageHandler;
            health.Died += state.DiedHandler;
            _heroes.Add(health, state);
        }
    }

    private static void Unsubscribe(HeroState state)
    {
        if (state.Health != null)
        {
            state.Health.DamageTaken -= state.DamageHandler;
            state.Health.Died -= state.DiedHandler;
        }
    }

    private static void HandleDamageTaken(HeroState state, DamageContext context)
    {
        if (context.Attacker != null)
        {
            state.LastAttacker = context.Attacker;
        }
    }

    private void HandleHeroDied(HeroState victim)
    {
        // TeamMemberは実行時付与のため、死亡時点で改めて取得する。
        if (victim.Member == null && victim.Hero != null)
        {
            victim.Member = victim.Hero.GetComponent<TeamMember>();
        }

        // キル者判定: 最後にダメージを与えたのが敵チームのヒーローの場合のみキル成立。
        PlayerClickMovement killerHero = victim.LastAttacker != null
            ? victim.LastAttacker.GetComponentInParent<PlayerClickMovement>()
            : null;
        TeamMember killerMember = killerHero != null ? killerHero.GetComponent<TeamMember>() : null;

        bool isEnemyHeroKill = killerHero != null
            && killerHero != victim.Hero
            && killerMember != null
            && victim.Member != null
            && killerMember.Team != victim.Member.Team;

        if (isEnemyHeroKill)
        {
            PointsManager.AddPoints(killerMember.Team, KillPoints, "hero kill");

            int shutdownBonus = GetShutdownBonus(victim.KillStreak);
            if (shutdownBonus > 0)
            {
                PointsManager.AddPoints(killerMember.Team, shutdownBonus, $"shutdown ({victim.KillStreak} kill streak)");
            }

            // キルしたヒーローの連続キル数を進める。
            HealthController killerHealth = killerHero.GetComponent<HealthController>();
            if (killerHealth != null && _heroes.TryGetValue(killerHealth, out HeroState killerState))
            {
                killerState.KillStreak++;
            }
        }

        // 死亡した側の連続キルは誰に倒されてもリセットする。
        victim.KillStreak = 0;
        victim.LastAttacker = null;
    }

    // シャットダウン報酬(GAME_DESIGN.md 6章): 1連続+10 / 2連続+20 / 3連続以上+30。
    private static int GetShutdownBonus(int killStreak)
    {
        if (killStreak >= 3)
        {
            return 30;
        }

        if (killStreak == 2)
        {
            return 20;
        }

        if (killStreak == 1)
        {
            return 10;
        }

        return 0;
    }
}
