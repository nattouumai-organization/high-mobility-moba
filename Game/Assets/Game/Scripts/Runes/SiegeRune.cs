using UnityEngine;
/// <summary>攻城ルーン(パッシブ): 味方ミニオンが敵タワー射程内 -> タワーダメージ12%増。</summary>
public class SiegeRune : MonoBehaviour
{
    private const float TowerRange = 800f / CharacterStats.RangeStatPerUnityUnit;
    private TeamMember _tm;
    private bool _active;
    private float _scan;
    private void Awake() { _tm = GetComponent<TeamMember>(); }
    private void Update()
    {
        if (_tm == null) _tm = GetComponent<TeamMember>();
        _scan -= Time.deltaTime;
        if (_scan <= 0f) { _scan = 0.5f; UpdateCondition(); }
    }
    public bool ConditionMet => _active;
    /// <summary>TowerControllerから呼び出す。対象が攻城条件満たしなら 1.12 を返す。</summary>
    public static float GetMultiplier(Transform attacker)
    {
        if (!attacker) return 1f;
        SiegeRune sr = attacker.GetComponentInParent<SiegeRune>();
        if (sr != null && sr._active)
        {
            // 発動確認用ログ: タワーへの攻撃に倍率が適用された瞬間。
            Debug.Log($"[ルーン/攻城] 発動！ {attacker.name} のタワーダメージ 1.12倍", sr);
            return 1.12f;
        }
        return 1f;
    }
    private void UpdateCondition()
    {
        bool met = CheckCondition();
        if (met != _active)
        {
            // 発動確認用ログ: 条件(味方ミニオンが敵タワー射程内)の切り替わり。
            if (met) Debug.Log("[ルーン/攻城] 条件成立: 味方ミニオンが敵タワー射程内 (タワーダメージ+12%)", this);
            else Debug.Log("[ルーン/攻城] 条件解除: 射程内に味方ミニオンなし", this);
        }
        _active = met;
    }
    private bool CheckCondition()
    {
        if (_tm == null) return false;
        foreach (TowerController t in FindObjectsByType<TowerController>(FindObjectsSortMode.None))
        {
            TeamMember tt = t.GetComponent<TeamMember>();
            if (tt == null || tt.Team == _tm.Team) continue;
            foreach (MinionController m in FindObjectsByType<MinionController>(FindObjectsSortMode.None))
            {
                TeamMember mt = m.GetComponent<TeamMember>();
                if (mt == null || mt.Team != _tm.Team) continue;
                if (Vector3.Distance(m.transform.position, t.transform.position) <= TowerRange)
                    return true;
            }
        }
        return false;
    }
}
