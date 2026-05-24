using Godot;

namespace OccultShop.UI;

public partial class DaySummaryPanel : Control
{
	[Signal]
	public delegate void ContinuePressedEventHandler();

	[Export] public NodePath TitlePath = default!;
	[Export] public NodePath BodyPath = default!;
	[Export] public NodePath ContinueButtonPath = default!;

	private Label _title = default!;
	private RichTextLabel _body = default!;
	private Button _continueButton = default!;

	public override void _Ready()
	{
		_title = GetNode<Label>(TitlePath);
		_body = GetNode<RichTextLabel>(BodyPath);
		_continueButton = GetNode<Button>(ContinueButtonPath);
		_continueButton.Pressed += OnContinuePressed;
		Visible = false;
	}

	public override void _ExitTree()
	{
		if (_continueButton != null)
			_continueButton.Pressed -= OnContinuePressed;
	}

	public void ShowSummary(
		int day,
		int customersServed,
		int successfulSales,
		int failedSales,
		int goldEarned,
		int dreadDelta,
		int finalGold,
		int finalDread)
	{
		_title.Text = $"Day {day} Summary";
		_body.Text =
			$"Customers served: {customersServed}\n" +
			$"Successful sales: {successfulSales}\n" +
			$"Failed sales: {failedSales}\n" +
			$"Gold earned: {goldEarned}\n" +
			$"Dread change: {dreadDelta}\n" +
			$"Final gold: {finalGold}\n" +
			$"Final dread: {finalDread}";
		Visible = true;
	}

	public void HidePanel()
	{
		Visible = false;
		_body.Text = string.Empty;
	}

	private void OnContinuePressed()
	{
		EmitSignal(SignalName.ContinuePressed);
	}
}
