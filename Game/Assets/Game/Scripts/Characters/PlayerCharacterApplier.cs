using UnityEngine;

/// <summary>
/// キャラクター選択結果のCharacterDataをPlayerへ適用し、Prefab Variantと一致しない固有スキルを除去する。
/// 朧選択時はOboroSkillInstallerを保証し、P/Q/W/E/RとLayerMask補完を自動構成する。
/// </summary>
[DefaultExecutionOrder(-100)]
[RequireComponent(typeof(CharacterStats))]
public sealed class PlayerCharacterApplier : MonoBehaviour
{
    [Header("フォールバック")]
    [SerializeField] private CharacterData _fallbackCharacterData;

    [Header("見た目")]
    [SerializeField] private bool _applyThemeColorToRenderer = true;

    private const string ZelfCharacterId = "Zelf";
    private const string VolbraakCharacterId = "Volbraak";
    private const string OboroCharacterId = "Oboro";

    private void Awake()
    {
        CharacterData selected = CharacterSelectionManager.Instance != null
            ? CharacterSelectionManager.Instance.SelectedCharacter
            : null;
        if (selected == null) selected = _fallbackCharacterData;

        if (selected == null)
        {
            Debug.LogWarning("PlayerCharacterApplier: CharacterDataが無いため、シーンの初期設定のまま開始します。", this);
            PlayerLayerMaskFallback.Apply(gameObject);
            return;
        }

        GetComponent<CharacterStats>().SetCharacterData(selected);
        RemoveMismatchedSkillComponents(selected.CharacterId);
        EnsureSelectedSkillComponents(selected.CharacterId);
        ApplyThemeColor(selected);
        PlayerLayerMaskFallback.Apply(gameObject);
        Debug.Log($"PlayerCharacterApplier: '{selected.DisplayName}'(Id={selected.CharacterId})としてPlayerを初期化しました。", this);
    }

    private void EnsureSelectedSkillComponents(string characterId)
    {
        if (characterId == OboroCharacterId && GetComponent<OboroSkillInstaller>() == null)
        {
            gameObject.AddComponent<OboroSkillInstaller>();
        }
    }

    private void RemoveMismatchedSkillComponents(string characterId)
    {
        if (characterId != ZelfCharacterId)
        {
            DestroyImmediateIfPresent<ZelfPassiveHeal>();
            DestroyImmediateIfPresent<ZelfQController>();
            DestroyImmediateIfPresent<ZelfWController>();
            DestroyImmediateIfPresent<ZelfEController>();
            DestroyImmediateIfPresent<ZelfRController>();
        }

        if (characterId != VolbraakCharacterId)
        {
            DestroyImmediateIfPresent<VolbraakPassiveShield>();
            DestroyImmediateIfPresent<VolbraakQController>();
            DestroyImmediateIfPresent<VolbraakWController>();
            DestroyImmediateIfPresent<VolbraakEController>();
            DestroyImmediateIfPresent<VolbraakRController>();
        }

        if (characterId != OboroCharacterId)
        {
            DestroyImmediateIfPresent<OboroSkillInstaller>();
            DestroyImmediateIfPresent<OboroPassiveBackstab>();
            DestroyImmediateIfPresent<OboroQController>();
            DestroyImmediateIfPresent<OboroWController>();
            DestroyImmediateIfPresent<OboroEController>();
            DestroyImmediateIfPresent<OboroRController>();
        }
    }

    private void DestroyImmediateIfPresent<T>() where T : Behaviour
    {
        T component = GetComponent<T>();
        if (component != null) DestroyImmediate(component);
    }

    private void ApplyThemeColor(CharacterData data)
    {
        if (!_applyThemeColorToRenderer) return;
        Renderer bodyRenderer = GetComponentInChildren<Renderer>();
        if (bodyRenderer != null) bodyRenderer.material.color = data.ThemeColor;
    }
}
