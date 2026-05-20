using System.Linq;
using System.Text;
using Godot;
using OccultShop.Autoload;
using OccultShop.Models;

namespace OccultShop.UI;

public partial class RecipeBookPanel : Control
{
    [Export] public NodePath CloseButtonPath = default!;
    [Export] public NodePath RecipesContainerPath = default!;

    private Button _closeButton = default!;
    private VBoxContainer _recipes = default!;

    public override void _Ready()
    {
        _closeButton = GetNode<Button>(CloseButtonPath);
        _recipes = GetNode<VBoxContainer>(RecipesContainerPath);

        MouseFilter = MouseFilterEnum.Ignore;
        _closeButton.Pressed += HidePanel;
        GameState.Changed += Refresh;

        Visible = false;
        Refresh();
    }

    public override void _ExitTree()
    {
        GameState.Changed -= Refresh;
    }

    public void Toggle()
    {
        Visible = !Visible;
        if (Visible)
            Refresh();
    }

    public void HidePanel()
    {
        Visible = false;
    }

    private void Refresh()
    {
        foreach (var child in _recipes.GetChildren())
            child.QueueFree();

        var knownPotions = DataDb.Potions.Where(p => GameState.KnowsPotion(p.Id)).ToList();

        if (knownPotions.Count == 0)
        {
            _recipes.AddChild(new Label { Text = "No known recipes" });
            return;
        }

        foreach (var potion in knownPotions.OrderBy(p => p.Name))
        {
            var card = new PanelContainer();
            var vbox = new VBoxContainer();
            var title = new Label { Text = potion.Name };
            var desc = new RichTextLabel { Text = potion.Description, FitContent = true };
            var ingredients = new Label { Text = $"Ingredients: {string.Join(", ", potion.Ingredients.Select(i => $"{ItemName(i.ItemId)} x{i.Qty}"))}" };
            var effects = new Label { Text = $"Effects: {PotionEffects(potion)}" };
            var cost = new Label { Text = $"Cost: {potion.Cost} gold" };

            vbox.AddChild(title);
            vbox.AddChild(ingredients);
            vbox.AddChild(effects);
            vbox.AddChild(cost);
            vbox.AddChild(desc);
            card.AddChild(vbox);
            _recipes.AddChild(card);
        }
    }

    private static string PotionEffects(PotionDef potion)
    {
        return $"{potion.OutputQty}x {potion.OutputItemId}";
    }

    private static string ItemName(string itemId)
    {
        return DataDb.Items.TryGetValue(itemId, out var item) ? item.Name : itemId;
    }

    private static DataDb DataDb => (DataDb)((SceneTree)Engine.GetMainLoop()).Root.GetNode("/root/DataDb");
    private static GameState GameState => (GameState)((SceneTree)Engine.GetMainLoop()).Root.GetNode("/root/GameState");
}
