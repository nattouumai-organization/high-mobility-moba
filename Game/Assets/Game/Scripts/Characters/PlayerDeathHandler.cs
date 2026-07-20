using UnityEngine;

/// <summary>
/// Playerの死亡処理。HealthControllerの死亡イベントを受け取り、
/// 操作系コンポーネントと見た目を無効化する。
/// TASKS.md「ダメージと死亡処理を実装する」用の試作スクリプト。
/// リスポーン・復活時間・拠点への帰還・クールダウン短縮は今回実装しない。
/// PlayerのHPバーは、WorldHealthBarが死亡イベントを受けて非表示にする。
/// </summary>
[RequireComponent(typeof(HealthController))]
public class PlayerDeathHandler : MonoBehaviour
{
    private HealthController _healthController;

    private void Awake()
    {
        _healthController = GetComponent<HealthController>();
    }

    private void OnEnable()
    {
        _healthController.Died += HandleDied;
    }

    private void OnDisable()
    {
        _healthController.Died -= HandleDied;
    }

    private void HandleDied()
    {
        // 操作系コンポーネントを無効化する。
        DisableIfPresent(GetComponent<PlayerClickMovement>());
        DisableIfPresent(GetComponent<PlayerMouseFacing>());
        DisableIfPresent(GetComponent<PlayerBasicAttackController>());

        // CharacterControllerはBehaviourではないため個別に無効化する。
        CharacterController characterController = GetComponent<CharacterController>();
        if (characterController != null)
        {
            characterController.enabled = false;
        }

        // 本体とFrontMarkerなど、子を含めた見た目を無効化する。
        foreach (Renderer childRenderer in GetComponentsInChildren<Renderer>())
        {
            childRenderer.enabled = false;
        }
    }

    private static void DisableIfPresent(Behaviour behaviour)
    {
        if (behaviour != null)
        {
            behaviour.enabled = false;
        }
    }
}
