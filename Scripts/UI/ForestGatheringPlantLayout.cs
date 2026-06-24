using System;
using System.Collections.Generic;
using Godot;

namespace OccultShop.UI;

public sealed class ForestGatheringPlantLayout
{
	private const int PlacementAttemptsPerPlant = 220;
	private const float PlacementEdgePadding = 0.025f;
	private const float PlacementMinY = 0.18f;
	private const float PlacementMaxY = 0.86f;
	private const float PlacementAcceptablePenalty = 0.0008f;

	private readonly List<ForestGatheringPlantEntry> _placedEntries = new();

	public List<ForestGatheringPlantEntry> CreateRandomizedEntries(
		IReadOnlyList<ForestGatheringPlantDefinition> definitions,
		RandomNumberGenerator random)
	{
		_placedEntries.Clear();

		foreach (var definition in definitions)
		{
			var center = FindPlantPlacement(definition.Size, random);
			_placedEntries.Add(new ForestGatheringPlantEntry(
				definition.ItemId,
				definition.Label,
				center,
				definition.Size,
				definition.TexturePath,
				ForestGatheringPlantCatalog.BuildInspectionTexturePath(definition.TexturePath)));
		}

		_placedEntries.Sort((left, right) => left.Center.Y.CompareTo(right.Center.Y));
		return new List<ForestGatheringPlantEntry>(_placedEntries);
	}

	private Vector2 FindPlantPlacement(Vector2 size, RandomNumberGenerator random)
	{
		var halfSize = size * 0.5f;
		var minX = Math.Clamp(halfSize.X + PlacementEdgePadding, 0.0f, 1.0f);
		var maxX = Math.Clamp(1.0f - halfSize.X - PlacementEdgePadding, 0.0f, 1.0f);
		var minY = Math.Clamp(Math.Max(PlacementMinY, halfSize.Y + PlacementEdgePadding), 0.0f, 1.0f);
		var maxY = Math.Clamp(Math.Min(PlacementMaxY, 1.0f - halfSize.Y - PlacementEdgePadding), 0.0f, 1.0f);

		if (minX > maxX)
			(minX, maxX) = (maxX, minX);
		if (minY > maxY)
			(minY, maxY) = (maxY, minY);

		var bestCenter = new Vector2(random.RandfRange(minX, maxX), random.RandfRange(minY, maxY));
		var bestPenalty = CalculatePlacementPenalty(bestCenter, size);
		for (var attempt = 0; attempt < PlacementAttemptsPerPlant; attempt++)
		{
			var candidate = new Vector2(random.RandfRange(minX, maxX), random.RandfRange(minY, maxY));
			var penalty = CalculatePlacementPenalty(candidate, size);
			if (penalty <= PlacementAcceptablePenalty)
				return candidate;

			if (penalty >= bestPenalty)
				continue;

			bestCenter = candidate;
			bestPenalty = penalty;
		}

		return bestCenter;
	}

	private float CalculatePlacementPenalty(Vector2 center, Vector2 size)
	{
		var penalty = 0.0f;
		foreach (var placed in _placedEntries)
		{
			var combinedHalfSize = (size + placed.Size) * 0.5f;
			var overlapX = combinedHalfSize.X - MathF.Abs(center.X - placed.Center.X);
			var overlapY = combinedHalfSize.Y - MathF.Abs(center.Y - placed.Center.Y);
			if (overlapX <= 0.0f || overlapY <= 0.0f)
				continue;

			penalty += overlapX * overlapY;
		}

		return penalty;
	}
}
