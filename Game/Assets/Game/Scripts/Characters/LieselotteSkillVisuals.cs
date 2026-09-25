using System.Collections.Generic;
using UnityEngine;

/// <summary>リーゼロッテ専用の実行時プレースホルダー。共有Materialは変更しない。</summary>
[DisallowMultipleComponent]
public sealed class LieselotteSkillVisuals : MonoBehaviour
{
    private sealed class Effect
    {
        public GameObject Object;
        public Material Material;
        public float EndTime;
    }

    private readonly List<Effect> _temporary = new List<Effect>();
    private GameObject _wRing;
    private Material _wMaterial;
    private GameObject _stealRing;
    private Material _stealMaterial;
    private float _wEndTime;

    public bool IsWVisible => _wRing != null && _wRing.activeSelf;
    public bool IsStealVisible => _stealRing != null && _stealRing.activeSelf;

    public void ShowPassive(Transform target) => Flash(target, 0.8f, 1.3f,
        new Color(1f, 0.2f, 0.35f), 0.55f, "Lieselotte P hit");

    public void ShowQ(Transform target) => Flash(target, 0.7f, 1.25f,
        new Color(1f, 0.75f, 0.8f), 0.45f, "Lieselotte Q bite");

    public void ShowBloodPool(Vector3 point, float radius, float seconds)
    {
        GameObject ring = Ring("Lieselotte E blood pool", null, point + Vector3.up * 0.06f,
            radius, new Color(0.6f, 0.04f, 0.12f), out Material material);
        _temporary.Add(new Effect { Object = ring, Material = material, EndTime = Time.time + seconds });
    }

    public void ShowRDash(Vector3 from, Vector3 to)
    {
        GameObject lineObject = new GameObject("Lieselotte R trail");
        LineRenderer line = lineObject.AddComponent<LineRenderer>();
        Material material = OboroCombatUtility.CreateUnlitMaterial(new Color(1f, 0.35f, 0.5f));
        line.sharedMaterial = material;
        line.positionCount = 2;
        line.SetPosition(0, from + Vector3.up);
        line.SetPosition(1, to + Vector3.up);
        line.startWidth = 0.25f;
        line.endWidth = 0.08f;
        _temporary.Add(new Effect { Object = lineObject, Material = material,
            EndTime = Time.time + 0.45f });
    }

    public void SetWActive(float seconds)
    {
        if (_wRing == null) _wRing = Ring("Lieselotte W surge", transform, Vector3.up * 0.15f,
            0.9f, new Color(1f, 0.45f, 0.55f), out _wMaterial);
        _wRing.SetActive(true);
        _wEndTime = Time.time + seconds;
    }

    public void SetStealActive(bool active)
    {
        if (active && _stealRing == null)
            _stealRing = Ring("Lieselotte R steal", transform, Vector3.up * 0.25f,
                1.05f, new Color(1f, 0.1f, 0.15f), out _stealMaterial);
        if (_stealRing != null) _stealRing.SetActive(active);
    }

    private void Flash(Transform target, float radius, float height, Color color, float seconds,
        string name)
    {
        if (target == null) return;
        GameObject ring = Ring(name, target, Vector3.up * height, radius, color,
            out Material material);
        _temporary.Add(new Effect { Object = ring, Material = material,
            EndTime = Time.time + seconds });
    }

    private static GameObject Ring(string name, Transform parent, Vector3 position,
        float radius, Color color, out Material material)
    {
        GameObject obj = new GameObject(name);
        if (parent != null)
        {
            obj.transform.SetParent(parent, false);
            obj.transform.localPosition = position;
        }
        else obj.transform.position = position;
        LineRenderer line = obj.AddComponent<LineRenderer>();
        line.useWorldSpace = false;
        line.loop = true;
        line.positionCount = 40;
        line.startWidth = line.endWidth = 0.1f;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        material = OboroCombatUtility.CreateUnlitMaterial(color);
        line.sharedMaterial = material;
        for (int i = 0; i < line.positionCount; i++)
        {
            float angle = i * Mathf.PI * 2f / line.positionCount;
            line.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius, 0f,
                Mathf.Sin(angle) * radius));
        }
        return obj;
    }

    private void Update()
    {
        if (IsWVisible && Time.time >= _wEndTime) _wRing.SetActive(false);
        for (int i = _temporary.Count - 1; i >= 0; i--)
        {
            Effect effect = _temporary[i];
            if (Time.time < effect.EndTime) continue;
            Dispose(effect);
            _temporary.RemoveAt(i);
        }
    }

    public void HidePersistent()
    {
        if (_wRing != null) _wRing.SetActive(false);
        if (_stealRing != null) _stealRing.SetActive(false);
    }

    public void HideAll()
    {
        HidePersistent();
        foreach (Effect effect in _temporary) Dispose(effect);
        _temporary.Clear();
    }

    private void OnDisable() => HideAll();

    private void OnDestroy()
    {
        HideAll();
        Dispose(new Effect { Object = _wRing, Material = _wMaterial });
        Dispose(new Effect { Object = _stealRing, Material = _stealMaterial });
    }

    private static void Dispose(Effect effect)
    {
        if (Application.isPlaying)
        {
            if (effect.Object != null) Destroy(effect.Object);
            if (effect.Material != null) Destroy(effect.Material);
        }
        else
        {
            if (effect.Object != null) DestroyImmediate(effect.Object);
            if (effect.Material != null) DestroyImmediate(effect.Material);
        }
    }
}
