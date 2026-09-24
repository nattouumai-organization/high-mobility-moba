using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class RinesAssetTests
{
    private const string CharacterPath = "Assets/Game/Data/Characters/RinesData.asset";
    private const string SkillsPath = "Assets/Game/Data/Characters/RinesSkillData.asset";
    private const string PrefabPath = "Assets/Game/Prefabs/Characters/PF_Player_Rines.prefab";

    [Test]
    public void CharacterData_WiresDedicatedPrefabAndPrototypeStats()
    {
        CharacterData data = AssetDatabase.LoadAssetAtPath<CharacterData>(CharacterPath);
        Assert.That(data, Is.Not.Null);
        Assert.That(data.CharacterId, Is.EqualTo("Rines"));
        Assert.That(data.DisplayName, Is.EqualTo("リネス"));
        Assert.That(data.IsAvailable, Is.True);
        Assert.That(data.BaseHp, Is.EqualTo(560f));
        Assert.That(data.BaseAttackDamage, Is.EqualTo(48f));
        Assert.That(data.BaseAttackRange, Is.EqualTo(450f));
        Assert.That(data.PlayerPrefab, Is.EqualTo(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath)));
    }

    [Test]
    public void Prefab_UsesDedicatedSkillsAndPlaceholderMaterial()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        Assert.That(prefab, Is.Not.Null);
        RinesSkillController controller = prefab.GetComponent<RinesSkillController>();
        Assert.That(controller, Is.Not.Null);
        var serialized = new SerializedObject(controller);
        Assert.That(serialized.FindProperty("_skillData").objectReferenceValue,
            Is.EqualTo(AssetDatabase.LoadAssetAtPath<RinesSkillData>(SkillsPath)));
        Assert.That(AssetDatabase.GetAssetPath(prefab.GetComponent<Renderer>().sharedMaterial),
            Is.EqualTo("Assets/Game/Art/Materials/M_Rines_Placeholder.mat"));
    }

    [Test]
    public void CharacterSelection_ReferencesRinesData()
    {
        string guid = AssetDatabase.AssetPathToGUID(CharacterPath);
        Assert.That(guid, Is.Not.Empty);
        string scene = File.ReadAllText(Path.Combine(Application.dataPath, "Game/Scenes/SC_CharacterSelect.unity"));
        Assert.That(scene, Does.Contain("guid: " + guid));
    }
}
