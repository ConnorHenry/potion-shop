using System;
using System.Collections.Generic;
using Godot;
using OccultShop.Autoload;
using OccultShop.Infrastructure;

namespace OccultShop.UI;

public partial class JuniperGathering : Control
{
	private const float GatheringDurationSeconds = 30.0f;
	private const int RipeBerryCompletionCount = 15;
	private const float FreezeDurationSeconds = 2.0f;
	private const float ShakeDistanceForBurst = 150.0f;
	private const float ShakeBurstCooldownSeconds = 0.16f;
	private const int BerriesPerBurst = 7;
	private const int MaxActiveBerries = 56;
	private const float BerryDiameter = 25.0f;
	private const float BasketWidth = 60.0f;
	private const float BasketHeight = 69.0f;
	private const float CatchLineThickness = 3.0f;
	private const float BasketBottomMargin = 86.0f;
	private const float BushShakeReturnSeconds = 0.08f;
	private const float BushTextureAspectRatio = 780.0f / 560.0f;
	private const float BushVisibleHeightRatio = 0.36f;
	private const float BushMinVisibleHeight = 260.0f;
	private const float BushMaxVisibleHeight = 410.0f;
	private const float BushCanopyYOffsetRatio = 0.08f;
	private const float BushCanopyMinYOffset = 48.0f;
	private const float BushCanopyMaxYOffset = 128.0f;

	[Export] public NodePath PlayAreaPath = default!;
	[Export] public NodePath BushPath = default!;
	[Export] public NodePath BushCanopyPath = default!;
	[Export] public NodePath BasketPath = default!;
	[Export] public NodePath CatchLinePath = default!;
	[Export] public NodePath TimerLabelPath = default!;
	[Export] public NodePath RipeCountLabelPath = default!;
	[Export] public NodePath RewardLabelPath = default!;
	[Export] public NodePath FreezeLabelPath = default!;
	[Export] public NodePath FeedbackLabelPath = default!;
	[Export] public NodePath ResultPromptPath = default!;
	[Export] public NodePath ResultMessagePath = default!;
	[Export] public NodePath ReturnButtonPath = default!;
	[Export] public NodePath GameStatePath = new(AutoloadNodePaths.GameState);
	[Export] public NodePath ItemCatalogPath = new(AutoloadNodePaths.ItemCatalog);
	[Export] public NodePath SaveGameManagerPath = new(AutoloadNodePaths.SaveGameManager);
	[Export] public NodePath SceneTransitionPath = new(AutoloadNodePaths.SceneTransition);
	[Export] public string TargetItemId = "juniper";
	[Export] public string RipeBerryTexturePath = "res://Assets/Gathering/Juniper/juniper_berry_ripe.png";
	[Export] public string WrongBerryRedTexturePath = "res://Assets/Gathering/Juniper/juniper_berry_wrong_red.png";
	[Export] public string WrongBerryAmberTexturePath = "res://Assets/Gathering/Juniper/juniper_berry_wrong_amber.png";
	[Export] public string WrongBerryGreenTexturePath = "res://Assets/Gathering/Juniper/juniper_berry_wrong_green.png";
	[Export] public string WrongBerryPaleBlueTexturePath = "res://Assets/Gathering/Juniper/juniper_berry_wrong_pale_blue.png";

	private readonly List<BerryState> _activeBerries = new();
	private readonly RandomNumberGenerator _random = new();
	private Texture2D _ripeBerryTexture = default!;
	private Texture2D[] _wrongBerryTextures = System.Array.Empty<Texture2D>();
	private Control _playArea = default!;
	private Control _bush = default!;
	private TextureRect _bushCanopy = default!;
	private Control _basket = default!;
	private ColorRect _catchLine = default!;
	private Label _timerLabel = default!;
	private Label _ripeCountLabel = default!;
	private Label _rewardLabel = default!;
	private Label _freezeLabel = default!;
	private Label _feedbackLabel = default!;
	private Control _resultPrompt = default!;
	private Label _resultMessage = default!;
	private Button _returnButton = default!;
	private GameState _gameState = default!;
	private ItemCatalogService _itemCatalog = default!;
	private SaveGameManager _saveGameManager = default!;
	private SceneTransition _sceneTransition = default!;
	private Vector2 _basketDragOffset;
	private Vector2 _lastShakeGlobalPosition;
	private Vector2 _bushBasePosition;
	private Vector2 _bushCanopyBasePosition;
	private float _remainingTime = GatheringDurationSeconds;
	private float _basketFreezeRemaining;
	private float _shakeTravel;
	private float _shakeBurstCooldown;
	private float _bushShakeReturnRemaining;
	private int _ripeCaught;
	private int _wrongCaught;
	private bool _basketDragActive;
	private bool _isShaking;
	private bool _finished;
	private bool _rewardsCommitted;
	private bool _basketInitialized;

