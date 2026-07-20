#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 配布物を既存プロトタイプへ安全に設定するための一回限りのEditor補助。
/// Tools/High Mobility MOBA/Apply Zelf Q To SC_Prototype から実行する。
/// </summary>
public static class ZelfQProjectSetup
{
    private const string PrototypeScenePath = "Assets/Game/Scenes/SC_Prototype.unity";
    private const string TasksPath = "../TASKS.md";
    private const string ChangelogPath = "../CHANGELOG.md";

    [MenuItem("Tools/High Mobility MOBA/Apply Zelf Q To SC_Prototype")]
    public static void Apply()
    {
        Scene scene = EditorSceneManager.OpenScene(PrototypeScenePath, OpenSceneMode.Single);
        GameObject player = GameObject.Find("Player");
        if (player == null)
        {
            Debug.LogError("Zelf Q setup: SC_Prototype 内に Player が見つかりません。");
            return;
        }

        ZelfQController controller = player.GetComponent<ZelfQController>();
        if (controller == null)
        {
            controller = Undo.AddComponent<ZelfQController>(player);
        }

        SerializedObject serializedController = new SerializedObject(controller);
        SetObjectReference(serializedController, "_characterController", player.GetComponent<CharacterController>());
        SetObjectReference(serializedController, "_characterStats", player.GetComponent<CharacterStats>());
        SetObjectReference(serializedController, "_targetSelector", player.GetComponent<PlayerTargetSelector>());
        SetObjectReference(serializedController, "_healthController", player.GetComponent<HealthController>());
        SetLayerMask(serializedController, "_groundLayer", 6);
        SetLayerMask(serializedController, "_targetableLayer", 7);
        SetBoolean(serializedController, "_logCastResults", true);
        serializedController.ApplyModifiedPropertiesWithoutUndo();

        GameObject trainingDummy = GameObject.Find("TrainingDummy");
        if (trainingDummy != null)
        {
            Targetable targetable = trainingDummy.GetComponent<Targetable>();
            if (targetable != null)
            {
                SerializedObject serializedTargetable = new SerializedObject(targetable);
                SerializedProperty classification = serializedTargetable.FindProperty("_classification");
                if (classification != null) classification.enumValueIndex = (int)TargetClassification.Character;
                serializedTargetable.ApplyModifiedPropertiesWithoutUndo();
            }

            HealthController health = trainingDummy.GetComponent<HealthController>();
            if (health != null)
            {
                SerializedObject serializedHealth = new SerializedObject(health);
                SetFloat(serializedHealth, "_maxHealth", 300f);
                SetFloat(serializedHealth, "_currentHealth", 300f);
                serializedHealth.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        UpdateDocumentation();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Zelf Q setup: SC_Prototype、TASKS.md、CHANGELOG.md を更新しました。");
    }

    private static void UpdateDocumentation()
    {
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        UpdateTasks(Path.GetFullPath(Path.Combine(projectRoot, TasksPath)));
        UpdateChangelog(Path.GetFullPath(Path.Combine(projectRoot, ChangelogPath)));
    }

    private static void UpdateTasks(string path)
    {
        if (!File.Exists(path)) return;
        string text = File.ReadAllText(path);
        text = text.Replace("- [ ] ゼルフQの対象ブリンクを実装する", "- [x] ゼルフQの対象ブリンクを実装する");
        text = text.Replace("- [ ] ゼルフQの同一対象への再使用制限を実装する", "- [x] ゼルフQの同一対象への再使用制限を実装する");
        text = text.Replace("- [ ] ゼルフQのスキル命中リセット処理を実装する", "- [x] ゼルフQのスキル命中リセット処理を実装する");
        File.WriteAllText(path, text);
    }

    private static void UpdateChangelog(string path)
    {
        if (!File.Exists(path)) return;
        string text = File.ReadAllText(path);
        const string marker = "- ゼルフQの対象ブリンク、対象指定ダメージ、同一対象ロック、分類別クールダウン処理を実装。";
        if (text.Contains(marker)) return;

        string entry =
            "## 2026-07-21\n\n" +
            "### Added\n\n" +
            marker + "\n" +
            "- ZelfQControllerを追加。Qキーで選択中の有効なCharacter / Minion / TrainingDummyへ、Collider最寄り点を基準に安全な停止距離でブリンクする。\n" +
            "- Qダメージを `Base Damage + Current Attack Damage × AD Ratio` としてHealthController経由で適用する。\n" +
            "- Q成功対象へSame Target Lockoutを設定し、ロック中の同一対象にはブリンク・ダメージ・クールダウン消費を発生させない。\n" +
            "- Character / TrainingDummy分類へのQ命中時はQクールダウンを即時リセットし、Minion分類への命中時は残りクールダウンを50%短縮する。Tower分類には発動しない。\n" +
            "- Qダメージでも既存の被弾フラッシュ、ダメージ表示、ゼルフP回復の通知経路を利用する。\n\n";

        int firstEntry = text.IndexOf("## ", StringComparison.Ordinal);
        if (firstEntry < 0) File.WriteAllText(path, text + "\n" + entry);
        else File.WriteAllText(path, text.Insert(firstEntry, entry));
    }

    private static void SetObjectReference(SerializedObject serializedObject, string propertyName, UnityEngine.Object value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property != null) property.objectReferenceValue = value;
    }

    private static void SetLayerMask(SerializedObject serializedObject, string propertyName, int layer)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property != null) property.intValue = 1 << layer;
    }

    private static void SetFloat(SerializedObject serializedObject, string propertyName, float value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property != null) property.floatValue = value;
    }

    private static void SetBoolean(SerializedObject serializedObject, string propertyName, bool value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property != null) property.boolValue = value;
    }
}
#endif
