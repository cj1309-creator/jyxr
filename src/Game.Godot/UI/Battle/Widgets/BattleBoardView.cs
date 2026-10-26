using Game.Core.Battle;
using Game.Core.Model;
using Game.Godot.Resources;
using Godot;

namespace Game.Godot.UI.Battle;

public partial class BattleBoardView : Control
{
	private const int CellTextFontSize = 13;
	private const double StepMoveDurationSeconds = 0.3d;
	private const double FloatTextQueueInitialDelaySeconds = 0.1d;
	private const double FloatTextQueueSpacingSeconds = 0.4d;
	private const float FloatTextHeadOffsetY = 90f;
	private const float PerspectiveTopScale = 0.8f;
	private const float PerspectiveYSquash = 0.62f;
	private const float CameraFollowBand = 40f;
	private static readonly Color BorderColor = new(0f, 0f, 0f, 0.45f);
	private static readonly Color TextColor = Colors.White;

	[Export]
	public PackedScene BattleUnitViewScene { get; set; } = null!;

	[Export]
	public PackedScene BattleSkillViewScene { get; set; } = null!;

	[Export]
	public PackedScene BattleFloatTextScene { get; set; } = null!;

	private readonly Dictionary<GridPosition, BattleBoardCellVisual> _cells = [];
	private readonly Dictionary<string, BattleUnitView> _unitViews = new(StringComparer.Ordinal);
	private readonly Dictionary<string, GridPosition> _presentedUnitPositions = new(StringComparer.Ordinal);
	private readonly Dictionary<string, Queue<QueuedFloatText>> _queuedFloatTexts = new(StringComparer.Ordinal);
	private readonly HashSet<string> _processingFloatTextUnits = new(StringComparer.Ordinal);

	private int _gridWidth;
	private int _gridHeight;
	private int _cellWidth;
	private int _cellHeight;
	private int _cellGap;
	private GridPosition? _hoveredCell;
	private Node2D _combatantLayer = null!;
	private Node2D _effectLayer = null!;
	private bool _showBaseBoard = true;
	private Vector2 _homePosition;
	private ProjectiveMap _projection;
	private float _boardRawWidth;
	private float _boardRawHeight;
	private bool _hasProjection;


	private sealed record QueuedFloatText(string Text, BattleFloatTextStyle Style);

	private readonly record struct ProjectiveMap(
		float A, float B, float C,
		float D, float E, float F,
		float G, float H)
	{
		public static ProjectiveMap FromUnitSquare(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3)
		{
			var dx1 = p1.X - p2.X;
			var dx2 = p3.X - p2.X;
			var dx3 = p0.X - p1.X + p2.X - p3.X;
			var dy1 = p1.Y - p2.Y;
			var dy2 = p3.Y - p2.Y;
			var dy3 = p0.Y - p1.Y + p2.Y - p3.Y;

			var denominator = dx1 * dy2 - dx2 * dy1;
			if (Mathf.Abs(dx3) < 1e-6f && Mathf.Abs(dy3) < 1e-6f ||
				Mathf.Abs(denominator) < 1e-6f)
			{
				return new ProjectiveMap(
					p1.X - p0.X, p2.X - p1.X, p0.X,
					p1.Y - p0.Y, p2.Y - p1.Y, p0.Y,
					0f, 0f);
			}

			var g = (dx3 * dy2 - dx2 * dy3) / denominator;
			var h = (dx1 * dy3 - dx3 * dy1) / denominator;
			return new ProjectiveMap(
				p1.X - p0.X + g * p1.X,
				p3.X - p0.X + h * p3.X,
				p0.X,
				p1.Y - p0.Y + g * p1.Y,
				p3.Y - p0.Y + h * p3.Y,
				p0.Y,
				g,
				h);
		}

		public Vector2 Forward(float u, float v)
		{
			var w = G * u + H * v + 1f;
			return new Vector2(
				(A * u + B * v + C) / w,
				(D * u + E * v + F) / w);
		}

		public bool TryInverse(Vector2 point, out Vector2 uv)
		{
			var m00 = A - point.X * G;
			var m01 = B - point.X * H;
			var m10 = D - point.Y * G;
			var m11 = E - point.Y * H;
			var determinant = m00 * m11 - m01 * m10;
			if (Mathf.Abs(determinant) < 1e-6f)
			{
				uv = default;
				return false;
			}

			var r0 = point.X - C;
			var r1 = point.Y - F;
			uv = new Vector2(
				(r0 * m11 - m01 * r1) / determinant,
				(m00 * r1 - r0 * m10) / determinant);
			return true;
		}
	}