	public override void _Ready()
	{
		if (!ResolveNodes())
			return;
		if (!LoadBerryTextures())
			return;

		_random.Randomize();
		_returnButton.Pressed += OnReturnPressed;
		_bush.GuiInput += OnBushGuiInput;
		_basket.GuiInput += OnBasketGuiInput;
		_playArea.Resized += LayoutPlayfield;
		_resultPrompt.Visible = false;
		_bush.MouseDefaultCursorShape = CursorShape.PointingHand;
		_basket.MouseDefaultCursorShape = CursorShape.Drag;
		LayoutPlayfield();
		RefreshStatusLabels();
		SetFeedback("Shake loose berries, then catch only the dark blue ones.");
		ValidateTargetItem();
		TryAutoSave("entering the juniper gathering scene");
	}

	public override void _ExitTree()
	{
		if (_returnButton is not null)
			_returnButton.Pressed -= OnReturnPressed;
		if (_bush is not null)
			_bush.GuiInput -= OnBushGuiInput;
		if (_basket is not null)
			_basket.GuiInput -= OnBasketGuiInput;
		if (_playArea is not null)
			_playArea.Resized -= LayoutPlayfield;
	}

	public override void _Input(InputEvent @event)
	{
		if (_finished)
			return;

		if (@event is InputEventMouseButton mouseButton &&
			mouseButton.ButtonIndex == MouseButton.Left &&
			!mouseButton.Pressed)
		{
			_basketDragActive = false;
			_isShaking = false;
			return;
		}

		if (@event is not InputEventMouseMotion mouseMotion)
			return;

		if (_basketDragActive)
			MoveBasketToMouse(mouseMotion.GlobalPosition);

		if (_isShaking)
			UpdateShake(mouseMotion);
	}

	public override void _Process(double delta)
	{
		if (_finished)
			return;

		var deltaSeconds = (float)delta;
		UpdateCountdown(deltaSeconds);
		if (_finished)
			return;

		UpdateFreeze(deltaSeconds);
		UpdateShakeCooldowns(deltaSeconds);
		UpdateBerries(deltaSeconds);
	}

	private bool ResolveNodes()
	{
		if (!NodeLookup.TryGetRequiredNode(this, PlayAreaPath, nameof(JuniperGathering), nameof(PlayAreaPath), out _playArea))
			return false;
		if (!NodeLookup.TryGetRequiredNode(this, BushPath, nameof(JuniperGathering), nameof(BushPath), out _bush))
			return false;
		if (!NodeLookup.TryGetRequiredNode(this, BushCanopyPath, nameof(JuniperGathering), nameof(BushCanopyPath), out _bushCanopy))
			return false;
		if (!NodeLookup.TryGetRequiredNode(this, BasketPath, nameof(JuniperGathering), nameof(BasketPath), out _basket))
			return false;
		if (!NodeLookup.TryGetRequiredNode(this, CatchLinePath, nameof(JuniperGathering), nameof(CatchLinePath), out _catchLine))
			return false;
		if (!NodeLookup.TryGetRequiredNode(this, TimerLabelPath, nameof(JuniperGathering), nameof(TimerLabelPath), out _timerLabel))
			return false;
		if (!NodeLookup.TryGetRequiredNode(this, RipeCountLabelPath, nameof(JuniperGathering), nameof(RipeCountLabelPath), out _ripeCountLabel))
			return false;
		if (!NodeLookup.TryGetRequiredNode(this, RewardLabelPath, nameof(JuniperGathering), nameof(RewardLabelPath), out _rewardLabel))
			return false;
		if (!NodeLookup.TryGetRequiredNode(this, FreezeLabelPath, nameof(JuniperGathering), nameof(FreezeLabelPath), out _freezeLabel))
			return false;
		if (!NodeLookup.TryGetRequiredNode(this, FeedbackLabelPath, nameof(JuniperGathering), nameof(FeedbackLabelPath), out _feedbackLabel))
			return false;
		if (!NodeLookup.TryGetRequiredNode(this, ResultPromptPath, nameof(JuniperGathering), nameof(ResultPromptPath), out _resultPrompt))
			return false;
		if (!NodeLookup.TryGetRequiredNode(this, ResultMessagePath, nameof(JuniperGathering), nameof(ResultMessagePath), out _resultMessage))
			return false;
		if (!NodeLookup.TryGetRequiredNode(this, ReturnButtonPath, nameof(JuniperGathering), nameof(ReturnButtonPath), out _returnButton))
			return false;

		var gameState = GetNodeOrNull<GameState>(GameStatePath);
		if (gameState is null)
		{
			GD.PushError($"JuniperGathering: GameState was not found at '{GameStatePath}'.");
			return false;
		}

		var itemCatalog = GetNodeOrNull<ItemCatalogService>(ItemCatalogPath);
		if (itemCatalog is null)
		{
			GD.PushError($"JuniperGathering: ItemCatalog was not found at '{ItemCatalogPath}'.");
			return false;
		}

		var saveGameManager = GetNodeOrNull<SaveGameManager>(SaveGameManagerPath);
		if (saveGameManager is null)
		{
			GD.PushError($"JuniperGathering: SaveGameManager was not found at '{SaveGameManagerPath}'.");
			return false;
		}

		var sceneTransition = GetNodeOrNull<SceneTransition>(SceneTransitionPath);
		if (sceneTransition is null)
		{
			GD.PushError($"JuniperGathering: SceneTransition was not found at '{SceneTransitionPath}'.");
			return false;
		}

		_gameState = gameState;
		_itemCatalog = itemCatalog;
		_saveGameManager = saveGameManager;
		_sceneTransition = sceneTransition;
		return true;
	}

