using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 通常攻撃・スキルの使用可否を一元管理する参照カウント式の行動ロック。
/// 「W発動中」「Eダッシュ中」「死亡中」など、スキルを使えない理由をAddLock/RemoveLockで登録し、ロックが1つでも残っている間はIsLockedがtrueになる。
/// 各コントローラーは他コンポーネントのenabledを直接切り替える代わりに、
/// 入力処理の先頭でIsLockedを確認する。
/// これにより複数の無効化理由が重なった場合の復元漏れ・二重復元を構造的に防ぐ。
/// Phase 3のCC(スタン・スネア・共通D硬直)もロック理由を追加するだけで実装できる。
/// 各コントローラーのAwakeでGetComponent→なければAddComponentするため、
/// Inspectorでの手動アタッチは不要(手動でアタッチしてもよい)。
/// </summary>
public sealed class AbilityLockController : MonoBehaviour
{
    /// <summary>ロック理由の定義。文字列の打ち間違いを防ぐため定数を使用する。</summary>
    public const string ReasonZelfW = "ZelfW";
    public const string ReasonZelfEDash = "ZelfEDash";
    public const string ReasonDeath = "Death";

    // 理由ごとのロック数。
    private readonly Dictionary<string, int> _locks = new Dictionary<string, int>();
    private int _totalLockCount;

    /// <summary>ロックが1つでも残っているか。trueの間は通常攻撃・スキルの入力を受け付けない。</summary>
    public bool IsLocked => _totalLockCount > 0;

    /// <summary>ロック状態が変化した(0件↔1件以上、または1件以上→0件)ときに通知する。</summary>
    public event Action<bool> LockStateChanged;

    /// <summary>指定理由のロックが残っているか。</summary>
    public bool IsLockedBy(string reason)
    {
        return _locks.TryGetValue(reason, out int count) && count > 0;
    }

    /// <summary>ロックを追加する。同じ理由でも呼び出し回数分だけRemoveLockが必要。</summary>
    public void AddLock(string reason)
    {
        bool wasLocked = IsLocked;
        _locks.TryGetValue(reason, out int count);
        _locks[reason] = count + 1;
        _totalLockCount++;
        Debug.Log($"AbilityLock: '{reason}' を追加しました(合計{_totalLockCount}件)。", this);
        if (!wasLocked) LockStateChanged?.Invoke(true);
    }

    /// <summary>AddLockで追加したロックを1つ解除する。追加されていない理由の解除は警告を出して無視する。</summary>
    public void RemoveLock(string reason)
    {
        if (!_locks.TryGetValue(reason, out int count) || count <= 0)
        {
            Debug.LogWarning($"AbilityLock: 追加されていないロック '{reason}' を解除しようとしました。", this);
            return;
        }
        _locks[reason] = count - 1;
        _totalLockCount--;
        Debug.Log($"AbilityLock: '{reason}' を解除しました(残り{_totalLockCount}件)。", this);
        if (!IsLocked) LockStateChanged?.Invoke(false);
    }
}
