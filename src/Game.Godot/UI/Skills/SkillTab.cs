using Game.Core.Model;
using Game.Core.Model.Character;
using Game.Core.Model.Skills;
using Godot;

namespace Game.Godot.UI;

public partial class SkillTab : Control
{
	public event Action<SkillInstance>? SkillToggleRequested;
	public event Action<SkillInstance>? SkillDetailRequested;

	[Export]
	public PackedScene SkillSlotScene { get; set; } = null!;

	public bool IsReadOnly { get; set; } = true;

	private SkillContainer _skillContainer = null!;
	private GridContainer _gridContainer = null!;
	private readonly List<SkillSlot> _slotViews = [];
	private CharacterInstance? _character;
	private int? _selectedSlotIndex;

	public override void _Ready()
	{
		_skillContainer = GetNode<SkillContainer>("%SkillContainer");
		_gridContainer = GetNode<GridContainer>("%GridContainer");
		_skillContainer.SkillToggleRequested += OnSkillContainerToggleRequested;
		_skillContainer.SkillDetailRequested += OnSkillSelected;
		_skillContainer.Visible = false;
		EnsureSlotViews();
		Refresh();
	}

	public void Setup(CharacterInstance character)
	{
		ArgumentNullException.ThrowIfNull(character);
		_character = character;
		_skillContainer.IsReadOnly = IsReadOnly;
		Refresh();
	}

	private void EnsureSlotViews()
	{
		if (_slotViews.Count > 0)
		{
			return;
		}

		if (SkillSlotScene is null)
		{
			throw new InvalidOperationException("SkillSlotScene is not assigned.");
		}

		for (var index = 0; index < CharacterInstance.UsedSkillSlotCount; index++)
		{
			var instance = SkillSlotScene.Instantiate();
			if (instance is not SkillSlot skillSlot)
			{
				instance.QueueFree();
				throw new InvalidOperationException("SkillSlot scene root must be SkillSlot.");
			}

			skillSlot.Setup(index);
			skillSlot.Requested += OnSkillSlotRequested;
			_gridContainer.AddChild(skillSlot);
			_slotViews.Add(skillSlot);
		}
	}

	private void Refresh()
	{
		for (var index = 0; index < _slotViews.Count; index++)
		{
			_slotViews[index].SetSkill(ResolveSlotSkill(index));
		}

		if (_character is not null && _skillContainer.Visible)
		{
			_skillContainer.Setup(_character, EquipmentSkillSlotType.External, ResolveSelectedSkillId());
		}
	}

	private string? ResolveSelectedSkillId() =>
		_character is not null && _selectedSlotIndex is { } slotIndex
			? _character.GetUsedSkillSlot(slotIndex)
			: null;

	private SkillInstance? ResolveSlotSkill(int slotIndex)
	{
		var character = _character;
		if (character is null || slotIndex >= character.UsedSkillSlots.Count)
		{
			return null;
		}

		var skillId = character.UsedSkillSlots[slotIndex];
		return skillId is null ? null : character.GetExternalSkill(skillId);
	}

	private void OnSkillSlotRequested(SkillSlot skillSlot)
	{
		if (_selectedSlotIndex == skillSlot.SlotIndex || _character is null)
		{

			_skillContainer.Visible = !_skillContainer.Visible;

		if (!_skillContainer.Visible || _character is null)
		{
	
			return;
		}
	}

		if (_selectedSlotIndex != skillSlot.SlotIndex)
		{
			_skillContainer.Visible = true;
			_selectedSlotIndex = skillSlot.SlotIndex;
		}

		var skillId= _character.GetUsedSkillSlot(skillSlot.SlotIndex);
		_skillContainer.IsReadOnly = IsReadOnly;
		_skillContainer.Setup(_character, EquipmentSkillSlotType.External, skillId);

	}

	private void OnSkillSelected(SkillInstance skill)
	{
		if (_character is null || _selectedSlotIndex is not { } slotIndex ||
			skill is not ExternalSkillInstance)
		{
			SkillDetailRequested?.Invoke(skill);
			return;
		}

		if (_character.GetUsedSkillSlot(slotIndex) is null)
		{
			Game.CharacterService.SetUsedSkillSlot(_character.Id, slotIndex, skill.Id);
		}
		else if (_character.GetUsedSkillSlot(slotIndex) is not null) {
			Game.CharacterService.RemoveUsedSkillSlot(_character.Id, slotIndex);
		}
	}




	private void OnSkillContainerToggleRequested(SkillInstance skill)
	{
		SkillToggleRequested?.Invoke(skill);
	}
}