	private bool LoadBerryTextures()
	{
		if (!TryLoadTexture(RipeBerryTexturePath, nameof(RipeBerryTexturePath), out _ripeBerryTexture))
			return false;

		var wrongTexturePaths = new[]
		{
			WrongBerryRedTexturePath,
			WrongBerryAmberTexturePath,
			WrongBerryGreenTexturePath,
			WrongBerryPaleBlueTexturePath
		};
		var wrongTextures = new Texture2D[wrongTexturePaths.Length];
		for (var index = 0; index < wrongTexturePaths.Length; index++)
		{
			if (!TryLoadTexture(wrongTexturePaths[index], $"WrongBerryTexture[{index}]", out wrongTextures[index]))
				return false;
		}

		_wrongBerryTextures = wrongTextures;
		return true;
	}

	private bool TryLoadTexture(string texturePath, string propertyName, out Texture2D texture)
	{
		texture = ResourceLoader.Load<Texture2D>(texturePath);
		if (texture is not null)
			return true;

		GD.PushError($"JuniperGathering: Failed to load {propertyName} from '{texturePath}'.");
		return false;
	}

	private void ValidateTargetItem()
	{
		if (!_itemCatalog.TryGetItem(TargetItemId, out _))
			GD.PushError($"JuniperGathering: Target item '{TargetItemId}' is not in the item catalog.");
	}

	private void LayoutPlayfield()
	{
		var playAreaSize = _playArea.Size;
		if (playAreaSize.X <= 0.0f || playAreaSize.Y <= 0.0f)
			return;

		var catchLineY = Mathf.Round(playAreaSize.Y * 0.70f);
		_catchLine.Position = new Vector2(0.0f, catchLineY);
		_catchLine.Size = new Vector2(playAreaSize.X, CatchLineThickness);

		var bushWidth = playAreaSize.X;
		var bushVisibleHeight = Mathf.Round(Math.Clamp(
			playAreaSize.Y * BushVisibleHeightRatio,
			BushMinVisibleHeight,
			BushMaxVisibleHeight));
		var bushTextureHeight = bushWidth / BushTextureAspectRatio;
		var maxCanopyYOffset = Math.Max(0.0f, bushTextureHeight - bushVisibleHeight);
		var preferredCanopyYOffset = Math.Clamp(
			bushTextureHeight * BushCanopyYOffsetRatio,
			BushCanopyMinYOffset,
			BushCanopyMaxYOffset);
		var canopyYOffset = Mathf.Round(Math.Min(preferredCanopyYOffset, maxCanopyYOffset));
		_bush.Size = new Vector2(bushWidth, bushVisibleHeight);
		_bushBasePosition = new Vector2(
			0.0f,
			0.0f);
		_bushCanopy.Size = new Vector2(bushWidth, bushTextureHeight);
		_bushCanopyBasePosition = new Vector2(0.0f, -canopyYOffset);
		if (_bushShakeReturnRemaining <= 0.0f)
		{
			_bush.Position = _bushBasePosition;
			_bushCanopy.Position = _bushCanopyBasePosition;
		}

		_basket.Size = new Vector2(BasketWidth, BasketHeight);
		var basketX = _basketInitialized
			? _basket.Position.X
			: (playAreaSize.X - BasketWidth) * 0.5f;
		var basketY = _basketInitialized
			? _basket.Position.Y
			: playAreaSize.Y - BasketHeight - BasketBottomMargin;
		_basket.Position = new Vector2(ClampBasketX(basketX), basketY);
		_basket.Position = new Vector2(_basket.Position.X, ClampBasketY(_basket.Position.Y));
		_basketInitialized = true;
	}

