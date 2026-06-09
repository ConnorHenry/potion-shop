using System;
using Godot;
using OccultShop.Controllers;

namespace OccultShop.UI;

public partial class ShopFloor : Control
{
	[Export] public NodePath PotionBrewingStationButtonPath = new("Hotspots/InventoryShelf");
	[Export] public NodePath CustomerButtonPath = new("Hotspots/Customer");
	[Export] public NodePath CustomerArtPath = new("Art/Customer");
	[Export] public NodePath BrewButtonPath = new("Hotspots/Counter");
	[Export] public NodePath BookButtonPath = new("../PotionBrewingStationView/Book/BookHotspot");
	[Export] public NodePath InventoryPanelPath = new("../InventoryPanel");
	[Export] public NodePath CustomerPanelPath = new("../CustomerPanel");
	[Export] public NodePath BrewPanelPath = new("../PotionBrewingStationView/BrewPanel");
	[Export] public NodePath PotionBookPanelPath = new("../PotionBookPanel");
	[Export] public NodePath IngredientBookPanelPath = new("../IngredientBookPanel");
	[Export] public NodePath TreatmentTrayPath = new("../PotionBrewingStationView/TreatmentTray");
	[Export] public NodePath CustomerCloseupViewPath = new("../CustomerCloseupView");
	[Export] public NodePath CustomerCloseupCustomerImagePath = new("../CustomerCloseupView/Customer");
	[Export] public NodePath CustomerCloseupReturnButtonPath = new("../CustomerCloseupView/ReturnHotspot");
	[Export] public NodePath PotionBookCloseupViewPath = new("../PotionBookCloseupView");
	[Export] public NodePath PotionBookCloseupReturnButtonPath = new("../PotionBookCloseupView/ReturnHotspot");
	[Export] public NodePath PotionBookCloseupLeftReturnButtonPath = new("../PotionBookCloseupView/ReturnHotspotLeft");
	[Export] public NodePath PotionBookCloseupRightReturnButtonPath = new("../PotionBookCloseupView/ReturnHotspotRight");
	[Export] public NodePath PotionBookCloseupBottomReturnButtonPath = new("../PotionBookCloseupView/ReturnHotspotBottom");
	[Export] public NodePath BookSwitchButtonPath = new("../PotionBookCloseupView/BookSwitch");
	[Export] public NodePath PotionBrewingStationViewPath = new("../PotionBrewingStationView");
	[Export] public NodePath PotionBrewingStationReturnButtonPath = new("../PotionBrewingStationView/ReturnHotspotLeft");
	[Export] public NodePath BedroomButtonPath = new("../PotionBrewingStationView/BedroomHotspotRight");
	[Export] public NodePath BedroomViewPath = new("../BedroomView");
	[Export] public NodePath BedroomReturnButtonPath = new("../BedroomView/ReturnHotspotLeft");
	[Export] public NodePath BedroomEndDayButtonPath = new("../BedroomView/EndDayHotspot");
	[Export] public NodePath EventModalPath = new("../EventModal");
	[Export] public NodePath DayControllerPath = new("/root/Main/DayController");
	[Export] public bool HideInventoryOnReady = true;
	[Export] public bool KeepInventoryVisibleInCustomerCloseup = true;

	private Button? _potionBrewingStationButton;
	private Button? _customerButton;
	private Control? _customerArt;
	private Button? _brewButton;
	private Button? _bookButton;
	private Button? _customerCloseupReturnButton;
	private Button? _potionBookCloseupReturnButton;
	private Button? _potionBookCloseupLeftReturnButton;
	private Button? _potionBookCloseupRightReturnButton;
	private Button? _potionBookCloseupBottomReturnButton;
	private Button? _bookSwitchButton;
	private Button? _potionBrewingStationReturnButton;
	private Button? _bedroomButton;
	private Button? _bedroomReturnButton;
	private Button? _bedroomEndDayButton;
	private Control? _customerCloseupView;
	private TextureRect? _customerCloseupCustomerImage;
	private Texture2D? _defaultCustomerCloseupCustomerTexture;
	private Control? _potionBookCloseupView;
	private Control? _potionBrewingStationView;
	private Control? _bedroomView;
	private Control? _eventModal;
	private InventoryPanel? _inventoryPanel;
	private CustomerPanel? _customerPanel;
	private BrewPanel? _brewPanel;
	private PotionBookPanel? _potionBookPanel;
	private IngredientBookPanel? _ingredientBookPanel;
	private TreatmentTray? _treatmentTray;
	private DayController? _dayController;
	private bool _inventoryWasVisible;
	private bool _brewWasVisible;
	private bool _potionBookWasVisible;
	private bool _ingredientBookWasVisible;
	private bool _returnToPotionBrewingStationAfterBook;
	private BookPanelKind _activeBookPanelKind = BookPanelKind.Potion;

