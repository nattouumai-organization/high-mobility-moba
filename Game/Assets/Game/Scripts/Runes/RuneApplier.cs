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
            switch (rune)
            {
                case RuneType.Relentless:  if (!hero.GetComponent<RelentlessRune>())  hero.gameObject.AddComponent<RelentlessRune>();  break;
                case RuneType.Indomitable: if (!hero.GetComponent<IndomitableRune>()) hero.gameObject.AddComponent<IndomitableRune>(); break;
                case RuneType.Pursuit:     if (!hero.GetComponent<PursuitRune>())     hero.gameObject.AddComponent<PursuitRune>();     break;
                case RuneType.Siege:       if (!hero.GetComponent<SiegeRune>())       hero.gameObject.AddComponent<SiegeRune>();       break;
            }
            // 発動確認用ログ: どのヒーローにどのルーンが付与されたか。
            Debug.Log($"[ルーン] {hero.name} に {rune} を適用", hero);
        }
    }
}
