using System;
using Godot;
using OccultShop.Controllers;

namespace OccultShop.UI;

public partial class ShopFloor : Control
{
	[Export] public NodePath PotionBrewingStationButtonPath = new("Hotspots/InventoryShelf");
	[Export] public NodePath CustomerButtonPath = new("Hotspots/Customer");
	[Export] public NodePath BrewButtonPath = new("Hotspots/Counter");
	[Export] public NodePath PotionBookButtonPath = new("Hotspots/PotionBook");
	[Export] public NodePath TreatmentTrayButtonPath = new("Hotspots/TreatmentTray");
	[Export] public NodePath InventoryPanelPath = new("../InventoryPanel");
	[Export] public NodePath CustomerPanelPath = new("../CustomerPanel");
	[Export] public NodePath BrewPanelPath = new("../PotionBrewingStationView/BrewPanel");
	[Export] public NodePath PotionBookPanelPath = new("../PotionBookPanel");
	[Export] public NodePath TreatmentTrayPanelPath = new("../TreatmentTray");
	[Export] public NodePath CustomerCloseupViewPath = new("../CustomerCloseupView");
	[Export] public NodePath CustomerCloseupCustomerImagePath = new("../CustomerCloseupView/Customer");
	[Export] public NodePath CustomerCloseupReturnButtonPath = new("../CustomerCloseupView/ReturnHotspot");
	[Export] public NodePath PotionBookCloseupViewPath = new("../PotionBookCloseupView");
	[Export] public NodePath PotionBookCloseupReturnButtonPath = new("../PotionBookCloseupView/ReturnHotspot");
	[Export] public NodePath PotionBookCloseupLeftReturnButtonPath = new("../PotionBookCloseupView/ReturnHotspotLeft");
	[Export] public NodePath PotionBookCloseupRightReturnButtonPath = new("../PotionBookCloseupView/ReturnHotspotRight");
	[Export] public NodePath PotionBookCloseupBottomReturnButtonPath = new("../PotionBookCloseupView/ReturnHotspotBottom");
	[Export] public NodePath PotionBrewingStationViewPath = new("../PotionBrewingStationView");
	[Export] public NodePath PotionBrewingStationReturnButtonPath = new("../PotionBrewingStationView/ReturnHotspotLeft");
	[Export] public NodePath HudPath = new("../Hud");
	[Export] public NodePath DayControllerPath = new("/root/Main/DayController");
	[Export] public bool HideInventoryOnReady = true;
	[Export] public bool KeepInventoryVisibleInCustomerCloseup = true;

	private Button? _potionBrewingStationButton;
	private Button? _customerButton;
	private Button? _brewButton;
	private Button? _potionBookButton;
	private Button? _treatmentTrayButton;
	private Button? _customerCloseupReturnButton;
	private Button? _potionBookCloseupReturnButton;
	private Button? _potionBookCloseupLeftReturnButton;
	private Button? _potionBookCloseupRightReturnButton;
	private Button? _potionBookCloseupBottomReturnButton;
	private Button? _potionBrewingStationReturnButton;
	private Control? _customerCloseupView;
	private TextureRect? _customerCloseupCustomerImage;
	private Texture2D? _defaultCustomerCloseupCustomerTexture;
	private Control? _potionBookCloseupView;
	private Control? _potionBrewingStationView;
	private Control? _hud;
	private InventoryPanel? _inventoryPanel;
	private CustomerPanel? _customerPanel;
	private BrewPanel? _brewPanel;
	private PotionBookPanel? _potionBookPanel;
	private TreatmentTray? _treatmentTray;
	private DayController? _dayController;
	private bool _hudWasVisible;
	private bool _inventoryWasVisible;
	private bool _brewWasVisible;
	private bool _potionBookWasVisible;
	private bool _treatmentTrayWasVisible;

