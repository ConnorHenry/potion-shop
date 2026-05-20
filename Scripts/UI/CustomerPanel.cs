using System.Linq;
using Godot;
using OccultShop.Autoload;
using OccultShop.Models;
using OccultShop.Systems;

namespace OccultShop.UI;

public partial class CustomerPanel : Control
{
	[Export] public NodePath TitlePath = default!;
	[Export] public NodePath PortraitPath = default!;
	[Export] public NodePath DialoguePath = default!;
	[Export] public NodePath SellDropBoxPath = default!;
	[Export] public NodePath ConfirmDialogPath = default!;
	[Export] public NodePath ConfirmDialogLabelPath = default!;
	[Export] public NodePath CloseButtonPath = default!;

	private Label _title = default!;
	private TextureRect _portrait = default!;
	private RichTextLabel _dialogue = default!;
	private CustomerSellDropBox _sellDropBox = default!;
	private ConfirmationDialog _confirmDialog = default!;
	private Label _confirmDialogLabel = default!;
	private Button _closeButton = default!;
	private CustomerInteractionDef? _interaction;
	private string? _pendingItemId;

	public override void _Ready()
	{
		_title = GetNode<Label>(TitlePath);
		_portrait = GetNode<TextureRect>(PortraitPath);
		_dialogue = GetNode<RichTextLabel>(DialoguePath);
		_sellDropBox = GetNode<CustomerSellDropBox>(SellDropBoxPath);
		_confirmDialog = GetNode<ConfirmationDialog>(ConfirmDialogPath);
		_confirmDialogLabel = GetNode<Label>(ConfirmDialogLabelPath);
		_closeButton = GetNode<Button>(CloseButtonPath);

		MouseFilter = MouseFilterEnum.Ignore;
		_closeButton.Pressed += HidePanel;
		_sellDropBox.Connect("ItemDropped", new Callable(this, nameof(OnItemDropped)));
		_confirmDialog.Confirmed += ConfirmPendingSale;
		_portrait.Visible = false;
		Visible = false;
	}

	public void ShowInteraction(CustomerInteractionDef interaction)
	{
		_interaction = interaction;
		Visible = true;
		_title.Text = interaction.Title;
		_dialogue.Text = interaction.Text;
		SetPortrait(interaction.CharacterImagePath);
	}

	public void HidePanel()
	{
		_interaction = null;
		_pendingItemId = null;
		_portrait.Texture = null;
		_portrait.Visible = false;
		_confirmDialog.Hide();
		Visible = false;
	}

	private void OnItemDropped(string itemId)
	{
		if (_interaction is null)
			return;

		var choice = ResolveChoice(itemId);
		if (choice is null)
		{
			_confirmDialogLabel.Text = "No valid customer response for that item.";
			_confirmDialog.PopupCentered();
			return;
		}

		var itemName = DataDb.Items.TryGetValue(itemId, out var item) ? item.Name : itemId;
		_pendingItemId = itemId;
		_confirmDialogLabel.Text = $"Sell {itemName} to this customer?";
		_confirmDialog.PopupCentered();
	}

	private void ConfirmPendingSale()
	{
		if (string.IsNullOrWhiteSpace(_pendingItemId))
			return;

		var choice = ResolveChoice(_pendingItemId);
		if (choice is null)
			return;

		ApplySale(_pendingItemId, choice);
	}

	private CustomerChoiceDef? ResolveChoice(string itemId)
	{
		if (_interaction is null)
			return null;

		var exact = _interaction.Choices.FirstOrDefault(x =>
			!x.IsRefuse &&
			!x.IsFallback &&
			string.Equals(x.ItemId, itemId, System.StringComparison.OrdinalIgnoreCase));

		if (exact is not null)
			return exact;

		return _interaction.Choices.FirstOrDefault(x => x.IsFallback);
	}

	private void ApplySale(string itemId, CustomerChoiceDef choice)
	{
		foreach (var effect in choice.Effects)
			EffectApplier.Apply(GameState, effect);
		GameState.ConsumeItem(itemId, 1);

		_pendingItemId = null;
		HidePanel();
	}

	private void SetPortrait(string? portraitPath)
	{
		if (string.IsNullOrWhiteSpace(portraitPath))
		{
			_portrait.Texture = null;
			_portrait.Visible = false;
			return;
		}

		var texture = ResourceLoader.Load<Texture2D>(portraitPath);
		_portrait.Texture = texture;
		_portrait.Visible = texture is not null;
	}

	private GameState GameState => GetTree().Root.GetNode<GameState>("/root/GameState");
	private DataDb DataDb => GetTree().Root.GetNode<DataDb>("/root/DataDb");
}
