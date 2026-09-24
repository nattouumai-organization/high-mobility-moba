using System.Collections.Generic;
using UnityEngine;

/// <summary>Runtime-only placeholder effects owned by Rines; no shared material is changed.</summary>
[DisallowMultipleComponent]
public sealed class RinesSkillVisuals : MonoBehaviour
{
    private const float WFlashSeconds = 0.4f;
    private const float HitFlashSeconds = 0.45f;
    private static readonly Color WColor = new Color(1f, 0.9f, 0.18f, 1f);
    private static readonly Color EColor = new Color(0.65f, 0.9f, 1f, 1f);
    private static readonly Color SnareColor = new Color(0.35f, 0.9f, 1f, 1f);

    private sealed class HitEffect
    {
        public GameObject Object;
        public Material Material;
        public float EndTime;
    }

    private GameObject _wRing;
    private Material _wMaterial;
    private float _wEndTime;
    private GameObject _eGlow;
    private Material _eMaterial;
    private readonly List<HitEffect> _hits = new List<HitEffect>();

    public bool IsWVisible => _wRing != null && _wRing.activeSelf;
    public bool IsEVisible => _eGlow != null && _eGlow.activeSelf;
    public int ActiveHitCount => _hits.Count;

    public void ShowW(float radius)
    {
        if (_wRing == null)
        {
            _wRing = CreateRing("Rines W range", transform, radius, 0.12f, 0.09f, WColor,
                out _wMaterial);
        }
        _wRing.SetActive(true);
        _wEndTime = Time.time + WFlashSeconds;
    }

    public void ShowHit(Transform target, bool snared)
    {
        if (target == null) return;
        Material material;
        GameObject ring = CreateRing(snared ? "Rines W snare hit" : "Rines W hit",
            target, 0.65f, 1.4f, 0.08f, snared ? SnareColor : WColor, out material);
        _hits.Add(new HitEffect { Object = ring, Material = material,
            EndTime = Time.time + HitFlashSeconds });
    }

    public void SetEActive(bool active)
    {
        if (!active)
        {
            if (_eGlow != null) _eGlow.SetActive(false);
            return;
        }
        if (_eGlow == null)
        {
            _eGlow = CreateRing("Rines E glow", transform, 0.9f, 0.18f, 0.11f, EColor,
                out _eMaterial);
            Light light = _eGlow.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = EColor;
            light.range = 2.4f;
            light.intensity = 1.5f;
            light.shadows = LightShadows.None;
        }
        _eGlow.SetActive(true);
    }

    private void Update()
    {
        if (IsWVisible && Time.time >= _wEndTime) _wRing.SetActive(false);
        for (int i = _hits.Count - 1; i >= 0; i--)
        {
            HitEffect hit = _hits[i];
            if (hit.Object != null && Time.time < hit.EndTime) continue;
            Dispose(hit.Object, hit.Material);
            _hits.RemoveAt(i);
        }
    }

    private void OnDisable()
    {
        HideAll();
    }

    private void OnDestroy()
    {
        HideAll();
        Dispose(_wRing, _wMaterial);
        Dispose(_eGlow, _eMaterial);
    }

    public void HideAll()
    {
        if (_wRing != null) _wRing.SetActive(false);
        if (_eGlow != null) _eGlow.SetActive(false);
        foreach (HitEffect hit in _hits) Dispose(hit.Object, hit.Material);
        _hits.Clear();
    }

    private static GameObject CreateRing(string label, Transform parent, float radius,
        float height, float width, Color color, out Material material)
    {
        GameObject ring = new GameObject(label);
        ring.transform.SetParent(parent, false);
        ring.transform.localPosition = Vector3.up * height;
        LineRenderer line = ring.AddComponent<LineRenderer>();
        line.useWorldSpace = false;
        line.loop = true;
        line.positionCount = 64;
        line.startWidth = line.endWidth = width;
        line.numCornerVertices = 2;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;
        material = OboroCombatUtility.CreateUnlitMaterial(color);
        line.sharedMaterial = material;
        for (int i = 0; i < line.positionCount; i++)
        {
            float angle = i * Mathf.PI * 2f / line.positionCount;
            line.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius, 0f,
                Mathf.Sin(angle) * radius));
        }
        return ring;
    }

    private static void Dispose(GameObject effect, Material material)
    {
        if (Application.isPlaying)
        {
            if (effect != null) Destroy(effect);
            if (material != null) Destroy(material);
        }
        else
        {
            if (effect != null) DestroyImmediate(effect);
            if (material != null) DestroyImmediate(material);
        }
    }
}
