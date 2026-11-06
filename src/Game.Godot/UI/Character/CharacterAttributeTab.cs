using Game.Application.Formatters;
using Game.Core.Battle;
using Game.Core.Model;
using Game.Core.Model.Character;
using Game.Godot.Resources;
using Godot;
using Parlot;

namespace Game.Godot.UI;

public partial class CharacterAttributeTab : Control
{
	private static readonly (StatType StatType, string NodeName)[] DisplayedStats =
	[
		(StatType.Quanzhang, "QuanzhangLabel"),
		(StatType.Jianfa, "JianfaLabel"),
		(StatType.Daofa, "DaofaLabel"),
		(StatType.Qimen, "QimenLabel"),
		(StatType.Bili, "BiliLabel"),
		(StatType.Shenfa, "ShenfaLabel"),
		(StatType.Wuxing, "WuxingLabel"),
		(StatType.Fuyuan, "FuyuanLabel"),
		(StatType.Gengu, "GenguLabel"),
		(StatType.Dingli, "DingliLabel"),
	];

	private static readonly (string ButtonName, StatType StatType)[] AssignableStats =
	[
		("AddBiliButton", StatType.Bili),
		("AddShenfaButton", StatType.Shenfa),
		("AddWuxingButton", StatType.Wuxing),
		("AddFuyuanButton", StatType.Fuyuan),
		("AddGenguButton", StatType.Gengu),
		("AddDingliButton", StatType.Dingli),
	];

    private static readonly IReadOnlyList<(string Label, BattleAiType Value)> BattleAiOptions =
[
    ("均衡", BattleAiType.Basic),
        ("练功", BattleAiType.Training),
        ("强攻", BattleAiType.AttackOnly),
        ("休息", BattleAiType.RestOnly),
    ];

    public string CharacterId
    {
        get => _characterId;
        set
        {
            _characterId = value;
            _character = null;
            _battleUnit = null;
            _mode = CharacterPanelMode.Editable;
            if (IsInsideTree())
            {
                //Render();
            }
        }
    }
    private CharacterInstance? _character;
    private BattleUnit? _battleUnit;
    private CharacterPanelMode _mode = CharacterPanelMode.Editable;
    private TextureRect _avatar = null!;
    private Label _nameLabel = null!;
    private Label _levelValueLabel = null!;
    private Label _hpValueLabel = null!;
    private Label _mpValueLabel = null!;
    private Label _xpValueLabel = null!;
    private Label _attackValueLabel = null!;
    private Label _defenceValueLabel = null!;
    private OptionButton _battleAiOptionButton = null!;
    private JyButton _addPointButton = null!;
	private Label _pointLabel = null!;
	private Control _assignStatWidget = null!;
	private JyButton _assignStatCloseButton = null!;
	private string _characterId = string.Empty;
	private bool _isReadOnly;

	public bool IsReadOnly
	{
		get => _isReadOnly;
		set
		{
			_isReadOnly = value;
			if (_isReadOnly && IsInsideTree())
			{
				HideAssignStatWidget();
			}
		}
	}

	public override void _Ready()
	{
        _avatar = GetNode<TextureRect>("%Avatar");
        _nameLabel = GetNode<Label>("%NameLabel");
        _levelValueLabel = GetNode<Label>("%LevelValueLabel");
        _hpValueLabel = GetNode<Label>("%HpValueLabel");
        _mpValueLabel = GetNode<Label>("%MpValueLabel");
        _xpValueLabel = GetNode<Label>("%XpValueLabel");
        _attackValueLabel = GetNode<Label>("%AttackValueLabel");
        _defenceValueLabel = GetNode<Label>("%DefenceValueLabel");
        _addPointButton = GetNode<JyButton>("%AddPointButton");
		_pointLabel = GetNode<Label>("%PointLabel");
		_assignStatWidget = GetNode<Control>("%AssignStatWidget");
		_assignStatCloseButton = GetNode<JyButton>("%CloseButton");
        _battleAiOptionButton = GetNode<OptionButton>("%BattleAiOptionButton");
        _battleAiOptionButton.ItemSelected += OnBattleAiSelected;

        _addPointButton.Pressed += OnAddPointButtonPressed;
		_assignStatCloseButton.Pressed += HideAssignStatWidget;

        OptionButtonBinder.PopulateEnum(_battleAiOptionButton, BattleAiOptions);
        foreach (var (buttonName, statType) in AssignableStats)
		{
			var button = GetNode<JyButton>($"%{buttonName}");
			button.Pressed += () => OnAssignStatButtonPressed(statType);
		}

		HideAssignStatWidget();
		_addPointButton.Disabled = true;
	}

