using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// ゼルフQの対象ブリンク、ダメージ、同一対象ロック、クールダウンを管理する。
/// Input System Package の Keyboard.current を使用し、旧 Input Manager は使用しない。
/// </summary>
[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(CharacterStats))]
[RequireComponent(typeof(PlayerTargetSelector))]
public sealed class ZelfQController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private CharacterController _characterController;
    [SerializeField] private CharacterStats _characterStats;
    [SerializeField] private PlayerTargetSelector _targetSelector;
    [SerializeField] private HealthController _healthController;

    [Header("Targeting")]
    [SerializeField, Min(0f)] private float _targetRange = 4.5f;
    [SerializeField, Min(0f)] private float _blinkStopDistance = 0.75f;
    [SerializeField] private LayerMask _groundLayer;
    [SerializeField] private LayerMask _targetableLayer;

    [Header("Damage")]
    [SerializeField, Min(0f)] private float _baseDamage = 30f;
    [SerializeField, Min(0f)] private float _adRatio = 0.6f;

    [Header("Cooldown")]
    [SerializeField, Min(0f)] private float _cooldown = 6f;
    [SerializeField, Min(0f)] private float _sameTargetLockout = 1.25f;
    [SerializeField, Range(0f, 1f)] private float _minionCooldownReductionPercent = 0.5f;

    [Header("Debug (Runtime)")]
    [SerializeField] private bool _logCastResults;
    [SerializeField] private bool _isQAvailable = true;
    [SerializeField] private float _remainingCooldown;
    [SerializeField] private bool _isCurrentTargetLocked;

    private readonly Dictionary<Targetable, float> _targetLockExpiryTimes = new Dictionary<Targetable, float>();
    private float _cooldownEndTime;

    public bool IsQAvailable => _isQAvailable;
    public float RemainingCooldown => _remainingCooldown;
    public bool IsCurrentTargetLocked => _isCurrentTargetLocked;

    private void Awake()
    {
        if (_characterController == null) _characterController = GetComponent<CharacterController>();
        if (_characterStats == null) _characterStats = GetComponent<CharacterStats>();
        if (_targetSelector == null) _targetSelector = GetComponent<PlayerTargetSelector>();
        if (_healthController == null) _healthController = GetComponent<HealthController>();
    }

    private void Update()
    {
        UpdateRuntimeState();

        if (Keyboard.current != null && Keyboard.current.qKey.wasPressedThisFrame)
        {
            TryCast();
        }
    }

    private void UpdateRuntimeState()
    {
        _remainingCooldown = Mathf.Max(0f, _cooldownEndTime - Time.time);
        _isQAvailable = _remainingCooldown <= 0f;

        Targetable currentTarget = _targetSelector != null ? _targetSelector.CurrentTarget : null;
        _isCurrentTargetLocked = IsTargetLocked(currentTarget);

        if (_targetLockExpiryTimes.Count == 0) return;

        List<Targetable> expiredTargets = null;
        foreach (KeyValuePair<Targetable, float> pair in _targetLockExpiryTimes)
        {
            if (pair.Key == null || !pair.Key.isActiveAndEnabled || pair.Key.IsDead || Time.time >= pair.Value)
            {
                if (expiredTargets == null) expiredTargets = new List<Targetable>();
                expiredTargets.Add(pair.Key);
            }
        }

        if (expiredTargets == null) return;
        foreach (Targetable target in expiredTargets) _targetLockExpiryTimes.Remove(target);
    }

    private void TryCast()
    {
        if (!_isQAvailable)
        {
            Log("Zelf Q: クールダウン中です。");
            return;
        }

        Targetable target = _targetSelector != null ? _targetSelector.CurrentTarget : null;
        if (!IsValidCastTarget(target))
        {
            Log("Zelf Q: 有効なターゲットが選択されていません。");
            return;
        }

        if (target.Classification == TargetClassification.Tower)
        {
            Log("Zelf Q: Tower分類の対象には発動できません。");
            return;
        }

        if (IsTargetLocked(target))
        {
            Log("Zelf Q: この対象は同一対象ロック中です。");
            return;
        }

        if (!IsInTargetRange(target))
        {
            Log("Zelf Q: ターゲットが射程外です。");
            return;
        }

        BlinkToTarget(target);
        float actualDamage = ApplyDamage(target);
        AddTargetLock(target);
        StartCooldown();
        ApplyHitCooldownResult(target.Classification);

        Log($"Zelf Q: 発動成功。実ダメージ {actualDamage:0.##}。残りクールダウン {RemainingCooldown:0.##} 秒。");
    }

    private bool IsValidCastTarget(Targetable target)
    {
        return target != null
            && target.isActiveAndEnabled
            && !target.IsDead
            && target.Health != null
            && !target.Health.IsDead;
    }

    private bool IsInTargetRange(Targetable target)
    {
        Vector3 nearestPoint = target.GetClosestPoint(transform.position);
        nearestPoint.y = transform.position.y;
        Vector3 horizontalOffset = nearestPoint - transform.position;
        horizontalOffset.y = 0f;
        return horizontalOffset.sqrMagnitude <= _targetRange * _targetRange;
    }

    private void BlinkToTarget(Targetable target)
    {
        Vector3 nearestPoint = target.GetClosestPoint(transform.position);
        Vector3 awayFromTarget = transform.position - nearestPoint;
        awayFromTarget.y = 0f;

        if (awayFromTarget.sqrMagnitude < 0.0001f)
        {
            awayFromTarget = transform.forward;
            awayFromTarget.y = 0f;
        }

        awayFromTarget.Normalize();
        Vector3 destination = nearestPoint + awayFromTarget * _blinkStopDistance;
        destination.y = GetGroundedY(destination, transform.position.y);

        // CharacterControllerが有効なままTransformを変更するとUnityの警告を出すため、
        // 一時的に無効化してから安全に移動する。
        bool wasEnabled = _characterController != null && _characterController.enabled;
        if (wasEnabled) _characterController.enabled = false;
        transform.position = destination;
        if (wasEnabled) _characterController.enabled = true;
    }

    private float GetGroundedY(Vector3 position, float fallbackY)
    {
        if (_groundLayer.value == 0) return fallbackY;

        const float rayStartHeight = 10f;
        const float rayLength = 30f;
        Vector3 rayStart = new Vector3(position.x, fallbackY + rayStartHeight, position.z);
        if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, rayLength, _groundLayer, QueryTriggerInteraction.Ignore))
        {
            return hit.point.y;
        }

        return fallbackY;
    }

    private float ApplyDamage(Targetable target)
    {
        float damage = _baseDamage + _characterStats.CurrentAttackDamage * _adRatio;
        float actualDamage = target.Health.TakeDamage(damage);
        if (actualDamage <= 0f) return 0f;

        target.PlayHitFlash();
        NotifyExistingCombatSystems(actualDamage, target);
        return actualDamage;
    }

    // 既存のダメージ表示とゼルフPをQでも共通利用するため、実装側の公開メソッドを呼び出す。
    // 互換性のため候補名を順番に解決し、対象メソッドが存在しない場合もQ本体は安全に動作する。
    private void NotifyExistingCombatSystems(float actualDamage, Targetable target)
    {
        foreach (MonoBehaviour component in GetComponents<MonoBehaviour>())
        {
            if (component == null) continue;
            if (component.GetType().Name == "ZelfPassiveHeal")
            {
                InvokeDamageReceiver(component, actualDamage, target);
            }
        }

        Type combatTextManagerType = FindType("CombatTextManager");
        if (combatTextManagerType == null) return;

        InvokeCombatText(combatTextManagerType, "ShowDamageDealt", actualDamage, target.transform);
        InvokeCombatText(combatTextManagerType, "ShowDamageTaken", actualDamage, target.transform);
    }

    private static void InvokeDamageReceiver(MonoBehaviour receiver, float damage, Targetable target)
    {
        string[] candidateNames = { "HandleDamageDealt", "OnDamageDealt", "NotifyDamageDealt" };
        foreach (string methodName in candidateNames)
        {
            MethodInfo method = receiver.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (method == null) continue;

            ParameterInfo[] parameters = method.GetParameters();
            if (parameters.Length == 2 && parameters[0].ParameterType == typeof(float) && parameters[1].ParameterType == typeof(TargetClassification))
            {
                method.Invoke(receiver, new object[] { damage, target.Classification });
                return;
            }
        }
    }

    private static void InvokeCombatText(Type managerType, string methodName, float damage, Transform targetTransform)
    {
        MethodInfo[] methods = managerType.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        foreach (MethodInfo method in methods)
        {
            if (method.Name != methodName) continue;
            ParameterInfo[] parameters = method.GetParameters();
            object[] arguments = BuildCombatTextArguments(parameters, damage, targetTransform);
            if (arguments == null) continue;
            method.Invoke(null, arguments);
            return;
        }
    }

    private static object[] BuildCombatTextArguments(ParameterInfo[] parameters, float damage, Transform targetTransform)
    {
        object[] result = new object[parameters.Length];
        for (int index = 0; index < parameters.Length; index++)
        {
            Type parameterType = parameters[index].ParameterType;
            if (parameterType == typeof(float)) result[index] = damage;
            else if (parameterType == typeof(int)) result[index] = Mathf.RoundToInt(damage);
            else if (parameterType == typeof(Transform)) result[index] = targetTransform;
            else if (parameterType == typeof(Vector3)) result[index] = targetTransform.position;
            else if (parameterType == typeof(GameObject)) result[index] = targetTransform.gameObject;
            else return null;
        }
        return result;
    }

    private static Type FindType(string typeName)
    {
        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type type = assembly.GetType(typeName);
            if (type != null) return type;
        }
        return null;
    }

    private void AddTargetLock(Targetable target)
    {
        _targetLockExpiryTimes[target] = Time.time + _sameTargetLockout;
    }

    private bool IsTargetLocked(Targetable target)
    {
        return target != null
            && _targetLockExpiryTimes.TryGetValue(target, out float expiresAt)
            && Time.time < expiresAt;
    }

    private void StartCooldown()
    {
        _cooldownEndTime = Time.time + _cooldown;
        _remainingCooldown = _cooldown;
        _isQAvailable = _cooldown <= 0f;
    }

    private void ApplyHitCooldownResult(TargetClassification classification)
    {
        if (classification == TargetClassification.Character || classification == TargetClassification.TrainingDummy)
        {
            _cooldownEndTime = Time.time;
        }
        else if (classification == TargetClassification.Minion)
        {
            float remaining = Mathf.Max(0f, _cooldownEndTime - Time.time);
            _cooldownEndTime = Time.time + remaining * (1f - _minionCooldownReductionPercent);
        }

        UpdateRuntimeState();
    }

    private void Log(string message)
    {
        if (_logCastResults) Debug.Log(message, this);
    }
}
