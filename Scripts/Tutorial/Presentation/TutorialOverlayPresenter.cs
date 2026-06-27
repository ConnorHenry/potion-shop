using Godot;
using OccultShop.UI;

namespace OccultShop.Tutorial.Presentation;

public sealed class TutorialOverlayPresenter
{
	private readonly TutorialOverlay _overlay;

	public TutorialOverlayPresenter(TutorialOverlay overlay)
	{
		_overlay = overlay;
	}

	public void Hide()
	{
		_overlay.HideOverlay();
	}

	public void SetSkipButtonVisible(bool visible)
	{
		_overlay.SetSkipButtonVisible(visible);
	}

	public void ShowForTarget(TutorialStepContentResource stepContent, Control? target, string? bodyOverride = null)
	{
		Prepare(stepContent);
		ShowCore(stepContent, bodyOverride ?? stepContent.Body, target);
	}

	public void ShowForHighlightRect(TutorialStepContentResource stepContent, Rect2 globalRect, string? bodyOverride = null)
	{
		Prepare(stepContent);
		var body = bodyOverride ?? stepContent.Body;
		if (!stepContent.DimBackground)
		{
			_overlay.ShowMessageWithoutDim(stepContent.Title, body);
			return;
		}

		_overlay.ShowWithHighlight(stepContent.Title, body, globalRect);
	}

	public void ShowForTargets(TutorialStepContentResource stepContent, string? bodyOverride = null, params Control?[] targets)
	{
		Prepare(stepContent);
		var body = bodyOverride ?? stepContent.Body;
		if (!stepContent.DimBackground)
		{
			_overlay.ShowMessageWithoutDim(stepContent.Title, body);
			return;
		}

		_overlay.ShowForTargets(stepContent.Title, body, targets);
	}

	public void ShowForTargetsWithArrow(TutorialStepContentResource stepContent, Control? fromTarget, Control? toTarget, string? bodyOverride = null, params Control?[] targets)
	{
		Prepare(stepContent);
		var body = bodyOverride ?? stepContent.Body;
		if (!stepContent.DimBackground)
		{
			_overlay.ShowMessageWithoutDim(stepContent.Title, body);
			return;
		}

		_overlay.ShowForTargetsWithArrow(stepContent.Title, body, fromTarget, toTarget, targets);
	}

	public void ShowMessage(TutorialStepContentResource stepContent, string? bodyOverride = null)
	{
		Prepare(stepContent);
		ShowCore(stepContent, bodyOverride ?? stepContent.Body, null);
	}

	private void ShowCore(TutorialStepContentResource stepContent, string body, Control? target)
	{
		if (!stepContent.DimBackground)
		{
			_overlay.ShowMessageWithoutDim(stepContent.Title, body);
			return;
		}

		if (target is null)
		{
			_overlay.ShowMessage(stepContent.Title, body);
			return;
		}

		_overlay.ShowForTarget(stepContent.Title, body, target);
	}

	private void Prepare(TutorialStepContentResource stepContent)
	{
		_overlay.SetNextButtonVisible(stepContent.ShowNextButton);
		_overlay.SetNextButtonEnabled(true);
		_overlay.SetNextButtonText(string.IsNullOrWhiteSpace(stepContent.NextButtonText) ? "Next" : stepContent.NextButtonText);

		if (stepContent.PanelAtTop)
		{
			_overlay.PlacePanelAtTop();
			return;
		}

		_overlay.PlacePanelAtBottom();
	}
}
