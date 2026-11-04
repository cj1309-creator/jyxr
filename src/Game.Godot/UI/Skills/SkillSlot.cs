using Game.Core.Model.Skills;
using Godot;

namespace Game.Godot.UI;

public partial class SkillSlot : JyButton
{
	public event Action<SkillSlot>? Requested;

	[Export]
	public PackedScene TooltipScene { get; set; } = null!;

	private Label _nameLabel = null!;
	private SkillSlotBox _skillSlotBox = null!;
	private SkillInstance? _skill;

	public int SlotIndex { get; private set; } = -1;

	public override void _Ready()
	{
		_nameLabel = GetNode<Label>("%NameLabel");
		_skillSlotBox = GetNode<SkillSlotBox>("%SkillSlotBox");
		Pressed += OnPressed;
		_skillSlotBox.Pressed += OnPressed;
		Refresh();
	}

	public void Setup(int slotIndex)
	{
		ArgumentOutOfRangeException.ThrowIfNegative(slotIndex);
		SlotIndex = slotIndex;
		Refresh();
	}

	public void SetSkill(SkillInstance? skill)
	{
		_skill = skill;
		_skillSlotBox.Setup(skill);
		Refresh();
	}

	public SkillSlotBox GetSkillBox() => _skillSlotBox;

	public SkillInstance? GetSkillInstance() => _skill;

	private void OnPressed() => Requested?.Invoke(this);

	private void Refresh()
	{
		if (!IsInsideTree())
		{
			return;
		}

		_nameLabel.Visible = false;
		_skillSlotBox.Visible = _skill is not null;
	}
}
