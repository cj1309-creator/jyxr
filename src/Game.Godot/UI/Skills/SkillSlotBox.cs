using Game.Core.Model;
using Game.Core.Model.Skills;
using Game.Godot.Resources;
using Godot;
using System;

public partial class SkillSlotBox : TextureButton
{
	// Called when the node enters the scene tree for the first time.
	public event Action? EquipmentPrimaryClicked;
	public event Action? EquipmentSecondaryClicked;
	private TextureRect _avatar = null!;
	private Label _nameLabel = null!;
	private ExternalSkillInstance? _Ebox;
	private InternalSkillInstance? _Ibox;
	private SpecialSkillInstance? _Sbox;

	public override void _Ready()
	{
		_avatar = GetNode<TextureRect>("%Avatar");
		_nameLabel = GetNode<Label>("%NameLabel");
	}
	public void Setup(SkillInstance? skillInstance)
	{
		ArgumentNullException.ThrowIfNull(skillInstance);
		switch (skillInstance) {

			case ExternalSkillInstance externalSkillInstance:
				_Ebox = externalSkillInstance;

				break;
			case SpecialSkillInstance:
				break;
		
		}
		//_Ebox = equipment;
		//TooltipText = equipment.Definition.Name;
		Refresh();
		Visible = true;
	}

	private void Refresh()
	{
		if (!IsInsideTree()) return;
		var skill = _Ebox ?? (SkillInstance?)_Ibox ?? _Sbox;
		if (skill is null) return;
		_avatar.Texture = ResolveTexture(skill) ?? _avatar.Texture;
		_nameLabel.Text = skill.Name;
	}
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
	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
	}
}
