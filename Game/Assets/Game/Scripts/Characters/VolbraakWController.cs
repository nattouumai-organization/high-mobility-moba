using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ヴォルブラークW(シールドと時限爆発)を管理するコンポーネント。SC_PrototypeのPlayerへアタッチして使用する。
/// Wキーで即時にHPシールドを獲得し(対象・方向指定なしの自己バフのためプレビューなし)、
/// 一定時間後(Shield Duration)に自動爆発して周囲へ範囲ダメージを与える(手動爆発なし)。
/// - シールドはIIncomingDamageModifierとしてダメージ種別(Normal / True)を問わず吸収する。
///   通常ダメージはAR軽減式(×100/(100+AR))を適用したHP換算値でシールドを消費し、吸収しきれない分だけHPへ通す。
/// - ヴォルブラークP(VolbraakPassiveShield)展開中にミニオン以外から攻撃を受けた場合は、
///   Wでは吸収せずPの初撃無効化を優先する(IIncomingDamageModifierの適用順に依存しない)。
/// - シールドが途中で割れても、爆発は予定どおり発生する。
/// - 爆発で実際に与えたダメージ×回復率(既定: 5%、ミニオンは半減の2.5%)を自身へ回復する。
/// - 移動を伴わないためスネア中も使用できる。スタン・他スキルの行動ロック中・死亡中は使用不可
///   (展開済みのシールド・爆発の進行はロック中も継続する)。自身の死亡時はシールド・爆発を中止する(爆発しない)。
/// シールド中はPlayerの周囲へ青系リングを、爆発時は爆発半径のリングを短時間表示する(LineRenderer実行時生成)。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(CharacterStats))]
public sealed class VolbraakWController : MonoBehaviour, IIncomingDamageModifier
{
    [Header("Settings")]
    [Tooltip("発動から自動爆発までの時間(秒)。この間シールドが持続する")]
    [SerializeField, Min(0.1f)] private float _shieldDuration = 3f;

    [Tooltip("クールダウン(秒)。発動した瞬間から計測する")]
    [SerializeField, Min(0f)] private float _cooldown = 12f;

    [Header("シールド")]
    [Tooltip("シールド量の基礎値(HP換算)")]
    [SerializeField, Min(0f)] private float _shieldBaseAmount = 80f;

    [Tooltip("シールド量へ加算するADレート(発動時のAD×この値)")]
    [SerializeField, Min(0f)] private float _shieldADRatio = 0.8f;

    [Header("爆発")]
    [Tooltip("爆発の半径(Unity units)")]
    [SerializeField, Min(0f)] private float _explosionRadius = 2.5f;

    [Tooltip("爆発ダメージの基礎値")]
    [SerializeField, Min(0f)] private float _explosionBaseDamage = 40f;

    [Tooltip("爆発ダメージへ加算するADレート(爆発時のAD×この値)")]
    [SerializeField, Min(0f)] private float _explosionADRatio = 0.9f;

    [Header("与ダメージ回復")]
    [Tooltip("Character分類へ実際に与えたダメージに対する回復率(%)。GAME_DESIGNの「与ダメージの5%を回復」に対応")]
    [SerializeField, Min(0f)] private float _characterHealPercent = 5f;

    [Tooltip("Minion分類へ実際に与えたダメージに対する回復率(%)。「ミニオン相手は半減」に対応")]
    [SerializeField, Min(0f)] private float _minionHealPercent = 2.5f;

    [Tooltip("Tower分類へ実際に与えたダメージに対する回復率(%)")]
    [SerializeField, Min(0f)] private float _towerHealPercent = 5f;

    [Tooltip("TrainingDummy分類へ実際に与えたダメージに対する回復率(%)。テスト用にCharacterと同じ扱い")]
    [SerializeField, Min(0f)] private float _trainingDummyHealPercent = 5f;

    [Header("レイヤー")]
    [Tooltip("爆発ダメージの対象レイヤー(ZelfQControllerと同じ設定にする)")]
    [SerializeField] private LayerMask _targetableLayer;

    [Header("見た目")]
    [Tooltip("シールド中にPlayerの周囲へリングを表示する")]
    [SerializeField] private bool _showShieldRing = true;

    [Tooltip("シールドリングの色(Pの黄色系リングと区別する青系)")]
    [SerializeField] private Color _shieldRingColor = new Color(0.35f, 0.7f, 1f, 0.9f);

