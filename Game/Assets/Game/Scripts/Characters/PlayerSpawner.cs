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
/// DefaultExecutionOrder(-200)により、Playerを自動検出する他コンポーネントのAwakeより先に生成する
/// (マップを生成するMapBuilder(-300)はさらに先に実行される)。
/// フェーズ5: シーンにMapBuilderがある場合は、自陣営(Team)の開始地点の位置・向きへSpawn Height Offset分浮かせて生成し
/// (地面への埋まり防止)、公開プロパティTeamをTowerControllerの敵味方判定へ提供する。
/// </summary>
[DefaultExecutionOrder(-200)]
public sealed class PlayerSpawner : MonoBehaviour
{
    [Header("フォールバック")]
    [Tooltip("キャラクター未選択でSC_Prototypeを直接起動した場合に使用するCharacterData(ZelfData想定)")]
    [SerializeField] private CharacterData _fallbackCharacterData;

    [Header("陣営 (フェーズ5)")]
    [Tooltip("このスポナーが生成するPlayerの陣営。MapBuilderがあるシーンでは陣営の開始地点へ生成する")]
    [SerializeField] private Team _team = Team.Blue;

    [Tooltip("開始地点から浮かせる高さ。CharacterControllerが地面へ埋まらないようにする")]
    [SerializeField, Min(0f)] private float _spawnHeightOffset = 1.1f;

    /// <summary>生成したPlayerインスタンス。生成していない場合はnull。</summary>
    public GameObject SpawnedPlayer { get; private set; }

    /// <summary>このスポナーの陣営。TowerControllerの敵味方判定が使用する。</summary>
    public Team Team => _team;

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

        Vector3 spawnPosition = transform.position;
        Quaternion spawnRotation = transform.rotation;

        // フェーズ5: マップがあるシーンでは自陣営の開始地点へ生成する(地面へ埋まらないよう少し浮かせる)。
        MapBuilder mapBuilder = FindFirstObjectByType<MapBuilder>();
        Transform spawnPoint = mapBuilder != null ? mapBuilder.GetSpawnPoint(_team) : null;
        if (spawnPoint != null)
        {
            spawnPosition = spawnPoint.position + Vector3.up * _spawnHeightOffset;
            spawnRotation = spawnPoint.rotation;
        }

        SpawnedPlayer = Instantiate(prefab, spawnPosition, spawnRotation);
        // "(Clone)"サフィックスを付けず、ヒエラルキー上で分かりやすい名前にする。
        SpawnedPlayer.name = prefab.name;

        Debug.Log($"PlayerSpawner: '{selected.DisplayName}'(Id={selected.CharacterId})のPlayerプレハブ '{prefab.name}' を生成しました(陣営: {_team})。", this);
    }
}