	public event Action<GridPosition>? CellActivated;

	public event Action? BackRequested;

	public event Action<GridPosition?>? HoveredCellChanged;

	public event Action? UnitPresentationPositionChanged;

	public event Action<BattleBoardUnitVisual>? UnitHoverEntered;

	public event Action? UnitHoverExited;

	public IReadOnlyDictionary<string, GridPosition> PresentedUnitPositions => _presentedUnitPositions;

	public bool ShowBaseBoard
	{
		get => _showBaseBoard;
		set
		{
			if (_showBaseBoard == value)
			{
				return;
			}

			_showBaseBoard = value;
			QueueRedraw();
		}
	}

	public override void _Ready()
	{
		_combatantLayer = GetNode<Node2D>("%CombatantLayer");
		_effectLayer = GetNode<Node2D>("%EffectLayer");
		_homePosition = Position;
	}

	public void RenderGrid(
		int gridWidth,
		int gridHeight,
		int cellWidth,
		int cellHeight,
		int cellGap,
		IReadOnlyList<BattleBoardCellVisual> cells)
	{
		ArgumentOutOfRangeException.ThrowIfLessThan(gridWidth, 1);
		ArgumentOutOfRangeException.ThrowIfLessThan(gridHeight, 1);
		ArgumentOutOfRangeException.ThrowIfLessThan(cellWidth, 1);
		ArgumentOutOfRangeException.ThrowIfLessThan(cellHeight, 1);
		ArgumentNullException.ThrowIfNull(cells);

		_gridWidth = gridWidth;
		_gridHeight = gridHeight;
		_cellWidth = cellWidth;
		_cellHeight = cellHeight;
		_cellGap = Math.Max(0, cellGap);

		_cells.Clear();
		foreach (var cell in cells)
		{
			_cells[cell.Position] = cell;
		}

		var rawWidth = _gridWidth * _cellWidth + Math.Max(0, _gridWidth - 1) * _cellGap;
		var rawHeight = _gridHeight * _cellHeight + Math.Max(0, _gridHeight - 1) * _cellGap;
		var destinationHeight = rawHeight * PerspectiveYSquash;
		var topInset = rawWidth * (1f - PerspectiveTopScale) * 0.5f;
		_boardRawWidth = rawWidth;
		_boardRawHeight = rawHeight;
		_projection = ProjectiveMap.FromUnitSquare(
			new Vector2(topInset, 0f),
			new Vector2(rawWidth - topInset, 0f),
			new Vector2(rawWidth, destinationHeight),
			new Vector2(0f, destinationHeight));
		_hasProjection = true;
		CustomMinimumSize = new Vector2(rawWidth, destinationHeight);
		QueueRedraw();
	}

	public void RenderUnits(IReadOnlyList<BattleBoardUnitVisual> units)
	{
		ArgumentNullException.ThrowIfNull(units);

		var activeUnitIds = new HashSet<string>(StringComparer.Ordinal);
		foreach (var unit in units)
		{
			activeUnitIds.Add(unit.UnitId);
			_presentedUnitPositions[unit.UnitId] = unit.Position;
			var isExistingView = _unitViews.TryGetValue(unit.UnitId, out var unitView);
			unitView ??= GetOrCreateUnitView(unit.UnitId);
			unitView.Configure(unit);
            
			var targetPosition = ResolveUnitAnchor(unit.Position);
			if (!isExistingView)
			{
				unitView.Position = targetPosition;
				unitView.PlayIdle();
				continue;
			}

			if (unitView.Position.DistanceSquaredTo(targetPosition) > 1f)
			{
				unitView.Position = targetPosition;
				unitView.PlayIdle();
			}
			else
			{
				unitView.Position = targetPosition;
			}
		}

		foreach (var staleUnitId in _unitViews.Keys.Except(activeUnitIds, StringComparer.Ordinal).ToArray())
		{
			if (_unitViews.Remove(staleUnitId, out var staleView))
			{
				staleView.QueueFree();
			}

			_queuedFloatTexts.Remove(staleUnitId);
			_processingFloatTextUnits.Remove(staleUnitId);
			_presentedUnitPositions.Remove(staleUnitId);
		}
	}