	private void OnBushGuiInput(InputEvent @event)
	{
		if (_finished)
			return;

		if (@event is not InputEventMouseButton mouseButton ||
			mouseButton.ButtonIndex != MouseButton.Left ||
			!mouseButton.Pressed)
		{
			return;
		}

		_isShaking = true;
		_basketDragActive = false;
		_lastShakeGlobalPosition = mouseButton.GlobalPosition;
		AcceptEvent();
	}

	private void OnBasketGuiInput(InputEvent @event)
	{
		if (_finished || _basketFreezeRemaining > 0.0f)
			return;

		if (@event is not InputEventMouseButton mouseButton ||
			mouseButton.ButtonIndex != MouseButton.Left ||
			!mouseButton.Pressed)
		{
			return;
		}

		_basketDragActive = true;
		_isShaking = false;
		_basketDragOffset = mouseButton.GlobalPosition - _basket.GlobalPosition;
		AcceptEvent();
	}

	private void MoveBasketToMouse(Vector2 mouseGlobalPosition)
	{
		if (_basketFreezeRemaining > 0.0f)
		{
			_basketDragActive = false;
			return;
		}

		var playAreaRect = _playArea.GetGlobalRect();
		var targetGlobalX = mouseGlobalPosition.X - _basketDragOffset.X;
		var targetGlobalY = mouseGlobalPosition.Y - _basketDragOffset.Y;
		var localPosition = new Vector2(
			targetGlobalX - playAreaRect.Position.X,
			targetGlobalY - playAreaRect.Position.Y);
		_basket.Position = new Vector2(ClampBasketX(localPosition.X), ClampBasketY(localPosition.Y));
	}

	private float ClampBasketX(float localX)
	{
		var maxX = Math.Max(0.0f, _playArea.Size.X - _basket.Size.X);
		return Math.Clamp(localX, 0.0f, maxX);
	}

	private float ClampBasketY(float localY)
	{
		var minY = _catchLine.Position.Y + CatchLineThickness;
		var maxY = Math.Max(minY, _playArea.Size.Y - _basket.Size.Y);
		return Math.Clamp(localY, minY, maxY);
	}

	private void UpdateShake(InputEventMouseMotion mouseMotion)
	{
		var distance = mouseMotion.GlobalPosition.DistanceTo(_lastShakeGlobalPosition);
		if (distance <= 0.35f)
			return;

		_shakeTravel += distance;
		_lastShakeGlobalPosition = mouseMotion.GlobalPosition;
		_bushCanopy.Position = _bushCanopyBasePosition + new Vector2(
			Math.Clamp(mouseMotion.Relative.X * 0.22f, -10.0f, 10.0f),
			Math.Clamp(mouseMotion.Relative.Y * 0.08f, -5.0f, 5.0f));
		_bushShakeReturnRemaining = BushShakeReturnSeconds;
		TryReleaseShakeBurst();
	}

	private void TryReleaseShakeBurst()
	{
		if (_shakeTravel < ShakeDistanceForBurst || _shakeBurstCooldown > 0.0f)
			return;

		_shakeTravel -= ShakeDistanceForBurst;
		_shakeBurstCooldown = ShakeBurstCooldownSeconds;
		SpawnBerryBurst();
		SetFeedback("Berries shake loose.");
	}

	private void SpawnBerryBurst()
	{
		var playAreaSize = _playArea.Size;
		if (playAreaSize.X <= BerryDiameter * 2.0f || playAreaSize.Y <= BerryDiameter * 2.0f)
			return;

		for (var index = 0; index < BerriesPerBurst && _activeBerries.Count < MaxActiveBerries; index++)
			SpawnBerry(playAreaSize);
	}

