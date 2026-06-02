namespace OccultShop.Tutorial;

public readonly struct TutorialTransition
{
	private TutorialTransition(bool hasNextStep, bool shouldComplete, TutorialStepId nextStep)
	{
		HasNextStep = hasNextStep;
		ShouldComplete = shouldComplete;
		NextStep = nextStep;
	}

	public bool HasNextStep { get; }
	public bool ShouldComplete { get; }
	public TutorialStepId NextStep { get; }

	public static TutorialTransition None => default;

	public static TutorialTransition To(TutorialStepId nextStep)
	{
		return new TutorialTransition(hasNextStep: true, shouldComplete: false, nextStep);
	}

	public static TutorialTransition Complete()
	{
		return new TutorialTransition(hasNextStep: false, shouldComplete: true, default);
	}
}
