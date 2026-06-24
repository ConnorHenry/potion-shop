using System.Collections.Generic;
using OccultShop.Autoload;
using OccultShop.Dialogue;
using OccultShop.Models;

namespace OccultShop.Systems;

public sealed class CustomerDialogueAdapter
{
	private readonly GameState _gameState;
	private readonly Dictionary<string, CustomerDialogueOptionDef> _optionsBySourceKey;

	private CustomerDialogueAdapter(
		CustomerInteractionDef interaction,
		GameState gameState,
		CustomerDialogueGraphBuildResult buildResult)
	{
		Interaction = interaction;
		_gameState = gameState;
		Graph = buildResult.Graph;
		_optionsBySourceKey = buildResult.OptionsBySourceKey;
		RequestLines = CustomerDialogueGraphBuilder.BuildLines(interaction.Lines);
	}

	public CustomerInteractionDef Interaction { get; }
	public DialogueGraph Graph { get; }
	public IReadOnlyList<DialogueLine> RequestLines { get; }
	public string RequestText => Interaction.Text;

	public static bool TryCreate(
		CustomerInteractionDef interaction,
		GameState gameState,
		out CustomerDialogueAdapter? adapter,
		out string error)
	{
		adapter = null;
		error = string.Empty;

		if (!interaction.IsStoryInteraction || !interaction.HasDialogueTree)
			return false;

		var buildResult = CustomerDialogueGraphBuilder.Build(interaction);
		if (!buildResult.Graph.TryGetStartNode(out _))
		{
			error = $"Story customer '{interaction.Id}' has dialogue data but no valid start node.";
			return false;
		}

		adapter = new CustomerDialogueAdapter(interaction, gameState, buildResult);
		return true;
	}

	public bool IsOptionAvailable(DialogueOption option)
	{
		return !TryGetCustomerOption(option, out var customerOption) ||
			Requirements.Met(_gameState, customerOption.Requires);
	}

	public bool HasOptionBeenSelected(DialogueOption option)
	{
		return TryGetCustomerOption(option, out var customerOption) &&
			_gameState.HasStoryCustomerDialogueOptionSelected(Interaction, customerOption.Id);
	}

	public void RecordOptionSelected(DialogueOption option)
	{
		if (TryGetCustomerOption(option, out var customerOption))
			_gameState.RecordStoryCustomerDialogueOptionSelected(Interaction, customerOption.Id);
	}

	public void ApplyOptionEffects(DialogueOption option)
	{
		if (!TryGetCustomerOption(option, out var customerOption))
			return;

		foreach (var effect in customerOption.Effects)
			EffectApplier.Apply(_gameState, effect);
	}

	public bool RevealsRequest(DialogueOption option)
	{
		return TryGetCustomerOption(option, out var customerOption) && customerOption.RevealsRequest;
	}

	public bool ReturnsToDialogue(DialogueOption option)
	{
		return TryGetCustomerOption(option, out var customerOption) && customerOption.ReturnsToDialogue;
	}

	public string BuildOutcome(DialogueOption option)
	{
		if (!TryGetCustomerOption(option, out var customerOption))
			return $"dialogue:{option.Id}";

		var outcomeId = string.IsNullOrWhiteSpace(customerOption.Id)
			? customerOption.Label
			: customerOption.Id;
		return $"dialogue:{outcomeId}";
	}

	private bool TryGetCustomerOption(DialogueOption option, out CustomerDialogueOptionDef customerOption)
	{
		return _optionsBySourceKey.TryGetValue(option.SourceKey, out customerOption!);
	}
}
