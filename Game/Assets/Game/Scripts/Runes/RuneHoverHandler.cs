using System;
using UnityEngine;
using UnityEngine.EventSystems;
public sealed class RuneHoverHandler : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private RuneType _rune;
    private Action<RuneType, Vector2> _enter;
    private Action _exit;
    public void Init(RuneType r, Action<RuneType, Vector2> enter, Action exit)
        { _rune = r; _enter = enter; _exit = exit; }
    public void OnPointerEnter(PointerEventData e) => _enter?.Invoke(_rune, e.position);
    public void OnPointerExit(PointerEventData e) => _exit?.Invoke();
}
