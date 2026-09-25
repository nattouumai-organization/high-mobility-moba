using System;
using UnityEngine;

/// <summary>実際に成立した自己移動スキルを、追撃ルーン等へ通知する共通窓口。</summary>
public sealed class MovementSkillSignal : MonoBehaviour
{
    public event Action Used;

    public static void Report(GameObject owner)
    {
        if (owner == null) return;
        MovementSkillSignal signal = owner.GetComponent<MovementSkillSignal>();
        if (signal == null) signal = owner.AddComponent<MovementSkillSignal>();
        signal.Used?.Invoke();
    }
}
