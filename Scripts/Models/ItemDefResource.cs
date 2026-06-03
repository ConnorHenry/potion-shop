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
	private int _quality = 50;
	private int _basePrice;
	private Godot.Collections.Array<string> _tags = new();
	private Godot.Collections.Dictionary<string, int> _traits = new();
	private Godot.Collections.Dictionary<string, int> _risks = new();
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
			Tags = tags,
			Quality = Quality,
			Traits = traits,
			Risks = risks,
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
}