	private void SpawnBerry(Vector2 playAreaSize)
	{
		var isRipe = _random.Randf() < 0.42f;
		var texture = isRipe
			? _ripeBerryTexture
			: _wrongBerryTextures[_random.RandiRange(0, _wrongBerryTextures.Length - 1)];
		var textureSize = texture.GetSize();
		var largestTextureDimension = Math.Max(textureSize.X, textureSize.Y);
		if (largestTextureDimension <= 0.0f)
		{
			GD.PushError($"JuniperGathering: Berry texture '{texture.ResourcePath}' has no drawable size.");
			return;
		}

		var berryScale = BerryDiameter / largestTextureDimension;
		var berry = new Sprite2D
		{
			Name = isRipe ? "RipeJuniperBerry" : "WrongJuniperBerry",
			Texture = texture,
			Centered = true,
			Scale = new Vector2(berryScale, berryScale),
			ZIndex = 12
		};
		_playArea.AddChild(berry);

		var halfBerryDiameter = BerryDiameter * 0.5f;
		var x = _random.RandfRange(halfBerryDiameter, playAreaSize.X - halfBerryDiameter);
		var y = _random.RandfRange(
			_bush.Position.Y + halfBerryDiameter,
			_bush.Position.Y + (_bush.Size.Y * 0.85f));
		var speed = _random.RandfRange(170.0f, 260.0f);
		var berryState = new BerryState(berry, x, y, speed, isRipe);
		_activeBerries.Add(berryState);
		PositionBerry(berryState);
	}

	private void UpdateCountdown(float deltaSeconds)
	{
		_remainingTime = Math.Max(0.0f, _remainingTime - deltaSeconds);
		RefreshStatusLabels();

		if (_remainingTime <= 0.0f)
			FinishGathering();
	}

	private void UpdateFreeze(float deltaSeconds)
	{
		if (_basketFreezeRemaining <= 0.0f)
			return;

		_basketFreezeRemaining = Math.Max(0.0f, _basketFreezeRemaining - deltaSeconds);
		if (_basketFreezeRemaining <= 0.0f)
			SetFeedback("The basket can move again.");

		RefreshStatusLabels();
	}

	private void UpdateShakeCooldowns(float deltaSeconds)
	{
		if (_shakeBurstCooldown > 0.0f)
			_shakeBurstCooldown = Math.Max(0.0f, _shakeBurstCooldown - deltaSeconds);

		if (_bushShakeReturnRemaining <= 0.0f)
			return;

		_bushShakeReturnRemaining = Math.Max(0.0f, _bushShakeReturnRemaining - deltaSeconds);
		if (_bushShakeReturnRemaining <= 0.0f)
			_bushCanopy.Position = _bushCanopyBasePosition;
	}

	private void UpdateBerries(float deltaSeconds)
	{
		var removeBelowY = _playArea.Size.Y + BerryDiameter;
		for (var index = _activeBerries.Count - 1; index >= 0; index--)
		{
			var berry = _activeBerries[index];
			berry.Y += berry.Speed * deltaSeconds;
			PositionBerry(berry);

			if (IsBerryTouchingBasket(berry))
			{
				CollectBerry(index, berry);
				if (_finished)
					break;

				continue;
			}

			if (berry.Y <= removeBelowY)
				continue;

			RemoveBerryAt(index);
		}
	}

	private void PositionBerry(BerryState berry)
	{
		berry.Visual.Position = new Vector2(berry.X, berry.Y);
	}

	private bool IsBerryTouchingBasket(BerryState berry)
	{
		var berryLeft = berry.X - (BerryDiameter * 0.5f);
		var berryTop = berry.Y - (BerryDiameter * 0.5f);
		var berryRight = berryLeft + BerryDiameter;
		var berryBottom = berryTop + BerryDiameter;
		var basketLeft = _basket.Position.X;
		var basketTop = _basket.Position.Y;
		var basketRight = basketLeft + _basket.Size.X;
		var basketBottom = basketTop + _basket.Size.Y;

		return berryRight >= basketLeft &&
			berryLeft <= basketRight &&
			berryBottom >= basketTop &&
			berryTop <= basketBottom;
	}

