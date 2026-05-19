using Godot;
using System.Collections.Generic;
using System.Linq;
using OccultShop.Autoload;
using OccultShop.Models;

namespace OccultShop.UI;

public partial class BrewPanel : Control
{
    [Export] public NodePath CloseButtonPath = default!;
    [Export] public NodePath BrewBoxPath = default!;
    [Export] public NodePath IngredientsLabelPath = default!;
    [Export] public NodePath ResultLabelPath = default!;
    [Export] public NodePath BrewButtonPath = default!;
    [Export] public NodePath ClearButtonPath = default!;

    private Button _closeButton = default!;
    private BrewDropBox _brewBox = default!;
    private Label _ingredientsLabel = default!;
    private Label _resultLabel = default!;
    private Button _brewButton = default!;
    private Button _clearButton = default!;
    private readonly List<string> _queuedIngredients = new();

    public override void _Ready()
    {
        _closeButton = GetNode<Button>(CloseButtonPath);
        _brewBox = GetNode<BrewDropBox>(BrewBoxPath);
        _ingredientsLabel = GetNode<Label>(IngredientsLabelPath);
        _resultLabel = GetNode<Label>(ResultLabelPath);
        _brewButton = GetNode<Button>(BrewButtonPath);
        _clearButton = GetNode<Button>(ClearButtonPath);

        MouseFilter = MouseFilterEnum.Ignore;
        _closeButton.Pressed += HidePanel;
        _brewBox.ItemDropped += QueueIngredient;
        _brewButton.Pressed += TryBrew;
        _clearButton.Pressed += ClearQueue;
        Visible = false;
        RefreshIngredientsLabel();
    }

    public void Toggle()
    {
        Visible = !Visible;
    }

    public void HidePanel()
    {
        Visible = false;
        _queuedIngredients.Clear();
        _resultLabel.Text = "";
        RefreshIngredientsLabel();
    }

    private void QueueIngredient(string itemId)
    {
        var qtyQueued = _queuedIngredients.Count(x => x == itemId);
        if (!GameState.HasItem(itemId, qtyQueued + 1))
        {
            _resultLabel.Text = "Not enough stock for that ingredient.";
            return;
        }

        _queuedIngredients.Add(itemId);
        _resultLabel.Text = "";
        RefreshIngredientsLabel();
    }

    private void ClearQueue()
    {
        _queuedIngredients.Clear();
        _resultLabel.Text = "";
        RefreshIngredientsLabel();
    }

    private void TryBrew()
    {
        var potion = FindMatchingPotion();
        if (potion is null)
        {
            _resultLabel.Text = "No known recipe matches these ingredients.";
            return;
        }

        if (GameState.Gold < potion.Cost)
        {
            _resultLabel.Text = $"Need {potion.Cost} gold to brew {potion.Name}.";
            return;
        }

        foreach (var ingredient in potion.Ingredients)
        {
            if (!GameState.HasItem(ingredient.ItemId, ingredient.Qty))
            {
                _resultLabel.Text = "Missing required ingredients.";
                return;
            }
        }

        foreach (var ingredient in potion.Ingredients)
            GameState.ConsumeItem(ingredient.ItemId, ingredient.Qty);

        GameState.AddGold(-potion.Cost);
        GameState.AddItem(potion.OutputItemId, potion.OutputQty);
        _queuedIngredients.Clear();
        RefreshIngredientsLabel();
        _resultLabel.Text = $"Brewed: {potion.Name}";
    }

    private PotionDef? FindMatchingPotion()
    {
        var queueCount = _queuedIngredients
            .GroupBy(x => x)
            .ToDictionary(g => g.Key, g => g.Count());

        foreach (var potion in DataDb.Potions)
        {
            if (potion.Ingredients.Count != queueCount.Count)
                continue;

            var allMatch = true;
            foreach (var ingredient in potion.Ingredients)
            {
                if (!queueCount.TryGetValue(ingredient.ItemId, out var qty) || qty != ingredient.Qty)
                {
                    allMatch = false;
                    break;
                }
            }

            if (allMatch)
                return potion;
        }

        return null;
    }

    private void RefreshIngredientsLabel()
    {
        if (_queuedIngredients.Count == 0)
        {
            _ingredientsLabel.Text = "Ingredients: (none)";
            return;
        }

        var grouped = _queuedIngredients
            .GroupBy(x => x)
            .OrderBy(g => ItemName(g.Key))
            .Select(g => $"{ItemName(g.Key)} x{g.Count()}");

        _ingredientsLabel.Text = $"Ingredients: {string.Join(", ", grouped)}";
    }

    private string ItemName(string itemId)
    {
        return DataDb.Items.TryGetValue(itemId, out var item) ? item.Name : itemId;
    }

    private DataDb DataDb => GetTree().Root.GetNode<DataDb>("/root/DataDb");
    private GameState GameState => GetTree().Root.GetNode<GameState>("/root/GameState");
}
