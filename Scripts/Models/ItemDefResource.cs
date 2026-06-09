using System.Collections.Generic;
using Godot;

namespace OccultShop.Models;

[GlobalClass]
public partial class ItemDefResource : Resource
{
	private string _id = string.Empty;
	private string _name = string.Empty;
	private string _iconPath = string.Empty;
	private string _description = string.Empty;
	private bool _startsKnownInIngredientBook;
	private int _quality = 50;
	private int _basePrice;
	private Godot.Collections.Array<string> _tags = new();
	private Godot.Collections.Dictionary<string, int> _traits = new();
	private Godot.Collections.Dictionary<string, int> _risks = new();
	private Godot.Collections.Array _ingredientEffects = new();
	private string _consumableEffectKind = string.Empty;
	private string _consumableEffectRiskId = string.Empty;
	private string _consumableEffectDescription = string.Empty;
	private Godot.Collections.Array<string> _consumableAllowedTargetTags = new();
	private string _treatmentBaseItemId = string.Empty;
	private string _treatmentConsumableItemId = string.Empty;
	private string _treatmentRemovedRisk = string.Empty;

	[Export]
	public string Id
	{
		get => _id;
		set => SetString(ref _id, value);
	}

	[Export]
	public string Name
	{
		get => _name;
		set => SetString(ref _name, value);
	}

	[Export]
	public string IconPath
	{
		get => _iconPath;
		set => SetString(ref _iconPath, value);
	}

	[Export(PropertyHint.MultilineText)]
	public string Description
	{
		get => _description;
		set => SetString(ref _description, value);
	}

	[Export]
	public bool StartsKnownInIngredientBook
	{
		get => _startsKnownInIngredientBook;
		set => SetBool(ref _startsKnownInIngredientBook, value);
	}

	[Export]
	public int Quality
	{
		get => _quality;
		set => SetInt(ref _quality, value);
	}

	[Export]
	public int BasePrice
	{
		get => _basePrice;
		set => SetInt(ref _basePrice, value);
	}

	[Export]
	public Godot.Collections.Array<string> Tags
	{
		get => _tags;
		set
		{
			_tags = value ?? new Godot.Collections.Array<string>();
			EmitChanged();
		}
	}

	[Export]
	public Godot.Collections.Dictionary<string, int> Traits
	{
		get => _traits;
		set
		{
			_traits = value ?? new Godot.Collections.Dictionary<string, int>();
			EmitChanged();
		}
	}

	[Export]
	public Godot.Collections.Dictionary<string, int> Risks
	{
		get => _risks;
		set
		{
			_risks = value ?? new Godot.Collections.Dictionary<string, int>();
			EmitChanged();
		}
	}

	[Export]
	public Godot.Collections.Array IngredientEffects
	{
		get => _ingredientEffects;
		set
		{
			_ingredientEffects = value ?? new Godot.Collections.Array();
			EmitChanged();
		}
	}

	[Export]
	public string ConsumableEffectKind
	{
		get => _consumableEffectKind;
		set => SetString(ref _consumableEffectKind, value);
	}

	[Export]
	public string ConsumableEffectRiskId
	{
		get => _consumableEffectRiskId;
		set => SetString(ref _consumableEffectRiskId, value);
	}

	[Export(PropertyHint.MultilineText)]
	public string ConsumableEffectDescription
	{
		get => _consumableEffectDescription;
		set => SetString(ref _consumableEffectDescription, value);
	}

	[Export]
	public Godot.Collections.Array<string> ConsumableAllowedTargetTags
	{
		get => _consumableAllowedTargetTags;
		set
		{
			_consumableAllowedTargetTags = value ?? new Godot.Collections.Array<string>();
			EmitChanged();
		}
	}

	[Export]
	public string TreatmentBaseItemId
	{
		get => _treatmentBaseItemId;
		set => SetString(ref _treatmentBaseItemId, value);
	}

	[Export]
	public string TreatmentConsumableItemId
	{
		get => _treatmentConsumableItemId;
		set => SetString(ref _treatmentConsumableItemId, value);
	}

