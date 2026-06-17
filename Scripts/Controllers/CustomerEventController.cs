using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using OccultShop.Autoload;
using OccultShop.Models;
using OccultShop.Systems;

namespace OccultShop.Controllers;

public partial class CustomerEventController : Node
{
	private const string NewGameWelcomeInteractionId = "plot_bridget_visit_1";

	private readonly Random _random = new();
	private readonly List<string> _customerOrder = new();
	private int _nextCustomerOrderIndex;
	private string _forcedNextCustomerInteractionId = string.Empty;

	public void BeginShopDay()
	{
		_customerOrder.Clear();
		_nextCustomerOrderIndex = 0;
	}

	public void ForceNextCustomerInteraction(string interactionId)
	{
		_forcedNextCustomerInteractionId = string.IsNullOrWhiteSpace(interactionId)
			? string.Empty
			: interactionId;
	}

	public CustomerInteractionDef? DrawCustomerInteraction(DataDb db, GameState state)
	{
		var interactions = db.CustomerInteractions;
		if (interactions.Count == 0)
			return null;

		if (TryDrawForcedInteraction(interactions, state, out var forcedInteraction))
			return forcedInteraction;

		if (TryDrawNewGameWelcomeInteraction(interactions, state, out var welcomeInteraction))
			return welcomeInteraction;

		var eligibleInteractions = interactions
			.Where(interaction => Requirements.Met(state, interaction.Requires))
			.Where(interaction => IsCustomerVisitAvailable(state, interaction))
			.ToList();
		if (eligibleInteractions.Count == 0)
			return null;

		if (!IsCustomerOrderCurrent(eligibleInteractions) || _nextCustomerOrderIndex >= _customerOrder.Count)
			RebuildCustomerOrder(eligibleInteractions);

		var selectedId = _customerOrder[_nextCustomerOrderIndex];
		_nextCustomerOrderIndex += 1;

		foreach (var candidate in eligibleInteractions)
		{
			if (string.Equals(candidate.Id, selectedId, StringComparison.OrdinalIgnoreCase))
				return MarkCustomerArrival(candidate, state);
		}

		GD.PushError($"CustomerEventController: Scheduled customer interaction '{selectedId}' was not eligible.");
		return null;
	}

	public CustomerInteractionDef? DrawShopDayCustomerInteraction(DataDb db, GameState state)
	{
		return DrawCustomerInteraction(db, state);
	}

	private bool TryDrawNewGameWelcomeInteraction(
		IReadOnlyList<CustomerInteractionDef> interactions,
		GameState state,
		out CustomerInteractionDef? interaction)
	{
		interaction = null;
		if (!state.HasStoryFlag(GameState.BridgetWelcomePendingStoryFlag))
			return false;

		foreach (var candidate in interactions)
		{
			if (!string.Equals(candidate.Id, NewGameWelcomeInteractionId, StringComparison.OrdinalIgnoreCase))
				continue;

			if (!Requirements.Met(state, candidate.Requires) || !IsCustomerVisitAvailable(state, candidate))
				return false;

			state.RemoveStoryFlag(GameState.BridgetWelcomePendingStoryFlag);
			interaction = MarkCustomerArrival(candidate, state);
			return true;
		}

		return false;
	}

	private bool TryDrawForcedInteraction(
		IReadOnlyList<CustomerInteractionDef> interactions,
		GameState state,
		out CustomerInteractionDef? interaction)
	{
		interaction = null;
		if (string.IsNullOrWhiteSpace(_forcedNextCustomerInteractionId))
			return false;

		var forcedInteractionId = _forcedNextCustomerInteractionId;
		_forcedNextCustomerInteractionId = string.Empty;

		if (!TryResolveForcedInteraction(interactions, forcedInteractionId, out var candidate) || candidate is null)
		{
			GD.PushError($"CustomerEventController: Forced customer interaction '{forcedInteractionId}' was not found.");
			return false;
		}

		if (!IsCustomerVisitAvailable(state, candidate))
		{
			GD.PushError($"CustomerEventController: Forced story customer interaction '{forcedInteractionId}' has already arrived.");
			return false;
		}

		interaction = MarkCustomerArrival(candidate, state);
		return true;
	}

