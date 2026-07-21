using UnityEngine;

/// <summary>
/// ダメージの種別。通常ダメージ(Normal)はゼルフWの前方ダメージ軽減や将来のARによる軽減の対象になる。
/// 確定ダメージ(True)は軽減できない分類(将来の朧Rの処刑・ヴォルブラークRの反射用)で、今回は使用しない。
/// </summary>
public enum DamageType
{
    Normal,
    True,
}

/// <summary>
/// 1回のダメージに関する情報(攻撃者・ダメージ種別・元ダメージ量)。
/// HealthControllerがHPへ適用する直前に、IIncomingDamageModifier(ゼルフWなど)へ渡す。
/// 攻撃者が取得できないダメージはAttackerがnullになる。
/// </summary>
public readonly struct DamageContext
{
    /// <summary>攻撃者のTransform。攻撃者情報が取得できない場合はnull。</summary>
    public readonly Transform Attacker;

    /// <summary>ダメージ種別。通常ダメージだけが軽減の対象になる。</summary>
    public readonly DamageType Type;

    /// <summary>軽減前の元ダメージ量。</summary>
    public readonly float BaseAmount;

    public DamageContext(Transform attacker, DamageType type, float baseAmount)
    {
        Attacker = attacker;
        Type = type;
        BaseAmount = baseAmount;
    }
}

/// <summary>
/// 受けるダメージをHPへ適用する直前に変更するコンポーネント用インターフェース(ゼルフWの前方ダメージ軽減など)。
/// HealthControllerと同じGameObject上のコンポーネントが自動的に呼び出される(Reflectionは使用しない)。
/// </summary>
public interface IIncomingDamageModifier
{
    /// <summary>
    /// 現在のダメージ量を受け取り、変更後のダメージ量を返す。変更しない場合はそのまま返す。
    /// W持続中に受けたダメージごとに呼び出される。
    /// </summary>
    float ModifyIncomingDamage(DamageContext context, float currentAmount);
}
