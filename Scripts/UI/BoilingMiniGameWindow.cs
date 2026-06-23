using System;
using Godot;
using OccultShop.Models;

namespace OccultShop.UI;

public partial class BoilingMiniGameWindow : Control
{
	[Signal]
	public delegate void CompletedEventHandler(bool succeeded);

	private const float GaugeMin = 0.0f;
	private const float GaugeMax = 1.0f;
	private const float StirringMotionGraceSeconds = 0.25f;
	private const float StirringProgressDecayRate = 0.7f;
	private const float SlowStirringMinRadiansPerSecond = 0.45f;
	private const float SlowStirringMaxRadiansPerSecond = 3.4f;
	private const float FastStirringMinRadiansPerSecond = 1.6f;
	private const float FastStirringMaxRadiansPerSecond = 8.5f;

	[Export] public NodePath IngredientNamePath = default!;
	[Export] public NodePath PhaseLabelPath = default!;
	[Export] public NodePath StatusLabelPath = default!;
	[Export] public NodePath CauldronTexturePath = default!;
	[Export] public NodePath TemperatureTrackPath = default!;
	[Export] public NodePath TemperatureFillPath = default!;
	[Export] public NodePath TemperatureTargetBandPath = default!;
	[Export] public NodePath TemperatureCurrentLinePath = default!;
	[Export] public NodePath BellowsButtonPath = default!;
	[Export] public NodePath LockHeatButtonPath = default!;
	[Export] public NodePath DonenessTrackPath = default!;
	[Export] public NodePath DonenessTargetBandPath = default!;
	[Export] public NodePath DonenessCurrentLinePath = default!;
	[Export] public NodePath TakeOutButtonPath = default!;
	[Export] public NodePath StirringAreaPath = default!;
	[Export] public NodePath StirringProgressFillPath = default!;
	[Export] public NodePath CancelButtonPath = default!;

	private enum BoilingPhase
	{
		Inactive,
		TemperatureControl,
		StirringRhythm
	}

	private Label _ingredientName = default!;
	private Label _phaseLabel = default!;
	private Label _statusLabel = default!;
	private TextureRect _cauldronTexture = default!;
	private Control _temperatureTrack = default!;
	private ColorRect _temperatureFill = default!;
	private ColorRect _temperatureTargetBand = default!;
	private ColorRect _temperatureCurrentLine = default!;
	private Button _bellowsButton = default!;
	private Button _lockHeatButton = default!;
	private Control _donenessTrack = default!;
	private ColorRect _donenessTargetBand = default!;
	private ColorRect _donenessCurrentLine = default!;
	private Button _takeOutButton = default!;
	private Control _stirringArea = default!;
	private ColorRect _stirringProgressFill = default!;
	private Button _cancelButton = default!;
	private BoilingMiniGameDef _config = default!;
	private BoilingPhase _phase = BoilingPhase.Inactive;
	private bool _bellowsPressed;
	private bool _draggingStirring;
	private bool _temperatureReadyToLock;
	private bool _heatLocked;
	private bool _stirringComplete;
	private float _temperatureValue;
	private float _temperatureHoldElapsed;
	private float _heatLockElapsed;
	private float _donenessValue;
	private float _stirringValidElapsed;
	private float _lastValidStirringMotionSeconds = -1.0f;
	private double _lastStirringMotionTimeSeconds;
	private Vector2 _previousStirringVector = Vector2.Zero;

	public override void _Ready()
	{
		_ingredientName = GetNode<Label>(IngredientNamePath);
		_phaseLabel = GetNode<Label>(PhaseLabelPath);
		_statusLabel = GetNode<Label>(StatusLabelPath);
		_cauldronTexture = GetNode<TextureRect>(CauldronTexturePath);
		_temperatureTrack = GetNode<Control>(TemperatureTrackPath);
		_temperatureFill = GetNode<ColorRect>(TemperatureFillPath);
		_temperatureTargetBand = GetNode<ColorRect>(TemperatureTargetBandPath);
		_temperatureCurrentLine = GetNode<ColorRect>(TemperatureCurrentLinePath);
		_bellowsButton = GetNode<Button>(BellowsButtonPath);
		_lockHeatButton = GetNode<Button>(LockHeatButtonPath);
		_donenessTrack = GetNode<Control>(DonenessTrackPath);
		_donenessTargetBand = GetNode<ColorRect>(DonenessTargetBandPath);
		_donenessCurrentLine = GetNode<ColorRect>(DonenessCurrentLinePath);
		_takeOutButton = GetNode<Button>(TakeOutButtonPath);
		_stirringArea = GetNode<Control>(StirringAreaPath);
		_stirringProgressFill = GetNode<ColorRect>(StirringProgressFillPath);
		_cancelButton = GetNode<Button>(CancelButtonPath);

		_bellowsButton.ButtonDown += OnBellowsButtonDown;
		_bellowsButton.ButtonUp += OnBellowsButtonUp;
		_lockHeatButton.Pressed += OnLockHeatPressed;
		_takeOutButton.Pressed += OnTakeOutPressed;
		_cancelButton.Pressed += FailAndClose;
		_stirringArea.GuiInput += OnStirringAreaGuiInput;

		MouseFilter = MouseFilterEnum.Stop;
		HideWindow();
	}

