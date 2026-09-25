using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class LieselotteTests
{
    private const string DataPath = "Assets/Game/Data/Characters/LieselotteData.asset";
    private const string SkillPath = "Assets/Game/Data/Characters/LieselotteSkillData.asset";
    private const string PrefabPath = "Assets/Game/Prefabs/Characters/PF_Player_Lieselotte.prefab";

    [Test]
    public void CharacterDataAndPrefab_UseDedicatedPrototypeAssets()
    {
        CharacterData data = AssetDatabase.LoadAssetAtPath<CharacterData>(DataPath);
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        Assert.That(data, Is.Not.Null);
        Assert.That(data.CharacterId, Is.EqualTo("Lieselotte"));
        Assert.That(data.IsAvailable, Is.True);
        Assert.That(data.BaseHp, Is.EqualTo(575f));
        Assert.That(data.BaseAttackRange, Is.EqualTo(500f));
        Assert.That(data.PlayerPrefab, Is.EqualTo(prefab));
        Assert.That(prefab.GetComponent<LieselotteSkillController>(), Is.Not.Null);
        SerializedObject controller = new SerializedObject(prefab.GetComponent<LieselotteSkillController>());
        Assert.That(controller.FindProperty("_skillData").objectReferenceValue,
            Is.EqualTo(AssetDatabase.LoadAssetAtPath<LieselotteSkillData>(SkillPath)));
        Assert.That(AssetDatabase.GetAssetPath(prefab.GetComponentInChildren<MeshRenderer>().sharedMaterial),
            Is.EqualTo("Assets/Game/Art/Materials/M_Lieselotte_Placeholder.mat"));
        string scene = File.ReadAllText(Path.Combine(Application.dataPath,
            "Game/Scenes/SC_CharacterSelect.unity"));
        Assert.That(scene, Does.Contain("guid: " + AssetDatabase.AssetPathToGUID(DataPath)));
    }

    [Test]
    public void RPrototypeValues_AreTenPercentForCombatStatsAndEightPercentForMove()
    {
        LieselotteSkillData data = AssetDatabase.LoadAssetAtPath<LieselotteSkillData>(SkillPath);
        Assert.That(data, Is.Not.Null);
        Assert.That(data.RDuration, Is.EqualTo(5f));
        Assert.That(data.RStatStealPercent, Is.EqualTo(10f));
        Assert.That(data.RMoveSpeedStealPercent, Is.EqualTo(8f));
    }

    [Test]
    public void PrototypeTrainingDummy_HasTransferableStatsWithoutChangingHealthOrArmor()
    {
        Scene scene = EditorSceneManager.OpenScene("Assets/Game/Scenes/SC_Prototype.unity",
            OpenSceneMode.Additive);
        try
        {
            GameObject dummy = null;
            foreach (GameObject root in scene.GetRootGameObjects())
                if (root.name == "TrainingDummy") dummy = root;
            Assert.That(dummy, Is.Not.Null);
            CharacterStats stats = dummy.GetComponent<CharacterStats>();
            Assert.That(stats, Is.Not.Null);
            Assert.That(stats.CurrentMaxHealth, Is.EqualTo(300f));
            Assert.That(stats.CurrentArmor, Is.EqualTo(0f));
            Assert.That(stats.CurrentAttackDamage, Is.GreaterThan(0f));
            Assert.That(dummy.GetComponent<Targetable>().IsTrainingDummyPlayerProxy, Is.True);
        }
        finally { EditorSceneManager.CloseScene(scene, true); }
    }

    [Test]
    public void RSteal_TransfersStatsAndPreservesHealthFractionsThenRestores()
    {
        GameObject owner = new GameObject("Lieselotte test owner");
        GameObject victim = new GameObject("Lieselotte test target");
        try
        {
            CharacterStats ownerStats = owner.AddComponent<CharacterStats>();
            CharacterStats targetStats = victim.AddComponent<CharacterStats>();
            ownerStats.SetCharacterData(AssetDatabase.LoadAssetAtPath<CharacterData>(DataPath));
            targetStats.SetCharacterData(AssetDatabase.LoadAssetAtPath<CharacterData>(
                "Assets/Game/Data/Characters/ZelfData.asset"));
            HealthController ownerHealth = owner.AddComponent<HealthController>();
            HealthController targetHealth = victim.AddComponent<HealthController>();
            EnsureAwake(ownerHealth);
            EnsureAwake(targetHealth);
            ownerHealth.PreserveHealthRatio(0.4f);
            targetHealth.PreserveHealthRatio(0.6f);
            LieselotteStatSteal steal = owner.AddComponent<LieselotteStatSteal>();
            EnsureAwake(steal);
            float ownerMax = ownerStats.CurrentMaxHealth;
            float targetMax = targetStats.CurrentMaxHealth;
            float ownerAd = ownerStats.CurrentAttackDamage;
            float targetAd = targetStats.CurrentAttackDamage;
            float ownerAs = ownerStats.CurrentAttackSpeed;
            float targetAs = targetStats.CurrentAttackSpeed;
            float ownerArmor = ownerStats.CurrentArmor;
            float targetArmor = targetStats.CurrentArmor;
            float ownerMs = ownerStats.CurrentMoveSpeed;
            float targetMs = targetStats.CurrentMoveSpeed;

            Assert.That(steal.Apply(targetStats, targetHealth, 10f, 8f, 5f), Is.True);
            Assert.That(steal.IsActive, Is.True);
            Assert.That(ownerStats.CurrentMaxHealth, Is.EqualTo(ownerMax + targetMax * 0.1f).Within(0.001f));
            Assert.That(targetStats.CurrentMaxHealth, Is.EqualTo(targetMax * 0.9f).Within(0.001f));
            Assert.That(ownerStats.CurrentAttackDamage, Is.EqualTo(ownerAd + targetAd * 0.1f).Within(0.001f));
            Assert.That(targetStats.CurrentAttackDamage, Is.EqualTo(targetAd * 0.9f).Within(0.001f));
            Assert.That(ownerStats.CurrentAttackSpeed, Is.EqualTo(ownerAs + targetAs * 0.1f).Within(0.001f));
            Assert.That(targetStats.CurrentAttackSpeed, Is.EqualTo(targetAs * 0.9f).Within(0.001f));
            Assert.That(ownerStats.CurrentArmor, Is.EqualTo(ownerArmor + targetArmor * 0.1f).Within(0.001f));
            Assert.That(targetStats.CurrentArmor, Is.EqualTo(targetArmor * 0.9f).Within(0.001f));
            Assert.That(ownerStats.CurrentMoveSpeed, Is.EqualTo(ownerMs + targetStats.BaseMoveSpeed * 0.08f).Within(0.001f));
            Assert.That(targetStats.CurrentMoveSpeed, Is.EqualTo(targetMs - targetStats.BaseMoveSpeed * 0.08f).Within(0.001f));
            Assert.That(ownerHealth.CurrentHealth / ownerHealth.MaxHealth, Is.EqualTo(0.4f).Within(0.001f));
            Assert.That(targetHealth.CurrentHealth / targetHealth.MaxHealth, Is.EqualTo(0.6f).Within(0.001f));

            steal.End();
            Assert.That(steal.IsActive, Is.False);
            Assert.That(ownerStats.CurrentMaxHealth, Is.EqualTo(ownerMax).Within(0.001f));
            Assert.That(targetStats.CurrentMaxHealth, Is.EqualTo(targetMax).Within(0.001f));
            Assert.That(ownerStats.CurrentAttackDamage, Is.EqualTo(ownerAd).Within(0.001f));
            Assert.That(targetStats.CurrentAttackDamage, Is.EqualTo(targetAd).Within(0.001f));
            Assert.That(ownerHealth.CurrentHealth / ownerHealth.MaxHealth, Is.EqualTo(0.4f).Within(0.001f));
            Assert.That(targetHealth.CurrentHealth / targetHealth.MaxHealth, Is.EqualTo(0.6f).Within(0.001f));
        }
        finally
        {
            Object.DestroyImmediate(owner);
            Object.DestroyImmediate(victim);
        }
    }

    [Test]
    public void RSteal_ReapplicationDoesNotStack()
    {
        GameObject owner = new GameObject("owner");
        GameObject victim = new GameObject("victim");
        try
        {
            CharacterStats ownerStats = owner.AddComponent<CharacterStats>();
            CharacterStats targetStats = victim.AddComponent<CharacterStats>();
            owner.AddComponent<HealthController>();
            HealthController targetHealth = victim.AddComponent<HealthController>();
            LieselotteStatSteal steal = owner.AddComponent<LieselotteStatSteal>();
            EnsureAwake(steal);
            float original = ownerStats.CurrentAttackDamage;
            Assert.That(steal.Apply(targetStats, targetHealth, 10f, 8f, 5f), Is.True);
            float once = ownerStats.CurrentAttackDamage;
            Assert.That(steal.Apply(targetStats, targetHealth, 10f, 8f, 5f), Is.True);
            Assert.That(ownerStats.CurrentAttackDamage, Is.EqualTo(once).Within(0.001f));
            steal.End();
            Assert.That(ownerStats.CurrentAttackDamage, Is.EqualTo(original).Within(0.001f));
        }
        finally
        {
            Object.DestroyImmediate(owner);
            Object.DestroyImmediate(victim);
        }
    }

    [Test]
    public void RSteal_DisableRestoresBonuses()
    {
        GameObject owner = new GameObject("owner");
        GameObject victim = new GameObject("victim");
        try
        {
            CharacterStats ownerStats = owner.AddComponent<CharacterStats>();
            CharacterStats targetStats = victim.AddComponent<CharacterStats>();
            owner.AddComponent<HealthController>();
            HealthController targetHealth = victim.AddComponent<HealthController>();
            LieselotteStatSteal steal = owner.AddComponent<LieselotteStatSteal>();
            EnsureAwake(steal);
            float original = ownerStats.CurrentAttackDamage;
            steal.Apply(targetStats, targetHealth, 10f, 8f, 5f);
            steal.enabled = false;
            steal.GetType().GetMethod("OnDisable", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.Invoke(steal, null);
            Assert.That(steal.IsActive, Is.False);
            Assert.That(ownerStats.CurrentAttackDamage, Is.EqualTo(original).Within(0.001f));
        }
        finally
        {
            Object.DestroyImmediate(owner);
            Object.DestroyImmediate(victim);
        }
    }

    [Test]
    public void RSteal_TargetDeathRestoresBothStatBonuses()
    {
        GameObject owner = new GameObject("owner");
        GameObject victim = new GameObject("victim");
        try
        {
            CharacterStats ownerStats = owner.AddComponent<CharacterStats>();
            CharacterStats targetStats = victim.AddComponent<CharacterStats>();
            owner.AddComponent<HealthController>();
            HealthController targetHealth = victim.AddComponent<HealthController>();
            LieselotteStatSteal steal = owner.AddComponent<LieselotteStatSteal>();
            EnsureAwake(steal);
            float ownerAd = ownerStats.CurrentAttackDamage;
            float targetAd = targetStats.CurrentAttackDamage;
            Assert.That(steal.Apply(targetStats, targetHealth, 10f, 8f, 5f), Is.True);
            targetHealth.TakeDamage(10000f);
            Assert.That(steal.IsActive, Is.False);
            Assert.That(ownerStats.CurrentAttackDamage, Is.EqualTo(ownerAd).Within(0.001f));
            Assert.That(targetStats.CurrentAttackDamage, Is.EqualTo(targetAd).Within(0.001f));
        }
        finally
        {
            Object.DestroyImmediate(owner);
            Object.DestroyImmediate(victim);
        }
    }

    [Test]
    public void RSteal_ExpiryRestoresBonuses()
    {
        GameObject owner = new GameObject("owner");
        GameObject victim = new GameObject("victim");
        try
        {
            CharacterStats ownerStats = owner.AddComponent<CharacterStats>();
            CharacterStats targetStats = victim.AddComponent<CharacterStats>();
            owner.AddComponent<HealthController>();
            HealthController targetHealth = victim.AddComponent<HealthController>();
            LieselotteStatSteal steal = owner.AddComponent<LieselotteStatSteal>();
            EnsureAwake(steal);
            float original = ownerStats.CurrentAttackDamage;
            steal.Apply(targetStats, targetHealth, 10f, 8f, 5f);
            SetField(steal, "_endTime", Time.time - 1f);
            steal.GetType().GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.Invoke(steal, null);
            Assert.That(steal.IsActive, Is.False);
            Assert.That(ownerStats.CurrentAttackDamage, Is.EqualTo(original).Within(0.001f));
        }
        finally
        {
            Object.DestroyImmediate(owner);
            Object.DestroyImmediate(victim);
        }
    }

    [Test]
    public void RSteal_TwoCastersCannotStackOnOneTarget()
    {
        GameObject first = new GameObject("first caster");
        GameObject second = new GameObject("second caster");
        GameObject victim = new GameObject("victim");
        try
        {
            CharacterStats firstStats = first.AddComponent<CharacterStats>();
            CharacterStats secondStats = second.AddComponent<CharacterStats>();
            CharacterStats targetStats = victim.AddComponent<CharacterStats>();
            first.AddComponent<HealthController>();
            second.AddComponent<HealthController>();
            HealthController targetHealth = victim.AddComponent<HealthController>();
            LieselotteStatSteal firstSteal = first.AddComponent<LieselotteStatSteal>();
            LieselotteStatSteal secondSteal = second.AddComponent<LieselotteStatSteal>();
            EnsureAwake(firstSteal);
            EnsureAwake(secondSteal);
            float targetAd = targetStats.CurrentAttackDamage;
            float firstAd = firstStats.CurrentAttackDamage;
            float secondAd = secondStats.CurrentAttackDamage;
            Assert.That(firstSteal.Apply(targetStats, targetHealth, 10f, 8f, 5f), Is.True);
            Assert.That(secondSteal.Apply(targetStats, targetHealth, 10f, 8f, 5f), Is.True);
            Assert.That(firstSteal.IsActive, Is.False);
            Assert.That(secondSteal.IsActive, Is.True);
            Assert.That(firstStats.CurrentAttackDamage, Is.EqualTo(firstAd).Within(0.001f));
            Assert.That(secondStats.CurrentAttackDamage, Is.EqualTo(secondAd + targetAd * 0.1f).Within(0.001f));
            Assert.That(targetStats.CurrentAttackDamage, Is.EqualTo(targetAd * 0.9f).Within(0.001f));
        }
        finally
        {
            Object.DestroyImmediate(first);
            Object.DestroyImmediate(second);
            Object.DestroyImmediate(victim);
        }
    }

    [Test]
    public void Passive_ThirdSuccessfulHeroAttackTriggersDamageAndWThenResetsStacks()
    {
        GameObject owner = new GameObject("Lieselotte");
        GameObject victim = new GameObject("Enemy character");
        try
        {
            CharacterStats stats = owner.AddComponent<CharacterStats>();
            stats.SetCharacterData(AssetDatabase.LoadAssetAtPath<CharacterData>(DataPath));
            LieselotteSkillController skills = owner.AddComponent<LieselotteSkillController>();
            LieselotteSkillVisuals visuals = owner.GetComponent<LieselotteSkillVisuals>();
            if (visuals == null) visuals = owner.AddComponent<LieselotteSkillVisuals>();
            SetField(skills, "_stats", stats);
            SetField(skills, "_skillData", AssetDatabase.LoadAssetAtPath<LieselotteSkillData>(SkillPath));
            SetField(skills, "_visuals", visuals);
            victim.AddComponent<HealthController>();
            Targetable target = victim.AddComponent<Targetable>();
            Assert.That(skills.PassiveBonusFor(target), Is.Zero);
            skills.NotifyBasicAttackHit(target);
            Assert.That(skills.PassiveStacks, Is.EqualTo(1));
            skills.NotifyBasicAttackHit(target);
            Assert.That(skills.PassiveStacks, Is.EqualTo(2));
            Assert.That(skills.PassiveBonusFor(target),
                Is.EqualTo(20f + stats.CurrentAttackDamage * 0.4f));
            skills.NotifyBasicAttackHit(target);
            Assert.That(skills.PassiveStacks, Is.Zero);
            Assert.That(skills.IsWActive, Is.True);
            Assert.That(skills.PassiveBonusFor(target), Is.Zero);
        }
        finally
        {
            Object.DestroyImmediate(owner);
            Object.DestroyImmediate(victim);
        }
    }

    [Test]
    public void Passive_MinionDoesNotAccumulateStacks()
    {
        GameObject owner = new GameObject("Lieselotte");
        GameObject victim = new GameObject("Minion");
        try
        {
            CharacterStats stats = owner.AddComponent<CharacterStats>();
            LieselotteSkillController skills = owner.AddComponent<LieselotteSkillController>();
            SetField(skills, "_stats", stats);
            SetField(skills, "_skillData", AssetDatabase.LoadAssetAtPath<LieselotteSkillData>(SkillPath));
            victim.AddComponent<HealthController>();
            Targetable target = victim.AddComponent<Targetable>();
            SetField(target, "_classification", TargetClassification.Minion);
            for (int i = 0; i < 3; i++) skills.NotifyBasicAttackHit(target);
            Assert.That(skills.PassiveStacks, Is.Zero);
            Assert.That(skills.PassiveBonusFor(target), Is.Zero);
        }
        finally
        {
            Object.DestroyImmediate(owner);
            Object.DestroyImmediate(victim);
        }
    }

    [Test]
    public void Q_SelectedTargetApproachesThenDealsDamageInsideRange()
    {
        GameObject owner = new GameObject("Lieselotte Q owner");
        GameObject victim = new GameObject("Lieselotte Q victim");
        try
        {
            owner.AddComponent<CharacterStats>().SetCharacterData(
                AssetDatabase.LoadAssetAtPath<CharacterData>(DataPath));
            owner.AddComponent<HealthController>();
            owner.AddComponent<TeamMember>().SetTeam(Team.Blue);
            PlayerTargetSelector selector = owner.AddComponent<PlayerTargetSelector>();
            LieselotteSkillController skills = owner.AddComponent<LieselotteSkillController>();
            SetField(skills, "_skillData", AssetDatabase.LoadAssetAtPath<LieselotteSkillData>(SkillPath));
            EnsureAwake(skills);

            victim.transform.position = new Vector3(4f, 0f, 0f);
            victim.AddComponent<SphereCollider>().radius = 0.5f;
            HealthController health = victim.AddComponent<HealthController>();
            victim.AddComponent<TeamMember>().SetTeam(Team.Red);
            Targetable target = victim.AddComponent<Targetable>();
            EnsureAwake(target);
            SetField(selector, "_currentTarget", target);

            typeof(LieselotteSkillController).GetMethod("CastQ",
                BindingFlags.Instance | BindingFlags.NonPublic).Invoke(skills, null);
            SkillApproachController approach = owner.GetComponent<SkillApproachController>();
            Assert.That(approach, Is.Not.Null);
            Assert.That(approach.IsApproaching, Is.True);
            float before = health.CurrentHealth;

            owner.transform.position = new Vector3(2.1f, 0f, 0f);
            typeof(SkillApproachController).GetMethod("Update",
                BindingFlags.Instance | BindingFlags.NonPublic).Invoke(approach, null);
            Assert.That(approach.IsApproaching, Is.False);
            Assert.That(health.CurrentHealth, Is.LessThan(before));
            Assert.That(skills.QRemainingCooldown, Is.GreaterThan(0f));
        }
        finally
        {
            Object.DestroyImmediate(owner);
            Object.DestroyImmediate(victim);
        }
    }

    [Test]
    public void PlaceholderVisuals_ShowAndClearWPoolAndStealWithoutSharedMaterial()
    {
        GameObject owner = new GameObject("Lieselotte visuals");
        GameObject victim = new GameObject("Target");
        try
        {
            LieselotteSkillVisuals visuals = owner.AddComponent<LieselotteSkillVisuals>();
            visuals.SetWActive(2f);
            visuals.SetStealActive(true);
            visuals.ShowQ(victim.transform);
            visuals.ShowBloodPool(Vector3.zero, 0.65f, 3f);
            visuals.ShowRDash(Vector3.zero, Vector3.forward);
            Assert.That(visuals.IsWVisible, Is.True);
            Assert.That(visuals.IsStealVisible, Is.True);
            Assert.That(victim.transform.Find("Lieselotte Q bite"), Is.Not.Null);
            visuals.HideAll();
            Assert.That(visuals.IsWVisible || visuals.IsStealVisible, Is.False);
            Assert.That(victim.transform.Find("Lieselotte Q bite"), Is.Null);
        }
        finally
        {
            Object.DestroyImmediate(owner);
            Object.DestroyImmediate(victim);
        }
    }

    private static void EnsureAwake(object component)
    {
        component.GetType().GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)
            ?.Invoke(component, null);
    }

    private static void SetField(object component, string field, object value)
    {
        component.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)
            ?.SetValue(component, value);
    }
}
