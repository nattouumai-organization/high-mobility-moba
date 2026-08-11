using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 朧W。3秒間透明化し、MSを上げる。解除条件が先に成立した場合はその時点で終了する。
/// 対象指定攻撃は受けられないが、Collider/Targetableは維持するため方向・地点指定スキルには命中する。
/// 敵が近い場合は輪郭リングを表示し、敵タワー射程内では輪郭表示に加えて通常どおり対象指定可能になる。
/// 攻撃、Q/E/R、D/F、W再発動で解除する。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(HealthController))]
[RequireComponent(typeof(CharacterStats))]
public sealed class OboroWController : MonoBehaviour, IIncomingDamageModifier
{
    [Header("Stealth")]
    [SerializeField, Min(0f)] private float _cooldown = 12f;
    [SerializeField, Min(0f)] private float _duration = 3f;
    [SerializeField, Range(0f, 100f)] private float _moveSpeedBoostPercent = 20f;
    [SerializeField, Min(0f)] private float _enemyOutlineRevealRadius = 3f;
    [SerializeField, Min(0f)] private float _enemyTowerRevealRange = 8f;

    [Header("Visual")]
    [SerializeField] private Color _outlineColor = new Color(0.55f, 0.35f, 0.85f, 0.95f);
    [SerializeField, Min(0.1f)] private float _outlineRadius = 0.72f;
    [SerializeField, Min(0.005f)] private float _outlineWidth = 0.06f;
    [SerializeField, Min(12)] private int _outlineSegments = 48;

    [Header("Debug (Runtime)")]
    [SerializeField] private bool _isWActive;
    [SerializeField] private float _remainingDuration;
    [SerializeField] private float _remainingCooldown;
    [SerializeField] private bool _isOutlineRevealed;
    [SerializeField] private bool _isInsideEnemyTowerRange;

    // HUDの既存経路と同じフィールド名。
    private double _cooldownEndTime;
    private double _effectEndTime;
    private HealthController _health;
    private CharacterStats _stats;
    private PlayerInputHub _inputHub;
    private AbilityLockController _abilityLock;
    private Renderer[] _bodyRenderers;
    private readonly Dictionary<Renderer, bool> _rendererStates = new Dictionary<Renderer, bool>();
    private LineRenderer _outline;
    private Material _outlineMaterial;
    private float _appliedMoveSpeedBonus;

    public bool IsWActive => _isWActive;
    public float RemainingCooldown => _remainingCooldown;
    public bool IsInsideEnemyTowerRange => _isInsideEnemyTowerRange;

    private void Awake()
    {
        _health = GetComponent<HealthController>();
        _stats = GetComponent<CharacterStats>();
        _inputHub = GetComponent<PlayerInputHub>();
        if (_inputHub == null) _inputHub = gameObject.AddComponent<PlayerInputHub>();
        _abilityLock = GetComponent<AbilityLockController>();
        if (_abilityLock == null) _abilityLock = gameObject.AddComponent<AbilityLockController>();

        // 輪郭自身を含めないよう、生成前に本体Renderer一覧を保存する。
        _bodyRenderers = GetComponentsInChildren<Renderer>(true);
        CreateOutline();

        _health.Died += HandleDied;
        _health.Revived += HandleRevived;
        _health.RefreshDamageModifiers();
    }

    private void OnDestroy()
    {
        if (_health != null)
        {
            _health.Died -= HandleDied;
            _health.Revived -= HandleRevived;
        }
        if (_outline != null) Destroy(_outline.gameObject);
        if (_outlineMaterial != null) Destroy(_outlineMaterial);
    }

    private void OnDisable()
    {
        if (_isWActive) Deactivate("コンポーネント停止", restoreRenderers: _health == null || !_health.IsDead);
        if (_outline != null) _outline.enabled = false;
    }

