using UnityEngine;
/// <summary>攻城ルーン(パッシブ): 味方ミニオンが敵タワー射程内 -> タワーダメーコ12%増。</summary>
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
        return (sr != null && sr._active) ? 1.12f : 1f;
    }
    private void UpdateCondition()
    {
        if (_tm == null) { _active = false; return; }
        foreach (TowerController t in FindObjectsByType<TowerController>(FindObjectsSortMode.None))
        {
            TeamMember tt = t.GetComponent<TeamMember>();
            if (tt == null || tt.Team == _tm.Team) continue;
            foreach (MinionController m in FindObjectsByType<MinionController>(FindObjectsSortMode.None))
            {
                TeamMember mt = m.GetComponent<TeamMember>();
                if (mt == null || mt.Team != _tm.Team) continue;
                if (Vector3.Distance(m.transform.position, t.transform.position) <= TowerRange)
                    { _active = true; return; }
            }
        }
        _active = false;
    }
}