	public override void _ExitTree()
	{
		if (_bellowsButton is not null)
			_bellowsButton.ButtonDown -= OnBellowsButtonDown;
		if (_bellowsButton is not null)
			_bellowsButton.ButtonUp -= OnBellowsButtonUp;
		if (_lockHeatButton is not null)
			_lockHeatButton.Pressed -= OnLockHeatPressed;
		if (_takeOutButton is not null)
			_takeOutButton.Pressed -= OnTakeOutPressed;
		if (_cancelButton is not null)
			_cancelButton.Pressed -= FailAndClose;
		if (_stirringArea is not null)
			_stirringArea.GuiInput -= OnStirringAreaGuiInput;
	}

	public void ShowForIngredient(string ingredientName, string iconPath, BoilingMiniGameDef config)
	{
		_config = config;
		_ingredientName.Text = string.IsNullOrWhiteSpace(ingredientName) ? "Ingredient" : ingredientName;
		Visible = true;
		MoveToFront();
		BeginTemperaturePhase();
		SetProcess(true);
	}

	public override void _Process(double delta)
	{
		var deltaSeconds = (float)delta;
		if (_phase == BoilingPhase.Inactive)
			return;

		ProcessDoneness(deltaSeconds);
		if (_phase == BoilingPhase.Inactive)
			return;

		switch (_phase)
		{
			case BoilingPhase.TemperatureControl:
				ProcessTemperatureControl(deltaSeconds);
				break;
			case BoilingPhase.StirringRhythm:
				ProcessStirringRhythm(deltaSeconds);
				break;
		}

		RefreshGauges();
	}

	private void BeginTemperaturePhase()
	{
		_phase = BoilingPhase.TemperatureControl;
		_temperatureValue = 0.0f;
		_donenessValue = 0.0f;
		_temperatureHoldElapsed = 0.0f;
		_heatLockElapsed = 0.0f;
		_temperatureReadyToLock = false;
		_heatLocked = false;
		_stirringComplete = false;
		_bellowsPressed = false;
		_bellowsButton.Disabled = false;
		_lockHeatButton.Disabled = true;
		_takeOutButton.Disabled = false;
		_stirringArea.MouseFilter = MouseFilterEnum.Ignore;
		_phaseLabel.Text = "Temperature Control";
		_statusLabel.Text = "Hold the heat inside the marked band.";
	}

	private void BeginStirringPhase()
	{
		_phase = BoilingPhase.StirringRhythm;
		_stirringValidElapsed = 0.0f;
		_lastValidStirringMotionSeconds = -1.0f;
		_lastStirringMotionTimeSeconds = 0.0;
		_draggingStirring = false;
		_bellowsButton.Disabled = true;
		_lockHeatButton.Disabled = true;
		_takeOutButton.Disabled = false;
		_stirringArea.MouseFilter = MouseFilterEnum.Stop;
		_phaseLabel.Text = "Stirring Rhythm";
		_statusLabel.Text = BuildStirringStatusText();
	}

	private void ProcessTemperatureControl(float delta)
	{
		if (_temperatureReadyToLock)
		{
			_heatLockElapsed += delta;
			if (_heatLockElapsed > _config.HeatLockSeconds)
				FailAndClose();

			return;
		}

		var rate = _bellowsPressed ? _config.HeatRiseRate : -_config.HeatFallRate;
		_temperatureValue = Mathf.Clamp(_temperatureValue + (rate * delta), GaugeMin, GaugeMax);
		if (_temperatureValue >= _config.TemperatureTargetMin && _temperatureValue <= _config.TemperatureTargetMax)
		{
			_temperatureHoldElapsed += delta;
			if (_temperatureHoldElapsed >= _config.TemperatureHoldSeconds)
			{
				_temperatureReadyToLock = true;
				_heatLockElapsed = 0.0f;
				_bellowsButton.Disabled = true;
				_lockHeatButton.Disabled = false;
				_statusLabel.Text = "Heat is steady. Lock it now.";
			}

			return;
		}

		_temperatureHoldElapsed = 0.0f;
	}

