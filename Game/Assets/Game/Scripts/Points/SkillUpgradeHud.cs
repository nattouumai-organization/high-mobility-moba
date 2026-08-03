using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 画面下部中央にQ/W/E/Rのスキルアイコン(仮)を表示し、強化できるスキルの上に
/// 上向き矢印を表示するHUD(フェーズ7)。GameManagerが実行時にAddComponentで生成する。
/// - 矢印の色: 通常強化=緑 / Lv6追加強化=金色(見た目で区別できるようにする)。
/// - アイコン下部の黄色いピップは現在の強化ランク(最大2)を表す。
/// - 操作はCtrl+Q/W/E/R(HeroSkillUpgrades側で処理)。アイコンの右に操作ヒントを表示する。
/// - 本格的なスキルアイコンはフェーズ8「全キャラクターのUIアイコンを仮実装する」で差し替える想定。
/// - 内蔵フォント(LegacyRuntime.ttf)に日本語グリフが無いため表記は英語。
/// </summary>
public class SkillUpgradeHud : MonoBehaviour
{
    private const float ScanInterval = 1f;
    private const float SlotSize = 64f;
    private const float SlotSpacing = 12f;
    private const float BottomMargin = 24f;

    private static readonly Color SlotBackgroundColor = new Color(0.1f, 0.1f, 0.12f, 0.85f);
    private static readonly Color NormalArrowColor = new Color(0.25f, 1f, 0.4f, 1f);
    private static readonly Color FinalArrowColor = new Color(1f, 0.78f, 0.1f, 1f);
    private static readonly Color RankPipOnColor = new Color(1f, 0.9f, 0.2f, 1f);
    private static readonly Color RankPipOffColor = new Color(1f, 1f, 1f, 0.15f);

    private class SkillSlot
    {
        public HeroSkillSlot Skill;
        public GameObject ArrowRoot;
        public Image[] ArrowParts;
        public Image[] RankPips;
    }

    private readonly List<SkillSlot> _slots = new List<SkillSlot>();
    private HeroSkillUpgrades _target;
    private float _scanTimer;
    private Text _hintLabel;

    private void Start()
    {
        CreateUi();
    }

    private void Update()
    {
        _scanTimer -= Time.deltaTime;
        if (_scanTimer <= 0f)
        {
            _scanTimer = ScanInterval;
            if (_target == null)
            {
                _target = FindLocalHero();
            }
        }

        RefreshSlots();
    }

    // 入力(PlayerInputHub)を持つローカル操作のヒーローを探す。
    private HeroSkillUpgrades FindLocalHero()
    {
        HeroSkillUpgrades[] candidates = FindObjectsByType<HeroSkillUpgrades>(FindObjectsSortMode.None);
        foreach (HeroSkillUpgrades candidate in candidates)
        {
            if (candidate.GetComponent<PlayerInputHub>() != null)
            {
                return candidate;
            }
        }

        return null;
    }

    private void RefreshSlots()
    {
        foreach (SkillSlot slot in _slots)
        {
            bool canUpgrade = _target != null && _target.CanUpgrade(slot.Skill);
            if (slot.ArrowRoot.activeSelf != canUpgrade)
            {
                slot.ArrowRoot.SetActive(canUpgrade);
            }

            if (canUpgrade)
            {
                Color color = _target.IsFinalUpgradeCandidate(slot.Skill) ? FinalArrowColor : NormalArrowColor;
                foreach (Image part in slot.ArrowParts)
                {
                    part.color = color;
                }
            }

            int rank = _target != null ? _target.GetRank(slot.Skill) : 0;
            for (int i = 0; i < slot.RankPips.Length; i++)
            {
                slot.RankPips[i].color = i < rank ? RankPipOnColor : RankPipOffColor;
            }
        }

        if (_hintLabel != null && _hintLabel.enabled != (_target != null))
        {
            _hintLabel.enabled = _target != null;
        }
    }

    private void CreateUi()
    {
        var canvasObject = new GameObject("SkillUpgradeHudCanvas");
        canvasObject.transform.SetParent(transform, false);
        var canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 41;
        var scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        HeroSkillSlot[] skills = { HeroSkillSlot.Q, HeroSkillSlot.W, HeroSkillSlot.E, HeroSkillSlot.R };
        float totalWidth = skills.Length * SlotSize + (skills.Length - 1) * SlotSpacing;
        for (int i = 0; i < skills.Length; i++)
        {
            float x = -totalWidth / 2f + SlotSize / 2f + i * (SlotSize + SlotSpacing);
            _slots.Add(CreateSlot(canvasObject.transform, skills[i], x));
        }

        _hintLabel = CreateText(canvasObject.transform, "UpgradeHint", "Ctrl+Q/W/E/R: upgrade skill", 16, FontStyle.Normal);
        _hintLabel.alignment = TextAnchor.MiddleLeft;
        _hintLabel.color = new Color(1f, 1f, 1f, 0.7f);
        var hintRect = _hintLabel.rectTransform;
        hintRect.anchorMin = new Vector2(0.5f, 0f);
        hintRect.anchorMax = new Vector2(0.5f, 0f);
        hintRect.pivot = new Vector2(0f, 0.5f);
        hintRect.anchoredPosition = new Vector2(totalWidth / 2f + 16f, BottomMargin + SlotSize / 2f);
        hintRect.sizeDelta = new Vector2(320f, 24f);
        _hintLabel.enabled = false;
    }