    [Tooltip("シールドリングの半径(Pのリングより少し大きくして区別する)")]
    [SerializeField, Min(0.1f)] private float _shieldRingRadius = 1.1f;

    [Tooltip("リングのPlayer中心からの高さ(ローカル座標)")]
    [SerializeField] private float _ringLocalHeight = 0.2f;

    [Tooltip("リングの線の太さ")]
    [SerializeField, Min(0.005f)] private float _ringWidth = 0.06f;

    [Tooltip("爆発時に表示するリングの色")]
    [SerializeField] private Color _explosionRingColor = new Color(0.95f, 0.5f, 0.15f, 0.9f);

    [Tooltip("爆発リングの表示時間(秒)")]
    [SerializeField, Min(0.05f)] private float _explosionFlashDuration = 0.2f;

    [Header("Debug (Runtime)")]
    [SerializeField] private bool _isWActive;
    [SerializeField] private float _currentShield;
    [SerializeField] private float _remainingCooldown;
    [SerializeField] private float _remainingTimeToExplosion;

    // リング円周の分割数。多いほど滑らかになる。
    private const int RingSegmentCount = 48;

    private CharacterStats _characterStats;
    private HealthController _health;
    private VolbraakPassiveShield _passiveShield;
    private AbilityLockController _abilityLock;
    private PlayerInputHub _inputHub;

    // クールダウン終了時刻。長時間起動でもfloat精度が落ちないよう、Time.timeAsDouble基準のdoubleで管理する。
    private double _cooldownEndTime;

    // 自動爆発の時刻(Time.time基準)。
    private float _explodeTime;

    private LineRenderer _shieldRing;
    private LineRenderer _explosionRing;
    private Material _shieldRingMaterial;
    private Material _explosionRingMaterial;
    private Coroutine _flashCoroutine;

    /// <summary>W発動中(シールド獲得から自動爆発まで)かどうか。</summary>
    public bool IsWActive => _isWActive;

    /// <summary>現在の残りシールド量(HP換算)。</summary>
    public float CurrentShield => _currentShield;

    private void Awake()
    {
        _characterStats = GetComponent<CharacterStats>();
        _health = GetComponent<HealthController>();
        _passiveShield = GetComponent<VolbraakPassiveShield>();
        _abilityLock = GetComponent<AbilityLockController>();
        if (_abilityLock == null) _abilityLock = gameObject.AddComponent<AbilityLockController>();
        _inputHub = GetComponent<PlayerInputHub>();
        if (_inputHub == null) _inputHub = gameObject.AddComponent<PlayerInputHub>();

        if (_targetableLayer.value == 0)
        {
            Debug.LogWarning("VolbraakWController: Targetable Layerが未設定です。InspectorでZelfQControllerと同じ設定にしてください。", this);
        }

        _shieldRing = CreateRing("Volbraak W Shield Ring", _shieldRingRadius, _shieldRingColor, out _shieldRingMaterial);
        _explosionRing = CreateRing("Volbraak W Explosion Ring", _explosionRadius, _explosionRingColor, out _explosionRingMaterial);

        if (_health != null)
        {
            _health.Died += HandleDied;
        }
    }

    private void OnDestroy()
    {
        if (_health != null)
        {
            _health.Died -= HandleDied;
        }

        if (_shieldRing != null) Destroy(_shieldRing.gameObject);
        if (_explosionRing != null) Destroy(_explosionRing.gameObject);
        if (_shieldRingMaterial != null) Destroy(_shieldRingMaterial);
        if (_explosionRingMaterial != null) Destroy(_explosionRingMaterial);
    }

    private void Update()
    {
        _remainingCooldown = (float)System.Math.Max(0.0, _cooldownEndTime - Time.timeAsDouble);
        _remainingTimeToExplosion = _isWActive ? Mathf.Max(0f, _explodeTime - Time.time) : 0f;

        // PlayerDeathHandlerが復活時に従属コンポーネントを一括再有効化しても表示が残らないよう毎フレーム強制同期。
        // シールドが割れた後(残量0)はリングを消すが、爆発は予定どおり発生する。
        if (_shieldRing != null)
        {
            _shieldRing.enabled = _showShieldRing && _isWActive && _currentShield > 0f;
        }

        // 自動爆発(手動爆発なし)。スタンなどの行動ロック中でも進行する。
        if (_isWActive && Time.time >= _explodeTime)
        {
            Explode();
        }

        // 対象・方向指定のない自己バフのため、押した瞬間に発動する(プレビューなし)。
        if (_inputHub != null && _inputHub.WPressedThisFrame)
        {
            HandleWPressed();
        }
    }

