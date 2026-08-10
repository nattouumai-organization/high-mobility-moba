using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// GameManager.MatchEndedを受け、結果UIの表示とゲームプレイ停止を一度だけ行う。
/// コンポーネントはDestroyせず、入力・自動進行・戦闘処理を無効化してからunscaled時間で待機し、Time.timeScaleを0にする。
/// </summary>
[DefaultExecutionOrder(-240)]
public sealed class MatchResultController : MonoBehaviour
{
    private const string MatchEndLockReason = "MatchEnded";

    [SerializeField, Min(0f)] private float _pauseDelaySeconds = 1f;

    private readonly HashSet<string> _gameplayBehaviourNames = new HashSet<string>
    {
        nameof(PlayerInputHub),
        nameof(PlayerClickMovement),
        nameof(PlayerMouseFacing),
        nameof(PlayerBasicAttackController),
        nameof(PlayerTargetSelector),
        "ZelfPassiveHeal",
        "ZelfQController",
        "ZelfWController",
        "ZelfEController",
        "ZelfRController",
        "VolbraakPassiveShield",
        "VolbraakQController",
        "VolbraakWController",
        "VolbraakEController",
        "VolbraakRController",
        "CommonDController",
        "FlashController",
        "HeroSkillUpgrades",
        "SkillRangeIndicator",
        "SkillRangePreview",
        "PlayerDeathHandler",
        nameof(RespawnController),
        "DummyAutoAttack",
        "HardCcTestEmitter",
        "RelentlessRune",
        "IndomitableRune",
        "PursuitRune",
        "SiegeRune",
        "HeroKillRewards",
        "HeroLevelGrowth",
        "RuneApplier",
    };

    private GameManager _gameManager;
    private MatchResultUI _resultUI;
    private bool _handled;
    private Coroutine _pauseCoroutine;

    /// <summary>Inspectorから調整可能な、UI表示後にTime.timeScaleを0へするまでの実時間。</summary>
    public float PauseDelaySeconds
    {
        get => _pauseDelaySeconds;
        set => _pauseDelaySeconds = Mathf.Max(0f, value);
    }

    private void Awake()
    {
        // Time.timeScaleはシーンをまたいで残るため、シーン再読込時に必ず復元する。
        Time.timeScale = 1f;

        _gameManager = GetComponent<GameManager>();
        if (_gameManager == null)
        {
            _gameManager = GameManager.Instance;
        }

        _resultUI = GetComponent<MatchResultUI>();
        if (_resultUI == null)
        {
            _resultUI = gameObject.AddComponent<MatchResultUI>();
        }
    }

    private void OnEnable()
    {
        Subscribe();
    }

    private void Start()
    {
        Subscribe();

        // 実行順の変更などで購読前に終了していても取りこぼさない。
        if (_gameManager != null && _gameManager.IsMatchEnded)
        {
            HandleMatchEnded(_gameManager.WinningTeam);
        }
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void OnDestroy()
    {
        Unsubscribe();
        Time.timeScale = 1f;
    }

    private void Subscribe()
    {
        if (_gameManager == null)
        {
            _gameManager = GameManager.Instance;
        }

        if (_gameManager == null)
        {
            return;
        }

        _gameManager.MatchEnded -= HandleMatchEnded;
        _gameManager.MatchEnded += HandleMatchEnded;
    }

    private void Unsubscribe()
    {
        if (_gameManager != null)
        {
            _gameManager.MatchEnded -= HandleMatchEnded;
        }
    }

    private void HandleMatchEnded(Team winningTeam)
    {
        if (_handled)
        {
            return;
        }

        _handled = true;
        Team losingTeam = winningTeam.Opponent();

        // UIはゲーム停止より先に構築・表示する。標準UGUIの表示自体はtimeScaleに依存しない。
        if (_resultUI != null)
        {
            _resultUI.ShowResult(winningTeam, losingTeam);
        }

        StopGameplayImmediately();

        if (_pauseCoroutine != null)
        {
            StopCoroutine(_pauseCoroutine);
        }
        _pauseCoroutine = StartCoroutine(PauseAfterDelay());
    }

    private void StopGameplayImmediately()
    {
        // ヒーロー: 移動予約・選択・Q/R自動接近を先に解除し、行動ロックを追加する。
        PlayerInputHub[] playerInputs = FindObjectsByType<PlayerInputHub>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);

        foreach (PlayerInputHub input in playerInputs)
        {
            if (input == null)
            {
                continue;
            }

            GameObject player = input.gameObject;

            PlayerClickMovement movement = player.GetComponent<PlayerClickMovement>();
            if (movement != null)
            {
                movement.StopMovement();
            }

            PlayerTargetSelector selector = player.GetComponent<PlayerTargetSelector>();
            if (selector != null)
            {
                selector.ClearTargetSelection();
            }

            // Zelf Q/Rなど、公開されている中止APIをコンポーネント構成に依存せず呼ぶ。
            player.SendMessage("CancelPendingApproach", SendMessageOptions.DontRequireReceiver);

            AbilityLockController abilityLock = player.GetComponent<AbilityLockController>();
            if (abilityLock != null && !abilityLock.IsLockedBy(MatchEndLockReason))
            {
                abilityLock.AddLock(MatchEndLockReason);
            }

            DisableGameplayBehaviours(player.GetComponents<MonoBehaviour>());

            CharacterController characterController = player.GetComponent<CharacterController>();
            if (characterController != null)
            {
                characterController.enabled = false;
            }
        }

        // 既存ミニオン: Updateを止めることで移動・索敵・通常攻撃・分離処理を停止する。HPバーは残る。
        foreach (MinionController minion in FindObjectsByType<MinionController>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (minion == null)
            {
                continue;
            }

            minion.StopAllCoroutines();
            minion.enabled = false;
        }

        // タワー: Updateを止めることで索敵・アグロ更新・通常攻撃を停止する。
        foreach (TowerController tower in FindObjectsByType<TowerController>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (tower == null)
            {
                continue;
            }

            tower.StopAllCoroutines();
            tower.enabled = false;
        }

        // Player以外のダミー復活や報酬監視なども停止する。
        DisableGameplayBehaviours(FindObjectsByType<MonoBehaviour>(
            FindObjectsInactive.Include, FindObjectsSortMode.None));

        // 投射物などにRigidbodyが使われていても惰性で進み続けないよう速度を0にする。
        foreach (Rigidbody body in FindObjectsByType<Rigidbody>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (body == null)
            {
                continue;
            }

            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
        }
    }

    private void DisableGameplayBehaviours(MonoBehaviour[] behaviours)
    {
        foreach (MonoBehaviour behaviour in behaviours)
        {
            if (behaviour == null || behaviour == this || behaviour == _resultUI || behaviour == _gameManager)
            {
                continue;
            }

            if (!_gameplayBehaviourNames.Contains(behaviour.GetType().Name))
            {
                continue;
            }

            behaviour.StopAllCoroutines();
            behaviour.enabled = false;
        }
    }

    private IEnumerator PauseAfterDelay()
    {
        if (_pauseDelaySeconds > 0f)
        {
            yield return new WaitForSecondsRealtime(_pauseDelaySeconds);
        }

        Time.timeScale = 0f;
        _pauseCoroutine = null;
    }
}
