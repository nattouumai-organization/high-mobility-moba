/// <summary>発動前の射程外自動接近をS・Fで中止できるスキル。</summary>
public interface ICancelableSkillApproach
{
    void CancelPendingApproach();
}
