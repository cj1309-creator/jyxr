using Game.Core.Model.Skills;
using Game.Godot.Resources;
using Godot;

namespace Game.Godot.UI;

public partial class SkillSlotBox : TextureButton
{
	private TextureRect _avatar = null!;
	private Label _nameLabel = null!;
	private SkillInstance? _skill;

	public override void _Ready()
	{
		_avatar = GetNode<TextureRect>("%Avatar");
		_nameLabel = GetNode<Label>("%NameLabel");
		Refresh();
	}

	public void Setup(SkillInstance? skill)
	{
		_skill = skill;
		Visible = skill is not null;
		Refresh();
	}

	private void Refresh()
	{
		if (_avatar is null || _nameLabel is null)
		{
			return;
		}

		if (_skill is null)
		{
			_avatar.Texture = null;
			_nameLabel.Text = string.Empty;
			return;
		}

		_avatar.Texture = ResolveTexture(_skill);
		_nameLabel.Text = _skill.Name;
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
}
