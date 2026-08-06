using UnityEngine;
public class RuneSelectionManager : MonoBehaviour
{
    private static RuneSelectionManager _instance;
    public static RuneSelectionManager Instance => _instance;
    public RuneType SelectedRune { get; private set; } = RuneType.None;
    public static RuneSelectionManager GetOrCreateInstance()
    {
        if (_instance == null)
        {
            GameObject go = new GameObject("RuneSelectionManager");
            _instance = go.AddComponent<RuneSelectionManager>();
        }
        return _instance;
    }
    private void Awake()
    {
        if (_instance != null && _instance != this) { Destroy(gameObject); return; }
        _instance = this;
        DontDestroyOnLoad(gameObject);
    }
    private void OnDestroy() { if (_instance == this) _instance = null; }
    public void SelectRune(RuneType r) { SelectedRune = r; }
}
