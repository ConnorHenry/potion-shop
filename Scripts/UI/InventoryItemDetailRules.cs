using OccultShop.Models;

namespace OccultShop.UI;

public enum PotionBookAddAvailability
{
	Hidden,
	DisabledNoRecordedRecipe,
	Available
}

public static class InventoryItemDetailRules
{
	public static bool HasActiveRisk(ItemDef? item)
	{
		if (item?.Risks is null || item.Risks.Count == 0)
			return false;

		foreach (var risk in item.Risks)
		{
			if (!string.IsNullOrWhiteSpace(risk.Key) && risk.Value > 0)
				return true;
		}

		return false;
	}

	public static PotionBookAddAvailability GetPotionBookAddAvailability(
		string itemId,
		ItemDef? item,
		bool isPotion,
		bool knowsPotion,
		bool hasRecordedRecipe)
	{
		if (string.IsNullOrWhiteSpace(itemId))
			return PotionBookAddAvailability.Hidden;
		if (item is null)
			return PotionBookAddAvailability.Hidden;
		if (!isPotion)
			return PotionBookAddAvailability.Hidden;
		if (item.Treatment is not null)
			return PotionBookAddAvailability.Hidden;
		if (HasActiveRisk(item))
			return PotionBookAddAvailability.Hidden;
		if (knowsPotion)
			return PotionBookAddAvailability.Hidden;

		return hasRecordedRecipe
			? PotionBookAddAvailability.Available
			: PotionBookAddAvailability.DisabledNoRecordedRecipe;
	}
}
