using Godot;
using OccultShop.Autoload;

namespace OccultShop.UI;

public partial class Hud : Control
{
    [Export] public NodePath GoldLabelPath = default!;
    [Export] public NodePath DreadLabelPath = default!;
    [Export] public NodePath DayLabelPath = default!;

    private Label _gold = default!;
    private Label _dread = default!;
    private Label _day = default!;

    public override void _Ready()
    {
        _gold = GetNode<Label>(GoldLabelPath);
        _dread = GetNode<Label>(DreadLabelPath);
        _day = GetNode<Label>(DayLabelPath);

        GameState.Changed += Refresh;
        Refresh();
    }

    public override void _ExitTree()
    {
        GameState.Changed -= Refresh;
    }

    private void Refresh()
    {
        _gold.Text = $"Gold: {GameState.Gold}";
        _dread.Text = $"Dread: {GameState.Dread}";
        _day.Text = $"Day: {GameState.Day}";
    }

    private static GameState GameState => ((SceneTree)Engine.GetMainLoop()).Root.GetNode<GameState>("GameState");
}

