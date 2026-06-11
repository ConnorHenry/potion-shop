using System;
using System.Collections.Generic;
using System.Linq;
using OccultShop.Models;

namespace OccultShop.Systems;

public sealed class PotionBatchStore
{
	private readonly Dictionary<string, Queue<List<string>>> _potionBatches = new(StringComparer.OrdinalIgnoreCase);
	private readonly Dictionary<string, Queue<List<IngredientPortionDef>>> _potionIngredientPortionBatches = new(StringComparer.OrdinalIgnoreCase);

	public void Clear()
	{
		_potionBatches.Clear();
		_potionIngredientPortionBatches.Clear();
	}

	public void Restore(
		Dictionary<string, List<List<string>>>? potionBatches,
		Dictionary<string, List<List<IngredientPortionDef>>>? potionIngredientPortionBatches)
	{
		_potionBatches.Clear();
		if (potionBatches is not null)
			RestorePotionBatches(potionBatches);

		_potionIngredientPortionBatches.Clear();
		if (potionIngredientPortionBatches is not null && potionIngredientPortionBatches.Count > 0)
			RestorePotionIngredientPortionBatches(potionIngredientPortionBatches);
		else
			RestoreUnmeasuredPortionBatchesFromLegacyBatches();
	}

	public Dictionary<string, List<List<string>>> ClonePotionBatches()
	{
		var copy = new Dictionary<string, List<List<string>>>(StringComparer.OrdinalIgnoreCase);
		foreach (var pair in _potionBatches)
			copy[pair.Key] = pair.Value.Select(batch => new List<string>(batch)).ToList();

		return copy;
	}

	public Dictionary<string, List<List<IngredientPortionDef>>> ClonePotionIngredientPortionBatches()
	{
		var copy = new Dictionary<string, List<List<IngredientPortionDef>>>(StringComparer.OrdinalIgnoreCase);
		foreach (var pair in _potionIngredientPortionBatches)
			copy[pair.Key] = pair.Value.Select(CloneIngredientPortionBatch).ToList();

		return copy;
	}

	public void RecordPotionBatch(string potionItemId, IReadOnlyList<string> ingredientIds)
	{
		if (string.IsNullOrWhiteSpace(potionItemId) || ingredientIds is null || ingredientIds.Count == 0)
			return;

		EnqueuePotionBatch(potionItemId, ingredientIds);
		EnqueuePotionIngredientPortionBatch(potionItemId, BuildUnmeasuredPortions(ingredientIds));
	}

	public void RecordPotionBatch(string potionItemId, IReadOnlyList<IngredientPortionDef> ingredientPortions)
	{
		if (string.IsNullOrWhiteSpace(potionItemId) || ingredientPortions is null || ingredientPortions.Count == 0)
			return;

		EnqueuePotionBatch(potionItemId, ingredientPortions.Select(x => x.IngredientId).ToList());
		EnqueuePotionIngredientPortionBatch(potionItemId, ingredientPortions);
	}

	public bool TryPeekPotionBatch(string potionItemId, out List<string> ingredientIds)
	{
		ingredientIds = new List<string>();
		if (!_potionBatches.TryGetValue(potionItemId, out var queue) || queue.Count == 0)
			return false;

		ingredientIds = new List<string>(queue.Peek());
		return true;
	}

	public bool TryPeekPotionIngredientPortionBatch(string potionItemId, out List<IngredientPortionDef> ingredientPortions)
	{
		ingredientPortions = new List<IngredientPortionDef>();
		if (!_potionIngredientPortionBatches.TryGetValue(potionItemId, out var queue) || queue.Count == 0)
			return false;

		ingredientPortions = CloneIngredientPortionBatch(queue.Peek());
		return true;
	}

	public void ConsumePotionBatches(string itemId, int quantity)
	{
		ConsumePotionBatchQueue(_potionBatches, itemId, quantity);
		ConsumePotionBatchQueue(_potionIngredientPortionBatches, itemId, quantity);
	}