	private static bool TryResolveForcedInteraction(
		IReadOnlyList<CustomerInteractionDef> interactions,
		string forcedInteractionId,
		out CustomerInteractionDef? interaction)
	{
		foreach (var candidate in interactions)
		{
			if (string.Equals(candidate.Id, forcedInteractionId, StringComparison.OrdinalIgnoreCase))
			{
				interaction = candidate;
				return true;
			}
		}

		var normalizedForcedId = NormalizeForcedInteractionId(forcedInteractionId);
		if (string.IsNullOrWhiteSpace(normalizedForcedId))
		{
			interaction = null;
			return false;
		}

		if (TryFindInteractionBySuffix(interactions, normalizedForcedId, out interaction))
			return true;

		return false;
	}

	private static bool TryFindInteractionBySuffix(
		IReadOnlyList<CustomerInteractionDef> interactions,
		string normalizedForcedId,
		out CustomerInteractionDef? interaction)
	{
		interaction = null;

		var suffix = "_" + normalizedForcedId;
		CustomerInteractionDef? shortestMatch = null;
		var shortestMatchIdLength = int.MaxValue;
		var shortestMatchCount = 0;

		foreach (var candidate in interactions)
		{
			if (!candidate.Id.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
				continue;

			var candidateIdLength = candidate.Id.Length;
			if (candidateIdLength < shortestMatchIdLength)
			{
				shortestMatch = candidate;
				shortestMatchIdLength = candidateIdLength;
				shortestMatchCount = 1;
				continue;
			}

			if (candidateIdLength == shortestMatchIdLength)
				shortestMatchCount += 1;
		}

		if (shortestMatch is null)
			return false;

		if (shortestMatchCount > 1)
		{
			GD.PushError(
				$"CustomerEventController: Forced customer interaction '{normalizedForcedId}' matched multiple equally specific candidates.");
			return false;
		}

		interaction = shortestMatch;
		return true;
	}

	private static string NormalizeForcedInteractionId(string forcedInteractionId)
	{
		const string legacyPrefix = "customer_requests_";
		return forcedInteractionId.StartsWith(legacyPrefix, StringComparison.OrdinalIgnoreCase)
			? forcedInteractionId[legacyPrefix.Length..]
			: forcedInteractionId;
	}

	private static bool IsCustomerVisitAvailable(GameState state, CustomerInteractionDef interaction)
	{
		return !interaction.IsStoryInteraction || !state.HasStoryCustomerVisitArrived(interaction);
	}

	private static CustomerInteractionDef MarkCustomerArrival(CustomerInteractionDef interaction, GameState state)
	{
		state.RecordStoryCustomerArrived(interaction);
		return interaction;
	}

	private bool IsCustomerOrderCurrent(IReadOnlyList<CustomerInteractionDef> eligibleInteractions)
	{
		if (_customerOrder.Count != eligibleInteractions.Count)
			return false;

		foreach (var interaction in eligibleInteractions)
		{
			if (!_customerOrder.Any(id => string.Equals(id, interaction.Id, StringComparison.OrdinalIgnoreCase)))
				return false;
		}

		return true;
	}

	private void RebuildCustomerOrder(IReadOnlyList<CustomerInteractionDef> eligibleInteractions)
	{
		_customerOrder.Clear();

		var remainingInteractions = new List<CustomerInteractionDef>(eligibleInteractions);
		while (remainingInteractions.Count > 0)
		{
			var selectedIndex = PickWeightedIndex(remainingInteractions);
			_customerOrder.Add(remainingInteractions[selectedIndex].Id);
			remainingInteractions.RemoveAt(selectedIndex);
		}

		_nextCustomerOrderIndex = 0;
	}

	private int PickWeightedIndex(IReadOnlyList<CustomerInteractionDef> interactions)
	{
		var totalWeight = 0;
		foreach (var interaction in interactions)
			totalWeight += Math.Max(1, interaction.Weight);

		var roll = _random.Next(totalWeight);
		var accumulatedWeight = 0;
		for (var index = 0; index < interactions.Count; index += 1)
		{
			accumulatedWeight += Math.Max(1, interactions[index].Weight);
			if (roll < accumulatedWeight)
				return index;
		}

		return interactions.Count - 1;
	}
}
