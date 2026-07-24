using System.Collections;
using UnityEngine;

/// <summary>
/// 死亡した対象を一定時間後に復活させる共通コンポーネント。
/// HealthControllerの死亡イベントを受け取り、Respawn Delay秒後に初期位置・初期向きへ戻して
/// HealthController.Revive()で現在HPを全快する。
/// 見た目・操作・HPバーの復元は、復活イベントを購読する各コンポーネント
/// (Targetable / PlayerDeathHandler / WorldHealthBar)がそれぞれ行う。
/// Player・TrainingDummy・AttackDummyで共通利用でき、将来のキャラクター・ミニオンにも再利用できる。
/// 復活時間はInspectorで設定し、C#コードへ直接書かない。
/// </summary>
[RequireComponent(typeof(HealthController))]
public class RespawnController : MonoBehaviour
{
    // 死亡から復活までの時間(秒)。
    // GAME_DESIGN.md: Lv1〜2=4秒 / Lv3〜4=6秒 / Lv5〜6=8秒。
    // レベルシステム実装まではLv1〜2の4秒を既定値とする(レベル連動はレベルシステム実装時の後続タスク)。
    // PlayerはInspectorで4秒へ設定すること。TrainingDummyなどテスト用の対象は短い値(1秒など)のままでよい。
    [SerializeField] private float _respawnDelay = 4f;

    private HealthController _healthController;
    private Vector3 _initialPosition;
    private Quaternion _initialRotation;
    private Coroutine _respawnCoroutine;

    private void Awake()
    {
        _healthController = GetComponent<HealthController>();

        // シーン開始時の位置・向きを復活地点として記録する。
        _initialPosition = transform.position;
        _initialRotation = transform.rotation;
    }

    private void OnEnable()
    {
        _healthController.Died += HandleDied;
    }

    private void OnDisable()
    {
        _healthController.Died -= HandleDied;

        if (_respawnCoroutine != null)
        {
            StopCoroutine(_respawnCoroutine);
            _respawnCoroutine = null;
        }
    }

    private void HandleDied()
    {
        if (_respawnCoroutine != null)
        {
            StopCoroutine(_respawnCoroutine);
        }

        _respawnCoroutine = StartCoroutine(RespawnRoutine());
    }

    private IEnumerator RespawnRoutine()
    {
        yield return new WaitForSeconds(_respawnDelay);

        // 死亡中(CharacterControllerなどが無効化されている間)に初期位置・初期向きへ戻してから全快する。
        transform.position = _initialPosition;
        transform.rotation = _initialRotation;

        _healthController.Revive();
        _respawnCoroutine = null;
    }
}
