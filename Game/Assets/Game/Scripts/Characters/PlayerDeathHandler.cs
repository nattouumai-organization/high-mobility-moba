using UnityEngine;

/// <summary>
/// Playerの死亡処理。HealthControllerの死亡イベントを受け取り、
/// 操作系コンポーネントと見た目を無効化する。
/// TASKS.md「ダメージと死亡処理を実装する」用の試作スクリプト。
/// 復活イベントを受け取った場合は、無効化した操作系コンポーネントと見た目を元へ戻す
/// (復活までの時間と復活位置はRespawnControllerが管理する)。
/// 拠点への帰還・クールダウン短縮は今回実装しない。
/// PlayerのHPバーは、WorldHealthBarが死亡・復活イベントを受けて非表示・再表示する。
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
        _healthController.Revived += HandleRevived;
    }

    private void OnDisable()
    {
        _healthController.Died -= HandleDied;
        _healthController.Revived -= HandleRevived;
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

    private void HandleRevived()
    {
        // 死亡時に無効化した操作系コンポーネントを元へ戻す。
        EnableIfPresent(GetComponent<PlayerClickMovement>());
        EnableIfPresent(GetComponent<PlayerMouseFacing>());
        EnableIfPresent(GetComponent<PlayerBasicAttackController>());

        // CharacterControllerはBehaviourではないため個別に有効化する。
        // 復活位置は、無効化中にRespawnControllerが初期位置へ戻している。
        CharacterController characterController = GetComponent<CharacterController>();
        if (characterController != null)
        {
            characterController.enabled = true;
        }

        // 本体とFrontMarkerなど、子を含めた見た目を元へ戻す。
        foreach (Renderer childRenderer in GetComponentsInChildren<Renderer>())
        {
            childRenderer.enabled = true;
        }

        // 死亡前の移動先が残らないよう、移動を停止した状態で復活する。
        PlayerClickMovement clickMovement = GetComponent<PlayerClickMovement>();
        if (clickMovement != null)
        {
            clickMovement.StopMovement();
        }
    }

    private static void EnableIfPresent(Behaviour behaviour)
    {
        if (behaviour != null)
        {
            behaviour.enabled = true;
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
