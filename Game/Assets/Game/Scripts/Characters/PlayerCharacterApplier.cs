using UnityEngine;

/// <summary>
/// フェーズ4前準備: SC_Prototype開始時に、キャラクター選択画面で選択したCharacterDataを
/// Playerへ適用するコンポーネント。SC_PrototypeのPlayerへアタッチして使用する。
/// - CharacterSelectionManagerが保持する選択中CharacterDataをCharacterStatsへ適用する
///   (未選択でSC_Prototypeを直接起動した場合はFallback Character Data(ZelfData想定)を使用する)。
/// - 選択キャラクターがゼルフ以外の場合、ゼルフ固有のスキルコンポーネント(P/Q/W/E/R)を取り除く。
///   移動・通常攻撃・共通D・Fフラッシュなどの共通コンポーネントはどのキャラクターでも動作する。
/// - 選択キャラクターがヴォルブラーク以外の場合、ヴォルブラーク固有のスキルコンポーネント
///   (P: VolbraakPassiveShield, Q: VolbraakQController)を取り除く。
///   ヴォルブラークのW/E/Rはフェーズ4の各タスクで実装後、このクラスへ登録していく。
/// - 見た目の区別のため、PlayerのRendererへテーマカラーを適用する(Inspectorで無効化可能)。
/// DefaultExecutionOrder(-100)により、CharacterStatsや各スキルコントローラーのAwakeより先に実行する。
/// </summary>
[DefaultExecutionOrder(-100)]
[RequireComponent(typeof(CharacterStats))]
public sealed class PlayerCharacterApplier : MonoBehaviour
{
    [Header("フォールバック")]
    [Tooltip("キャラクター未選択でSC_Prototypeを直接起動した場合に使用するCharacterData(ZelfData想定)")]
    [SerializeField] private CharacterData _fallbackCharacterData;

    [Header("見た目")]
    [Tooltip("PlayerのRendererへ選択キャラクターのテーマカラーを適用する")]
    [SerializeField] private bool _applyThemeColorToRenderer = true;

    /// <summary>ゼルフのCharacterId(ZelfData.assetの_characterIdと一致させる)。</summary>
    private const string ZelfCharacterId = "Zelf";

    /// <summary>ヴォルブラークのCharacterId(VolbraakData.assetの_characterIdと一致させる)。</summary>
    private const string VolbraakCharacterId = "Volbraak";

    private void Awake()
    {
        CharacterData selected = CharacterSelectionManager.Instance != null
            ? CharacterSelectionManager.Instance.SelectedCharacter
            : null;

        if (selected == null)
        {
            selected = _fallbackCharacterData;
        }

        if (selected == null)
        {
            // 選択もフォールバックもない場合は何も適用しない(従来どおりシーンのInspector設定で動作する)。
            Debug.LogWarning("PlayerCharacterApplier: CharacterDataが無いため、シーンの初期設定のまま開始します。", this);
            return;
        }

        CharacterStats stats = GetComponent<CharacterStats>();
        stats.SetCharacterData(selected);

        RemoveMismatchedSkillComponents(selected.CharacterId);
        ApplyThemeColor(selected);

        Debug.Log($"PlayerCharacterApplier: '{selected.DisplayName}'(Id={selected.CharacterId})としてPlayerを初期化しました。", this);
    }

    // 選択キャラクターに合わないキャラクター固有スキルコンポーネントを取り除く。
    // DefaultExecutionOrder(-100)により対象コンポーネントのAwakeより先に実行されるため、
    // DestroyImmediateで即時に取り除き、対象のAwake(エフェクト生成・イベント購読など)自体を走らせない。
    private void RemoveMismatchedSkillComponents(string characterId)
    {
        if (characterId != ZelfCharacterId)
        {
            // ゼルフ固有(P/Q/W/E/R)。共通の移動・通常攻撃・共通D・Fはそのまま残す。
            // 参照する側(HUD・スキルプレビュー・移動など)はいずれもnull安全に実装済み。
            DestroyImmediateIfPresent<ZelfPassiveHeal>();
            DestroyImmediateIfPresent<ZelfQController>();
            DestroyImmediateIfPresent<ZelfWController>();
            DestroyImmediateIfPresent<ZelfEController>();
            DestroyImmediateIfPresent<ZelfRController>();
        }

        // ヴォルブラーク固有(P/Q)。W/E/Rはフェーズ4の各タスクで実装後、ここへ追加する。
        if (characterId != VolbraakCharacterId)
        {
            DestroyImmediateIfPresent<VolbraakPassiveShield>();
            DestroyImmediateIfPresent<VolbraakQController>();
        }
    }

    private void DestroyImmediateIfPresent<T>() where T : Behaviour
    {
        T component = GetComponent<T>();
        if (component == null)
        {
            return;
        }

        DestroyImmediate(component);
    }

    // Playerの見た目(Renderer)へテーマカラーを適用する(どのキャラクターを操作しているか区別するための仮表示)。
    private void ApplyThemeColor(CharacterData data)
    {
        if (!_applyThemeColorToRenderer)
        {
            return;
        }

        Renderer bodyRenderer = GetComponentInChildren<Renderer>();
        if (bodyRenderer == null)
        {
            return;
        }

        // sharedMaterialではなくmaterialを使い、このPlayerだけの色として適用する(他オブジェクトの見た目へ影響しない)。
        bodyRenderer.material.color = data.ThemeColor;
    }
}
