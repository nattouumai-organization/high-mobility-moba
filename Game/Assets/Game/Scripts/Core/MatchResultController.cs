using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>試合終了時に結果UIを表示し、ゲームプレイを一度だけ停止する。</summary>
[DefaultExecutionOrder(-240)]
public sealed class MatchResultController : MonoBehaviour
{
    private const string MatchEndLockReason = "MatchEnded";
    [SerializeField, Min(0f)] private float _pauseDelaySeconds = 1f;

    private readonly HashSet<string> _gameplayBehaviourNames = new HashSet<string>
    {
        nameof(PlayerInputHub), nameof(PlayerClickMovement), nameof(PlayerMouseFacing),
        nameof(PlayerBasicAttackController), nameof(PlayerTargetSelector),
        "ZelfPassiveHeal", "ZelfQController", "ZelfWController", "ZelfEController", "ZelfRController",
        "VolbraakPassiveShield", "VolbraakQController", "VolbraakWController", "VolbraakEController", "VolbraakRController",
        "OboroSkillInstaller", "OboroPassiveBackstab", "OboroQController", "OboroWController", "OboroEController", "OboroRController",
        "CommonDController", "FlashController", "HeroSkillUpgrades", "SkillRangeIndicator", "SkillRangePreview",
        "PlayerDeathHandler", nameof(RespawnController), "DummyAutoAttack", "HardCcTestEmitter",
        "RelentlessRune", "IndomitableRune", "PursuitRune", "SiegeRune", "HeroKillRewards",
        "HeroLevelGrowth", "RuneApplier",
    };

    private GameManager _gameManager;
    private MatchResultUI _resultUI;
    private bool _handled;
    private Coroutine _pauseCoroutine;

    public float PauseDelaySeconds
    {
        get => _pauseDelaySeconds;
        set => _pauseDelaySeconds = Mathf.Max(0f, value);
    }

    private void Awake()
    {
        Time.timeScale = 1f;
        _gameManager = GetComponent<GameManager>();
        if (_gameManager == null) _gameManager = GameManager.Instance;
        _resultUI = GetComponent<MatchResultUI>();
        if (_resultUI == null) _resultUI = gameObject.AddComponent<MatchResultUI>();
    }

    private void OnEnable() { Subscribe(); }

    private void Start()
    {
        Subscribe();
        if (_gameManager != null && _gameManager.IsMatchEnded) HandleMatchEnded(_gameManager.WinningTeam);
    }

    private void OnDisable() { Unsubscribe(); }

    private void OnDestroy()
    {
        Unsubscribe();
        Time.timeScale = 1f;
    }

    private void Subscribe()
    {
        if (_gameManager == null) _gameManager = GameManager.Instance;
        if (_gameManager == null) return;
        _gameManager.MatchEnded -= HandleMatchEnded;
        _gameManager.MatchEnded += HandleMatchEnded;
    }

    private void Unsubscribe()
    {
        if (_gameManager != null) _gameManager.MatchEnded -= HandleMatchEnded;
    }

    private void HandleMatchEnded(Team winningTeam)
    {
        if (_handled) return;
        _handled = true;
        Team losingTeam = winningTeam.Opponent();
        if (_resultUI != null) _resultUI.ShowResult(winningTeam, losingTeam);
        StopGameplayImmediately();
        if (_pauseCoroutine != null) StopCoroutine(_pauseCoroutine);
        _pauseCoroutine = StartCoroutine(PauseAfterDelay());
    }

    private void StopGameplayImmediately()
    {
        PlayerInputHub[] inputs = FindObjectsByType<PlayerInputHub>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (PlayerInputHub input in inputs)
        {
            if (input == null) continue;
            GameObject player = input.gameObject;
            PlayerClickMovement movement = player.GetComponent<PlayerClickMovement>();
            if (movement != null) movement.StopMovement();
            PlayerTargetSelector selector = player.GetComponent<PlayerTargetSelector>();
            if (selector != null) selector.ClearTargetSelection();
            player.SendMessage("CancelPendingApproach", SendMessageOptions.DontRequireReceiver);
            AbilityLockController abilityLock = player.GetComponent<AbilityLockController>();
            if (abilityLock != null && !abilityLock.IsLockedBy(MatchEndLockReason)) abilityLock.AddLock(MatchEndLockReason);
            DisableGameplayBehaviours(player.GetComponents<MonoBehaviour>());
            CharacterController controller = player.GetComponent<CharacterController>();
            if (controller != null) controller.enabled = false;
        }

        foreach (MinionController minion in FindObjectsByType<MinionController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (minion == null) continue;
            minion.StopAllCoroutines(); minion.enabled = false;
        }
        foreach (TowerController tower in FindObjectsByType<TowerController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (tower == null) continue;
            tower.StopAllCoroutines(); tower.enabled = false;
        }
        DisableGameplayBehaviours(FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None));
        foreach (Rigidbody body in FindObjectsByType<Rigidbody>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (body == null) continue;
            body.linearVelocity = Vector3.zero; body.angularVelocity = Vector3.zero;
        }
    }

    private void DisableGameplayBehaviours(MonoBehaviour[] behaviours)
    {
        foreach (MonoBehaviour behaviour in behaviours)
        {
            if (behaviour == null || behaviour == this || behaviour == _resultUI || behaviour == _gameManager) continue;
            if (!_gameplayBehaviourNames.Contains(behaviour.GetType().Name)) continue;
            behaviour.StopAllCoroutines();
            behaviour.enabled = false;
        }
    }

    private IEnumerator PauseAfterDelay()
    {
        if (_pauseDelaySeconds > 0f) yield return new WaitForSecondsRealtime(_pauseDelaySeconds);
        Time.timeScale = 0f;
        _pauseCoroutine = null;
    }
}