	[Export]
	public string TreatmentRemovedRisk
	{
		get => _treatmentRemovedRisk;
		set => SetString(ref _treatmentRemovedRisk, value);
	}

	public ItemDef ToItemDef()
	{
		var tags = new List<string>(_tags.Count);
		foreach (var tag in _tags)
		{
			if (!string.IsNullOrWhiteSpace(tag))
				tags.Add(tag);
		}

		var traits = new Dictionary<string, int>(_traits.Count);
		foreach (var pair in _traits)
		{
			if (string.IsNullOrWhiteSpace(pair.Key))
				continue;

			traits[pair.Key] = pair.Value;
		}

		var risks = new Dictionary<string, int>(_risks.Count);
		foreach (var pair in _risks)
		{
			if (string.IsNullOrWhiteSpace(pair.Key))
				continue;

			risks[pair.Key] = pair.Value;
		}

		var item = new ItemDef
		{
			Id = Id,
			Name = Name,
			IconPath = string.IsNullOrWhiteSpace(IconPath) ? null : IconPath,
			Description = Description,
			StartsKnownInIngredientBook = StartsKnownInIngredientBook,
			Tags = tags,
			Quality = Quality,
			Traits = traits,
			Risks = risks,
			IngredientEffects = ParseIngredientEffects(_ingredientEffects),
			BasePrice = BasePrice
		};

		if (!string.IsNullOrWhiteSpace(ConsumableEffectKind))
		{
			item.ConsumableEffect = new ConsumableEffectDef
			{
				Kind = ConsumableEffectKind,
				RiskId = ConsumableEffectRiskId,
				Description = ConsumableEffectDescription
			};
		}

		var allowedTargetTags = new List<string>(_consumableAllowedTargetTags.Count);
		foreach (var tag in _consumableAllowedTargetTags)
		{
			if (!string.IsNullOrWhiteSpace(tag))
				allowedTargetTags.Add(tag);
		}

		if (allowedTargetTags.Count > 0)
		{
			item.ConsumableGate = new ConsumableGateDef
			{
				AllowedTargetTags = allowedTargetTags
			};
		}

		if (!string.IsNullOrWhiteSpace(TreatmentBaseItemId) || !string.IsNullOrWhiteSpace(TreatmentConsumableItemId))
		{
			item.Treatment = new ItemTreatmentDef
			{
				BaseItemId = TreatmentBaseItemId,
				ConsumableItemId = TreatmentConsumableItemId,
				RemovedRisk = TreatmentRemovedRisk
			};
		}

		return item;
	}

	public static ItemDefResource FromItemDef(ItemDef item)
	{
		var resource = new ItemDefResource();
		resource.ApplyFromItemDef(item);
		return resource;
	}

	public void ApplyFromItemDef(ItemDef item)
	{
		Id = item.Id;
		Name = item.Name;
		IconPath = item.IconPath ?? string.Empty;
		Description = item.Description;
		StartsKnownInIngredientBook = item.StartsKnownInIngredientBook;
		Quality = item.Quality;
		BasePrice = item.BasePrice;

		var tags = new Godot.Collections.Array<string>();
		if (item.Tags is not null)
		{
			foreach (var tag in item.Tags)
			{
				if (!string.IsNullOrWhiteSpace(tag))
					tags.Add(tag);
			}
		}

		var traits = new Godot.Collections.Dictionary<string, int>();
		if (item.Traits is not null)
		{
			foreach (var pair in item.Traits)
			{
				if (string.IsNullOrWhiteSpace(pair.Key))
					continue;

				traits[pair.Key] = pair.Value;
			}
		}

		var risks = new Godot.Collections.Dictionary<string, int>();
		if (item.Risks is not null)
		{
			foreach (var pair in item.Risks)
			{
				if (string.IsNullOrWhiteSpace(pair.Key))
					continue;

				risks[pair.Key] = pair.Value;
			}
		}

		Tags = tags;
		Traits = traits;
		Risks = risks;
		IngredientEffects = BuildIngredientEffectArray(item.IngredientEffects);

		ConsumableEffectKind = item.ConsumableEffect?.Kind ?? string.Empty;
		ConsumableEffectRiskId = item.ConsumableEffect?.RiskId ?? string.Empty;
		ConsumableEffectDescription = item.ConsumableEffect?.Description ?? string.Empty;

		var allowedTargetTags = new Godot.Collections.Array<string>();
		if (item.ConsumableGate?.AllowedTargetTags is not null)
		{
			foreach (var tag in item.ConsumableGate.AllowedTargetTags)
			{
				if (!string.IsNullOrWhiteSpace(tag))
					allowedTargetTags.Add(tag);
			}
		}
		ConsumableAllowedTargetTags = allowedTargetTags;

		TreatmentBaseItemId = item.Treatment?.BaseItemId ?? string.Empty;
		TreatmentConsumableItemId = item.Treatment?.ConsumableItemId ?? string.Empty;
		TreatmentRemovedRisk = item.Treatment?.RemovedRisk ?? string.Empty;
	}

