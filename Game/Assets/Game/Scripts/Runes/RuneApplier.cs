using UnityEngine;
[DefaultExecutionOrder(-200)]
public class RuneApplier : MonoBehaviour
{
    private void Start()
    {
        RuneType rune = RuneSelectionManager.Instance?.SelectedRune ?? RuneType.None;
        if (rune == RuneType.None)
        {
            // 発動確認用ログ: ルーン未選択(ルーン選択画面を経由していない場合など)。
            Debug.Log("[ルーン] 未選択のため適用なし", this);
            return;
        }
        foreach (PlayerClickMovement hero in FindObjectsByType<PlayerClickMovement>(FindObjectsSortMode.None))
        {
            TeamMember tm = hero.GetComponent<TeamMember>();
            if (tm != null && tm.Team != Team.Blue) continue;
            ApplyTo(hero.gameObject, rune);
        }
    }

    public static void ApplyTo(GameObject hero, RuneType rune)
    {
        if (rune == RuneType.Relentless || rune == RuneType.AllForTesting)
            if (!hero.GetComponent<RelentlessRune>()) hero.AddComponent<RelentlessRune>();
        if (rune == RuneType.Indomitable || rune == RuneType.AllForTesting)
            if (!hero.GetComponent<IndomitableRune>()) hero.AddComponent<IndomitableRune>();
        if (rune == RuneType.Pursuit || rune == RuneType.AllForTesting)
            if (!hero.GetComponent<PursuitRune>()) hero.AddComponent<PursuitRune>();
        if (rune == RuneType.Siege || rune == RuneType.AllForTesting)
            if (!hero.GetComponent<SiegeRune>()) hero.AddComponent<SiegeRune>();
    }
}
