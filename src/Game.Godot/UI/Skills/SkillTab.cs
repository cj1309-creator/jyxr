using Game.Core.Model;
using Game.Core.Model.Character;
using Game.Core.Model.Skills;
using Godot;
using Parlot;

namespace Game.Godot.UI;

public partial class SkillTab : Control
{
	public event Action<SkillInstance>? SkillToggleRequested;
	public event Action<SkillInstance>? SkillDetailRequested;

	[Export]
	public PackedScene SkillSlotScene { get; set; } = null!;

	public bool IsReadOnly { get; set; } = true;

	private string? _skillTabType { get; set; }
	private Label _emptyLabel = null!;
	private SkillContainer _skillContainer = null!;

	private GridContainer _gridContainer = null!;

	private CharacterInstance? _character;

	private EquipmentSkillSlotType _lastSlotType = EquipmentSkillSlotType.External;
	private string? _lastSkillId;

	public override void _Ready()
	{
		_skillContainer = GetNode<SkillContainer>("%SkillContainer");
		_gridContainer = GetNode<GridContainer>("%GridContainer");
		_skillContainer.SkillToggleRequested += OnSkillContainerToggleRequested;
		_skillContainer.SkillDetailRequested += OnSkillContainerDetailRequested;
		_skillContainer.Visible = false;

		Refresh();
	}

	public void Setup(CharacterInstance character)
	{
		ArgumentNullException.ThrowIfNull(character);
		_character = character;

		_skillContainer.IsReadOnly = IsReadOnly;
		Refresh();
	}
	private void SyncSlot(
	SkillSlot box)
	{
		var skillInstance = box.GetSkillInstance();

		if (skillInstance is null){
			box.GetSkillBox().Visible = false;
			return;
		}
		box.GetSkillBox().Setup(skillInstance);
	}
	private void Refresh()
	{
		ClearGrid();
		foreach (var skillSlot in CreateSkillSlots())
		{
			_gridContainer.AddChild(skillSlot);
			SyncSlot(skillSlot);
		}

		if (_character is not null && _skillContainer.Visible)
		{
			_skillContainer.Setup(_character, _lastSlotType, _lastSkillId);
		}
		//foreach (var skillSlot in _gridContainer)
		//{
		//    _gridContainer.AddChild(skillSlot);
		//    SyncSlot(skillSlot);
		//}


	}
	private IEnumerable<SkillSlot> CreateSkillSlots()
	{
		var character = _character;
		if (character is null)
		{
			yield break;
		}

		foreach (var externalSkill in character.ExternalSkills)
		{
			var skillSlot = InstantiateSkillSlot();
			skillSlot.SetSkillInstance(externalSkill);
			yield return skillSlot;
		}
	}

	private SkillSlot InstantiateSkillSlot()
	{
		if (SkillSlotScene is null)
		{
			throw new InvalidOperationException("SkillSlotScene is not assigned.");
		}

		var instance = SkillSlotScene.Instantiate();
		if (instance is not SkillSlot skillSlot)
		{
			instance.QueueFree();
			throw new InvalidOperationException("SkillSlot scene root must be SkillSlot.");
		}

		skillSlot.Setup(EquipmentSkillSlotType.External, true);
		skillSlot.Requested += OnSkillSlotRequested;
		skillSlot.ToggleRequested += OnSkillSlotToggleRequested;
		skillSlot.DetailRequested += OnSkillSlotDetailRequested;
		return skillSlot;
	}
	private void ClearGrid()
	{
		foreach (var child in _gridContainer.GetChildren())
		{
			child.QueueFree();
		}
	}
   
	private void OnSkillSlotRequested(SkillSlot skillSlot)
	{
		ArgumentNullException.ThrowIfNull(_character);
		//_skillContainer.Visible= true;
		switch (_skillContainer.Visible) {
			case true:
				_skillContainer.Visible = false;
				break;
			case false:
				_skillContainer.Visible = true;
				break;
		}
		//_skillContainer.SkillToggleRequested += OnSkillContainerToggleRequested;
		//_skillContainer.SkillDetailRequested += OnSkillContainerDetailRequested;
		_skillContainer.IsReadOnly = IsReadOnly;
		_lastSlotType = skillSlot._equipmentSkillSlotType;
		_lastSkillId = skillSlot.GetSkillInstance()?.Id;
		_skillContainer.Setup(_character, _lastSlotType, _lastSkillId);

	}
	private void OnSkillSlotToggleRequested(SkillInstance skill)
	{

		SkillToggleRequested?.Invoke(skill);
	}

	private void OnSkillSlotDetailRequested(SkillInstance skill)
	{
		SkillDetailRequested?.Invoke(skill);
	}
	private void OnSkillContainerToggleRequested(SkillInstance skill)
	{
		SkillToggleRequested?.Invoke(skill);
	}

	private void OnSkillContainerDetailRequested(SkillInstance skill)
	{
		SkillDetailRequested?.Invoke(skill);
	}








}