    private void HandleWPressed()
    {
        if (_isWActive)
        {
            Debug.Log("Volbraak W: 発動中です(手動爆発・再発動はできません)。", this);
            return;
        }

        if (Time.timeAsDouble < _cooldownEndTime)
        {
            Debug.Log("Volbraak W: クールダウン中です。", this);
            return;
        }

        if (_health == null || _health.IsDead)
        {
            return;
        }

        // スタンや他スキルの行動ロック中は発動できない。移動を伴わないためスネア中は使用できる。
        if (_abilityLock != null && _abilityLock.IsLocked)
        {
            Debug.Log("Volbraak W: 行動ロック中のため発動できません。", this);
            return;
        }

        Activate();
    }

    private void Activate()
    {
        // シールド量は発動時のADでスナップショットする。
        float ad = _characterStats != null ? _characterStats.CurrentAttackDamage : 0f;
        _currentShield = _shieldBaseAmount + ad * _shieldADRatio;
        _isWActive = true;
        _explodeTime = Time.time + _shieldDuration;
        _cooldownEndTime = Time.timeAsDouble + _cooldown;

        if (_shieldRing != null)
        {
            _shieldRing.enabled = _showShieldRing;
        }

        Debug.Log($"Volbraak W: シールド{_currentShield:F1}を獲得しました({_shieldDuration:F1}秒後に自動爆発)。", this);
    }

    /// <summary>
    /// HealthControllerがHPへ適用する直前に呼び出すダメージ変更処理。
    /// シールド残量がある間、ダメージ種別(Normal / True)を問わず吸収する。
    /// 通常ダメージはAR軽減後のHP換算値でシールドを消費し、吸収しきれない分だけを元ダメージ換算へ戻して返す
    /// (返した値にはこの後HealthControllerがARによる軽減を適用するため、二重軽減にはならない)。
    /// </summary>
    public float ModifyIncomingDamage(DamageContext context, float currentAmount)
    {
        if (!_isWActive || _currentShield <= 0f || currentAmount <= 0f)
        {
            return currentAmount;
        }

        // ヴォルブラークP展開中のミニオン以外からの攻撃は、Pの初撃無効化を優先する(Wシールドは消費しない)。
        // Pが先に適用済みの場合はcurrentAmountが0になっているため、上の早期returnで抜ける。
        if (_passiveShield != null && _passiveShield.IsShieldReady && !IsMinionAttack(context.Attacker))
        {
            return currentAmount;
        }

        // 通常ダメージはAR軽減後のHP換算値でシールドを消費する(確定ダメージは軽減なしのHP換算)。
        float mitigationFactor = 1f;
        if (context.Type == DamageType.Normal && _characterStats != null)
        {
            float armor = _characterStats.CurrentArmor;
            if (armor > 0f)
            {
                mitigationFactor = 100f / (100f + armor);
            }
        }

        float hpEquivalent = currentAmount * mitigationFactor;
        float absorbed = Mathf.Min(_currentShield, hpEquivalent);
        _currentShield -= absorbed;

        Debug.Log($"Volbraak W: シールドがダメージを{absorbed:F1}吸収しました(残りシールド{_currentShield:F1})。", this);
        if (_currentShield <= 0f)
        {
            _currentShield = 0f;
            Debug.Log("Volbraak W: シールドが割れました(爆発は予定どおり発生します)。", this);
        }

        return (hpEquivalent - absorbed) / mitigationFactor;
    }

    // 攻撃者がミニオン(TargetClassification.Minion)かどうか(VolbraakPassiveShieldと同じ判定)。
    private static bool IsMinionAttack(Transform attacker)
    {
        if (attacker == null)
        {
            return false;
        }

        Targetable targetable = attacker.GetComponentInParent<Targetable>();
        return targetable != null && targetable.Classification == TargetClassification.Minion;
    }