	public override void _Ready()
	{
		_potionBrewingStationButton = GetRequiredButton(PotionBrewingStationButtonPath, nameof(PotionBrewingStationButtonPath));
		_customerButton = GetRequiredButton(CustomerButtonPath, nameof(CustomerButtonPath));
		_customerArt = GetOptionalNode<Control>(CustomerArtPath, nameof(CustomerArtPath));
		_brewButton = GetRequiredButton(BrewButtonPath, nameof(BrewButtonPath));
		_bookButton = GetRequiredButton(BookButtonPath, nameof(BookButtonPath));
		_customerCloseupReturnButton = GetRequiredButton(CustomerCloseupReturnButtonPath, nameof(CustomerCloseupReturnButtonPath));
		_potionBookCloseupReturnButton = GetRequiredButton(PotionBookCloseupReturnButtonPath, nameof(PotionBookCloseupReturnButtonPath));
		_potionBookCloseupLeftReturnButton = GetNodeOrNull<Button>(PotionBookCloseupLeftReturnButtonPath);
		_potionBookCloseupRightReturnButton = GetNodeOrNull<Button>(PotionBookCloseupRightReturnButtonPath);
		_potionBookCloseupBottomReturnButton = GetNodeOrNull<Button>(PotionBookCloseupBottomReturnButtonPath);
		_bookSwitchButton = GetNodeOrNull<Button>(BookSwitchButtonPath);
		_potionBrewingStationReturnButton = GetRequiredButton(PotionBrewingStationReturnButtonPath, nameof(PotionBrewingStationReturnButtonPath));
		_bedroomButton = GetRequiredButton(BedroomButtonPath, nameof(BedroomButtonPath));
		_bedroomReturnButton = GetRequiredButton(BedroomReturnButtonPath, nameof(BedroomReturnButtonPath));
		_bedroomEndDayButton = GetRequiredButton(BedroomEndDayButtonPath, nameof(BedroomEndDayButtonPath));

		_customerCloseupView = GetOptionalNode<Control>(CustomerCloseupViewPath, nameof(CustomerCloseupViewPath));
		_customerCloseupCustomerImage = GetOptionalNode<TextureRect>(
			CustomerCloseupCustomerImagePath,
			nameof(CustomerCloseupCustomerImagePath));
		_defaultCustomerCloseupCustomerTexture = _customerCloseupCustomerImage?.Texture;
		_potionBookCloseupView = GetOptionalNode<Control>(PotionBookCloseupViewPath, nameof(PotionBookCloseupViewPath));
		_potionBrewingStationView = GetOptionalNode<Control>(PotionBrewingStationViewPath, nameof(PotionBrewingStationViewPath));
		_bedroomView = GetOptionalNode<Control>(BedroomViewPath, nameof(BedroomViewPath));
		_eventModal = GetOptionalNode<Control>(EventModalPath, nameof(EventModalPath));
		_inventoryPanel = GetOptionalNode<InventoryPanel>(InventoryPanelPath, nameof(InventoryPanelPath));
		_customerPanel = GetOptionalNode<CustomerPanel>(CustomerPanelPath, nameof(CustomerPanelPath));
		_brewPanel = GetOptionalNode<BrewPanel>(BrewPanelPath, nameof(BrewPanelPath));
		_potionBookPanel = GetOptionalNode<PotionBookPanel>(PotionBookPanelPath, nameof(PotionBookPanelPath));
		_ingredientBookPanel = GetOptionalNode<IngredientBookPanel>(IngredientBookPanelPath, nameof(IngredientBookPanelPath));
		_treatmentTray = GetOptionalNode<TreatmentTray>(TreatmentTrayPath, nameof(TreatmentTrayPath));
		_dayController = GetOptionalNode<DayController>(DayControllerPath, nameof(DayControllerPath));

		ConnectButton(_potionBrewingStationButton, OnPotionBrewingStationPressed);
		ConnectButton(_customerButton, OnCustomerPressed);
		ConnectButton(_brewButton, OnBrewPressed);
		ConnectButton(_bookButton, OnBookPressed);
		ConnectButton(_customerCloseupReturnButton, OnReturnToShopFloorPressed);
		ConnectButton(_potionBookCloseupReturnButton, OnReturnToShopFloorPressed);
		ConnectButton(_potionBookCloseupLeftReturnButton, OnReturnToShopFloorPressed);
		ConnectButton(_potionBookCloseupRightReturnButton, OnReturnToShopFloorPressed);
		ConnectButton(_potionBookCloseupBottomReturnButton, OnReturnToShopFloorPressed);
		ConnectButton(_bookSwitchButton, OnBookSwitchPressed);
		ConnectButton(_potionBrewingStationReturnButton, OnReturnToShopFloorPressed);
		ConnectButton(_bedroomButton, OnBedroomPressed);
		ConnectButton(_bedroomReturnButton, OnReturnFromBedroomPressed);
		ConnectButton(_bedroomEndDayButton, OnBedroomEndDayPressed);
		if (_dayController is not null)
		{
			_dayController.ShopStateChanged += UpdateBedroomEndDayHotspotState;
			_dayController.ShopStateChanged += UpdateCustomerPresence;
		}

		Callable.From(ApplyInitialPanelState).CallDeferred();
		UpdateBedroomEndDayHotspotState();
		UpdateCustomerPresence();
	}

