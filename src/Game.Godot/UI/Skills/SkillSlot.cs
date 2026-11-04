using Game.Application;
using Game.Core.Model;
using Game.Core.Model.Skills;
using Game.Godot.Resources;
using Godot;
using Parlot;
using System.Globalization;

namespace Game.Godot.UI;

public partial class SkillSlot : JyButton
{
	public event Action<SkillInstance>? ToggleRequested;
	public event Action<SkillInstance>? DetailRequested;

	public event Action<SkillSlot>? Requested;

	[Export]
	public PackedScene TooltipScene { get; set; } = null!;

	private Label _nameLabel = null!;
	//private Label _formNameLabel = null!;
	//private Label _levelLabel = null!;
	private TextureRect _avatar = null!;
	//private TextureButton _activeButton = null!;
	//private TextureRect _checkMark = null!;

	private SkillInstance? _skill;
	private SkillSlotBox _skillSlotBox= null!;
	public EquipmentSkillSlotType _equipmentSkillSlotType { get; set; }

	private bool _showToggleButton = true;
	public bool IsReadOnly { get; set; }

	public override void _Ready()
	{
		_nameLabel = GetNode<Label>("%NameLabel");
		_avatar = GetNode<TextureRect>("%Avatar");
		_skillSlotBox= GetNode<SkillSlotBox>("%SkillSlotBox");
		ConnectSlot(this, _skillSlotBox);

		//_activeButton.Pressed += OnActiveButtonPressed;
		Refresh();
	}

	public void Setup(EquipmentSkillSlotType equipmentSkillSlotType, bool showToggleButton = true)
	{
		//ArgumentNullException.ThrowIfNull(equipmentSkillSlotType);
		_equipmentSkillSlotType = equipmentSkillSlotType;
		_showToggleButton = showToggleButton;
		//TooltipText = skill.Name;
		Refresh();
	}

	private void Refresh()
	{
		if (!IsInsideTree() )
		{
			return;
		}

		//改
		//_avatar.Texture = ResolveTexture(_skill) ?? _avatar.Texture;

		_nameLabel.Visible = false;
	}

	private void ConnectSlot(JyButton slotButton, SkillSlotBox skillSlotBox)
	{
		ArgumentNullException.ThrowIfNull(Requested);
		slotButton.Pressed += () => Requested(this);
		slotButton.GuiInput += inputEvent => OnSlotGuiInput(inputEvent);
		skillSlotBox.EquipmentPrimaryClicked += () => Requested(this);
		//skillSlotBox.EquipmentSecondaryClicked += () => Unequip(this);
	}
	private void OpenSlot()
	{

		//_skillContainer.Setup();

	}
	private void OnSlotGuiInput(InputEvent inputEvent)
	{
		if (IsReadOnly)
		{
			return;
		}

		if (inputEvent is not InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Right })
		{
			return;
		}

		//Unequip(slotType);
		GetViewport().SetInputAsHandled();
	}
	private void OnActiveButtonPressed()
	{
		if (_skill is null || !CanToggle(_skill))
		{
			return;
		}

		ToggleRequested?.Invoke(_skill);
	}
	private void Unequip(EquipmentSlotType slotType)
	{
		if (IsReadOnly)
		{
			return;
		}

		//if (_character is null || _character.GetEquipment(slotType) is null)
		//{
		//    UIRoot.Instance.ShowSuggestion("该装备槽为空。");
		//    return;
		//}

		try
		{
			//var equipment = Game.InventoryService.UnequipToInventory(_character, slotType);
			Game.Audio.PlaySfx("音效.装备");
			//UIRoot.Instance.ShowToast($"卸下【{equipment.Definition.Name}】");
		}
		catch (Exception exception)
		{
			Game.Logger.Error("Unequipping character equipment failed.", exception);
			UIRoot.Instance.ShowSuggestion(exception.Message);
		}
	}

	private static void ApplyLabelColor(Label label, Color color)
	{
		label.AddThemeColorOverride("font_color", color);

		if (label.LabelSettings is null)
		{
			return;
		}

		var labelSettings = (LabelSettings)label.LabelSettings.Duplicate();
		labelSettings.FontColor = color;
		label.LabelSettings = labelSettings;
	}

	private static bool ShouldShowLevel(SkillInstance skill) =>
		skill is ExternalSkillInstance or InternalSkillInstance;

	private static bool CanToggle(SkillInstance skill) =>
		skill is ExternalSkillInstance or InternalSkillInstance or SpecialSkillInstance or FormSkillInstance;

	private static bool ShouldShowFormToggle(FormSkillInstance formSkill) =>
		formSkill.Parent is not InternalSkillInstance { IsEquipped: false };

	private static Color ResolveNameColor(SkillInstance skill) =>
		skill switch
		{
			InternalSkillInstance => Colors.Magenta,
			SpecialSkillInstance => Colors.CornflowerBlue,
			_ => Colors.White,
		};

	private static Texture2D? ResolveTexture(SkillInstance skill)
	{
		var texture = AssetResolver.LoadTexture(skill.Icon);
		if (texture is not null)
		{
			return texture;
		}

		if (skill is FormSkillInstance formSkill)
		{
			return AssetResolver.LoadTexture(formSkill.Parent.Icon);
		}

		return null;
	}
	public SkillSlotBox GetSkillBox() {
		return _skillSlotBox;
	}
	public SkillInstance? GetSkillInstance()
	{
		//ArgumentNullException.ThrowIfNull(_skill);
		return _skill;
	}
	public void SetSkillInstance(SkillInstance skillInstance)
	{

		_skill = skillInstance;
	}
}
