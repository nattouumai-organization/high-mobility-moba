using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 既存のプレイヤーHUD(SkillCooldownHud)のQ/W/E/Rスロットへ、スキル強化表示を重ねるHUD(フェーズ7)。
/// GameManagerが実行時にAddComponentで生成する。
/// - 独自のスキルスロットは作らず、SkillCooldownHudが生成する「Player Status HUD Canvas」内の
///   「Status Panel/Skill Bar/Skill Slot Q〜R」を定期スキャンで探し、各スロットの子として
///   強化表示を追加する(生成タイミングに依存しない。SkillCooldownHud本体は変更しない)。
/// - 強化可能なスキルのスロット真上に上向き矢印を表示する(通常強化=緑 / Lv6追加強化=金色)。
/// - スロット下部のピップは現在の強化ランク(最大2)を表す。
/// - 強化操作はCtrl+Q/W/E/R(HeroSkillUpgrades側で処理)。強化可能なスキルがあるときだけ
///   スキルバー上部へ操作ヒントを表示する。
/// - 内蔵フォント(LegacyRuntime.ttf)に日本語グリフが無いためヒント表記は英語。
/// - SkillCooldownHud側でCanvas・スロットの名前を変更する場合は、本クラスの検索名も更新すること。
/// </summary>
public class SkillUpgradeHud : MonoBehaviour
{
    private const float ScanInterval = 1f;

    private static readonly Color NormalArrowColor = new Color(0.25f, 1f, 0.4f, 1f);
    private static readonly Color FinalArrowColor = new Color(1f, 0.78f, 0.1f, 1f);
    private static readonly Color RankPipOnColor = new Color(1f, 0.9f, 0.2f, 1f);
    private static readonly Color RankPipOffColor = new Color(1f, 1f, 1f, 0.15f);

    private class SlotOverlay
    {
        public HeroSkillSlot Skill;
        public GameObject ArrowRoot;
        public Image[] ArrowParts;
        public Image[] RankPips;
    }

    private readonly List<SlotOverlay> _overlays = new List<SlotOverlay>();
    private HeroSkillUpgrades _target;
    private Text _hintLabel;
    private bool _attached;
    private float _scanTimer;

    private void Update()
    {
        _scanTimer -= Time.deltaTime;
        if (_scanTimer <= 0f)
        {
            _scanTimer = ScanInterval;
            if (!_attached)
            {
                TryAttachToExistingHud();
            }

            if (_target == null)
            {
                _target = FindLocalHero();
            }
        }

        RefreshOverlays();
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

    // SkillCooldownHudが生成した既存スロットを探し、強化表示を子として追加する。
    // SkillCooldownHudの生成タイミングに依存しないよう、見つかるまで定期リトライする。
    private void TryAttachToExistingHud()
    {
        GameObject canvasObject = GameObject.Find("Player Status HUD Canvas");
        if (canvasObject == null)
        {
            return;
        }

        Transform skillBar = canvasObject.transform.Find("Status Panel/Skill Bar");
        if (skillBar == null)
        {
            return;
        }

        HeroSkillSlot[] skills = { HeroSkillSlot.Q, HeroSkillSlot.W, HeroSkillSlot.E, HeroSkillSlot.R };
        foreach (HeroSkillSlot skill in skills)
        {
            Transform slot = skillBar.Find($"Skill Slot {skill}");
            if (slot == null)
            {
                // コントローラー未検出などでスロット自体が無い場合は、そのスキルの強化表示を追加しない。
                Debug.LogWarning($"[SkillUpgradeHud] 既存HUDにSkill Slot {skill}が見つからないため、強化表示を追加しません。", this);
                continue;
            }

            _overlays.Add(CreateOverlay(slot, skill));
        }

        // 操作ヒント。強化可能なスキルがあるときだけ表示する(RefreshOverlaysで制御)。
        _hintLabel = CreateText(skillBar, "UpgradeHint", "Ctrl+Q/W/E/R: upgrade skill", 16, FontStyle.Normal);
        _hintLabel.alignment = TextAnchor.MiddleCenter;
        _hintLabel.color = new Color(1f, 1f, 1f, 0.7f);
        var hintRect = _hintLabel.rectTransform;
        hintRect.anchorMin = new Vector2(0.5f, 1f);
        hintRect.anchorMax = new Vector2(0.5f, 1f);
        hintRect.pivot = new Vector2(0.5f, 0f);
        hintRect.anchoredPosition = new Vector2(0f, 38f);
        hintRect.sizeDelta = new Vector2(360f, 24f);
        _hintLabel.enabled = false;

        _attached = true;
        Debug.Log($"[SkillUpgradeHud] 既存HUDへ強化表示を追加しました(スロット数{_overlays.Count})。", this);
    }

    private void RefreshOverlays()
    {
        bool anyUpgrade = false;
        foreach (SlotOverlay overlay in _overlays)
        {
            bool canUpgrade = _target != null && _target.CanUpgrade(overlay.Skill);
            anyUpgrade |= canUpgrade;
            if (overlay.ArrowRoot.activeSelf != canUpgrade)
            {
                overlay.ArrowRoot.SetActive(canUpgrade);
            }

            if (canUpgrade)
            {
                Color color = _target.IsFinalUpgradeCandidate(overlay.Skill) ? FinalArrowColor : NormalArrowColor;
                foreach (Image part in overlay.ArrowParts)
                {
                    part.color = color;
                }
            }

            int rank = _target != null ? _target.GetRank(overlay.Skill) : 0;
            for (int i = 0; i < overlay.RankPips.Length; i++)
            {
                overlay.RankPips[i].color = i < rank ? RankPipOnColor : RankPipOffColor;
            }
        }

        if (_hintLabel != null && _hintLabel.enabled != anyUpgrade)
        {
            _hintLabel.enabled = anyUpgrade;
        }
    }

    // 既存スロットの子として、上向き矢印(スロット真上)とランクピップ(スロット下部)を生成する。
    private SlotOverlay CreateOverlay(Transform slot, HeroSkillSlot skill)
    {
        // ランク表示ピップ(最大2)。後から追加する子は既存のオーバーレイより手前に描画される。
        var pips = new Image[HeroSkillUpgrades.MaxRank];
        for (int i = 0; i < pips.Length; i++)
        {
            var pipObject = new GameObject($"UpgradeRankPip{i + 1}");
            pipObject.transform.SetParent(slot, false);
            var pipRect = pipObject.AddComponent<RectTransform>();
            pipRect.anchorMin = new Vector2(0.5f, 0f);
            pipRect.anchorMax = new Vector2(0.5f, 0f);
            pipRect.pivot = new Vector2(0.5f, 0f);
            pipRect.anchoredPosition = new Vector2((i - 0.5f) * 14f, 4f);
            pipRect.sizeDelta = new Vector2(10f, 6f);
            pips[i] = pipObject.AddComponent<Image>();
            pips[i].color = RankPipOffColor;
            pips[i].raycastTarget = false;
        }

        // 強化可能時に表示する上向き矢印(斜めバー2本+縦棒)。スロットの真上(パネル外側)に置く。
        var arrowRoot = new GameObject("UpgradeArrow");
        arrowRoot.transform.SetParent(slot, false);
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

        return new SlotOverlay { Skill = skill, ArrowRoot = arrowRoot, ArrowParts = arrowParts, RankPips = pips };
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
        image.raycastTarget = false;
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
        image.raycastTarget = false;
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
        text.raycastTarget = false;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        var outline = textObject.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.8f);
        outline.effectDistance = new Vector2(1.2f, -1.2f);
        return text;
    }
}
