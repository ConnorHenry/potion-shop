using System;
using OccultShop.Persistence;
using OccultShop.Tutorial;

namespace OccultShop.Systems;

public sealed class TutorialProgressState
{
	public TutorialStatus Status { get; private set; } = TutorialStatus.NotStarted;
	public bool Requested => Status == TutorialStatus.InProgress;
	public bool Completed => Status == TutorialStatus.Completed;
	public bool Skipped => Status == TutorialStatus.Skipped;
	public int Step { get; private set; }

	public void Reset()
	{
		Status = TutorialStatus.NotStarted;
		Step = 0;
	}

	public void ApplySnapshot(GameStateSnapshot snapshot)
	{
		Status = ResolveTutorialStatus(snapshot);
		var restoredStep = snapshot.TutorialStepIndex > 0
			? snapshot.TutorialStepIndex
			: snapshot.TutorialStep;
		Step = Math.Max(0, restoredStep);
	}

	public void Request()
	{
		Status = TutorialStatus.InProgress;
		Step = 0;
	}

	public void Skip()
	{
		Status = TutorialStatus.Skipped;
		Step = 0;
	}

	public void Complete()
	{
		Status = TutorialStatus.Completed;
		Step = 0;
	}

	public bool SetStep(int step)
	{
		var normalizedStep = Math.Max(0, step);
		if (Step == normalizedStep)
			return false;

		Step = normalizedStep;
		return true;
	}

	private static TutorialStatus ResolveTutorialStatus(GameStateSnapshot snapshot)
	{
		if (snapshot.TutorialStatus is TutorialStatus explicitStatus)
			return NormalizeTutorialStatus(explicitStatus);

		if (snapshot.TutorialCompleted)
			return TutorialStatus.Completed;
		if (snapshot.TutorialSkipped)
			return TutorialStatus.Skipped;
		if (snapshot.TutorialRequested)
			return TutorialStatus.InProgress;

		return TutorialStatus.NotStarted;
	}

	private static TutorialStatus NormalizeTutorialStatus(TutorialStatus status)
	{
		return status switch
		{
			TutorialStatus.InProgress => TutorialStatus.InProgress,
			TutorialStatus.Completed => TutorialStatus.Completed,
			TutorialStatus.Skipped => TutorialStatus.Skipped,
			_ => TutorialStatus.NotStarted
		};
	}
}