	private static List<IngredientEffectDef> ParseIngredientEffects(Godot.Collections.Array entries)
	{
		var effects = new List<IngredientEffectDef>(entries.Count);
		foreach (var entryValue in entries)
		{
			if (entryValue.VariantType != Variant.Type.Dictionary)
				continue;

			var entry = entryValue.As<Godot.Collections.Dictionary>();
			var kind = ReadEffectString(entry, "kind");
			if (string.IsNullOrWhiteSpace(kind))
				continue;

			effects.Add(new IngredientEffectDef
			{
				Kind = kind,
				Family = ReadEffectString(entry, "family"),
				Name = ReadEffectString(entry, "name"),
				Description = ReadEffectString(entry, "description"),
				Amount = ReadEffectInt(entry, "amount"),
				SecondaryAmount = ReadEffectInt(entry, "secondaryAmount"),
				TraitId = ReadEffectString(entry, "traitId"),
				RiskId = ReadEffectString(entry, "riskId")
			});
		}

		return effects;
	}

	private static Godot.Collections.Array BuildIngredientEffectArray(List<IngredientEffectDef>? effects)
	{
		var array = new Godot.Collections.Array();
		if (effects is null)
			return array;

		foreach (var effect in effects)
		{
			if (effect is null || string.IsNullOrWhiteSpace(effect.Kind))
				continue;

			array.Add(new Godot.Collections.Dictionary
			{
				["kind"] = effect.Kind,
				["family"] = effect.Family,
				["name"] = effect.Name,
				["description"] = effect.Description,
				["amount"] = effect.Amount,
				["secondaryAmount"] = effect.SecondaryAmount,
				["traitId"] = effect.TraitId,
				["riskId"] = effect.RiskId
			});
		}

		return array;
	}

	private static string ReadEffectString(Godot.Collections.Dictionary entry, string key)
	{
		if (!entry.ContainsKey(key))
			return string.Empty;

		var value = entry[key];
		if (value.VariantType == Variant.Type.Nil)
			return string.Empty;

		return value.VariantType == Variant.Type.String
			? value.As<string>()
			: value.ToString();
	}

	private static int ReadEffectInt(Godot.Collections.Dictionary entry, string key)
	{
		if (!entry.ContainsKey(key))
			return 0;

		var value = entry[key];
		if (value.VariantType == Variant.Type.Int)
			return (int)value.As<long>();
		if (value.VariantType == Variant.Type.Float)
			return (int)System.Math.Round(value.As<double>());

		return int.TryParse(ReadEffectString(entry, key), out var parsed) ? parsed : 0;
	}

	private void SetString(ref string target, string? value)
	{
		var sanitized = value ?? string.Empty;
		if (target == sanitized)
			return;

		target = sanitized;
		EmitChanged();
	}

	private void SetInt(ref int target, int value)
	{
		if (target == value)
			return;

		target = value;
		EmitChanged();
	}

	private void SetBool(ref bool target, bool value)
	{
		if (target == value)
			return;

		target = value;
		EmitChanged();
	}
}
