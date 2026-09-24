using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>One-time, idempotent Rines prototype asset setup. Run via -executeMethod.</summary>
public static class RinesAssetBuilder
{
    private const string CharacterPath = "Assets/Game/Data/Characters/RinesData.asset";
    private const string SkillsPath = "Assets/Game/Data/Characters/RinesSkillData.asset";
    private const string BasePrefabPath = "Assets/Game/Prefabs/Characters/PF_Player_Base.prefab";
    private const string RinesPrefabPath = "Assets/Game/Prefabs/Characters/PF_Player_Rines.prefab";
    private const string MaterialPath = "Assets/Game/Art/Materials/M_Rines_Placeholder.mat";
    private const string SelectionScenePath = "Assets/Game/Scenes/SC_CharacterSelect.unity";

    public static void CreateDataAndPrefab()
    {
        RinesSkillData skills = AssetDatabase.LoadAssetAtPath<RinesSkillData>(SkillsPath);
        if (skills == null)
        {
            skills = ScriptableObject.CreateInstance<RinesSkillData>();
            AssetDatabase.CreateAsset(skills, SkillsPath);
        }
        SerializedObject skillValues = new SerializedObject(skills);
        skillValues.FindProperty("_rankDamageStep").floatValue = 0.1f;
        skillValues.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(skills);

        Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (material == null)
        {
            GameObject basePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BasePrefabPath);
            Renderer baseRenderer = basePrefab != null ? basePrefab.GetComponent<Renderer>() : null;
            if (baseRenderer == null || baseRenderer.sharedMaterial == null)
                throw new System.InvalidOperationException("Rines base prefab has no root material.");
            material = new Material(baseRenderer.sharedMaterial);
            material.name = "M_Rines_Placeholder";
            material.color = new Color(1f, 0.82f, 0.2f, 1f);
            AssetDatabase.CreateAsset(material, MaterialPath);
        }

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(RinesPrefabPath);
        if (prefab == null)
        {
            GameObject basePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BasePrefabPath);
            if (basePrefab == null) throw new System.InvalidOperationException("Base player prefab missing.");
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(basePrefab);
            try
            {
                instance.name = "PF_Player_Rines";
                RinesSkillController controller = instance.AddComponent<RinesSkillController>();
                SerializedObject serialized = new SerializedObject(controller);
                serialized.FindProperty("_skillData").objectReferenceValue = skills;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                Renderer renderer = instance.GetComponent<Renderer>();
                if (renderer == null) throw new System.InvalidOperationException("Player root renderer missing.");
                renderer.sharedMaterial = material;
                PrefabUtility.SaveAsPrefabAsset(instance, RinesPrefabPath);
            }
            finally { Object.DestroyImmediate(instance); }
            prefab = AssetDatabase.LoadAssetAtPath<GameObject>(RinesPrefabPath);
        }

        CharacterData data = AssetDatabase.LoadAssetAtPath<CharacterData>(CharacterPath);
        if (data == null)
        {
            data = ScriptableObject.CreateInstance<CharacterData>();
            AssetDatabase.CreateAsset(data, CharacterPath);
        }
        SerializedObject character = new SerializedObject(data);
        SetString(character, "_characterId", "Rines");
        SetString(character, "_displayName", "リネス");
        SetString(character, "_roleName", "雷術師 / 遠隔CC");
        SetString(character, "_shortDescription", "雷を重ねて敵の動きを止める低HPの遠隔メイジ。");
        character.FindProperty("_themeColor").colorValue = new Color(1f, 0.82f, 0.2f, 1f);
        character.FindProperty("_characterStatus").enumValueIndex = (int)CharacterStatus.Available;
        character.FindProperty("_playerPrefab").objectReferenceValue = prefab;
        SetFloat(character, "_baseHp", 560f);
        SetFloat(character, "_hpGrowth", 80f);
        SetFloat(character, "_baseHpRegeneration", 2.5f);
        SetFloat(character, "_hpRegenerationGrowth", 0.25f);
        SetFloat(character, "_baseAttackDamage", 48f);
        SetFloat(character, "_attackDamageGrowth", 3f);
        SetFloat(character, "_baseAttackSpeed", 0.66f);
        SetFloat(character, "_attackSpeedGrowthPercent", 1.5f);
        SetFloat(character, "_baseArmor", 18f);
        SetFloat(character, "_armorGrowth", 3f);
        SetFloat(character, "_baseMoveSpeed", 350f);
        SetFloat(character, "_baseAttackRange", 450f);
        SetString(character, "_passiveDescription", "重雷: ハードCC中の敵へ追加のハードCCを当てると、そのスキルダメージが20%増加。");
        SetString(character, "_qDescription", "雷標: 地点を指定し、再入力で落雷。ダメージと0.6秒スネア。");
        SetString(character, "_wDescription", "環雷: 周囲へダメージ。通常はスロー、ハードCC中の敵はスネア。");
        SetString(character, "_eDescription", "雷走: 移動速度を上げ、最初の接触でダメージとスネア。");
        SetString(character, "_rDescription", "天雷: 予告後に範囲ダメージ、中心命中でスタン。");
        character.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(data);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Rines data and prefab ready.");
    }

    public static void AddToCharacterSelection()
    {
        var scene = EditorSceneManager.OpenScene(SelectionScenePath, OpenSceneMode.Single);
        CharacterData data = AssetDatabase.LoadAssetAtPath<CharacterData>(CharacterPath);
        if (data == null || data.PlayerPrefab == null)
            throw new System.InvalidOperationException("Create Rines data and prefab first.");
        CharacterSelectionUI ui = Object.FindFirstObjectByType<CharacterSelectionUI>(FindObjectsInactive.Include);
        if (ui == null) throw new System.InvalidOperationException("CharacterSelectionUI missing.");
        SerializedObject serialized = new SerializedObject(ui);
        serialized.Update();
        SerializedProperty characters = serialized.FindProperty("_characters");
        int index = -1;
        for (int i = 0; i < characters.arraySize; i++)
        {
            if (characters.GetArrayElementAtIndex(i).FindPropertyRelative("_characterData").objectReferenceValue == data)
                return;
            if (characters.GetArrayElementAtIndex(i).FindPropertyRelative("_fallbackDisplayName").stringValue == "リネス")
                index = i;
        }
        if (index < 0)
        {
            index = characters.arraySize;
            characters.InsertArrayElementAtIndex(index);
        }
        SerializedProperty entry = characters.GetArrayElementAtIndex(index);
        entry.FindPropertyRelative("_fallbackDisplayName").stringValue = "リネス";
        entry.FindPropertyRelative("_fallbackRoleName").stringValue = "雷術師 / 遠隔CC";
        entry.FindPropertyRelative("_fallbackThemeColor").colorValue = data.ThemeColor;
        entry.FindPropertyRelative("_fallbackStatus").enumValueIndex = (int)CharacterStatus.Available;
        SerializedProperty summary = entry.FindPropertyRelative("_skillSummaryLines");
        summary.arraySize = 5;
        summary.GetArrayElementAtIndex(0).stringValue = "P 重雷: 連続CCでダメージ増加";
        summary.GetArrayElementAtIndex(1).stringValue = "Q 雷標: 再入力で落雷・スネア";
        summary.GetArrayElementAtIndex(2).stringValue = "W 環雷: 周囲へスロー/スネア";
        summary.GetArrayElementAtIndex(3).stringValue = "E 雷走: 加速して接触スネア";
        summary.GetArrayElementAtIndex(4).stringValue = "R 天雷: 範囲攻撃・中心スタン";
        entry.FindPropertyRelative("_characterData").objectReferenceValue = data;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        if (new SerializedObject(ui).FindProperty("_characters").GetArrayElementAtIndex(index)
            .FindPropertyRelative("_characterData").objectReferenceValue != data)
            throw new System.InvalidOperationException("Rines character reference was not serialized.");
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new System.InvalidOperationException("Could not save character selection scene.");
        Debug.Log("Rines added to character selection.");
    }

    private static void SetString(SerializedObject obj, string name, string value) =>
        obj.FindProperty(name).stringValue = value;

    private static void SetFloat(SerializedObject obj, string name, float value) =>
        obj.FindProperty(name).floatValue = value;
}