	public override void _ExitTree()
	{
		DisconnectButton(_potionBrewingStationButton, OnPotionBrewingStationPressed);
		DisconnectButton(_customerButton, OnCustomerPressed);
		DisconnectButton(_brewButton, OnBrewPressed);
		DisconnectButton(_bookButton, OnBookPressed);
		DisconnectButton(_customerCloseupReturnButton, OnReturnToShopFloorPressed);
		DisconnectButton(_potionBookCloseupReturnButton, OnReturnToShopFloorPressed);
		DisconnectButton(_potionBookCloseupLeftReturnButton, OnReturnToShopFloorPressed);
		DisconnectButton(_potionBookCloseupRightReturnButton, OnReturnToShopFloorPressed);
		DisconnectButton(_potionBookCloseupBottomReturnButton, OnReturnToShopFloorPressed);
		DisconnectButton(_bookSwitchButton, OnBookSwitchPressed);
		DisconnectButton(_potionBrewingStationReturnButton, OnReturnToShopFloorPressed);
		DisconnectButton(_bedroomButton, OnBedroomPressed);
		DisconnectButton(_bedroomReturnButton, OnReturnFromBedroomPressed);
		DisconnectButton(_bedroomEndDayButton, OnBedroomEndDayPressed);
		if (_dayController is not null)
		{
			_dayController.ShopStateChanged -= UpdateBedroomEndDayHotspotState;
			_dayController.ShopStateChanged -= UpdateCustomerPresence;
		}
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
		if (_bedroomView is not null)
			_bedroomView.Visible = false;
		if (_potionBookPanel is not null)
			_potionBookPanel.Visible = false;
		if (_ingredientBookPanel is not null)
			_ingredientBookPanel.Visible = false;
	}

	private void OnPotionBrewingStationPressed()
	{
		OpenPotionBrewingStation();
	}