	public async Task PlayUnitMoveAsync(
		string unitId,
		IReadOnlyList<GridPosition> path,
		BattleMovementPresentationMode mode)
	{
		if (!_unitViews.TryGetValue(unitId, out var unitView) || path.Count == 0)
		{
			return;
		}

		IReadOnlyList<GridPosition> positions = path;
		if (mode == BattleMovementPresentationMode.Instant)
		{
			positions = [path[^1]];
		}

		var cameraTask = FocusAnchorAsync(
			ResolveUnitAnchor(positions[^1]),
			mode == BattleMovementPresentationMode.Instant ? 0d : StepMoveDurationSeconds * positions.Count);

		unitView.PlayMoveLoop();
		foreach (var position in positions)
		{
			var targetPosition = ResolveUnitAnchor(position);
			if (targetPosition.X < unitView.Position.X)
			{
				unitView.ApplyFacing(BattleFacing.Left);
			}
			else if (targetPosition.X > unitView.Position.X)
			{
				unitView.ApplyFacing(BattleFacing.Right);
			}

			if (mode == BattleMovementPresentationMode.Instant)
			{
				unitView.Position = targetPosition;
				SetPresentedUnitPosition(unitId, position);
				break;
			}

			var midpoint = unitView.Position.Lerp(targetPosition, 0.5f);
			await TweenUnitPositionAsync(unitView, midpoint, StepMoveDurationSeconds * 0.5d);
			SetPresentedUnitPosition(unitId, position);
			await TweenUnitPositionAsync(unitView, targetPosition, StepMoveDurationSeconds * 0.5d);
		}

		unitView.PlayIdle();
		await cameraTask;
	}

	private async Task TweenUnitPositionAsync(BattleUnitView unitView, Vector2 targetPosition, double durationSeconds)
	{
		var tween = CreateTween();
		tween.TweenProperty(unitView, "position", targetPosition, durationSeconds)
			.SetTrans(Tween.TransitionType.Linear)
			.SetEase(Tween.EaseType.InOut);
		await ToSignal(tween, Tween.SignalName.Finished);
	}

	private async Task FocusAnchorAsync(Vector2 anchor, double durationSeconds)
	{
		var target = ResolveCameraPosition(anchor);
		if (Position.IsEqualApprox(target))
		{
			return;
		}

		if (durationSeconds <= 0d)
		{
			Position = target;
			return;
		}

		var tween = CreateTween();
		tween.TweenProperty(this, "position", target, durationSeconds)
			.SetTrans(Tween.TransitionType.Sine)
			.SetEase(Tween.EaseType.InOut);
		await ToSignal(tween, Tween.SignalName.Finished);
	}

	private Vector2 ResolveCameraPosition(Vector2 anchor)
	{
		var parent = GetParent() as Control;
		var parentSize = parent is null ? GetViewportRect().Size : parent.Size;
		var boardSize = CustomMinimumSize;

		var idealX = parentSize.X * 0.4f - anchor.X;
		var idealY = parentSize.Y * 0.4f - anchor.Y;

		// 棋盘放得下时只在初始位置附近小范围跟随；溢出时整轴跟随。
		var targetX = boardSize.X > parentSize.X
			? idealX
			: Mathf.Clamp(idealX, _homePosition.X - CameraFollowBand, _homePosition.X + CameraFollowBand);
		var targetY = boardSize.Y > parentSize.Y
			? idealY
			: Mathf.Clamp(idealY, _homePosition.Y - CameraFollowBand, _homePosition.Y + CameraFollowBand);

		var maxOffsetX = parentSize.X - boardSize.X;
		var maxOffsetY = parentSize.Y - boardSize.Y;
		return new Vector2(
			Mathf.Clamp(targetX, Mathf.Min(0f, maxOffsetX), Mathf.Max(0f, maxOffsetX)),
			Mathf.Clamp(targetY, Mathf.Min(0f, maxOffsetY), Mathf.Max(0f, maxOffsetY)));
	}

	private void SetPresentedUnitPosition(string unitId, GridPosition position)
	{
		if (_presentedUnitPositions.TryGetValue(unitId, out var current) && current == position)
		{
			return;
		}

		_presentedUnitPositions[unitId] = position;
		UnitPresentationPositionChanged?.Invoke();
	}

	public void PlayAttack(string actingUnitId)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(actingUnitId);

