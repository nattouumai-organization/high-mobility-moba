using UnityEngine;

/// <summary>
/// 試合シーン開始時に、キャラクター選択画面で選択したCharacterDataをPlayerへ適用するコンポーネント。
/// Playerプレハブ(PF_Player_Base)へアタッチし、各キャラクターのPrefab Variantが共通で使用する
/// (フェーズ5前準備でシーン直置きのPlayerからPrefab Variant方式へ移行。生成はPlayerSpawnerが行う)。
/// - CharacterSelectionManagerが保持する選択中CharacterDataをCharacterStatsへ適用する
///   (未選択でSC_Prototypeを直接起動した場合はFallback Character Dataを使用する。
///    各Prefab VariantのFallbackにはそのキャラクター自身のCharacterDataを設定する)。
/// - 選択キャラクターと一致しないキャラクター固有スキルコンポーネントを取り除く。
///   Prefab Variantは本来自分のスキルしか持たないため通常は何も取り除かれないが、
///   CharacterDataとPrefab Variantの組み合わせをInspectorで誤設定した場合の安全網として残す。
/// - 見た目の区別のため、PlayerのRendererへテーマカラーを適用する(Inspectorで無効化可能)。
/// DefaultExecutionOrder(-100)により、CharacterStatsや各スキルコントローラーのAwakeより先に実行する。
/// </summary>
[DefaultExecutionOrder(-100)]
[RequireComponent(typeof(CharacterStats))]
public sealed class PlayerCharacterApplier : MonoBehaviour
{
    [Header("フォールバック")]
    [Tooltip("キャラクター未選択でSC_Prototypeを直接起動した場合に使用するCharacterData。各Prefab Variantにはそのキャラクター自身のCharacterDataを設定する")]
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
    // Prefab Variant方式では各Variantは自分のスキルしか持たないため通常は何も起きず、
    // CharacterDataとVariantの誤設定に備えた安全網として機能する。
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

        // ヴォルブラーク固有(P/Q/W/E/R)。共通の移動・通常攻撃・共通D・Fはそのまま残す。
        if (characterId != VolbraakCharacterId)
        {
            DestroyImmediateIfPresent<VolbraakPassiveShield>();
            DestroyImmediateIfPresent<VolbraakQController>();
            DestroyImmediateIfPresent<VolbraakWController>();
            DestroyImmediateIfPresent<VolbraakEController>();
            DestroyImmediateIfPresent<VolbraakRController>();
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
