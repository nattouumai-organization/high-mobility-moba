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
/// - テストプレイ用: RespawnControllerを持つ非ヒーロー(トレーニングダミー・攻撃ダミー)もキル対象として追跡する。
///   ダミーは倒されるたびに連続キル数が1増える扱いにし、2回目以降の撃破でシャットダウン報酬も順に確認できる。
///   (実際のヒーロー同士のキル判定には影響しない)
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
        public bool IsTestDummy;
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

        // テストプレイ用: RespawnControllerを持つ非ヒーロー(トレーニングダミーなど)もキル対象として追跡する。
        // ミニオン・タワーはRespawnControllerを持たないため対象外。ヒーローは上の走査で追跡済みのため除外する。
        foreach (RespawnController respawn in FindObjectsByType<RespawnController>(FindObjectsSortMode.None))
        {
            if (respawn == null || respawn.GetComponent<PlayerClickMovement>() != null)
            {
                continue;
            }

            HealthController health = respawn.GetComponent<HealthController>();
            if (health == null || _heroes.ContainsKey(health))
            {
                continue;
            }

            HeroState state = new HeroState
            {
                Health = health,
                Member = respawn.GetComponent<TeamMember>(),
                IsTestDummy = true,
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
        if (victim.IsTestDummy)
        {
            HandleTestDummyDied(victim);
            return;
        }

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
        else
        {
            // 調査用ログ: 最後の攻撃者が敵チームのヒーローでない(ミニオン・タワー・同一チーム・不明)場合はキル不成立。
            string victimName = victim.Hero != null ? victim.Hero.name : "(不明)";
            string attackerName = victim.LastAttacker != null ? victim.LastAttacker.name : "(不明)";
            Debug.Log($"HeroKillRewards: {victimName}が死亡しましたがキル不成立のためポイントなし(最後の攻撃者: {attackerName})。");
        }

        // 死亡した側の連続キルは誰に倒されてもリセットする。
        victim.KillStreak = 0;
        victim.LastAttacker = null;
    }

    // テストプレイ用: ダミー撃破でもキルポイント・シャットダウン報酬を確認できるようにする。
    // ダミーは倒されるたびに連続キル数が1増える扱いにし、2回目以降の撃破で
    // シャットダウン報酬(+10/+20/+30pt)を順に確認できる。
    private void HandleTestDummyDied(HeroState victim)
    {
        string victimName = victim.Health != null ? victim.Health.name : "(不明)";
        PlayerClickMovement killerHero = victim.LastAttacker != null
            ? victim.LastAttacker.GetComponentInParent<PlayerClickMovement>()
            : null;

        if (killerHero == null)
        {
            Debug.Log($"HeroKillRewards: {victimName}が死亡しましたがヒーローのとどめではないためポイントなし。");
            victim.LastAttacker = null;
            return;
        }

        // ダミーはTeamMemberを持たないことが多いため、キル者のチーム(未設定ならブルー)へ付与する。
        TeamMember killerMember = killerHero.GetComponent<TeamMember>();
        Team killerTeam = killerMember != null ? killerMember.Team : Team.Blue;

        PointsManager.AddPoints(killerTeam, KillPoints, "hero kill (test dummy)");
        int shutdownBonus = GetShutdownBonus(victim.KillStreak);
        if (shutdownBonus > 0)
        {
            PointsManager.AddPoints(killerTeam, shutdownBonus, $"shutdown ({victim.KillStreak} kill streak, test dummy)");
        }

        Debug.Log($"HeroKillRewards: テストプレイ用: {victimName}を撃破(キル+{KillPoints}pt / シャットダウン+{shutdownBonus}pt)。次の撃破はシャットダウン+{GetShutdownBonus(victim.KillStreak + 1)}pt。");

        // 次の撃破でシャットダウンを確認できるよう、ダミーの連続キル数を1進める。
        victim.KillStreak++;
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