	public override void _Ready()
	{
		_potionBrewingStationButton = GetRequiredButton(PotionBrewingStationButtonPath, nameof(PotionBrewingStationButtonPath));
		_customerButton = GetRequiredButton(CustomerButtonPath, nameof(CustomerButtonPath));
		_brewButton = GetRequiredButton(BrewButtonPath, nameof(BrewButtonPath));
		_potionBookButton = GetRequiredButton(PotionBookButtonPath, nameof(PotionBookButtonPath));
		_treatmentTrayButton = GetRequiredButton(TreatmentTrayButtonPath, nameof(TreatmentTrayButtonPath));
		_customerCloseupReturnButton = GetRequiredButton(CustomerCloseupReturnButtonPath, nameof(CustomerCloseupReturnButtonPath));
		_potionBookCloseupReturnButton = GetRequiredButton(PotionBookCloseupReturnButtonPath, nameof(PotionBookCloseupReturnButtonPath));
		_potionBookCloseupLeftReturnButton = GetNodeOrNull<Button>(PotionBookCloseupLeftReturnButtonPath);
		_potionBookCloseupRightReturnButton = GetNodeOrNull<Button>(PotionBookCloseupRightReturnButtonPath);
		_potionBookCloseupBottomReturnButton = GetNodeOrNull<Button>(PotionBookCloseupBottomReturnButtonPath);
		_potionBrewingStationReturnButton = GetRequiredButton(PotionBrewingStationReturnButtonPath, nameof(PotionBrewingStationReturnButtonPath));

		_customerCloseupView = GetOptionalNode<Control>(CustomerCloseupViewPath, nameof(CustomerCloseupViewPath));
		_customerCloseupCustomerImage = GetOptionalNode<TextureRect>(
			CustomerCloseupCustomerImagePath,
			nameof(CustomerCloseupCustomerImagePath));
		_defaultCustomerCloseupCustomerTexture = _customerCloseupCustomerImage?.Texture;
		_potionBookCloseupView = GetOptionalNode<Control>(PotionBookCloseupViewPath, nameof(PotionBookCloseupViewPath));
		_potionBrewingStationView = GetOptionalNode<Control>(PotionBrewingStationViewPath, nameof(PotionBrewingStationViewPath));
		_hud = GetOptionalNode<Control>(HudPath, nameof(HudPath));
		_inventoryPanel = GetOptionalNode<InventoryPanel>(InventoryPanelPath, nameof(InventoryPanelPath));
		_customerPanel = GetOptionalNode<CustomerPanel>(CustomerPanelPath, nameof(CustomerPanelPath));
		_brewPanel = GetOptionalNode<BrewPanel>(BrewPanelPath, nameof(BrewPanelPath));
		_potionBookPanel = GetOptionalNode<PotionBookPanel>(PotionBookPanelPath, nameof(PotionBookPanelPath));
		_treatmentTray = GetOptionalNode<TreatmentTray>(TreatmentTrayPanelPath, nameof(TreatmentTrayPanelPath));
		_dayController = GetOptionalNode<DayController>(DayControllerPath, nameof(DayControllerPath));

		ConnectButton(_potionBrewingStationButton, OnPotionBrewingStationPressed);
		ConnectButton(_customerButton, OnCustomerPressed);
		ConnectButton(_brewButton, OnBrewPressed);
		ConnectButton(_potionBookButton, OnPotionBookPressed);
		ConnectButton(_treatmentTrayButton, OnTreatmentTrayPressed);
		ConnectButton(_customerCloseupReturnButton, OnReturnToShopFloorPressed);
		ConnectButton(_potionBookCloseupReturnButton, OnReturnToShopFloorPressed);
		ConnectButton(_potionBookCloseupLeftReturnButton, OnReturnToShopFloorPressed);
		ConnectButton(_potionBookCloseupRightReturnButton, OnReturnToShopFloorPressed);
		ConnectButton(_potionBookCloseupBottomReturnButton, OnReturnToShopFloorPressed);
		ConnectButton(_potionBrewingStationReturnButton, OnReturnToShopFloorPressed);

		Callable.From(ApplyInitialPanelState).CallDeferred();
	}

	public override void _ExitTree()
	{
		DisconnectButton(_potionBrewingStationButton, OnPotionBrewingStationPressed);
		DisconnectButton(_customerButton, OnCustomerPressed);
		DisconnectButton(_brewButton, OnBrewPressed);
		DisconnectButton(_potionBookButton, OnPotionBookPressed);
		DisconnectButton(_treatmentTrayButton, OnTreatmentTrayPressed);
		DisconnectButton(_customerCloseupReturnButton, OnReturnToShopFloorPressed);
		DisconnectButton(_potionBookCloseupReturnButton, OnReturnToShopFloorPressed);
		DisconnectButton(_potionBookCloseupLeftReturnButton, OnReturnToShopFloorPressed);
		DisconnectButton(_potionBookCloseupRightReturnButton, OnReturnToShopFloorPressed);
		DisconnectButton(_potionBookCloseupBottomReturnButton, OnReturnToShopFloorPressed);
		DisconnectButton(_potionBrewingStationReturnButton, OnReturnToShopFloorPressed);
	}

