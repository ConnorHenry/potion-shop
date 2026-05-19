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
	[Export] public NodePath RefuseButtonPath = default!;
	[Export] public NodePath ConfirmDialogPath = default!;
	[Export] public NodePath ConfirmDialogLabelPath = default!;
	[Export] public NodePath CloseButtonPath = default!;

	private Label _title = default!;
	private TextureRect _portrait = default!;
	private RichTextLabel _dialogue = default!;
	private CustomerSellDropBox _sellDropBox = default!;
	private Button _refuseButton = default!;
	private ConfirmationDialog _confirmDialog = default!;
	private Label _confirmDialogLabel = default!;
	private Button _closeButton = default!;
	private CustomerInteractionDef? _interaction;
	private CustomerChoiceDef? _pendingChoice;
	private string? _pendingItemId;

	public override void _Ready()
	{
		_title = GetNode<Label>(TitlePath);
		_portrait = GetNode<TextureRect>(PortraitPath);
		_dialogue = GetNode<RichTextLabel>(DialoguePath);
		_sellDropBox = GetNode<CustomerSellDropBox>(SellDropBoxPath);
		_refuseButton = GetNode<Button>(RefuseButtonPath);
		_confirmDialog = GetNode<ConfirmationDialog>(ConfirmDialogPath);
		_confirmDialogLabel = GetNode<Label>(ConfirmDialogLabelPath);
		_closeButton = GetNode<Button>(CloseButtonPath);

		MouseFilter = MouseFilterEnum.Ignore;
		_closeButton.Pressed += HidePanel;
		_sellDropBox.Connect("ItemDropped", new Callable(this, nameof(OnItemDropped)));
		_refuseButton.Pressed += OnRefusePressed;
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
		_refuseButton.Visible = interaction.Choices.Any(x => x.IsRefuse);
		_refuseButton.Disabled = !interaction.Choices.Any(x => x.IsRefuse && Requirements.Met(GameState, x.Requires));
	}

	public void HidePanel()
	{
		_interaction = null;
		_pendingChoice = null;
		_pendingItemId = null;
		_portrait.Texture = null;
		_portrait.Visible = false;
		_confirmDialog.Hide();
		Visible = false;
	}

	private void OnRefusePressed()
	{
		if (_interaction is null)
			return;

		var choice = _interaction.Choices.FirstOrDefault(x => x.IsRefuse);
		if (choice is null || !Requirements.Met(GameState, choice.Requires))
			return;

		ApplyChoice(choice);
	}

	private void OnItemDropped(string itemId)
	{
		if (_interaction is null)
			return;

		var choice = _interaction.Choices.FirstOrDefault(x =>
			!x.IsRefuse &&
			string.Equals(x.ItemId, itemId, System.StringComparison.OrdinalIgnoreCase));

		if (choice is null || !Requirements.Met(GameState, choice.Requires))
			return;

		var itemName = DataDb.Items.TryGetValue(itemId, out var item) ? item.Name : itemId;
		_pendingChoice = choice;
		_pendingItemId = itemId;
		_confirmDialogLabel.Text = $"Sell {itemName} to this customer?";
		_confirmDialog.PopupCentered();
	}

	private void ConfirmPendingSale()
	{
		if (_pendingChoice is null)
			return;

		ApplyChoice(_pendingChoice);
	}

	private void ApplyChoice(CustomerChoiceDef choice)
	{
		foreach (var effect in choice.Effects)
			EffectApplier.Apply(GameState, effect);

		_pendingChoice = null;
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
