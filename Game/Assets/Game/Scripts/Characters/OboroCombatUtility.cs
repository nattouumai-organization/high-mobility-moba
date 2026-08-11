using UnityEngine;

/// <summary>
/// 朧の各スキルで共通利用する、敵味方・対象・LayerMask・安全な瞬間移動の補助処理。
/// 既存のPlayerLayerMaskFallbackと同じくGroundLayer=6、TargetableLayer=7を最終フォールバックにする。
/// </summary>
public static class OboroCombatUtility
{
    private static GameManager _gameManager;

    public static bool IsMatchEnded
    {
        get
        {
            if (_gameManager == null)
            {
                _gameManager = Object.FindFirstObjectByType<GameManager>();
            }

            return _gameManager != null && _gameManager.IsMatchEnded;
        }
    }

    public static LayerMask ResolveGroundLayer(LayerMask configured)
    {
        return configured.value != 0 ? configured : ResolveLayer("GroundLayer", 6);
    }

    public static LayerMask ResolveTargetableLayer(LayerMask configured)
    {
        return configured.value != 0 ? configured : ResolveLayer("TargetableLayer", 7);
    }

    private static LayerMask ResolveLayer(string layerName, int fallbackLayer)
    {
        int layer = LayerMask.NameToLayer(layerName);
        if (layer < 0) layer = Mathf.Clamp(fallbackLayer, 0, 31);
        return 1 << layer;
    }

    public static HealthController GetHealth(Targetable target)
    {
        return target == null ? null : target.Health != null ? target.Health : target.GetComponent<HealthController>();
    }

    public static bool IsAlive(Targetable target)
    {
        if (target == null || !target.isActiveAndEnabled || target.IsDead) return false;
        HealthController health = GetHealth(target);
        return health != null && !health.IsDead;
    }

    public static bool IsOwner(Transform owner, Targetable target)
    {
        if (owner == null || target == null) return false;
        Transform targetTransform = target.transform;
        return targetTransform == owner || targetTransform.IsChildOf(owner) || owner.IsChildOf(targetTransform);
    }

    /// <summary>
    /// 敵かどうかを判定する。TeamMemberが両方にある場合はチーム差を必須とする。
    /// TeamMemberを持たないCharacter分類は、TrainingDummyを敵チャンピオンの代用にするローカル検証経路として許可する。
    /// </summary>
    public static bool IsEnemy(Transform owner, Targetable target, bool allowTrainingDummy = true)
    {
        if (!IsAlive(target) || IsOwner(owner, target)) return false;

        TeamMember ownerTeam = owner != null ? owner.GetComponentInParent<TeamMember>() : null;
        TeamMember targetTeam = target.GetComponentInParent<TeamMember>();
        if (ownerTeam != null && targetTeam != null)
        {
            return ownerTeam.Team != targetTeam.Team;
        }

        if (targetTeam == null)
        {
            if (target.Classification == TargetClassification.Character) return true;
            return allowTrainingDummy && target.Classification == TargetClassification.TrainingDummy;
        }

        return false;
    }

    /// <summary>
    /// E/R用の敵チャンピオン判定。通常のヒーローは敵TeamMemberを必須とし、
    /// TeamMemberのないテスト対象はClassificationをCharacterへ変更した場合だけ許可する。
    /// </summary>
    public static bool IsEnemyChampion(Transform owner, Targetable target)
    {
        if (!IsAlive(target) || target.Classification != TargetClassification.Character || IsOwner(owner, target))
        {
            return false;
        }

        TeamMember ownerTeam = owner != null ? owner.GetComponentInParent<TeamMember>() : null;
        TeamMember targetTeam = target.GetComponentInParent<TeamMember>();
        if (ownerTeam != null && targetTeam != null)
        {
            return ownerTeam.Team != targetTeam.Team;
        }

        return targetTeam == null;
    }

    /// <summary>敵ヒーロー専用判定。TrainingDummy・Minion・Towerは含めない。</summary>
    public static bool IsEnemyHero(Transform owner, Targetable target)
    {
        if (!IsAlive(target) || target.Classification != TargetClassification.Character || IsOwner(owner, target))
        {
            return false;
        }

        TeamMember ownerTeam = owner != null ? owner.GetComponentInParent<TeamMember>() : null;
        TeamMember targetTeam = target.GetComponentInParent<TeamMember>();
        if (ownerTeam == null || targetTeam == null) return false;
        return ownerTeam.Team != targetTeam.Team;
    }

    public static bool IsHeroOrTrainingDummy(Targetable target)
    {
        return target != null &&
               (target.Classification == TargetClassification.Character ||
                target.Classification == TargetClassification.TrainingDummy);
    }

    public static bool TryGetMouseGroundPoint(PlayerInputHub inputHub, ref Camera camera, LayerMask groundLayer, out Vector3 point)
    {
        point = Vector3.zero;
        if (inputHub == null || groundLayer.value == 0) return false;
        if (camera == null)
        {
            camera = Camera.main;
            if (camera == null) return false;
        }

        Ray ray = camera.ScreenPointToRay(inputHub.MousePosition);
        if (!Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, groundLayer, QueryTriggerInteraction.Ignore))
        {
            return false;
        }

        point = hit.point;
        return true;
    }

    public static bool TryGetMouseTarget(PlayerInputHub inputHub, ref Camera camera, LayerMask targetableLayer, out Targetable target)
    {
        target = null;
        if (inputHub == null || targetableLayer.value == 0) return false;
        if (camera == null)
        {
            camera = Camera.main;
            if (camera == null) return false;
        }

        Ray ray = camera.ScreenPointToRay(inputHub.MousePosition);
        if (!Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, targetableLayer, QueryTriggerInteraction.Ignore))
        {
            return false;
        }

        target = hit.collider.GetComponentInParent<Targetable>();
        return target != null;
    }

    public static Vector3 Flatten(Vector3 vector)
    {
        vector.y = 0f;
        return vector;
    }

    public static float GetGroundedY(Vector3 position, Transform actor, CharacterController controller, LayerMask groundLayer)
    {
        if (actor == null) return position.y;
        if (groundLayer.value != 0 &&
            Physics.Raycast(new Vector3(position.x, actor.position.y + 20f), Vector3.down,
                out RaycastHit hit, 50f, groundLayer, QueryTriggerInteraction.Ignore))
        {
            if (controller != null)
            {
                return hit.point.y + controller.height * 0.5f - controller.center.y + controller.skinWidth;
            }

            return hit.point.y;
        }

        return actor.position.y;
    }

    public static void Teleport(Transform actor, CharacterController controller, Vector3 destination, LayerMask groundLayer)
    {
        if (actor == null) return;
        destination.y = GetGroundedY(destination, actor, controller, groundLayer);
        bool wasEnabled = controller != null && controller.enabled;
        if (controller != null && wasEnabled) controller.enabled = false;
        actor.position = destination;
        if (controller != null && wasEnabled) controller.enabled = true;
    }

    public static Material CreateUnlitMaterial(Color color)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        Material material = new Material(shader);
        material.color = color;
        return material;
    }
}
