using UnityEngine;

/// <summary>
/// ダメージの種別。通常ダメージ(Normal)はゼルフWの前方ダメージ軽減やARによる軽減の対象になる。
/// 確定ダメージ(True)はARで軽減できない分類(将来の朧Rの処刑・ヴォルブラークRの反射用)。
/// IIncomingDamageModifierによる変更(ヴォルブラークPの完全無効化など)は種別を問わず適用され、
/// 軽減・無効化するかどうかは各コンポーネントが種別を見て判断する(ゼルフWはNormalのみ軽減、ヴォルブラークPは両方を無効化)。
/// </summary>
public enum DamageType
{
    Normal,
    True,
}

/// <summary>
/// 1回のダメージに関する情報(攻撃者・ダメージ種別・元ダメージ量・反射フラグ)。
/// HealthControllerがHPへ適用する直前に、IIncomingDamageModifier(ゼルフWなど)へ渡す。
/// 攻撃者が取得できないダメージはAttackerがnullになる。
/// IsReflectedは反射によるダメージ(ヴォルブラークRの反射など)であることを表し、反射ダメージの再反射防止に使用する。
/// </summary>
public readonly struct DamageContext
{
    /// <summary>攻撃者のTransform。攻撃者情報が取得できない場合はnull。</summary>
    public readonly Transform Attacker;

    /// <summary>ダメージ種別。通常ダメージだけが軽減の対象になる。</summary>
    public readonly DamageType Type;

    /// <summary>軽減前の元ダメージ量。</summary>
    public readonly float BaseAmount;

    /// <summary>反射によるダメージかどうか(ヴォルブラークRの反射など)。反射フラグ付きのダメージは再反射されない。</summary>
    public readonly bool IsReflected;

    public DamageContext(Transform attacker, DamageType type, float baseAmount, bool isReflected = false)
    {
        Attacker = attacker;
        Type = type;
        BaseAmount = baseAmount;
        IsReflected = isReflected;
    }
}

/// <summary>
/// 受けるダメージをHPへ適用する直前に変更するコンポーネント用インターフェース(ゼルフWの前方ダメージ軽減・ヴォルブラークPの初撃無効化など)。
/// HealthControllerと同じGameObject上のコンポーネントが自動的に呼び出される(Reflectionは使用しない)。
/// </summary>
public interface IIncomingDamageModifier
{
    /// <summary>
    /// 現在のダメージ量を受け取り、変更後のダメージ量を返す。変更しない場合はそのまま返す。
    /// 受けたダメージごとに呼び出される。
    /// </summary>
    float ModifyIncomingDamage(DamageContext context, float currentAmount);
}
