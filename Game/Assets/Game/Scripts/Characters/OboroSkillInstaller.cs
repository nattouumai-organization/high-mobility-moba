using UnityEngine;

/// <summary>
/// PF_Player_Oboroへ1つだけ追加し、P/Q/W/E/Rを確実に構成するインストーラー。
/// PlayerCharacterApplierより少し前に実行し、同クラスの誤Prefab安全網とLayerMask補完が全スキルへ作用するようにする。
/// </summary>
[DefaultExecutionOrder(-110)]
[DisallowMultipleComponent]
public sealed class OboroSkillInstaller : MonoBehaviour
{
    private void Awake()
    {
        Ensure<OboroPassiveBackstab>();
        // Q/E/RのAwakeが透明化解除先を取得できるよう、Wを先に追加する。
        Ensure<OboroWController>();
        Ensure<OboroQController>();
        Ensure<OboroEController>();
        Ensure<OboroRController>();

        // WはIIncomingDamageModifierなので、HealthControllerが先にAwake済みでもキャッシュを更新する。
        HealthController health = GetComponent<HealthController>();
        if (health != null) health.RefreshDamageModifiers();

        Debug.Log("朧: P/Q/W/E/Rを初期化しました。", this);
    }

    private void Ensure<T>() where T : Component
    {
        if (GetComponent<T>() == null) gameObject.AddComponent<T>();
    }
}