    // 自動爆発: 周囲の対象へ範囲ダメージを与え、実際に与えたダメージ×回復率で自身を回復する。
    private void Explode()
    {
        _isWActive = false;
        _currentShield = 0f;
        if (_shieldRing != null)
        {
            _shieldRing.enabled = false;
        }

        // 爆発ダメージは爆発時のADで計算する。
        float ad = _characterStats != null ? _characterStats.CurrentAttackDamage : 0f;
        float damage = _explosionBaseDamage + ad * _explosionADRatio;

        int hitCount = 0;
        float healAmount = 0f;

        if (_targetableLayer.value != 0 && damage > 0f)
        {
            List<Targetable> hitTargets = new List<Targetable>();
            Collider[] hits = Physics.OverlapSphere(transform.position, _explosionRadius, _targetableLayer, QueryTriggerInteraction.Ignore);
            foreach (Collider col in hits)
            {
                // 自分自身は対象外。
                if (col.transform == transform || col.transform.IsChildOf(transform))
                {
                    continue;
                }

                Targetable target = col.GetComponentInParent<Targetable>();
                if (target == null || !target.isActiveAndEnabled || target.IsDead || hitTargets.Contains(target))
                {
                    continue;
                }

                if (target.transform == transform || target.transform.IsChildOf(transform))
                {
                    continue;
                }

                hitTargets.Add(target);

                HealthController health = target.Health != null ? target.Health : target.GetComponent<HealthController>();
                if (health == null || health.IsDead)
                {
                    continue;
                }

                float actual = health.TakeDamage(damage, transform);
                if (actual <= 0f)
                {
                    continue;
                }

                hitCount++;
                target.PlayHitFlash();
                CombatTextManager.ShowDamageDealt(target.transform.position, actual);

                // 実際に与えたダメージ(実ダメージ)×分類ごとの回復率で回復量を合算する。
                healAmount += actual * GetHealPercent(target.Classification) / 100f;
            }
        }

        if (healAmount > 0f && _health != null && !_health.IsDead)
        {
            // Healは最大HPを超えない実回復量を返すため、満タン時は0が返り表示も行われない。
            float actualHeal = _health.Heal(healAmount);
            if (actualHeal > 0f)
            {
                CombatTextManager.ShowHeal(transform.position, actualHeal);
            }
        }

        ShowExplosionFlash();
        Debug.Log($"Volbraak W: 爆発しました(命中{hitCount}体)。", this);
    }

    private float GetHealPercent(TargetClassification targetClassification)
    {
        switch (targetClassification)
        {
            case TargetClassification.Character:
                return _characterHealPercent;
            case TargetClassification.Minion:
                return _minionHealPercent;
            case TargetClassification.Tower:
                return _towerHealPercent;
            case TargetClassification.TrainingDummy:
                return _trainingDummyHealPercent;
            default:
                return 0f;
        }
    }

    private void HandleDied()
    {
        // 死亡時はシールド・爆発を中止する(爆発しない)。クールダウンは維持する。
        if (_isWActive)
        {
            _isWActive = false;
            _currentShield = 0f;
            if (_shieldRing != null)
            {
                _shieldRing.enabled = false;
            }

            Debug.Log("Volbraak W: 死亡によりシールドを解除しました(爆発は発生しません)。", this);
        }
    }

    private void ShowExplosionFlash()
    {
        if (_explosionRing == null)
        {
            return;
        }

        if (_flashCoroutine != null)
        {
            StopCoroutine(_flashCoroutine);
        }

        _flashCoroutine = StartCoroutine(ExplosionFlashLoop());
    }

    private IEnumerator ExplosionFlashLoop()
    {
        _explosionRing.enabled = true;
        yield return new WaitForSeconds(_explosionFlashDuration);
        _explosionRing.enabled = false;
        _flashCoroutine = null;
    }

    // 表示用リングを実行時生成する(子オブジェクトのローカル座標描画のため、Playerの移動へ自動追従する)。
    private LineRenderer CreateRing(string ringName, float radius, Color color, out Material material)
    {
        GameObject ringObject = new GameObject(ringName);
        ringObject.transform.SetParent(transform, false);
        ringObject.transform.localPosition = new Vector3(0f, _ringLocalHeight, 0f);

        LineRenderer ring = ringObject.AddComponent<LineRenderer>();

        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        material = new Material(shader);
        material.color = color;

        ring.useWorldSpace = false;
        ring.loop = true;
        ring.positionCount = RingSegmentCount;
        ring.material = material;
        ring.startColor = color;
        ring.endColor = color;
        ring.startWidth = _ringWidth;
        ring.endWidth = _ringWidth;
        ring.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        ring.receiveShadows = false;

        for (int i = 0; i < RingSegmentCount; i++)
        {
            float angle = i * Mathf.PI * 2f / RingSegmentCount;
            ring.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius));
        }

        ring.enabled = false;
        return ring;
    }
}
