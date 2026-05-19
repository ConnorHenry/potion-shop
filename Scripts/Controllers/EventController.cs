using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using OccultShop.Autoload;
using OccultShop.Models;
using OccultShop.Systems;

namespace OccultShop.Controllers;

public partial class EventController : Node
{
	private readonly Random _rng = new();

	public EventCardDef? DrawNightEvent(DataDb db, GameState state)
	{
		var eligible = db.Events
			.Where(e => Requirements.Met(state, e.Requires))
			.ToList();

		if (eligible.Count == 0) return null;

		return WeightedPick(eligible, x => Math.Max(1, x.Weight));
	}

	private T WeightedPick<T>(IReadOnlyList<T> list, Func<T, int> weight)
	{
		var total = 0;
		for (var i = 0; i < list.Count; i++) total += weight(list[i]);
		var roll = _rng.Next(0, total);
		var acc = 0;
		for (var i = 0; i < list.Count; i++)
		{
			acc += weight(list[i]);
			if (roll < acc) return list[i];
		}
		return list[^1];
	}
}