	private void OnCustomerPressed()
	{
		if (_dayController is not null && !_dayController.IsShopOpen)
		{
			GD.PushError("ShopFloor: Customer screen requested while the shop is closed.");
			return;
		}

		if (_customerPanel is null)
		{
			GD.PushError("ShopFloor: CustomerPanel was not found.");
			return;
		}

		if (!_customerPanel.HasActiveInteraction)
		{
			GD.PushError("ShopFloor: Customer screen requested, but no active customer is available.");
			return;
		}

		_customerPanel.ShowPreparedInteraction();
		ShowCustomerCloseup();
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
		if (_brewPanel is not null)
			_brewPanel.Visible = false;
		if (_potionBookPanel is not null)
			_potionBookPanel.Visible = false;
		if (_ingredientBookPanel is not null)
			_ingredientBookPanel.Visible = false;
		if (_treatmentTray is not null)
			_treatmentTray.ClearStagedItems();
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
		if (_potionBookCloseupView is not null
			&& _potionBookCloseupView.Visible
			&& _returnToPotionBrewingStationAfterBook)
		{
			ReturnFromBookToPotionBrewingStation();
			return;
		}

		_returnToPotionBrewingStationAfterBook = false;
		if (_customerCloseupView is not null)
			_customerCloseupView.Visible = false;
		if (_potionBookCloseupView is not null)
			_potionBookCloseupView.Visible = false;
		if (_potionBrewingStationView is not null)
			_potionBrewingStationView.Visible = false;
		if (_bedroomView is not null)
			_bedroomView.Visible = false;
		if (_treatmentTray is not null)
			_treatmentTray.ClearStagedItems();
		if (_customerPanel is not null)
			_customerPanel.Visible = false;
		if (_potionBookPanel is not null)
			_potionBookPanel.Visible = false;
		if (_ingredientBookPanel is not null)
			_ingredientBookPanel.Visible = false;

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

	private void OnBookPressed()
	{
		ShowBookCloseup(_activeBookPanelKind);
	}

	private void OnBookSwitchPressed()
	{
		ShowBookPanel(GetOppositeBookPanelKind(_activeBookPanelKind));
	}

	private void OnBedroomPressed()
	{
		ShowBedroom();
	}

	private void OnReturnFromBedroomPressed()
	{
		ReturnFromBedroomToPotionBrewingStation();
	}

	private void OnBedroomEndDayPressed()
	{
		if (_dayController is null)
		{
			GD.PushError("ShopFloor: DayController was not found.");
			UpdateBedroomEndDayHotspotState();
			return;
		}

		_dayController.EndDayAndRunNight();
		UpdateBedroomEndDayHotspotState();
		if (_eventModal is not null && _eventModal.Visible)
			_eventModal.MoveToFront();
	}

	private void ShowBookCloseup(BookPanelKind bookPanelKind)
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
		if (_ingredientBookPanel is null)
		{
			GD.PushError("ShopFloor: IngredientBookPanel was not found.");
			return;
		}

		_returnToPotionBrewingStationAfterBook = _potionBrewingStationView is not null && _potionBrewingStationView.Visible;
		StoreShopFloorPanelState();
		Visible = false;
		if (_inventoryPanel is not null)
			_inventoryPanel.Visible = false;
		if (_customerPanel is not null)
			_customerPanel.Visible = false;
		if (_returnToPotionBrewingStationAfterBook)
		{
			if (_potionBrewingStationView is not null)
				_potionBrewingStationView.Visible = false;
		}
		else
		{
			if (_brewPanel is not null)
				_brewPanel.Visible = false;
			if (_treatmentTray is not null)
				_treatmentTray.ClearStagedItems();
		}

		_potionBookCloseupView.Visible = true;
		_potionBookCloseupView.MoveToFront();

		ShowBookPanel(bookPanelKind);
	}

	private void ReturnFromBookToPotionBrewingStation()
	{
		_returnToPotionBrewingStationAfterBook = false;

		if (_potionBookCloseupView is not null)
			_potionBookCloseupView.Visible = false;
		if (_potionBookPanel is not null)
			_potionBookPanel.Visible = false;
		if (_ingredientBookPanel is not null)
			_ingredientBookPanel.Visible = false;
		if (_potionBrewingStationView is not null)
		{
			_potionBrewingStationView.Visible = true;
			_potionBrewingStationView.MoveToFront();
		}
	}

	private void ShowBookPanel(BookPanelKind bookPanelKind)
	{
		if (bookPanelKind == BookPanelKind.Potion)
		{
			if (_ingredientBookPanel is not null)
				_ingredientBookPanel.Visible = false;
			if (_potionBookPanel is null)
			{
				GD.PushError("ShopFloor: PotionBookPanel was not found.");
				return;
			}

			if (!_potionBookPanel.Visible)
				_potionBookPanel.Toggle();

			_potionBookPanel.MoveToFront();
			UpdateBookSwitchButtonState(BookPanelKind.Potion);
			return;
		}

		if (_potionBookPanel is not null)
			_potionBookPanel.Visible = false;
		if (_ingredientBookPanel is null)
		{
			GD.PushError("ShopFloor: IngredientBookPanel was not found.");
			return;
		}

		_ingredientBookPanel.ShowPanel();
		_ingredientBookPanel.MoveToFront();
		UpdateBookSwitchButtonState(BookPanelKind.Ingredient);
	}

	private void UpdateBookSwitchButtonState(BookPanelKind activeBookPanelKind)
	{
		_activeBookPanelKind = activeBookPanelKind;

		if (_bookSwitchButton is null)
			return;

		var targetBookPanelKind = GetOppositeBookPanelKind(activeBookPanelKind);
		_bookSwitchButton.Text = GetBookSwitchButtonText(targetBookPanelKind);
		_bookSwitchButton.TooltipText = GetBookSwitchButtonTooltipText(targetBookPanelKind);
		_bookSwitchButton.Disabled = false;
	}