	private void ApplyInitialPanelState()
	{
		if (HideInventoryOnReady && _inventoryPanel is not null)
			_inventoryPanel.Visible = false;

		if (_customerCloseupView is not null)
			_customerCloseupView.Visible = false;
		if (_potionBookCloseupView is not null)
			_potionBookCloseupView.Visible = false;
		if (_potionBrewingStationView is not null)
			_potionBrewingStationView.Visible = false;
	}

	private void OnPotionBrewingStationPressed()
	{
		OpenPotionBrewingStation();
	}

	private void OnCustomerPressed()
	{
		if (_customerPanel is not null && _customerPanel.Visible)
		{
			ShowCustomerCloseup();
			return;
		}

		if (_dayController is null)
		{
			ShowControl(_customerPanel, "CustomerPanel");
			ShowCustomerCloseup();
			return;
		}

		if (!_dayController.IsShopOpen)
		{
			_dayController.StartShopDay();
			if (_customerPanel is not null && _customerPanel.Visible)
				ShowCustomerCloseup();
			return;
		}

		if (_customerPanel is not null && _customerPanel.HasActiveInteraction)
		{
			ShowControl(_customerPanel, "CustomerPanel");
			ShowCustomerCloseup();
			return;
		}

		GD.PushError("ShopFloor: Customer screen requested, but no active customer is available.");
	}

	private void ShowCustomerCloseup()
	{
		if (_customerCloseupView is null)
		{
			GD.PushError("ShopFloor: CustomerCloseupView was not found.");
			return;
		}

		RefreshCustomerCloseupImage();
		StoreShopFloorPanelState();
		Visible = false;
		if (_hud is not null)
			_hud.Visible = false;
		if (_brewPanel is not null)
			_brewPanel.Visible = false;
		if (_potionBookPanel is not null)
			_potionBookPanel.Visible = false;
		if (_treatmentTray is not null)
			_treatmentTray.Visible = false;
		if (!KeepInventoryVisibleInCustomerCloseup && _inventoryPanel is not null)
			_inventoryPanel.Visible = false;

		_customerCloseupView.Visible = true;
		_customerCloseupView.MoveToFront();

		if (_inventoryPanel is not null && _inventoryPanel.Visible)
			_inventoryPanel.MoveToFront();

		if (_customerPanel is not null)
		{
			_customerPanel.Visible = true;
			_customerPanel.MoveToFront();
		}
	}

	private void RefreshCustomerCloseupImage()
	{
		if (_customerCloseupCustomerImage is null)
			return;

		var imagePath = _customerPanel?.CurrentCustomerImagePath;
		if (string.IsNullOrWhiteSpace(imagePath))
		{
			SetFallbackCustomerCloseupImage();
			return;
		}

		var texture = ResourceLoader.Load<Texture2D>(imagePath);
		if (texture is null)
		{
			GD.PushError($"ShopFloor: Customer image could not be loaded from '{imagePath}'.");
			SetFallbackCustomerCloseupImage();
			return;
		}

		_customerCloseupCustomerImage.Texture = texture;
		_customerCloseupCustomerImage.Visible = true;
	}

	private void SetFallbackCustomerCloseupImage()
	{
		if (_customerCloseupCustomerImage is null)
			return;

		_customerCloseupCustomerImage.Texture = _defaultCustomerCloseupCustomerTexture;
		_customerCloseupCustomerImage.Visible = _defaultCustomerCloseupCustomerTexture is not null;
	}

	private void OnReturnToShopFloorPressed()
	{
		if (_customerCloseupView is not null)
			_customerCloseupView.Visible = false;
		if (_potionBookCloseupView is not null)
			_potionBookCloseupView.Visible = false;
		if (_potionBrewingStationView is not null)
			_potionBrewingStationView.Visible = false;
		if (_customerPanel is not null)
			_customerPanel.Visible = false;
		if (_potionBookPanel is not null)
			_potionBookPanel.Visible = false;

		Visible = true;
		RestoreShopFloorPanelState();
	}

	private void OnBrewPressed()
	{
		OpenPotionBrewingStation();
	}

	public void OpenPotionBrewingStation()
	{
		ShowPotionBrewingStation();
	}

