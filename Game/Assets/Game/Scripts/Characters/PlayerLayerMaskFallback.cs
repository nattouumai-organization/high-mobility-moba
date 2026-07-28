using System.Reflection;
using UnityEngine;

/// <summary>
/// Player配下のコンポーネントで未設定(Nothing)のGround/Targetable LayerMaskを既定値へ自動補正する静的ヘルパー。
/// PlayerCharacterApplierのAwakeから呼び出す(フェーズ5前準備のPrefab Variant移行対応)。
/// - Prefab Variantへスキルコンポーネントを追加し直すとLayerMaskがNothingのままになり、
///   「マウスカーソルがGroundを指していないため発動しません」等で全スキルが不発になるのを防ぐ。
/// - フィールド名が _groundLayer / _targetableLayer のLayerMaskだけを対象に、未設定(Nothing)の場合のみ
///   レイヤー名(GroundLayer / TargetableLayer、見つからなければ6 / 7番)から補正する。
/// - Inspectorで設定済みの値は上書きしない(Inspector設定を最優先する方針は維持)。
/// - FlashControllerの_wallLayerのように「未設定=機能オフ」を意味するフィールドは対象外(フィールド名が一致しない)。
/// - 対象フィールドはprivateのため、各コントローラーを改変せずに済むようReflectionで補正する
///   (ゲームロジックの書き換えではなく、Inspector設定漏れの救済に限定して使用する)。
/// PlayerCharacterApplier(DefaultExecutionOrder(-100))から呼ばれるため、各スキルコントローラーのAwake
/// (ZelfE/ZelfW/FlashControllerによるZelfQ設定の流用など)より先に補正が完了する。
/// </summary>
public static class PlayerLayerMaskFallback
{
    private const string GroundFieldName = "_groundLayer";
    private const string TargetableFieldName = "_targetableLayer";
    private const string GroundLayerName = "GroundLayer";
    private const string TargetableLayerName = "TargetableLayer";

    // TECHNICAL_DESIGN.md準拠の既定レイヤー番号(同名レイヤーが見つからない場合に使用)。
    private const int GroundLayerNumberFallback = 6;
    private const int TargetableLayerNumberFallback = 7;

    /// <summary>player配下の全MonoBehaviourの未設定LayerMaskを補正する。</summary>
    public static void Apply(GameObject player)
    {
        if (player == null) return;

        LayerMask groundMask = ResolveMask(GroundLayerName, GroundLayerNumberFallback);
        LayerMask targetableMask = ResolveMask(TargetableLayerName, TargetableLayerNumberFallback);

        foreach (MonoBehaviour behaviour in player.GetComponentsInChildren<MonoBehaviour>(true))
        {
            // スクリプト欠損(Missing Script)はnullになるため読み飛ばす。
            if (behaviour == null) continue;
            FixFieldIfUnset(behaviour, GroundFieldName, groundMask);
            FixFieldIfUnset(behaviour, TargetableFieldName, targetableMask);
        }
    }

    // レイヤー名からLayerMaskを求める。プロジェクトに同名レイヤーが無い場合は既定番号を使う。
    private static LayerMask ResolveMask(string layerName, int fallbackLayerNumber)
    {
        int layer = LayerMask.NameToLayer(layerName);
        if (layer < 0) layer = fallbackLayerNumber;
        return 1 << layer;
    }

    // 指定名のLayerMaskフィールドが存在し、未設定(Nothing)の場合のみ補正する。
    private static void FixFieldIfUnset(MonoBehaviour behaviour, string fieldName, LayerMask fallbackMask)
    {
        FieldInfo field = behaviour.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (field == null || field.FieldType != typeof(LayerMask)) return;

        LayerMask currentMask = (LayerMask)field.GetValue(behaviour);
        if (currentMask.value != 0) return;

        field.SetValue(behaviour, fallbackMask);
        Debug.LogWarning(
            behaviour.GetType().Name + ": " + fieldName +
            " が未設定(Nothing)のため既定値へ自動補正しました。InspectorでLayerMaskを設定するとこの警告は消えます。",
            behaviour);
    }
}
