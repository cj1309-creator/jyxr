using Godot;

namespace Game.Godot.UI;

[GlobalClass]
public partial class JyButton : TextureButton
{
	[Export]
	public bool UsePressOffset { get; set; } = true;

	[Export]
	public Vector2 PressOffset { get; set; } = new(2, 2);

	[Export]
	public Color PressColor { get; set; } = new(0.8f, 0.8f, 0.8f);

	private Vector2 _originPosition;
	private bool _pressedWithOffset;
	private bool _signalsConnected;

	public override void _EnterTree()
	{
		if (_signalsConnected)
		{
			return;
		}

		ButtonDown += OnButtonDown;
		ButtonUp += OnButtonUp;
		_signalsConnected = true;
	}

	private void OnButtonDown()
	{
		_pressedWithOffset = UsePressOffset;
		if (_pressedWithOffset)
		{
			_originPosition = Position;
			Position = _originPosition + PressOffset;
		}

		Modulate = PressColor;
		OnPressedDown();
	}

	private void OnButtonUp()
	{
		if (_pressedWithOffset)
		{
			Position = _originPosition;
			_pressedWithOffset = false;
		}

		Modulate = Colors.White;
		OnPressedUp();
	}

	protected virtual void OnPressedDown()
	{
	}

	protected virtual void OnPressedUp()
	{
	}
}
