using UnityEngine;

/// <summary>
/// 全キャラクター・ダミー共通の「CC(行動妨害)を受け取る入口」。
/// CC付きスキルは必ずこのApplyHardCCを経由させることで、共通Dの無効化判定を一箇所に集約する。
/// 【重要】このAPIはハードCC(スタン・スネアなど)専用。
/// スロウはハードCCではなく共通Dで防げないため、このAPIの対象外(ゼルフRのスロウのように各スキルが個別に処理する)。
/// 実際の行動制限(移動・攻撃・スキルの禁止)は後続タスク「ハードCC、スネア、スタン、スロウを実装する」で実装し、
/// 現段階では受けたCCをログに記録するのみ。
/// </summary>
public class CrowdControlController : MonoBehaviour
{
    private CommonDController _commonD;

    private void Awake()
    {
        _commonD = GetComponent<CommonDController>();
    }

    /// <summary>
    /// ハードCCを適用する。
    /// 戻り値がtrueの場合は共通Dに無効化された。CC付きスキルの呼び元は、
    /// trueのときそのスキルのダメージも適用しないこと(ダメージとCCの両方を無効化する仕様)。
    /// </summary>
    /// <param name="duration">CCの持続時間(秒)。</param>
    /// <param name="attacker">CCを発生させた攻撃者。共通D成功時のカウンター対象になる。null可。</param>
    /// <returns>共通Dに無効化された場合はtrue。</returns>
    public bool ApplyHardCC(float duration, Transform attacker)
    {
        // CommonDControllerが後から追加された場合に備えて再取得する。
        if (_commonD == null) _commonD = GetComponent<CommonDController>();

        if (_commonD != null && _commonD.TryBlockHardCC(attacker))
        {
            return true;
        }

        // 行動制限は後続タスクで実装する。現段階では受けたハードCCを記録するのみ。
        Debug.Log($"CrowdControl: ハードCCを受けました({duration:F2}秒)。行動制限は後続タスクで実装します。", this);
        return false;
    }
}
