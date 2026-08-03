using UnityEngine;

/// <summary>スキル強化対象のスロット。</summary>
public enum HeroSkillSlot
{
    Q = 0,
    W = 1,
    E = 2,
    R = 3,
}

/// <summary>
/// ヒーロー1体分のスキル強化状態(Q/W/E/Rのランク)を管理するコンポーネント(フェーズ7)。
/// HeroLevelGrowthがヒーローへ自動追加する。
/// - GAME_DESIGN.md 6章の順序ルールに従う:
///   Lv2/Lv3/Lv4でQ/W/Eをそれぞれ1回ずつ強化(同じスキルの重複強化は不可)、Lv5でRを強化、
///   Lv6で好きなスキル1つを追加強化できる(最終強化。1回のみ。ランク上限2)。
/// - Ctrl+Q/W/E/Rの同時押しで強化する(PlayerInputHubのUpgrade*PressedThisFrameを使用)。
///   Ctrl押下中は通常のスキル入力がPlayerInputHub側で抑制されるため、強化操作でスキルは発動しない。
/// - 本タスクではランクの保持・UI・入力のみを実装し、ランクによるスキル性能の変化は
///   今後のタスクで実装する(各スキルコントローラーがGetRankを参照する想定)。
/// </summary>
public class HeroSkillUpgrades : MonoBehaviour
{
    public const int MaxRank = 2;

    private readonly int[] _ranks = new int[4];
    private bool _finalUpgradeUsed;
    private PlayerInputHub _input;
    private TeamMember _member;

    /// <summary>Lv6の追加強化(最終強化)を使用済みかどうか。</summary>
    public bool FinalUpgradeUsed => _finalUpgradeUsed;

    /// <summary>現在のチームレベル(TeamMember未割当の間はLv1扱い)。</summary>
    public int CurrentLevel => _member != null ? LevelSystem.GetLevelForTeam(_member.Team) : LevelSystem.MinLevel;

    private void Awake()
    {
        _input = GetComponent<PlayerInputHub>();
        _member = GetComponent<TeamMember>();
    }

    private void Update()
    {
        if (_member == null)
        {
            _member = GetComponent<TeamMember>();
        }

        if (_input == null)
        {
            return;
        }

        if (_input.UpgradeQPressedThisFrame)
        {
            TryUpgrade(HeroSkillSlot.Q);
        }

        if (_input.UpgradeWPressedThisFrame)
        {
            TryUpgrade(HeroSkillSlot.W);
        }

        if (_input.UpgradeEPressedThisFrame)
        {
            TryUpgrade(HeroSkillSlot.E);
        }

        if (_input.UpgradeRPressedThisFrame)
        {
            TryUpgrade(HeroSkillSlot.R);
        }
    }

    public int GetRank(HeroSkillSlot skill)
    {
        return _ranks[(int)skill];
    }

    // Q/W/Eのうち強化済みの数。Lv2:1回 / Lv3:2回 / Lv4:3回 の上限判定に使用する。
    private int BasicUpgradesUsed =>
        (_ranks[(int)HeroSkillSlot.Q] > 0 ? 1 : 0) +
        (_ranks[(int)HeroSkillSlot.W] > 0 ? 1 : 0) +
        (_ranks[(int)HeroSkillSlot.E] > 0 ? 1 : 0);

    // 通常強化(Lv2〜Lv5の枠)が可能か。
    private bool CanNormalUpgrade(HeroSkillSlot skill, int level)
    {
        if (skill == HeroSkillSlot.R)
        {
            return _ranks[(int)HeroSkillSlot.R] == 0 && level >= 5;
        }

        return _ranks[(int)skill] == 0 && BasicUpgradesUsed < Mathf.Clamp(level - 1, 0, 3);
    }

    // Lv6の追加強化(最終強化)が可能か。
    private bool CanFinalUpgrade(HeroSkillSlot skill, int level)
    {
        return level >= LevelSystem.MaxLevel && !_finalUpgradeUsed && _ranks[(int)skill] < MaxRank;
    }

    /// <summary>このスキルをいま強化できるか(通常強化またはLv6追加強化)。HUDの矢印表示に使用する。</summary>
    public bool CanUpgrade(HeroSkillSlot skill)
    {
        int level = CurrentLevel;
        return CanNormalUpgrade(skill, level) || CanFinalUpgrade(skill, level);
    }

    /// <summary>いま行える強化がLv6の追加強化(最終強化)かどうか。HUDの矢印の色分けに使用する。</summary>
    public bool IsFinalUpgradeCandidate(HeroSkillSlot skill)
    {
        int level = CurrentLevel;
        return !CanNormalUpgrade(skill, level) && CanFinalUpgrade(skill, level);
    }

    /// <summary>スキルを強化する。通常強化を優先し、不可ならLv6追加強化として消費する。</summary>
    public bool TryUpgrade(HeroSkillSlot skill)
    {
        int level = CurrentLevel;
        if (CanNormalUpgrade(skill, level))
        {
            _ranks[(int)skill]++;
            Debug.Log($"[HeroSkillUpgrades] {name} のスキル{skill}を強化しました (Rank {_ranks[(int)skill]}, Lv{level})。", this);
            return true;
        }

        if (CanFinalUpgrade(skill, level))
        {
            _ranks[(int)skill]++;
            _finalUpgradeUsed = true;
            Debug.Log($"[HeroSkillUpgrades] {name} のスキル{skill}へLv6追加強化を使用しました (Rank {_ranks[(int)skill]})。", this);
            return true;
        }

        Debug.Log($"[HeroSkillUpgrades] スキル{skill}はいま強化できません (Lv{level})。", this);
        return false;
    }
}
