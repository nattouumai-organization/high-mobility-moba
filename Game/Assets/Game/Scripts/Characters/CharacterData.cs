using UnityEngine;

/// <summary>
/// キャラクターの状態。今回はAvailableとComing Soonのみ使用する。
/// </summary>
public enum CharacterStatus
{
    Available = 0,
    ComingSoon = 1,
    Locked = 2,
}

/// <summary>
/// TASKS.md「ゼルフのCharacterDataを作成する」用のScriptableObject。
/// 各キャラクターの固定情報(ID・表示名・役割・説明・テーマカラー・Character Status)と、
/// 基礎ステータス・成長値・P/Q/W/E/Rのスキル説明を保持する。
/// CharacterDataはキャラクター固有の初期設定データとして扱い、実行中の現在HPや現在ステータスは
/// 従来どおりCharacterStats / HealthControllerが扱う(SC_Prototype開始時は、キャラクター選択結果を
/// PlayerCharacterApplierがPlayerのCharacterStatsへ適用する。フェーズ4前準備)。
/// スキルの詳細説明は、今後ツールチップや別パネルの表示元として使用できる。
/// </summary>
[CreateAssetMenu(fileName = "CharacterData", menuName = "Game/Character Data")]
public class CharacterData : ScriptableObject
{
    [Header("基本情報")]
    [SerializeField] private string _characterId = "";
    [SerializeField] private string _displayName = "";
    [SerializeField] private string _roleName = "";
    [SerializeField] [TextArea] private string _shortDescription = "";
    [SerializeField] private Color _themeColor = Color.white;
    [SerializeField] private CharacterStatus _characterStatus = CharacterStatus.ComingSoon;

    [Header("基礎ステータス")]
    [SerializeField] private float _baseHp;
    [SerializeField] private float _hpGrowth;
    [SerializeField] private float _baseHpRegeneration;
    [SerializeField] private float _hpRegenerationGrowth;
    [SerializeField] private float _baseAttackDamage;
    [SerializeField] private float _attackDamageGrowth;
    [Tooltip("毎秒の攻撃回数")]
    [SerializeField] private float _baseAttackSpeed;
    [Tooltip("攻撃速度の成長(%)。例: 3 = レベルごとに+3.0%")]
    [SerializeField] private float _attackSpeedGrowthPercent;
    [SerializeField] private float _baseArmor;
    [SerializeField] private float _armorGrowth;
    [SerializeField] private float _baseMoveSpeed;
    [SerializeField] private float _baseAttackRange;

    [Header("スキル説明")]
    [SerializeField] [TextArea] private string _passiveDescription = "";
    [SerializeField] [TextArea] private string _qDescription = "";
    [SerializeField] [TextArea] private string _wDescription = "";
    [SerializeField] [TextArea] private string _eDescription = "";
    [SerializeField] [TextArea] private string _rDescription = "";

    public string CharacterId => _characterId;
    public string DisplayName => _displayName;
    public string RoleName => _roleName;
    public string ShortDescription => _shortDescription;
    public Color ThemeColor => _themeColor;
    public CharacterStatus CharacterStatus => _characterStatus;

    /// <summary>Character StatusがAvailable(選択可能)かどうか。</summary>
    public bool IsAvailable => _characterStatus == CharacterStatus.Available;

    public float BaseHp => _baseHp;
    public float HpGrowth => _hpGrowth;
    public float BaseHpRegeneration => _baseHpRegeneration;
    public float HpRegenerationGrowth => _hpRegenerationGrowth;
    public float BaseAttackDamage => _baseAttackDamage;
    public float AttackDamageGrowth => _attackDamageGrowth;
    public float BaseAttackSpeed => _baseAttackSpeed;
    public float AttackSpeedGrowthPercent => _attackSpeedGrowthPercent;
    public float BaseArmor => _baseArmor;
    public float ArmorGrowth => _armorGrowth;
    public float BaseMoveSpeed => _baseMoveSpeed;
    public float BaseAttackRange => _baseAttackRange;

    public string PassiveDescription => _passiveDescription;
    public string QDescription => _qDescription;
    public string WDescription => _wDescription;
    public string EDescription => _eDescription;
    public string RDescription => _rDescription;
}