    private void Update()
    {
        _remainingCooldown = (float)System.Math.Max(0.0, _cooldownEndTime - Time.timeAsDouble);

        if (OboroCombatUtility.IsMatchEnded)
        {
            if (_isWActive) Deactivate("試合終了", restoreRenderers: true);
            return;
        }

        if (_isWActive)
        {
            _remainingDuration = (float)System.Math.Max(0.0, _effectEndTime - Time.timeAsDouble);
            if (Time.timeAsDouble >= _effectEndTime)
            {
                Deactivate("効果時間終了", restoreRenderers: true);
                return;
            }

            _isInsideEnemyTowerRange = EvaluateEnemyTowerRange();
            _isOutlineRevealed = _isInsideEnemyTowerRange || IsEnemyNear();
            if (_outline != null) _outline.enabled = _isOutlineRevealed;

            // W再発動、共通D、Fも「スキル使用」として透明化を解除する。
            if (_inputHub != null && _inputHub.WPressedThisFrame)
            {
                Deactivate("W再発動", restoreRenderers: true);
                return;
            }
            if (_inputHub != null && (_inputHub.DPressedThisFrame || _inputHub.FPressedThisFrame))
            {
                Deactivate("共通スキル使用", restoreRenderers: true);
            }
            return;
        }

        _remainingDuration = 0f;
        _isOutlineRevealed = false;
        _isInsideEnemyTowerRange = false;
        if (_outline != null) _outline.enabled = false;

        if (_inputHub != null && _inputHub.WPressedThisFrame)
        {
            TryActivate();
        }
    }

    private void TryActivate()
    {
        if (_health != null && _health.IsDead) return;
        if (_abilityLock != null && _abilityLock.IsLocked)
        {
            Debug.Log("朧 W: 他の行動中のため発動できません。", this);
            return;
        }
        if (Time.timeAsDouble < _cooldownEndTime)
        {
            Debug.Log("朧 W: クールダウン中です。", this);
            return;
        }

        _isWActive = true;
        _effectEndTime = Time.timeAsDouble + _duration;
        _remainingDuration = _duration;
        _cooldownEndTime = Time.timeAsDouble + _cooldown;
        _remainingCooldown = _cooldown;
        _rendererStates.Clear();
        foreach (Renderer renderer in _bodyRenderers)
        {
            if (renderer == null) continue;
            _rendererStates[renderer] = renderer.enabled;
            renderer.enabled = false;
        }

        if (_stats != null)
        {
            _appliedMoveSpeedBonus = _stats.BaseMoveSpeed * (_moveSpeedBoostPercent / 100f);
            _stats.AddMoveSpeedBonus(_appliedMoveSpeedBonus);
        }

        _isInsideEnemyTowerRange = EvaluateEnemyTowerRange();
        _isOutlineRevealed = _isInsideEnemyTowerRange || IsEnemyNear();
        if (_outline != null) _outline.enabled = _isOutlineRevealed;
        Debug.Log("朧 W: 透明化を開始しました。", this);
    }

    /// <summary>攻撃または他スキルが成立した時点で透明化を解除する。</summary>
    public void BreakStealth(string reason)
    {
        if (_isWActive) Deactivate(reason, restoreRenderers: _health == null || !_health.IsDead);
    }

    private void Deactivate(string reason, bool restoreRenderers)
    {
        if (!_isWActive) return;
        _isWActive = false;
        _effectEndTime = 0.0;
        _remainingDuration = 0f;
        _isOutlineRevealed = false;
        _isInsideEnemyTowerRange = false;
        if (_outline != null) _outline.enabled = false;

        if (_stats != null && !Mathf.Approximately(_appliedMoveSpeedBonus, 0f))
        {
            _stats.RemoveMoveSpeedBonus(_appliedMoveSpeedBonus);
        }
        _appliedMoveSpeedBonus = 0f;

        if (restoreRenderers)
        {
            foreach (KeyValuePair<Renderer, bool> pair in _rendererStates)
            {
                if (pair.Key != null) pair.Key.enabled = pair.Value;
            }
        }
        _rendererStates.Clear();
        Debug.Log($"朧 W: {reason}により透明化を解除しました。", this);
    }

    private void HandleDied()
    {
        if (_isWActive) Deactivate("死亡", restoreRenderers: false);
    }

    private void HandleRevived()
    {
        _isWActive = false;
        _effectEndTime = 0.0;
        _remainingDuration = 0f;
        _appliedMoveSpeedBonus = 0f;
        if (_outline != null) _outline.enabled = false;
    }

