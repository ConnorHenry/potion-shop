using System;
using System.Collections.Generic;
using System.Linq;
using OccultShop.Models;

namespace OccultShop.Systems;

public sealed class StoryCustomerVisitState
{
	private readonly Dictionary<string, StoryCustomerVisitRecord> _storyCustomerVisits = new(StringComparer.OrdinalIgnoreCase);

	public IReadOnlyDictionary<string, StoryCustomerVisitRecord> Visits => _storyCustomerVisits;

	public void Clear()
	{
		_storyCustomerVisits.Clear();
	}

	public void Restore(IEnumerable<StoryCustomerVisitRecord>? visits, string arrivedOutcome)
	{
		_storyCustomerVisits.Clear();
		if (visits is null)
			return;

		foreach (var visit in visits)
			RestoreStoryCustomerVisit(visit, arrivedOutcome);
	}

	public List<StoryCustomerVisitRecord> CloneStoryCustomerVisits()
	{
		var visits = new List<StoryCustomerVisitRecord>(_storyCustomerVisits.Count);
		foreach (var visit in _storyCustomerVisits.Values)
			visits.Add(CloneStoryCustomerVisit(visit));

		return visits;
	}

	public bool HasStoryCustomerVisitArrived(CustomerInteractionDef interaction)
	{
		var visitKey = BuildStoryCustomerVisitKey(interaction);
		return !string.IsNullOrWhiteSpace(visitKey) &&
			_storyCustomerVisits.TryGetValue(visitKey, out var visit) &&
			visit.HasArrived;
	}

	public bool RecordStoryCustomerArrived(CustomerInteractionDef interaction, int day, string arrivedOutcome)
	{
		var visitKey = BuildStoryCustomerVisitKey(interaction);
		if (string.IsNullOrWhiteSpace(visitKey))
			return false;

		var visit = GetOrCreateStoryCustomerVisit(interaction, visitKey, day);
		visit.HasArrived = true;
		if (visit.ArrivalDay <= 0)
			visit.ArrivalDay = day;
		if (string.IsNullOrWhiteSpace(visit.LastOutcome))
			visit.LastOutcome = arrivedOutcome;

		return true;
	}

	public bool RecordStoryCustomerInteractionOutcome(
		CustomerInteractionDef interaction,
		string outcome,
		int day,
		string arrivedOutcome)
	{
		var visitKey = BuildStoryCustomerVisitKey(interaction);
		if (string.IsNullOrWhiteSpace(visitKey))
			return false;

		var visit = GetOrCreateStoryCustomerVisit(interaction, visitKey, day);
		visit.HasArrived = true;
		if (visit.ArrivalDay <= 0)
			visit.ArrivalDay = day;
		visit.LastOutcome = NormalizeStoryCustomerOutcome(outcome, arrivedOutcome);
		visit.OutcomeDay = day;

		return true;
	}

	public bool HasStoryCustomerDialogueOptionSelected(CustomerInteractionDef interaction, string optionId)
	{
		if (string.IsNullOrWhiteSpace(optionId))
			return false;

		var visitKey = BuildStoryCustomerVisitKey(interaction);
		return !string.IsNullOrWhiteSpace(visitKey) &&
			_storyCustomerVisits.TryGetValue(visitKey, out var visit) &&
			visit.SelectedDialogueOptionIds.Any(id => string.Equals(id, optionId, StringComparison.OrdinalIgnoreCase));
	}

	public bool RecordStoryCustomerDialogueOptionSelected(CustomerInteractionDef interaction, string optionId, int day)
	{
		if (string.IsNullOrWhiteSpace(optionId))
			return false;

		var visitKey = BuildStoryCustomerVisitKey(interaction);
		if (string.IsNullOrWhiteSpace(visitKey))
			return false;

		var visit = GetOrCreateStoryCustomerVisit(interaction, visitKey, day);
		if (visit.SelectedDialogueOptionIds.Any(id => string.Equals(id, optionId, StringComparison.OrdinalIgnoreCase)))
			return false;

		visit.SelectedDialogueOptionIds.Add(optionId);
		return true;
	}