	private void ProcessDoneness(float delta)
	{
		_donenessValue = Mathf.Clamp(
			_donenessValue + (delta / Mathf.Max(0.01f, _config.DonenessDurationSeconds)),
			GaugeMin,
			GaugeMax);

		if (_donenessValue > _config.DonenessWindowEnd)
			FailAndClose();
	}

	private void ProcessStirringRhythm(float delta)
	{
		if (_stirringComplete)
		{
			RefreshCompletionStatus();
			return;
		}

		var recentValidMotion =
			_lastValidStirringMotionSeconds >= 0.0f &&
			(float)Time.GetTicksMsec() / 1000.0f - _lastValidStirringMotionSeconds <= StirringMotionGraceSeconds;

		if (recentValidMotion)
			_stirringValidElapsed += delta;
		else
			_stirringValidElapsed = Mathf.Max(0.0f, _stirringValidElapsed - (delta * StirringProgressDecayRate));

		if (_stirringValidElapsed >= _config.StirringHoldSeconds)
		{
			_stirringComplete = true;
			_stirringArea.MouseFilter = MouseFilterEnum.Ignore;
			RefreshCompletionStatus();
		}
	}

	private void OnBellowsButtonDown()
	{
		_bellowsPressed = true;
	}

	private void OnBellowsButtonUp()
	{
		_bellowsPressed = false;
	}

	private void OnLockHeatPressed()
	{
		if (_phase != BoilingPhase.TemperatureControl || !_temperatureReadyToLock)
			return;

		_heatLocked = true;
		BeginStirringPhase();
	}

	private void OnTakeOutPressed()
	{
		if (_phase == BoilingPhase.Inactive)
			return;

		if (!_heatLocked || !_stirringComplete)
		{
			FailAndClose();
			return;
		}

		if (_donenessValue < _config.DonenessWindowStart || _donenessValue > _config.DonenessWindowEnd)
		{
			FailAndClose();
			return;
		}

		CompleteAndClose();
	}

	private void OnStirringAreaGuiInput(InputEvent @event)
	{
		if (_phase != BoilingPhase.StirringRhythm)
			return;

		if (@event is InputEventMouseButton mouseButton && mouseButton.ButtonIndex == MouseButton.Left)
		{
			_draggingStirring = mouseButton.Pressed;
			if (_draggingStirring)
			{
				_previousStirringVector = BuildStirringVector(mouseButton.GlobalPosition);
				_lastStirringMotionTimeSeconds = Time.GetTicksMsec() / 1000.0;
			}

			AcceptEvent();
			return;
		}

		if (!_draggingStirring || @event is not InputEventMouseMotion mouseMotion)
			return;

		var currentVector = BuildStirringVector(mouseMotion.GlobalPosition);
		if (_previousStirringVector.LengthSquared() <= 0.0001f || currentVector.LengthSquared() <= 0.0001f)
		{
			_previousStirringVector = currentVector;
			return;
		}

		var nowSeconds = Time.GetTicksMsec() / 1000.0;
		var elapsedSeconds = Math.Max(0.01, nowSeconds - _lastStirringMotionTimeSeconds);
		var cross = (_previousStirringVector.X * currentVector.Y) - (_previousStirringVector.Y * currentVector.X);
		var angleDelta = MathF.Abs(_previousStirringVector.AngleTo(currentVector));
		var clockwise = cross > 0.0f;
		var radiansPerSecond = angleDelta / (float)elapsedSeconds;

		if (clockwise == ExpectsClockwise() && IsExpectedStirringSpeed(radiansPerSecond))
			_lastValidStirringMotionSeconds = (float)nowSeconds;

		_previousStirringVector = currentVector;
		_lastStirringMotionTimeSeconds = nowSeconds;
		AcceptEvent();
	}

	private Vector2 BuildStirringVector(Vector2 globalPosition)
	{
		var rect = _stirringArea.GetGlobalRect();
		return globalPosition - (rect.Position + (rect.Size * 0.5f));
	}

	private bool ExpectsClockwise()
	{
		return _config.StirringRhythm is BoilingStirringRhythm.ClockwiseSlow or BoilingStirringRhythm.ClockwiseFast;
	}

