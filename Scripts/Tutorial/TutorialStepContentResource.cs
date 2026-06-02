using Godot;

namespace OccultShop.Tutorial;

[GlobalClass]
public partial class TutorialStepContentResource : Resource
{
	[Export]
	public int StepId { get; set; }

	[Export]
	public string Title { get; set; } = string.Empty;

	[Export(PropertyHint.MultilineText)]
	public string Body { get; set; } = string.Empty;

	[Export]
	public bool ShowNextButton { get; set; }

	[Export]
	public string NextButtonText { get; set; } = "Next";

	[Export]
	public bool PanelAtTop { get; set; }

	[Export]
	public bool DimBackground { get; set; } = true;

	[Export]
	public bool LockOtherButtons { get; set; }
}