		if (_unitViews.TryGetValue(actingUnitId, out var actingView))
		{
			actingView.PlayAttack();
		}
	}

	public void PlayHit(string unitId)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(unitId);

		if (_unitViews.TryGetValue(unitId, out var unitView))
		{
			unitView.PlayHit();
		}
	}

	public double GetUnitAnimationLength(string unitId, string animationName)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(unitId);
		ArgumentException.ThrowIfNullOrWhiteSpace(animationName);

		return _unitViews.TryGetValue(unitId, out var unitView)
			? unitView.GetAnimationLength(animationName)
			: 0d;
	}

	public void PlayHitFlash(string unitId, double durationSeconds)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(unitId);

		if (_unitViews.TryGetValue(unitId, out var unitView))
		{
			unitView.PlayHitFlash(durationSeconds);
		}
	}

	public void StopAllHitFlashes()
	{
		foreach (var unitView in _unitViews.Values)
		{
			if (GodotObject.IsInstanceValid(unitView))
			{
				unitView.StopHitFlash();
			}
		}
	}

	public void ApplyUnitFacing(string unitId, BattleFacing facing)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(unitId);

		if (_unitViews.TryGetValue(unitId, out var unitView))
		{
			unitView.ApplyFacing(facing);
		}
	}

	public async Task PlaySkillImpactAsync(
		IReadOnlyList<GridPosition> impactPositions,
		string? skillAnimationId)
	{
		ArgumentNullException.ThrowIfNull(impactPositions);

		Task? firstImpactTask = null;
		foreach (var impactPosition in impactPositions)
		{
			var impactTask = PlaySkillAnimationAtAsync(ResolveUnitAnchor(impactPosition), skillAnimationId);
			if (firstImpactTask is null)
			{
				firstImpactTask = impactTask;
			}
			else
			{
				_ = impactTask;
			}
		}

		if (firstImpactTask is not null)
		{
			await firstImpactTask;
		}
	}

	public Task PlaySkillAnimationAtAsync(Vector2 position, string? skillAnimationId)
	{
		var skillAnimation = AssetResolver.LoadSkillAnimation(skillAnimationId);
		if (skillAnimation is null)
		{
			return Task.CompletedTask;
		}

		if (BattleSkillViewScene is null)
		{
			throw new InvalidOperationException("BattleSkillViewScene is not assigned.");
		}

		var instance = BattleSkillViewScene.Instantiate();
		if (instance is not BattleSkillView effectView)
		{
			instance.QueueFree();
			throw new InvalidOperationException("Battle skill view scene root must be BattleSkillView.");
		}

		effectView.Position = position;
		_effectLayer.AddChild(effectView);
		return effectView.PlayAsync(skillAnimation);
	}

	public void PlayFloatText(string unitId, string text, BattleFloatTextStyle style)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(unitId);
		ArgumentException.ThrowIfNullOrWhiteSpace(text);

		if (!_unitViews.ContainsKey(unitId))
		{
			return;
		}

		if (!_queuedFloatTexts.TryGetValue(unitId, out var queue))
		{
			queue = new Queue<QueuedFloatText>();
			_queuedFloatTexts[unitId] = queue;
		}

		queue.Enqueue(new QueuedFloatText(text, style));
		if (_processingFloatTextUnits.Add(unitId))
		{
			ProcessFloatTextQueueAsync(unitId);
		}
	}

	public void PlayPopupText(string text, BattleFloatTextStyle style)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(text);

		PlayFloatTextAt(CustomMinimumSize * 0.5f, text, style, popup: true);
	}

	public void PlaySpeech(string unitId, string text)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(unitId);
		ArgumentException.ThrowIfNullOrWhiteSpace(text);

		if (_unitViews.TryGetValue(unitId, out var unitView))
		{
			unitView.ShowSpeech(text);
		}
	}

	public override void _Draw()
	{
		base._Draw();
		var font = GetThemeDefaultFont();
		if (font is null)
		{
			return;
		}

		for (var y = 0; y < _gridHeight; y++)
		{
			for (var x = 0; x < _gridWidth; x++)
			{
				var position = new GridPosition(x, y);
				if (!_cells.TryGetValue(position, out var cell))
				{
					continue;
				}

				var quad = ResolveCellQuad(position);
				if (_showBaseBoard || cell.HasOverlay)
				{
					DrawColoredPolygon(quad, cell.Color);
				}

				if (_showBaseBoard)
				{
					//DrawPolyline([.. quad, quad[0]], BorderColor, 2f, antialiased: true);
					DrawCellText(font, ResolveCellCenter(position), cell.Label);
				}
			}
		}
	}

	public override void _GuiInput(InputEvent @event)
	{
		base._GuiInput(@event);

		switch (@event)
		{
			case InputEventMouseMotion motion:
				UpdateHoveredCell(motion.Position);
				break;
			case InputEventMouseButton { ButtonIndex: MouseButton.Left } button:
				UpdateHoveredCell(button.Position);
				if (!button.Pressed &&
					TryGetCellAt(button.Position, out var position) &&
					_cells.TryGetValue(position, out var cell) &&
					cell.IsInteractive)
				{
					CellActivated?.Invoke(position);
				}
				break;
			case InputEventMouseButton { ButtonIndex: MouseButton.Right, Pressed: false } button:
				UpdateHoveredCell(button.Position);
				BackRequested?.Invoke();
				break;
		}
	}

	public override void _Notification(int what)
	{
		base._Notification(what);
		if (what == NotificationMouseExit && _hoveredCell is not null)
		{
			_hoveredCell = null;
			HoveredCellChanged?.Invoke(null);
		}
	}

	private void UpdateHoveredCell(Vector2 mousePosition)
	{
		GridPosition? hovered = TryGetCellAt(mousePosition, out var position) &&
			_cells.TryGetValue(position, out var cell) &&
			cell.IsInteractive
			? position
			: null;
		if (_hoveredCell == hovered)
		{
			return;
		}

		_hoveredCell = hovered;
		HoveredCellChanged?.Invoke(hovered);
	}

	private bool TryGetCellAt(Vector2 point, out GridPosition position)
	{
		if (!_hasProjection || _cellWidth <= 0 || _cellHeight <= 0)
		{
			position = default;
			return false;
		}

		if (!_projection.TryInverse(point, out var uv))
		{
			position = default;
			return false;
		}

		var gridX = uv.X * _boardRawWidth;
		var gridY = uv.Y * _boardRawHeight;
		var strideX = _cellWidth + _cellGap;
		var strideY = _cellHeight + _cellGap;
		var x = Mathf.FloorToInt(gridX / strideX);
		var y = Mathf.FloorToInt(gridY / strideY);
		if (x < 0 || x >= _gridWidth || y < 0 || y >= _gridHeight)
		{
			position = default;
			return false;
		}

		var localX = gridX - x * strideX;
		var localY = gridY - y * strideY;
		if (localX < 0 || localX > _cellWidth || localY < 0 || localY > _cellHeight)
		{
			position = default;
			return false;
		}

		position = new GridPosition(x, y);
		return true;
	}

	private Vector2 ProjectBoardPoint(float gridX, float gridY)
	{
		if (!_hasProjection || _boardRawWidth <= 0f || _boardRawHeight <= 0f)
		{
			return new Vector2(gridX, gridY);
		}

		return _projection.Forward(gridX / _boardRawWidth, gridY / _boardRawHeight);
	}

	private Vector2 ResolveCellCorner(GridPosition position, float u, float v) =>
		ProjectBoardPoint(
			position.X * (_cellWidth + _cellGap) + u * _cellWidth,
			position.Y * (_cellHeight + _cellGap) + v * _cellHeight);

	private Vector2[] ResolveCellQuad(GridPosition position) =>
	[
		ResolveCellCorner(position, 0f, 0f),
		ResolveCellCorner(position, 1f, 0f),
		ResolveCellCorner(position, 1f, 1f),
		ResolveCellCorner(position, 0f, 1f),
	];

	private Vector2 ResolveCellCenter(GridPosition position) =>
		ResolveCellCorner(position, 0.5f, 0.5f);

	private Vector2 ResolveUnitAnchor(GridPosition position) => ResolveCellCenter(position);

	private BattleUnitView GetOrCreateUnitView(string unitId)
	{
		if (_unitViews.TryGetValue(unitId, out var existingView))
		{
			return existingView;
		}

		if (BattleUnitViewScene is null)
		{
			throw new InvalidOperationException("BattleUnitViewScene is not assigned.");
		}

		var instance = BattleUnitViewScene.Instantiate();
		if (instance is not BattleUnitView unitView)
		{
			instance.QueueFree();
			throw new InvalidOperationException("Battle unit view scene root must be BattleUnitView.");
		}

		unitView.Name = $"Unit_{unitId}";
		unitView.HoverEntered += OnUnitViewHoverEntered;
		unitView.HoverExited += _ => UnitHoverExited?.Invoke();
		_combatantLayer.AddChild(unitView);
		_unitViews[unitId] = unitView;
		return unitView;
	}

	private void OnUnitViewHoverEntered(BattleUnitView unitView)
	{
		if (unitView.Unit is { } unit)
		{
			UnitHoverEntered?.Invoke(unit);
		}
	}

	private void PlayFloatTextAt(Vector2 position, string text, BattleFloatTextStyle style, bool popup = false)
	{
		if (BattleFloatTextScene is null)
		{
			throw new InvalidOperationException("BattleFloatTextScene is not assigned.");
		}

		var instance = BattleFloatTextScene.Instantiate();
		if (instance is not BattleFloatText floatText)
		{
			instance.QueueFree();
			throw new InvalidOperationException("Battle float text scene root must be BattleFloatText.");
		}

		floatText.Position = position;
		_effectLayer.AddChild(floatText);
		if (popup)
		{
			floatText.PlayPopup(text, BattleFloatTextTheme.ResolveColor(style));
			return;
		}

		floatText.Play(text, BattleFloatTextTheme.ResolveColor(style));
	}

	private async void ProcessFloatTextQueueAsync(string unitId)
	{
		try
		{
			await ToSignal(GetTree().CreateTimer(FloatTextQueueInitialDelaySeconds), SceneTreeTimer.SignalName.Timeout);
			while (_queuedFloatTexts.TryGetValue(unitId, out var queue) && queue.Count > 0)
			{
				var item = queue.Dequeue();
				if (_unitViews.TryGetValue(unitId, out var unitView))
				{
					PlayFloatTextAt(unitView.Position + new Vector2(0f, -FloatTextHeadOffsetY), item.Text, item.Style);
				}

				await ToSignal(GetTree().CreateTimer(FloatTextQueueSpacingSeconds), SceneTreeTimer.SignalName.Timeout);
			}
		}
		finally
		{
			_processingFloatTextUnits.Remove(unitId);
			if (_queuedFloatTexts.TryGetValue(unitId, out var queue) && queue.Count == 0)
			{
				_queuedFloatTexts.Remove(unitId);
			}

			if (_queuedFloatTexts.TryGetValue(unitId, out var pendingQueue) &&
				pendingQueue.Count > 0 &&
				_processingFloatTextUnits.Add(unitId))
			{
				ProcessFloatTextQueueAsync(unitId);
			}
		}
	}

	private void DrawCellText(Font font, Vector2 center, string text)
	{
		if (string.IsNullOrWhiteSpace(text))
		{
			return;
		}

		var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
		if (lines.Length == 0)
		{
			return;
		}

		var lineHeight = CellTextFontSize + 6f;
		var totalHeight = lines.Length * lineHeight;
		var startY = center.Y - totalHeight * 0.5f + CellTextFontSize;
		for (var index = 0; index < lines.Length; index++)
		{
			var line = lines[index];
			var size = font.GetStringSize(line, HorizontalAlignment.Left, -1, CellTextFontSize);
			var x = center.X - size.X * 0.5f;
			var y = startY + index * lineHeight;
			DrawString(font, new Vector2(x, y), line, HorizontalAlignment.Left, -1, CellTextFontSize, TextColor);
		}
	}
}

public sealed record BattleBoardCellVisual(
	GridPosition Position,
	string Label,
	Color Color,
	bool IsInteractive,
	bool HasOverlay);

public sealed record BattleBoardUnitVisual(
	string UnitId,
	string Name,
	GridPosition Position,
	BattleFacing Facing,
	AnimationLibrary? AnimationLibrary,
	bool IsActing,
	bool IsAlive,
	bool IsPlayerUnit,
	int Hp,
	int MaxHp,
	int Mp,
	int MaxMp,
	int Rage,
	int ActionGauge,
	Texture2D? PortraitTexture,
	IReadOnlyList<BattleBoardBuffVisual> Buffs);

public sealed record BattleBoardBuffVisual(
	string Name,
	bool IsDebuff,
	int Level,
	int RemainingTurns);

public enum BattleMovementPresentationMode
{
	Instant,
	Step,
}
