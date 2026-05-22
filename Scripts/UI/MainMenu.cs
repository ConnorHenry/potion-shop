using Godot;

public partial class MainMenu : Control
{
	[Export]
	public NodePath StartButtonPath { get; set; } = new NodePath("");

	private Button? _startButton;

	public override void _Ready()
	{
		if (StartButtonPath.IsEmpty)
		{
			GD.PushError("MainMenu: StartButtonPath is not assigned.");
			return;
		}

		_startButton = GetNodeOrNull<Button>(StartButtonPath);
		if (_startButton == null)
		{
			GD.PushError($"MainMenu: Start button not found at path '{StartButtonPath}'.");
			return;
		}

		_startButton.Pressed += OnStartButtonPressed;
	}

	private void OnStartButtonPressed()
	{
		Error error = GetTree().ChangeSceneToFile("res://Main.tscn");
		if (error != Error.Ok)
		{
			GD.PushError($"MainMenu: Failed to load main scene. Error: {error}");
		}
	}
}