	private void RestoreStoryCustomerVisit(StoryCustomerVisitRecord? visit, string arrivedOutcome)
	{
		if (visit is null)
			return;

		var visitKey = string.IsNullOrWhiteSpace(visit.VisitKey)
			? BuildStoryCustomerVisitKey(visit.StoryCharacterId, visit.VisitId, visit.InteractionId)
			: visit.VisitKey;
		if (string.IsNullOrWhiteSpace(visitKey))
			return;

		_storyCustomerVisits[visitKey] = new StoryCustomerVisitRecord
		{
			VisitKey = visitKey,
			StoryCharacterId = visit.StoryCharacterId,
			VisitId = string.IsNullOrWhiteSpace(visit.VisitId) ? visit.InteractionId : visit.VisitId,
			InteractionId = visit.InteractionId,
			ScheduledDay = Math.Max(0, visit.ScheduledDay),
			HasArrived = visit.HasArrived,
			ArrivalDay = Math.Max(0, visit.ArrivalDay),
			LastOutcome = NormalizeStoryCustomerOutcome(visit.LastOutcome, arrivedOutcome),
			OutcomeDay = Math.Max(0, visit.OutcomeDay),
			SelectedDialogueOptionIds = CloneSelectedDialogueOptionIds(visit.SelectedDialogueOptionIds)
		};
	}

	private StoryCustomerVisitRecord GetOrCreateStoryCustomerVisit(CustomerInteractionDef interaction, string visitKey, int day)
	{
		if (_storyCustomerVisits.TryGetValue(visitKey, out var visit))
			return visit;

		visit = new StoryCustomerVisitRecord
		{
			VisitKey = visitKey,
			StoryCharacterId = interaction.StoryCharacterId,
			VisitId = interaction.GetStoryVisitId(),
			InteractionId = interaction.Id,
			ScheduledDay = ResolveStoryCustomerScheduledDay(interaction, day)
		};
		_storyCustomerVisits[visitKey] = visit;
		return visit;
	}

	private static int ResolveStoryCustomerScheduledDay(CustomerInteractionDef interaction, int day)
	{
		if (interaction.Requires?.DayExact is int dayExact)
			return Math.Max(1, dayExact);
		if (interaction.Requires?.DayMin is int dayMin)
			return Math.Max(1, dayMin);

		return day;
	}

	private static StoryCustomerVisitRecord CloneStoryCustomerVisit(StoryCustomerVisitRecord visit)
	{
		return new StoryCustomerVisitRecord
		{
			VisitKey = visit.VisitKey,
			StoryCharacterId = visit.StoryCharacterId,
			VisitId = visit.VisitId,
			InteractionId = visit.InteractionId,
			ScheduledDay = visit.ScheduledDay,
			HasArrived = visit.HasArrived,
			ArrivalDay = visit.ArrivalDay,
			LastOutcome = visit.LastOutcome,
			OutcomeDay = visit.OutcomeDay,
			SelectedDialogueOptionIds = CloneSelectedDialogueOptionIds(visit.SelectedDialogueOptionIds)
		};
	}

	private static List<string> CloneSelectedDialogueOptionIds(IEnumerable<string>? selectedOptionIds)
	{
		var result = new List<string>();
		if (selectedOptionIds is null)
			return result;

		foreach (var selectedOptionId in selectedOptionIds)
		{
			if (string.IsNullOrWhiteSpace(selectedOptionId))
				continue;
			if (result.Any(id => string.Equals(id, selectedOptionId, StringComparison.OrdinalIgnoreCase)))
				continue;

			result.Add(selectedOptionId);
		}

		return result;
	}

	private static string BuildStoryCustomerVisitKey(CustomerInteractionDef interaction)
	{
		if (!interaction.IsStoryInteraction)
			return string.Empty;

		return BuildStoryCustomerVisitKey(interaction.StoryCharacterId, interaction.GetStoryVisitId(), interaction.Id);
	}

	private static string BuildStoryCustomerVisitKey(string storyCharacterId, string visitId, string interactionId)
	{
		if (string.IsNullOrWhiteSpace(storyCharacterId))
			return string.Empty;

		var resolvedVisitId = string.IsNullOrWhiteSpace(visitId) ? interactionId : visitId;
		if (string.IsNullOrWhiteSpace(resolvedVisitId))
			return string.Empty;

		return $"{storyCharacterId.Trim()}:{resolvedVisitId.Trim()}";
	}

	private static string NormalizeStoryCustomerOutcome(string? outcome, string arrivedOutcome)
	{
		if (string.IsNullOrWhiteSpace(outcome))
			return arrivedOutcome;

		return outcome.Trim().ToLowerInvariant();
	}
}