	private static BookPanelKind GetOppositeBookPanelKind(BookPanelKind bookPanelKind)
	{
		return bookPanelKind == BookPanelKind.Potion
			? BookPanelKind.Ingredient
			: BookPanelKind.Potion;
	}

	private static string GetBookSwitchButtonText(BookPanelKind targetBookPanelKind)
	{
		return targetBookPanelKind == BookPanelKind.Potion ? "Potions" : "Ingredients";
	}

	private static string GetBookSwitchButtonTooltipText(BookPanelKind targetBookPanelKind)
	{
		return targetBookPanelKind == BookPanelKind.Potion ? "Open potion book" : "Open ingredient book";
	}

	private void ShowBedroom()
	{
		if (_bedroomView is null)
		{
			GD.PushError("ShopFloor: BedroomView was not found.");
			return;
		}

		if (_potionBrewingStationView is null)
		{
			GD.PushError("ShopFloor: PotionBrewingStationView was not found.");
			return;
		}

		_potionBrewingStationView.Visible = false;
		_bedroomView.Visible = true;
		_bedroomView.MoveToFront();
		UpdateBedroomEndDayHotspotState();
	}

	private void ReturnFromBedroomToPotionBrewingStation()
	{
		if (_bedroomView is not null)
			_bedroomView.Visible = false;

		if (_potionBrewingStationView is null)
		{
			GD.PushError("ShopFloor: PotionBrewingStationView was not found.");
			return;
		}

		_potionBrewingStationView.Visible = true;
		_potionBrewingStationView.MoveToFront();
	}

	private void UpdateBedroomEndDayHotspotState()
	{
		if (_bedroomEndDayButton is null)
			return;

		_bedroomEndDayButton.Disabled = _dayController is null;
	}

	private void UpdateCustomerPresence()
	{
		var customerVisible = _dayController is not null
			&& _dayController.IsShopOpen
			&& _customerPanel is not null
			&& _customerPanel.HasActiveInteraction;

		if (_customerArt is not null)
			_customerArt.Visible = customerVisible;

		if (_customerButton is not null)
		{
			_customerButton.Visible = customerVisible;
			_customerButton.Disabled = !customerVisible;
		}
	}

	private bool ShowPotionBrewingStation()
	{
		if (_potionBrewingStationView is null)
		{
			GD.PushError("ShopFloor: PotionBrewingStationView was not found.");
			return false;
		}

		StoreShopFloorPanelState();
		Visible = false;
		if (_inventoryPanel is not null)
			_inventoryPanel.Visible = false;
		if (_customerPanel is not null)
			_customerPanel.Visible = false;
		if (_brewPanel is not null)
			_brewPanel.Visible = false;
		if (_potionBookPanel is not null)
			_potionBookPanel.Visible = false;
		if (_ingredientBookPanel is not null)
			_ingredientBookPanel.Visible = false;
		if (_bedroomView is not null)
			_bedroomView.Visible = false;

		_potionBrewingStationView.Visible = true;
		_potionBrewingStationView.MoveToFront();
		if (_treatmentTray is not null)
			_treatmentTray.Visible = true;

		if (_brewPanel is not null)
			_brewPanel.ShowPanel();

		return true;
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
		_inventoryWasVisible = _inventoryPanel is not null && _inventoryPanel.Visible;
		_brewWasVisible = _brewPanel is not null && _brewPanel.Visible;
		_potionBookWasVisible = _potionBookPanel is not null && _potionBookPanel.Visible;
		_ingredientBookWasVisible = _ingredientBookPanel is not null && _ingredientBookPanel.Visible;
	}

	private void RestoreShopFloorPanelState()
	{
		if (_inventoryPanel is not null)
			_inventoryPanel.Visible = _inventoryWasVisible;
		if (_brewPanel is not null)
			_brewPanel.Visible = _brewWasVisible;
		if (_potionBookPanel is not null)
			_potionBookPanel.Visible = _potionBookWasVisible;
		if (_ingredientBookPanel is not null)
			_ingredientBookPanel.Visible = _ingredientBookWasVisible;
		if (_treatmentTray is not null)
			_treatmentTray.Visible = true;
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

	private enum BookPanelKind
	{
		Potion,
		Ingredient
	}
}