	private bool ExpectsFast()
	{
		return _config.StirringRhythm is BoilingStirringRhythm.ClockwiseFast or BoilingStirringRhythm.AntiClockwiseFast;
	}

	private bool IsExpectedStirringSpeed(float radiansPerSecond)
	{
		return ExpectsFast()
			? radiansPerSecond >= FastStirringMinRadiansPerSecond && radiansPerSecond <= FastStirringMaxRadiansPerSecond
			: radiansPerSecond >= SlowStirringMinRadiansPerSecond && radiansPerSecond <= SlowStirringMaxRadiansPerSecond;
	}

	private string BuildStirringStatusText()
	{
		return _config.StirringRhythm switch
		{
			BoilingStirringRhythm.ClockwiseSlow => "Stir clockwise, steady and slow.",
			BoilingStirringRhythm.ClockwiseFast => "Stir clockwise with a quick rhythm.",
			BoilingStirringRhythm.AntiClockwiseSlow => "Stir anti-clockwise, steady and slow.",
			BoilingStirringRhythm.AntiClockwiseFast => "Stir anti-clockwise with a quick rhythm.",
			_ => "Stir the cauldron."
		};
	}

	private void RefreshCompletionStatus()
	{
		if (!_stirringComplete)
			return;

		_phaseLabel.Text = "Take Out";
		_statusLabel.Text = _donenessValue < _config.DonenessWindowStart
			? "Wait for the marked doneness band, then take it out."
			: "Take it out now.";
	}

	private void RefreshGauges()
	{
		UpdateVerticalBand(_temperatureTargetBand, _temperatureTrack, _config?.TemperatureTargetMin ?? 0.0f, _config?.TemperatureTargetMax ?? 0.0f);
		UpdateVerticalFill(_temperatureFill, _temperatureTrack, _temperatureValue);
		UpdateVerticalLine(_temperatureCurrentLine, _temperatureTrack, _temperatureValue);

		UpdateVerticalBand(_donenessTargetBand, _donenessTrack, _config?.DonenessWindowStart ?? 0.0f, _config?.DonenessWindowEnd ?? 0.0f);
		UpdateVerticalLine(_donenessCurrentLine, _donenessTrack, _donenessValue);
		UpdateHorizontalFill(_stirringProgressFill, _stirringValidElapsed / Mathf.Max(0.01f, _config?.StirringHoldSeconds ?? 1.0f));
	}

	private static void UpdateVerticalBand(Control band, Control track, float min, float max)
	{
		var height = track.Size.Y;
		var top = height * (1.0f - Mathf.Clamp(max, GaugeMin, GaugeMax));
		var bottom = height * (1.0f - Mathf.Clamp(min, GaugeMin, GaugeMax));
		band.Position = new Vector2(0.0f, top);
		band.Size = new Vector2(track.Size.X, Math.Max(2.0f, bottom - top));
	}

	private static void UpdateVerticalFill(Control fill, Control track, float value)
	{
		var clampedValue = Mathf.Clamp(value, GaugeMin, GaugeMax);
		var fillHeight = track.Size.Y * clampedValue;
		fill.Position = new Vector2(0.0f, track.Size.Y - fillHeight);
		fill.Size = new Vector2(track.Size.X, fillHeight);
	}

	private static void UpdateVerticalLine(Control line, Control track, float value)
	{
		var y = track.Size.Y * (1.0f - Mathf.Clamp(value, GaugeMin, GaugeMax));
		line.Position = new Vector2(0.0f, y - 2.0f);
		line.Size = new Vector2(track.Size.X, 4.0f);
	}

	private static void UpdateHorizontalFill(Control fill, float value)
	{
		if (fill.GetParent() is not Control parent)
			return;

		fill.Position = Vector2.Zero;
		fill.Size = new Vector2(parent.Size.X * Mathf.Clamp(value, GaugeMin, GaugeMax), parent.Size.Y);
	}

	private void CompleteAndClose()
	{
		HideWindow();
		EmitSignal(SignalName.Completed, true);
	}

	private void FailAndClose()
	{
		HideWindow();
		EmitSignal(SignalName.Completed, false);
	}

	private void HideWindow()
	{
		_phase = BoilingPhase.Inactive;
		_bellowsPressed = false;
		_draggingStirring = false;
		_heatLocked = false;
		_stirringComplete = false;
		Visible = false;
		SetProcess(false);
	}
}
