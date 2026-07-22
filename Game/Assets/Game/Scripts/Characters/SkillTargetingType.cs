/// <summary>
/// スキルの指定方式。フェーズ3以降のスキル実装で共通語彙として使用する。
/// 現行スキルの分類: Q=UnitTarget(対象指定) / W=NoTarget(無指定) / E=DirectionTarget(方向指定) / R=UnitTarget(対象指定)。
/// </summary>
public enum SkillTargetingType
{
    /// <summary>
    /// 対象指定。カーソル下に対象がいるときのみ発動する。
    /// 対象の分類(TargetClassification: キャラクター/ミニオン/タワーなど)によって発動する/しない・効果が変わる。
    /// </summary>
    UnitTarget = 0,

    /// <summary>
    /// 場所指定(地点指定)。カーソルが指すXZ平面上の地点でスキルが発動する。
    /// </summary>
    PointTarget = 1,

    /// <summary>
    /// 方向指定。本体からカーソル位置への「方向」にスキルを放つ。
    /// 場所指定と違い、重要なのは発動地点ではなく本体→カーソルの向き。
    /// (実装上は角度値ではなく正規化した方向ベクトルを用いる)
    /// </summary>
    DirectionTarget = 2,

    /// <summary>
    /// 無指定。カーソルの位置・カーソル下の対象に関わらず発動する(自己バフなど)。
    /// </summary>
    NoTarget = 3,
}
