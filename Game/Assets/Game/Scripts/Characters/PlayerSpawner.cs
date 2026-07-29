using UnityEngine;

/// <summary>
/// 試合シーン(SC_Prototype)開始時に、キャラクター選択結果に応じた
/// Playerプレハブ(Prefab Variant)を生成するスポナー。シーンの空オブジェクトへアタッチする。
/// - 生成位置・向き(フェーズ5): シーンにMapBuilderがある場合は自陣(Team・既定Blue)の開始地点
///   (SpawnPoint_Blue / SpawnPoint_Red)の位置・向きへ生成する(地面へめり込まないよう
///   Spawn Height Offsetだけ浮かせる)。MapBuilderが無い場合は従来どおりこのオブジェクトの
///   位置・向きへ生成する(復活位置は生成位置を引き継ぐ)。
/// - CharacterSelectionManagerが保持する選択中CharacterDataのPlayer Prefabを生成する
///   (未選択でSC_Prototypeを直接起動した場合はFallback Character Data(ZelfData想定)を使用する)。
/// - シーンへPlayerが直接配置されている場合は生成をスキップする(移行前のシーンでも安全に動作する)。
/// - 生成後のCharacterData適用(ステータス・テーマカラー・安全網のスキル整理)は、
///   プレハブ側のPlayerCharacterApplierが従来どおり行う。
/// DefaultExecutionOrder(-200)により、Playerを自動検出する他コンポーネントのAwakeより先に生成する
/// (マップ生成のMapBuilderは-300でさらに先に動く)。
/// </summary>
[DefaultExecutionOrder(-200)]
public sealed class PlayerSpawner : MonoBehaviour
{
    [Header("開始地点(フェーズ5)")]
    [Tooltip("このPlayerの陣営。シーンにMapBuilderがある場合、自陣の開始地点へ生成する。")]
    [SerializeField] private Team _team = Team.Blue;
    [Tooltip("開始地点から上へ浮かせる高さ。地面へめり込まないようにする(Playerのカプセルの半分程度)。")]
    [SerializeField, Min(0f)] private float _spawnHeightOffset = 1.1f;

    [Header("フォールバック")]
    [Tooltip("キャラクター未選択でSC_Prototypeを直接起動した場合に使用するCharacterData(ZelfData想定)")]
    [SerializeField] private CharacterData _fallbackCharacterData;

    /// <summary>このPlayerの陣営。</summary>
    public Team Team => _team;

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

        // 生成位置・向きを決める。MapBuilderがあれば自陣の開始地点、無ければ従来どおりこのオブジェクトの位置。
        Vector3 spawnPosition = transform.position;
        Quaternion spawnRotation = transform.rotation;
        string spawnSource = "PlayerSpawnerの位置";

        MapBuilder mapBuilder = FindFirstObjectByType<MapBuilder>();
        if (mapBuilder != null)
        {
            Transform spawnPoint = mapBuilder.GetSpawnPoint(_team);
            if (spawnPoint != null)
            {
                spawnPosition = spawnPoint.position + Vector3.up * _spawnHeightOffset;
                spawnRotation = spawnPoint.rotation;
                spawnSource = $"{_team}陣営の開始地点'{spawnPoint.name}'";
            }
        }

        SpawnedPlayer = Instantiate(prefab, spawnPosition, spawnRotation);
        // "(Clone)"サフィックスを付けず、ヒエラルキー上で分かりやすい名前にする。
        SpawnedPlayer.name = prefab.name;

        Debug.Log($"PlayerSpawner: '{selected.DisplayName}'(Id={selected.CharacterId})のPlayerプレハブ '{prefab.name}' を{spawnSource}へ生成しました。", this);
    }
}