    private SkillSlot CreateSlot(Transform parent, HeroSkillSlot skill, float offsetX)
    {
        var slotObject = new GameObject($"SkillSlot{skill}");
        slotObject.transform.SetParent(parent, false);
        var rect = slotObject.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0f);
        rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.anchoredPosition = new Vector2(offsetX, BottomMargin);
        rect.sizeDelta = new Vector2(SlotSize, SlotSize);

        var background = slotObject.AddComponent<Image>();
        background.color = SlotBackgroundColor;

        // スキルキーのラベル(仮アイコン)。
        Text label = CreateText(slotObject.transform, "KeyLabel", skill.ToString(), 30, FontStyle.Bold);
        label.alignment = TextAnchor.MiddleCenter;
        var labelRect = label.rectTransform;
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        // ランク表示ピップ(最大2)。
        var pips = new Image[HeroSkillUpgrades.MaxRank];
        for (int i = 0; i < pips.Length; i++)
        {
            var pipObject = new GameObject($"RankPip{i + 1}");
            pipObject.transform.SetParent(slotObject.transform, false);
            var pipRect = pipObject.AddComponent<RectTransform>();
            pipRect.anchorMin = new Vector2(0.5f, 0f);
            pipRect.anchorMax = new Vector2(0.5f, 0f);
            pipRect.pivot = new Vector2(0.5f, 0f);
            pipRect.anchoredPosition = new Vector2((i - 0.5f) * 14f, 4f);
            pipRect.sizeDelta = new Vector2(10f, 6f);
            pips[i] = pipObject.AddComponent<Image>();
            pips[i].color = RankPipOffColor;
        }

        // 強化可能時に表示する上向き矢印(斜めバー2本+縦棒)。スロットの真上に置く。
        var arrowRoot = new GameObject("UpgradeArrow");
        arrowRoot.transform.SetParent(slotObject.transform, false);
        var arrowRect = arrowRoot.AddComponent<RectTransform>();
        arrowRect.anchorMin = new Vector2(0.5f, 1f);
        arrowRect.anchorMax = new Vector2(0.5f, 1f);
        arrowRect.pivot = new Vector2(0.5f, 0f);
        arrowRect.anchoredPosition = new Vector2(0f, 6f);
        arrowRect.sizeDelta = new Vector2(28f, 24f);

        var arrowParts = new Image[3];
        arrowParts[0] = CreateArrowPart(arrowRect, "Left", new Vector2(-5f, 16f), 45f);
        arrowParts[1] = CreateArrowPart(arrowRect, "Right", new Vector2(5f, 16f), -45f);
        arrowParts[2] = CreateArrowStem(arrowRect);
        arrowRoot.SetActive(false);

        return new SkillSlot { Skill = skill, ArrowRoot = arrowRoot, ArrowParts = arrowParts, RankPips = pips };
    }

    // 矢印の山形(斜めバー)部分。sprite未設定のImageは全面描画されるため矩形バーとして使える。
    private Image CreateArrowPart(RectTransform parent, string partName, Vector2 position, float rotationZ)
    {
        var partObject = new GameObject($"ArrowPart{partName}");
        partObject.transform.SetParent(parent, false);
        var partRect = partObject.AddComponent<RectTransform>();
        partRect.anchorMin = new Vector2(0.5f, 0f);
        partRect.anchorMax = new Vector2(0.5f, 0f);
        partRect.pivot = new Vector2(0.5f, 0.5f);
        partRect.anchoredPosition = position;
        partRect.sizeDelta = new Vector2(16f, 5f);
        partRect.localRotation = Quaternion.Euler(0f, 0f, rotationZ);
        var image = partObject.AddComponent<Image>();
        image.color = NormalArrowColor;
        return image;
    }

    // 矢印の縦棒部分。
    private Image CreateArrowStem(RectTransform parent)
    {
        var stemObject = new GameObject("ArrowStem");
        stemObject.transform.SetParent(parent, false);
        var stemRect = stemObject.AddComponent<RectTransform>();
        stemRect.anchorMin = new Vector2(0.5f, 0f);
        stemRect.anchorMax = new Vector2(0.5f, 0f);
        stemRect.pivot = new Vector2(0.5f, 0f);
        stemRect.anchoredPosition = Vector2.zero;
        stemRect.sizeDelta = new Vector2(5f, 14f);
        var image = stemObject.AddComponent<Image>();
        image.color = NormalArrowColor;
        return image;
    }

    private Text CreateText(Transform parent, string objectName, string content, int fontSize, FontStyle style)
    {
        var textObject = new GameObject(objectName);
        textObject.transform.SetParent(parent, false);
        textObject.AddComponent<RectTransform>();
        var text = textObject.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.color = Color.white;
        text.text = content;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        var outline = textObject.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.8f);
        outline.effectDistance = new Vector2(1.2f, -1.2f);
        return text;
    }
}
