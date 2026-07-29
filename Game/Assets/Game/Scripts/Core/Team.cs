/// <summary>
/// 陣営(青/赤)。開始地点・タワーの対応付けと、TowerController・PlayerSpawnerの敵味方判定に使用する(フェーズ5)。
/// 将来のミニオン・本拠地・オンライン対戦でも同じ陣営定義を再利用する。
/// </summary>
public enum Team
{
    Blue = 0,
    Red = 1,
}