	private void CollectBerry(int berryIndex, BerryState berry)
	{
		if (berry.IsRipe)
		{
			_ripeCaught += 1;
			SetFeedback("Ripe juniper caught.");
		}
		else
		{
			_wrongCaught += 1;
			_basketFreezeRemaining = FreezeDurationSeconds;
			_basketDragActive = false;
			SetFeedback("Wrong berry. Basket frozen.");
		}

		RefreshStatusLabels();
		RemoveBerryAt(berryIndex);

		if (berry.IsRipe && _ripeCaught >= RipeBerryCompletionCount)
			FinishGathering();
	}

	private void RemoveBerryAt(int index)
	{
		var berry = _activeBerries[index];
		berry.Visual.QueueFree();
		_activeBerries.RemoveAt(index);
	}

	private void RefreshStatusLabels()
	{
		_timerLabel.Text = $"Time: {Mathf.CeilToInt(_remainingTime)}";
		_ripeCountLabel.Text = $"Ripe: {_ripeCaught}";
		_rewardLabel.Text = $"Reward: {CalculateRewardQuantity()} {GetTargetName()}";
		_freezeLabel.Text = _basketFreezeRemaining > 0.0f
			? $"Frozen: {_basketFreezeRemaining:0.0}s"
			: "";
	}

	private int CalculateRewardQuantity()
	{
		if (_ripeCaught >= RipeBerryCompletionCount)
			return 3;
		if (_ripeCaught >= 10)
			return 2;
		if (_ripeCaught >= 5)
			return 1;

		return 0;
	}

	private void FinishGathering()
	{
		if (_finished)
			return;

		_finished = true;
		_basketDragActive = false;
		_isShaking = false;
		RefreshStatusLabels();

		var rewardQuantity = CalculateRewardQuantity();
		var targetName = GetTargetName();
		_resultMessage.Text = rewardQuantity > 0
			? $"Caught {_ripeCaught} ripe berries.\nWrong catches: {_wrongCaught}\n\nReturn to the house to add:\n{targetName} x{rewardQuantity}"
			: $"Caught {_ripeCaught} ripe berries.\nWrong catches: {_wrongCaught}\n\nNo {targetName} gathered this time.";
		_resultPrompt.Visible = true;
		_resultPrompt.MoveToFront();
		_returnButton.GrabFocus();
	}

	private void OnReturnPressed()
	{
		CommitGatheredRewards();
		TryAutoSave("returning from the juniper gathering scene");
		if (ShouldShowWomanInGreenCutscene())
		{
			_sceneTransition.ChangeSceneWithFade(ScenePaths.WomanInGreenCutscene);
			return;
		}

		Error error = GetTree().ChangeSceneToFile(ScenePaths.Main);
		if (error != Error.Ok)
			GD.PushError($"JuniperGathering: Failed to load main scene. Error: {error}");
	}

	private bool ShouldShowWomanInGreenCutscene()
	{
		return _gameState.HasStoryFlag(GameState.TenYearsLaterCutsceneCompletedStoryFlag) &&
			!_gameState.HasStoryFlag(GameState.WomanInGreenCutsceneStartedStoryFlag) &&
			!_gameState.HasStoryFlag(GameState.WomanInGreenCutsceneCompletedStoryFlag);
	}

	private void CommitGatheredRewards()
	{
		if (_rewardsCommitted)
			return;

		var rewardQuantity = CalculateRewardQuantity();
		if (rewardQuantity > 0)
			_gameState.AddItem(TargetItemId, rewardQuantity);

		_rewardsCommitted = true;
	}

	private void SetFeedback(string message)
	{
		_feedbackLabel.Text = message;
	}

	private string GetTargetName()
	{
		return _itemCatalog.TryGetItem(TargetItemId, out var item) && !string.IsNullOrWhiteSpace(item.Name)
			? item.Name
			: TargetItemId;
	}

	private bool TryAutoSave(string context)
	{
		var saveSucceeded = _saveGameManager.SaveGame();
		if (!saveSucceeded)
			GD.PushError($"JuniperGathering: Auto-save failed while {context}.");

		return saveSucceeded;
	}

	private sealed class BerryState
	{
		public BerryState(Sprite2D visual, float x, float y, float speed, bool isRipe)
		{
			Visual = visual;
			X = x;
			Y = y;
			Speed = speed;
			IsRipe = isRipe;
		}

		public Sprite2D Visual { get; }
		public float X { get; }
		public float Speed { get; }
		public bool IsRipe { get; }
		public float Y { get; set; }
	}
}