	public void Setup(CharacterInstance character, BattleUnit? battleUnit)
	{
		ArgumentNullException.ThrowIfNull(character);
        _character = character;
        _characterId = character.Id;
		_pointLabel.Text = character.UnspentStatPoints.ToString();
		_addPointButton.Disabled = IsReadOnly || character.UnspentStatPoints <= 0;
        _battleUnit = battleUnit;
        var portrait = AssetResolver.LoadTexture(character.Portrait);
        if (portrait is not null)
        {
            _avatar.Texture = portrait;
        }
        _nameLabel.Text = character.Name;
        _levelValueLabel.Text = character.Level.ToString();
        _hpValueLabel.Text = battleUnit is null
            ? ToDisplayStat(character.GetStat(StatType.MaxHp)).ToString()
            : $"{battleUnit.Hp}/{battleUnit.MaxHp}";
        _mpValueLabel.Text = battleUnit is null
            ? ToDisplayStat(character.GetStat(StatType.MaxMp)).ToString()
            : $"{battleUnit.Mp}/{battleUnit.MaxMp}";
        _xpValueLabel.Text = character.Level >= Game.Config.MaxLevel
            ? "-/-"
            : FormatExperienceProgress(character);
        var combatStats = CharacterCombatStatFormatter.Calculate(character);
        _attackValueLabel.Text = combatStats.Attack.ToString();
        _defenceValueLabel.Text = combatStats.Defence.ToString();

        if (IsReadOnly || character.UnspentStatPoints <= 0)
		{
			HideAssignStatWidget();
		}

		foreach (var (statType, nodeName) in DisplayedStats)
		{
			var label = GetNode<Label>($"%{nodeName}");
			var baseValue = character.GetBaseStat(statType);
			var finalValue = Mathf.RoundToInt(character.GetStat(statType));
			label.Text = $"{baseValue}(+{finalValue - baseValue})";
		}
	}

	private void OnAddPointButtonPressed()
	{
		if (IsReadOnly || _addPointButton.Disabled)
		{
			return;
		}

		_assignStatWidget.Show();
	}

	private void OnAssignStatButtonPressed(StatType statType)
	{
		if (IsReadOnly)
		{
			return;
		}

		if (string.IsNullOrWhiteSpace(_characterId))
		{
			throw new InvalidOperationException("CharacterAttributeTab is not initialized with a character.");
		}

		Game.CharacterService.AllocateStat(_characterId, statType);
		Game.Audio.PlaySfx("音效.加点");
	}
    private void OnBattleAiSelected(long index)
    {
        if (_mode == CharacterPanelMode.ReadOnly)
        {
            return;
        }

        var character = ResolveCharacter();
        if (character is null)
        {
            return;
        }

        var aiType = OptionButtonBinder.ReadSelectedEnum(_battleAiOptionButton, character.AiType);
        Game.CharacterService.SetBattleAiType(CharacterId, aiType);
    }
    private CharacterInstance? ResolveCharacter()
    {
        if (_character is not null)
        {
            return _character;
        }

        return string.IsNullOrWhiteSpace(CharacterId)
            ? null
            : Game.State.Party.GetMember(CharacterId);
    }

    private void HideAssignStatWidget()
	{
		_assignStatWidget.Hide();
	}
    private static string FormatExperienceProgress(CharacterInstance character)
    {
        var experienceProgress = CharacterLevelProgression.GetDisplayProgress(
            character.Level,
            character.Experience,
            Game.Config.MaxLevel);
        return $"{experienceProgress.CurrentExperience}/{experienceProgress.NextLevelExperience}";
    }
    private static int ToDisplayStat(double value) => Mathf.RoundToInt(value);
}