	private void OnPotionBookPressed()
	{
		if (_potionBookPanel is null)
		{
			GD.PushError("ShopFloor: PotionBookPanel was not found.");
			return;
		}

		ShowPotionBookCloseup();
	}

	private void ShowPotionBookCloseup()
	{
		if (_potionBookCloseupView is null)
		{
			GD.PushError("ShopFloor: PotionBookCloseupView was not found.");
			return;
		}

		if (_potionBookPanel is null)
		{
			GD.PushError("ShopFloor: PotionBookPanel was not found.");
			return;
		}

		StoreShopFloorPanelState();
		Visible = false;
		if (_hud is not null)
			_hud.Visible = false;
		if (_inventoryPanel is not null)
			_inventoryPanel.Visible = false;
		if (_customerPanel is not null)
			_customerPanel.Visible = false;
		if (_brewPanel is not null)
			_brewPanel.Visible = false;
		if (_treatmentTray is not null)
			_treatmentTray.Visible = false;

		_potionBookCloseupView.Visible = true;
		_potionBookCloseupView.MoveToFront();

		if (!_potionBookPanel.Visible)
			_potionBookPanel.Toggle();

		_potionBookPanel.MoveToFront();
	}

	private void ShowPotionBrewingStation()
	{
		if (_potionBrewingStationView is null)
		{
			GD.PushError("ShopFloor: PotionBrewingStationView was not found.");
			return;
		}

		StoreShopFloorPanelState();
		Visible = false;
		if (_hud is not null)
			_hud.Visible = false;
		if (_inventoryPanel is not null)
			_inventoryPanel.Visible = false;
		if (_customerPanel is not null)
			_customerPanel.Visible = false;
		if (_brewPanel is not null)
			_brewPanel.Visible = false;
		if (_potionBookPanel is not null)
			_potionBookPanel.Visible = false;
		if (_treatmentTray is not null)
			_treatmentTray.Visible = false;

		_potionBrewingStationView.Visible = true;
		_potionBrewingStationView.MoveToFront();

		if (_brewPanel is not null)
			_brewPanel.ShowPanel();
	}

	private void OnTreatmentTrayPressed()
	{
		if (_treatmentTray is null)
		{
			GD.PushError("ShopFloor: TreatmentTray was not found.");
			return;
		}

		_treatmentTray.ShowPanel();
	}

	private void ShowControl(Control? control, string controlName)
	{
		if (control is null)
		{
			GD.PushError($"ShopFloor: {controlName} was not found.");
			return;
		}

		control.Visible = true;
		control.MoveToFront();
	}

	private void StoreShopFloorPanelState()
	{
		_hudWasVisible = _hud is not null && _hud.Visible;
		_inventoryWasVisible = _inventoryPanel is not null && _inventoryPanel.Visible;
		_brewWasVisible = _brewPanel is not null && _brewPanel.Visible;
		_potionBookWasVisible = _potionBookPanel is not null && _potionBookPanel.Visible;
		_treatmentTrayWasVisible = _treatmentTray is not null && _treatmentTray.Visible;
	}

	private void RestoreShopFloorPanelState()
	{
		if (_hud is not null)
			_hud.Visible = _hudWasVisible;
		if (_inventoryPanel is not null)
			_inventoryPanel.Visible = _inventoryWasVisible;
		if (_brewPanel is not null)
			_brewPanel.Visible = _brewWasVisible;
		if (_potionBookPanel is not null)
			_potionBookPanel.Visible = _potionBookWasVisible;
		if (_treatmentTray is not null)
			_treatmentTray.Visible = _treatmentTrayWasVisible;
	}

	private Button? GetRequiredButton(NodePath path, string exportName)
	{
		var button = GetNodeOrNull<Button>(path);
		if (button is null)
			GD.PushError($"ShopFloor: {exportName} was not found at '{path}'.");

		return button;
	}

	private TNode? GetOptionalNode<TNode>(NodePath path, string exportName) where TNode : Node
	{
		var node = GetNodeOrNull<TNode>(path);
		if (node is null)
			GD.PushError($"ShopFloor: {exportName} was not found at '{path}'.");

		return node;
	}

	private static void ConnectButton(Button? button, Action handler)
	{
		if (button is null)
			return;

		button.Pressed += handler;
	}

	private static void DisconnectButton(Button? button, Action handler)
	{
		if (button is null)
			return;

		button.Pressed -= handler;
	}
}
