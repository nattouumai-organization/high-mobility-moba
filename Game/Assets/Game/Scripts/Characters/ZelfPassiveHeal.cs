using UnityEngine;

/// <summary>
/// ゼルフP(パッシブ)の与ダメージ回復。TASKS.md「ゼルフPの与ダメージ回復を実装する」用のスクリプト。
/// 通常攻撃などで実際に与えたダメージ量(実ダメージ)を基準に、ターゲット分類ごとの回復率で自身を回復する。
/// GAME_DESIGN.mdの仕様: 敵ヒーローに与えたダメージの5%を回復し、ミニオンに対する回復量は半減。タワーでは回復しない。
/// 回復率はInspectorで設定し、C#コードへ直接書かない。
/// 実際に現在HPが増えた場合のみ、緑色の回復表示をCombatTextManagerへ要求する。
/// 回復のクールダウン・追加回復・回復阻害・ライフスティールは今回実装しない。
/// </summary>
public class ZelfPassiveHeal : MonoBehaviour
{
    // 回復先のHealthController。未設定の場合はAwakeで同じGameObjectから取得する。
    [SerializeField] private HealthController _healthController;

    // Character分類の対象へ与えた実ダメージに対する回復率(%)。GAME_DESIGN.mdの「敵ヒーローの5%」に対応する。
    [SerializeField] private float _characterHealPercent = 5f;

    // Minion分類の対象へ与えた実ダメージに対する回復率(%)。「ミニオンは半減」に対応する。
    [SerializeField] private float _minionHealPercent = 2.5f;

    // Tower分類の対象へ与えた実ダメージに対する回復率(%)。タワーでは回復しないため0。
    [SerializeField] private float _towerHealPercent = 0f;

    // TrainingDummy分類の対象へ与えた実ダメージに対する回復率(%)。テスト用の分類で、Characterと同じ扱いにしてある。
    [SerializeField] private float _trainingDummyHealPercent = 5f;

    private void Awake()
    {
        if (_healthController == null)
        {
            _healthController = GetComponent<HealthController>();
        }
    }

    /// <summary>
    /// 実際に与えたダメージ量とターゲット分類を受け取り、分類ごとの回復率で自身を回復する。
    /// 通常攻撃(PlayerBasicAttackController)から呼び出す。
    /// 過剰ダメージ分は呼び出し側で除外済みの実ダメージを渡すこと。
    /// 自身が死亡している場合は回復せず、最大HPを超える回復は行わない。
    /// </summary>
    public void NotifyDamageDealt(float actualDamage, TargetClassification targetClassification)
    {
        if (_healthController == null || _healthController.IsDead || actualDamage <= 0f)
        {
            return;
        }

        float healPercent = GetHealPercent(targetClassification);
        if (healPercent <= 0f)
        {
            return;
        }

        // Healは最大HPを超えない実回復量を返すため、満タン時は0が返り表示も行われない。
        float actualHeal = _healthController.Heal(actualDamage * healPercent / 100f);
        if (actualHeal > 0f)
        {
            CombatTextManager.ShowHeal(transform.position, actualHeal);
        }
    }

    private float GetHealPercent(TargetClassification targetClassification)
    {
        switch (targetClassification)
        {
            case TargetClassification.Character:
                return _characterHealPercent;
            case TargetClassification.Minion:
                return _minionHealPercent;
            case TargetClassification.Tower:
                return _towerHealPercent;
            case TargetClassification.TrainingDummy:
                return _trainingDummyHealPercent;
            default:
                return 0f;
        }
    }
}
