using Godot;

namespace OccultShop.UI;

[GlobalClass]
public partial class InventorySlotLayoutSettings : Resource
{
	public const string DefaultResourcePath = "res://Assets/UI/InventorySlotLayoutSettings.tres";

	[Export] public InventorySlotLayoutProfile IngredientShelfSlot { get; set; } = InventorySlotLayoutProfile.CreateIngredientShelfDefault();
	[Export] public InventorySlotLayoutProfile ConsumableShelfSlot { get; set; } = InventorySlotLayoutProfile.CreateConsumableShelfDefault();
	[Export] public InventorySlotLayoutProfile PotionInventorySlot { get; set; } = InventorySlotLayoutProfile.CreatePotionInventoryDefault();
	[Export] public InventorySlotLayoutProfile CustomerPotionSlot { get; set; } = InventorySlotLayoutProfile.CreateCustomerPotionDefault();

	public static InventorySlotLayoutSettings LoadDefault(bool forceReload = false)
	{
		return Load(DefaultResourcePath, forceReload);
	}

	public static InventorySlotLayoutSettings Load(string resourcePath, bool forceReload = false)
	{
		var resolvedPath = string.IsNullOrWhiteSpace(resourcePath)
			? DefaultResourcePath
			: resourcePath;
		if (!ResourceLoader.Exists(resolvedPath))
		{
			GD.PushError($"InventorySlotLayoutSettings: Resource '{resolvedPath}' does not exist. Using built-in defaults.");
			return CreateDefault();
		}

		var cacheMode = forceReload
			? ResourceLoader.CacheMode.Ignore
			: ResourceLoader.CacheMode.Reuse;
		var settings = ResourceLoader.Load<InventorySlotLayoutSettings>(resolvedPath, string.Empty, cacheMode);
		if (settings is null)
		{
			GD.PushError($"InventorySlotLayoutSettings: Failed to load '{resolvedPath}'. Using built-in defaults.");
			return CreateDefault();
		}

		settings.EnsureProfiles();
		return settings;
	}

	public static InventorySlotLayoutSettings CreateDefault()
	{
		var settings = new InventorySlotLayoutSettings();
		settings.ResetToDefaults();
		return settings;
	}

	public void ResetToDefaults()
	{
		IngredientShelfSlot = InventorySlotLayoutProfile.CreateIngredientShelfDefault();
		ConsumableShelfSlot = InventorySlotLayoutProfile.CreateConsumableShelfDefault();
		PotionInventorySlot = InventorySlotLayoutProfile.CreatePotionInventoryDefault();
		CustomerPotionSlot = InventorySlotLayoutProfile.CreateCustomerPotionDefault();
		EmitChanged();
	}

	public void EnsureProfiles()
	{
		IngredientShelfSlot ??= InventorySlotLayoutProfile.CreateIngredientShelfDefault();
		ConsumableShelfSlot ??= InventorySlotLayoutProfile.CreateConsumableShelfDefault();
		PotionInventorySlot ??= InventorySlotLayoutProfile.CreatePotionInventoryDefault();
		CustomerPotionSlot ??= InventorySlotLayoutProfile.CreateCustomerPotionDefault();
	}

	public InventorySlotLayoutProfile GetProfile(InventorySlotLayoutKind kind)
	{
		EnsureProfiles();
		return kind switch
		{
			InventorySlotLayoutKind.IngredientShelf => IngredientShelfSlot,
			InventorySlotLayoutKind.ConsumableShelf => ConsumableShelfSlot,
			InventorySlotLayoutKind.PotionInventory => PotionInventorySlot,
			InventorySlotLayoutKind.CustomerPotion => CustomerPotionSlot,
			_ => IngredientShelfSlot
		};
	}

	public void ResetProfileToDefault(InventorySlotLayoutKind kind)
	{
		var profile = GetProfile(kind);
		profile.CopyFrom(kind switch
		{
			InventorySlotLayoutKind.IngredientShelf => InventorySlotLayoutProfile.CreateIngredientShelfDefault(),
			InventorySlotLayoutKind.ConsumableShelf => InventorySlotLayoutProfile.CreateConsumableShelfDefault(),
			InventorySlotLayoutKind.PotionInventory => InventorySlotLayoutProfile.CreatePotionInventoryDefault(),
			InventorySlotLayoutKind.CustomerPotion => InventorySlotLayoutProfile.CreateCustomerPotionDefault(),
			_ => InventorySlotLayoutProfile.CreateIngredientShelfDefault()
		});
		EmitChanged();
	}
}
