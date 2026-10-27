using Game.Core.Battle;
using Godot;

namespace Game.Godot.UI.Battle;

public partial class BattleUnitStateTip : Control
{
    private static readonly Color PlayerBarColor = new(0.78f, 0.18f, 0.18f, 1f);
    private static readonly Color EnemyBarColor = new(0.6f, 0.12f, 0.12f, 1f);
    private static readonly Color ManaBarColor = new(0.2f, 0.48f, 0.9f, 1f);
    private static readonly Color GaugeBarColor = new(0.95f, 0.95f, 0.95f, 1f);
    private static readonly Color RagePipColor = new(1f, 0.78f, 0.1f, 1f);
    private static readonly Color EnemyNameColor = Colors.Red;
    private static readonly Color HitFlashColor = new(1f, 0.35f, 0.35f, 1f);
    private TextureRect _avatar = null!;
    private Label _nameLabel = null!;
    private ProgressBar _hpBar = null!;
    private ProgressBar _mpBar = null!;
    private ProgressBar _gaugeBar = null!;
    private RichTextLabel _buffListLabel = null!;
    public string UnitId { get; private set; } = string.Empty;
    public override void _Ready()
	{
        _avatar = GetNode<TextureRect>("%Avatar");
        _nameLabel = GetNode<Label>("%NameLabel");
        _hpBar = GetNode<ProgressBar>("%HpBar");
        _mpBar = GetNode<ProgressBar>("%MpBar");
        _gaugeBar = GetNode<ProgressBar>("%GaugeBar");
        _buffListLabel = GetNode<RichTextLabel>("%BuffListLabel");

    }
    public void Configure(BattleBoardUnitVisual unit)
    {
        ArgumentNullException.ThrowIfNull(unit);

        UnitId = unit.UnitId;
        _avatar.Texture = unit.PortraitTexture;
        _nameLabel.Text = unit.Name;
        _nameLabel.SelfModulate = unit.IsPlayerUnit ? Colors.White : EnemyNameColor;
        UpdateBar(_hpBar, unit.Hp, unit.MaxHp, unit.IsPlayerUnit ? PlayerBarColor : EnemyBarColor);
        UpdateBar(_mpBar, unit.Mp, unit.MaxMp, ManaBarColor);
        UpdateBar(_gaugeBar, unit.ActionGauge, 100, GaugeBarColor);
        UpdateBuffList(unit.Buffs);

    }

    private static void UpdateBar(ProgressBar bar, int current, int max, Color color)
    {

        bar.MaxValue = Math.Max(max, 1);
        bar.Value = Mathf.Clamp(current, 0, Math.Max(max, 1));
        bar.Modulate = color;
    }
    private void UpdateBuffList(IReadOnlyList<BattleBoardBuffVisual> buffs)
    {
        if (buffs.Count == 0)
        {
            _buffListLabel.Text = string.Empty;
            _buffListLabel.Visible = false;
            return;
        }

        _buffListLabel.Text = string.Join('\n', buffs.Select(FormatBuffLine));
        _buffListLabel.Visible = true;
    }
    private static string FormatBuffLine(BattleBoardBuffVisual buff)
    {
        var levelText = buff.Level == 0 ? string.Empty : buff.Level.ToString();
        var turnsText = buff.RemainingTurns.ToString();
        var color = buff.IsDebuff ? "red" : "yellow";
        return $"[color={color}]{buff.Name}{levelText} {turnsText}[/color]";
    }

}
