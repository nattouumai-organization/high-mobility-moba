using UnityEngine;

/// <summary>
/// フェーズ5前準備: 試合シーン(SC_Prototype)開始時に、キャラクター選択結果に応じた
/// Playerプレハブ(Prefab Variant)を生成するスポナー。シーンの空オブジェクトへアタッチし、
/// このオブジェクトの位置・向きがPlayerの開始位置になる。
/// - CharacterSelectionManagerが保持する選択中CharacterDataのPlayer Prefabを生成する
///   (未選択でSC_Prototypeを直接起動した場合はFallback Character Data(ZelfData想定)を使用する)。
/// - シーンへPlayerが直接配置されている場合は生成をスキップする(移行前のシーンでも安全に動作する)。
/// - 生成後のCharacterData適用(ステータス・テーマカラー・安全網のスキル整理)は、
///   プレハブ側のPlayerCharacterApplierが従来どおり行う。
/// DefaultExecutionOrder(-200)により、Playerを自動検出する他コンポーネントのAwakeより先に生成する。
/// </summary>
[DefaultExecutionOrder(-200)]
public sealed class PlayerSpawner : MonoBehaviour
{
    [Header("フォールバック")]
    [Tooltip("キャラクター未選択でSC_Prototypeを直接起動した場合に使用するCharacterData(ZelfData想定)")]
    [SerializeField] private CharacterData _fallbackCharacterData;

    /// <summary>生成したPlayerインスタンス。生成していない場合はnull。</summary>
    public GameObject SpawnedPlayer { get; private set; }

    private void Awake()
    {
        // シーンへPlayerが直接配置されている場合は生成しない(Prefab Variant方式への移行前のシーン用の安全網)。
        PlayerClickMovement existingPlayer = FindFirstObjectByType<PlayerClickMovement>();
        if (existingPlayer != null)
        {
            Debug.LogWarning("PlayerSpawner: シーンに既存のPlayerがあるため生成をスキップしました。Prefab Variant方式を使用する場合は、シーンのPlayerを削除してください。", this);
            return;
        }

        CharacterData selected = CharacterSelectionManager.Instance != null
            ? CharacterSelectionManager.Instance.SelectedCharacter
            : null;

        if (selected == null)
        {
            selected = _fallbackCharacterData;
        }

        if (selected == null)
        {
            Debug.LogError("PlayerSpawner: CharacterDataが無いためPlayerを生成できません。InspectorのFallback Character Dataを設定してください。", this);
            return;
        }

        GameObject prefab = selected.PlayerPrefab;
        if (prefab == null)
        {
            Debug.LogError($"PlayerSpawner: CharacterData '{selected.DisplayName}'(Id={selected.CharacterId})にPlayer Prefabが設定されていません。Data/Characters/の該当アセットへPrefab Variantを設定してください。", this);
            return;
        }

        // MapBuilderがあるシーンでは、本拠地前のスポーン位置を使用し、レーンの進軍方向を向く(高さはスポナーの値を維持)。
        // 1v1プロトタイプの操作ヒーローはBlue所属。
        Vector3 spawnPosition = transform.position;
        Quaternion spawnRotation = transform.rotation;
        if (MapBuilder.Instance != null)
        {
            Vector3 mapSpawn = MapBuilder.Instance.GetHeroSpawnPosition(Team.Blue);
            spawnPosition = new Vector3(mapSpawn.x, transform.position.y, mapSpawn.z);
            spawnRotation = Quaternion.LookRotation(MapBuilder.Instance.GetLaneForward(Team.Blue));
        }

        SpawnedPlayer = Instantiate(prefab, spawnPosition, spawnRotation);
        // "(Clone)"サフィックスを付けず、ヒエラルキー上で分かりやすい名前にする。
        SpawnedPlayer.name = prefab.name;

        Debug.Log($"PlayerSpawner: '{selected.DisplayName}'(Id={selected.CharacterId})のPlayerプレハブ '{prefab.name}' を生成しました。", this);
    }
}
