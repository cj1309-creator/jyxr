using Game.Core.Model;
using Game.Core.Model.Character;
using Game.Core.Model.Skills;
using Godot;
using Parlot;

namespace Game.Godot.UI;

public partial class SkillContainer : Control
{
	public event Action<SkillInstance>? SkillToggleRequested;
	public event Action<SkillInstance>? SkillDetailRequested;

	[Export]
	public PackedScene SkillBoxScene { get; set; } = null!;

	public bool IsReadOnly { get; set; } = true;

	private EquipmentSkillSlotType  _equipmentSkillSlotType { get; set; }

	private string? _skillId { get; set; }

	private GridContainer _gridContainer = null!;
	private Label _emptyLabel = null!;
	private readonly List<SkillBox> _boxViews = [];

	private CharacterInstance? _character;

	public override void _Ready()
	{
		_gridContainer = GetNode<GridContainer>("%GridContainer");
		_emptyLabel = GetNode<Label>("%EmptyLabel");
		Refresh();
	}

	public void Setup(CharacterInstance character, EquipmentSkillSlotType equipmentSkillSlotType, string? skillId)
	{
		ArgumentNullException.ThrowIfNull(character);
		_equipmentSkillSlotType = equipmentSkillSlotType;
		_character = character;
		_skillId = skillId;

		Refresh();
	}

	private void Refresh()
	{
		if (!IsInsideTree())
		{
			return;
		}

		if (_character is null)
		{
			ClearGrid();
			_emptyLabel.Visible = true;
			return;
		}

		var skills = EnumerateTabSkills(_character, _equipmentSkillSlotType, _skillId).ToArray();
		_emptyLabel.Visible = skills.Length == 0;
		SyncSkillBoxes(skills);
	}

	private void SyncSkillBoxes(IReadOnlyList<SkillInstance> skills)
	{
		if (NeedsRebuild(skills))
		{
			ClearGrid();
			foreach (var skill in skills)
			{
				var skillBox = CreateSkillBox(skill);
				_gridContainer.AddChild(skillBox);
				_boxViews.Add(skillBox);
			}

			return;
		}

		for (var index = 0; index < skills.Count; index++)
		{
			_boxViews[index].Setup(
				skills[index],
				isInteractive: !IsReadOnly,
				showToggleButton: ShouldShowToggleButton(skills[index]));
		}
	}

	private bool NeedsRebuild(IReadOnlyList<SkillInstance> skills)
	{
		if (_boxViews.Count != skills.Count)
		{
			return true;
		}

		for (var index = 0; index < skills.Count; index++)
		{
			if (!string.Equals(_boxViews[index].SkillId, skills[index].Id, StringComparison.Ordinal))
			{
				return true;
			}
		}

		return false;
	}

	private SkillBox CreateSkillBox(SkillInstance skill)
	{
		if (SkillBoxScene is null)
		{
			throw new InvalidOperationException("SkillBoxScene is not assigned.");
		}

		var instance = SkillBoxScene.Instantiate();
		if (instance is not SkillBox skillBox)
		{
			instance.QueueFree();
			throw new InvalidOperationException("SkillBox scene root must be SkillBox.");
		}
		
		var showToggleButton = ShouldShowToggleButton(skill);

		skillBox.Setup(skill, isInteractive: !IsReadOnly, showToggleButton);
		skillBox.ToggleRequested += OnSkillBoxToggleRequested;
		skillBox.DetailRequested += OnSkillBoxDetailRequested;
		return skillBox;
	}

	private static bool ShouldShowToggleButton(SkillInstance skill) =>
		skill is FormSkillInstance;

	private void OnSkillBoxToggleRequested(SkillInstance skill)
	{
		SkillToggleRequested?.Invoke(skill);
	}

	private void OnSkillBoxDetailRequested(SkillInstance skill)
	{
		SkillDetailRequested?.Invoke(skill);
	}

	private void ClearGrid()
	{
		foreach (var child in _gridContainer.GetChildren())
		{
			child.QueueFree();
		}

		_boxViews.Clear();
	}

	private static IEnumerable<SkillInstance> EnumerateSkills(CharacterInstance character)
	{
		foreach (var skill in character.SpecialSkills)
		{
			yield return skill;
		}

		foreach (var skill in character.ExternalSkills)
		{
			yield return skill;
			foreach (var formSkill in skill.GetFormSkills())
			{
				if (!formSkill.IsUnlocked)
				{
					continue;
				}

				yield return formSkill;
			}
		}

		foreach (var skill in character.InternalSkills)
		{
			yield return skill;
			foreach (var formSkill in skill.GetFormSkills())
			{
				if (!formSkill.IsUnlocked)
				{
					continue;
				}

				yield return formSkill;
			}
		}
	}

	private static IEnumerable<SkillInstance> EnumerateTabSkills(CharacterInstance character, EquipmentSkillSlotType equipmentSkillSlotType, string? SkillId)
	{


		switch (equipmentSkillSlotType) {
			case EquipmentSkillSlotType.Internal:
				foreach (var skill in character.InternalSkills)
				{
					yield return skill;
				}
				break;
			case EquipmentSkillSlotType.Special:
				foreach (var skill in character.SpecialSkills)
				{
					yield return skill;
				}
				break;
			case EquipmentSkillSlotType.External:
				if (SkillId != null) {
					var gskill = character.GetExternalSkill(SkillId);
					if (gskill != null) {
						yield return gskill;
						foreach (var formSkill in gskill.GetFormSkills())
						{
							if (!formSkill.IsUnlocked)
							{
								continue;
							}

							yield return formSkill;
						}
					}
				}
				else if (SkillId==null) { 
				foreach (var skill in character.ExternalSkills)
				{
					yield return skill;
				}				
				}
				break;
			default:

				break;
		}

	}


}
