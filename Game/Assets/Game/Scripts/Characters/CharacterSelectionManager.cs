using UnityEngine;

/// <summary>
/// TASKS.md「キャラクター選択画面を実装する」用の選択結果保持マネージャー。
/// 現在選択中のCharacterDataを保持し、DontDestroyOnLoadでシーン遷移後も参照できるようにする。
/// 二重生成された場合は、後から生成された方を破棄する。
/// セーブデータ化は行わない(実行中のみ保持する)。
/// </summary>
public class CharacterSelectionManager : MonoBehaviour
{
    private static CharacterSelectionManager _instance;

    /// <summary>現在のインスタンス。存在しない場合はnull。</summary>
    public static CharacterSelectionManager Instance => _instance;

    /// <summary>現在選択中のキャラクターデータ。未選択の場合はnull。</summary>
    public CharacterData SelectedCharacter { get; private set; }

    /// <summary>
    /// インスタンスを取得する。存在しない場合は専用GameObjectを生成して常駐させる。
    /// </summary>
    public static CharacterSelectionManager GetOrCreateInstance()
    {
        if (_instance == null)
        {
            GameObject managerObject = new GameObject("CharacterSelectionManager");
            _instance = managerObject.AddComponent<CharacterSelectionManager>();
        }

        return _instance;
    }

    private void Awake()
    {
        // 複数生成されないようにする(既存インスタンスを優先し、自分を破棄する)。
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnDestroy()
    {
        if (_instance == this)
        {
            _instance = null;
        }
    }

    /// <summary>選択中のキャラクターデータを設定する。</summary>
    public void SelectCharacter(CharacterData characterData)
    {
        SelectedCharacter = characterData;
    }
}
