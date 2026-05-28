using Godot;
using OccultShop.Autoload;
using OccultShop.Models;
using OccultShop.Systems;

namespace OccultShop.UI;

public partial class EventModal : Control
{
    [Export] public NodePath TitleLabelPath = default!;
    [Export] public NodePath BodyLabelPath = default!;
    [Export] public NodePath ChoicesContainerPath = default!;
    [Export] public NodePath CharacterImagePath = default!;
    [Export] public NodePath GameStatePath = new("/root/GameState");

    private Label _title = default!;
    private RichTextLabel _body = default!;
    private VBoxContainer _choices = default!;
    private TextureRect _characterImage = default!;

    private GameState _gameState = default!;
    private EventCardDef? _card;

    public override void _Ready()
    {
        var gameState = GetNodeOrNull<GameState>(GameStatePath);
        if (gameState is null)
        {
            GD.PushError($"EventModal: GameState was not found at '{GameStatePath}'.");
            return;
        }
        _gameState = gameState;

        _title = GetNode<Label>(TitleLabelPath);
        _body = GetNode<RichTextLabel>(BodyLabelPath);
        _choices = GetNode<VBoxContainer>(ChoicesContainerPath);
        _characterImage = GetNode<TextureRect>(CharacterImagePath);
        MouseFilter = MouseFilterEnum.Ignore;
        Visible = false;
        _characterImage.Visible = false;
    }

    public void ShowCard(EventCardDef card)
    {
        _card = card;
        Visible = true;
        _title.Text = card.Title;
        _body.Text = card.Text;
        SetCharacterImage(card.CharacterImagePath);

        foreach (var child in _choices.GetChildren())
            child.QueueFree();

        foreach (var choice in card.Choices)
        {
            var b = new Button();
            b.Text = choice.Label;
            b.Disabled = !Requirements.Met(_gameState, choice.Requires);
            b.Pressed += () => OnChoice(choice);
            _choices.AddChild(b);
        }
    }

    private void OnChoice(EventChoiceDef choice)
    {
        if (_card is null) return;
        if (!Requirements.Met(_gameState, choice.Requires)) return;

        foreach (var e in choice.Effects)
            EffectApplier.Apply(_gameState, e);

        // Night ends; advance day.
        _gameState.NextDay();
        _characterImage.Texture = null;
        _characterImage.Visible = false;
        Visible = false;
        _card = null;
    }

    private void SetCharacterImage(string? imagePath)
    {
        if (string.IsNullOrWhiteSpace(imagePath))
        {
            _characterImage.Texture = null;
            _characterImage.Visible = false;
            return;
        }

        var texture = ResourceLoader.Load<Texture2D>(imagePath);
        _characterImage.Texture = texture;
        _characterImage.Visible = texture is not null;
    }
}
