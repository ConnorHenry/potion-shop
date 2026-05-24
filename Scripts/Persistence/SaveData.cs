using System;
using System.Collections.Generic;
using OccultShop.Models;

namespace OccultShop.Persistence;

public sealed class SaveFileData
{
	public int Version { get; set; } = 1;
	public DateTime SavedAtUtc { get; set; } = DateTime.UtcNow;
	public GameStateSnapshot GameState { get; set; } = new();
	public List<ItemDef> RuntimeItems { get; set; } = new();
}

public sealed class GameStateSnapshot
{
	public int Day { get; set; } = 1;
	public int Gold { get; set; } = 0;
	public int Dread { get; set; } = 0;
	public Dictionary<string, int> Inventory { get; set; } = new();
	public List<string> ActiveRules { get; set; } = new();
	public List<string> KnownPotions { get; set; } = new();
	public Dictionary<string, string> PotionDisplayNames { get; set; } = new(StringComparer.OrdinalIgnoreCase);
	public Dictionary<string, int> PotionBasePrices { get; set; } = new(StringComparer.OrdinalIgnoreCase);
	public Dictionary<string, List<string>> PotionRecipes { get; set; } = new(StringComparer.OrdinalIgnoreCase);
	public Dictionary<string, string> CombinationPotionItems { get; set; } = new(StringComparer.OrdinalIgnoreCase);
	public Dictionary<string, List<List<string>>> PotionBatches { get; set; } = new(StringComparer.OrdinalIgnoreCase);
	public CustomerRequestDef? ActiveCustomerRequest { get; set; }
}