	private void RestorePotionBatches(Dictionary<string, List<List<string>>> potionBatches)
	{
		foreach (var pair in potionBatches)
		{
			if (string.IsNullOrWhiteSpace(pair.Key) || pair.Value is null || pair.Value.Count == 0)
				continue;

			var queue = new Queue<List<string>>();
			foreach (var batch in pair.Value)
			{
				if (batch is null || batch.Count == 0)
					continue;

				queue.Enqueue(new List<string>(batch));
			}

			if (queue.Count > 0)
				_potionBatches[pair.Key] = queue;
		}
	}

	private void RestorePotionIngredientPortionBatches(Dictionary<string, List<List<IngredientPortionDef>>> potionIngredientPortionBatches)
	{
		foreach (var pair in potionIngredientPortionBatches)
		{
			if (string.IsNullOrWhiteSpace(pair.Key) || pair.Value is null || pair.Value.Count == 0)
				continue;

			var queue = new Queue<List<IngredientPortionDef>>();
			foreach (var batch in pair.Value)
			{
				var normalizedBatch = CloneIngredientPortionBatch(batch);
				if (normalizedBatch.Count == 0)
					continue;

				queue.Enqueue(normalizedBatch);
			}

			if (queue.Count > 0)
				_potionIngredientPortionBatches[pair.Key] = queue;
		}
	}

	private void RestoreUnmeasuredPortionBatchesFromLegacyBatches()
	{
		foreach (var pair in _potionBatches)
		{
			var queue = new Queue<List<IngredientPortionDef>>();
			foreach (var batch in pair.Value)
			{
				var portionBatch = BuildUnmeasuredPortions(batch);
				if (portionBatch.Count > 0)
					queue.Enqueue(portionBatch);
			}

			if (queue.Count > 0)
				_potionIngredientPortionBatches[pair.Key] = queue;
		}
	}

	private void EnqueuePotionBatch(string potionItemId, IReadOnlyList<string> ingredientIds)
	{
		if (!_potionBatches.TryGetValue(potionItemId, out var queue))
		{
			queue = new Queue<List<string>>();
			_potionBatches[potionItemId] = queue;
		}

		queue.Enqueue(new List<string>(ingredientIds));
	}

	private void EnqueuePotionIngredientPortionBatch(
		string potionItemId,
		IReadOnlyList<IngredientPortionDef> ingredientPortions)
	{
		var batch = CloneIngredientPortionBatch(ingredientPortions);
		if (batch.Count == 0)
			return;

		if (!_potionIngredientPortionBatches.TryGetValue(potionItemId, out var queue))
		{
			queue = new Queue<List<IngredientPortionDef>>();
			_potionIngredientPortionBatches[potionItemId] = queue;
		}

		queue.Enqueue(batch);
	}

	private static List<IngredientPortionDef> CloneIngredientPortionBatch(IReadOnlyList<IngredientPortionDef>? batch)
	{
		var clones = new List<IngredientPortionDef>();
		if (batch is null)
			return clones;

		foreach (var ingredientPortion in batch)
		{
			if (ingredientPortion is null || string.IsNullOrWhiteSpace(ingredientPortion.IngredientId))
				continue;

			clones.Add(new IngredientPortionDef
			{
				IngredientId = ingredientPortion.IngredientId,
				ItemId = ingredientPortion.ItemId,
				PreparationId = ingredientPortion.PreparationId,
				Grams = Math.Max(0, ingredientPortion.Grams)
			});
		}

		return clones;
	}

	private static List<IngredientPortionDef> BuildUnmeasuredPortions(IReadOnlyList<string> ingredientIds)
	{
		var portions = new List<IngredientPortionDef>(ingredientIds.Count);
		foreach (var ingredientId in ingredientIds)
		{
			if (string.IsNullOrWhiteSpace(ingredientId))
				continue;

			portions.Add(new IngredientPortionDef
			{
				IngredientId = ingredientId,
				ItemId = ingredientId,
				Grams = 0
			});
		}

		return portions;
	}

	private static void ConsumePotionBatchQueue<TBatch>(
		Dictionary<string, Queue<List<TBatch>>> batches,
		string itemId,
		int quantity)
	{
		if (!batches.TryGetValue(itemId, out var queue) || queue.Count == 0)
			return;

		for (var i = 0; i < quantity && queue.Count > 0; i++)
			queue.Dequeue();

		if (queue.Count == 0)
			batches.Remove(itemId);
	}
}