    /// <summary>
    /// PlayerTargetSelector・通常攻撃・対象指定スキルが共通で使用する対象指定可否。
    /// 透明化していても敵タワー射程内ならtrueになる。方向・地点指定スキルはこのAPIを呼ばない。
    /// </summary>
    public static bool CanBeTargetSelected(Targetable target, Transform requester)
    {
        if (target == null) return false;
        OboroWController stealth = target.GetComponentInParent<OboroWController>();
        if (stealth == null || !stealth._isWActive) return true;
        return stealth.EvaluateEnemyTowerRange();
    }

    /// <summary>
    /// AI側の既存索敵が対象を保持していた場合の最終安全網。
    /// 透明化中の通常攻撃を0にする一方、方向/地点スキル(IsBasicAttack=false)はそのまま通す。
    /// 敵タワー射程内は通常攻撃も通す。
    /// </summary>
    public float ModifyIncomingDamage(DamageContext context, float currentAmount)
    {
        if (!_isWActive || currentAmount <= 0f || !context.IsBasicAttack) return currentAmount;
        if (EvaluateEnemyTowerRange()) return currentAmount;

        // タワー攻撃も射程外へ出た後の遅延命中なら無効化する。
        // 射程内では上のEvaluateEnemyTowerRange()で既に通過している。
        Debug.Log("朧 W: 透明化中の対象指定通常攻撃を無効化しました。", this);
        return 0f;
    }

    private bool EvaluateEnemyTowerRange()
    {
        TeamMember selfTeam = GetComponent<TeamMember>();
        foreach (TowerController tower in FindObjectsByType<TowerController>(FindObjectsSortMode.None))
        {
            if (tower == null || !tower.isActiveAndEnabled || tower.IsDestroyed) continue;
            TeamMember towerTeam = tower.GetComponent<TeamMember>();
            if (selfTeam != null && towerTeam != null && selfTeam.Team == towerTeam.Team) continue;

            Collider towerCollider = tower.GetComponent<Collider>();
            Vector3 closest = towerCollider != null && towerCollider.enabled
                ? towerCollider.ClosestPoint(transform.position)
                : tower.transform.position;
            Vector3 difference = OboroCombatUtility.Flatten(closest - transform.position);
            if (difference.sqrMagnitude <= _enemyTowerRevealRange * _enemyTowerRevealRange) return true;
        }
        return false;
    }

    private bool IsEnemyNear()
    {
        TeamMember selfTeam = GetComponent<TeamMember>();
        foreach (TeamMember member in FindObjectsByType<TeamMember>(FindObjectsSortMode.None))
        {
            if (member == null || member.transform == transform || member.transform.IsChildOf(transform)) continue;
            if (selfTeam != null && member.Team == selfTeam.Team) continue;
            HealthController health = member.GetComponent<HealthController>();
            if (health != null && health.IsDead) continue;
            Vector3 difference = OboroCombatUtility.Flatten(member.transform.position - transform.position);
            if (difference.sqrMagnitude <= _enemyOutlineRevealRadius * _enemyOutlineRevealRadius) return true;
        }
        return false;
    }

    private void CreateOutline()
    {
        GameObject outlineObject = new GameObject("Oboro W Reveal Outline");
        outlineObject.transform.SetParent(transform, false);
        outlineObject.transform.localPosition = Vector3.up * 0.9f;
        _outline = outlineObject.AddComponent<LineRenderer>();
        _outlineMaterial = OboroCombatUtility.CreateUnlitMaterial(_outlineColor);
        _outline.material = _outlineMaterial;
        _outline.useWorldSpace = false;
        _outline.loop = true;
        _outline.alignment = LineAlignment.View;
        _outline.startWidth = _outlineWidth;
        _outline.endWidth = _outlineWidth;
        _outline.startColor = _outlineColor;
        _outline.endColor = _outlineColor;
        _outline.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _outline.receiveShadows = false;
        int count = Mathf.Max(12, _outlineSegments);
        _outline.positionCount = count;
        for (int i = 0; i < count; i++)
        {
            float angle = Mathf.PI * 2f * i / count;
            _outline.SetPosition(i, new Vector3(Mathf.Cos(angle) * _outlineRadius, Mathf.Sin(angle) * 0.9f, 0f));
        }
        _outline.enabled = false;
    }
}
