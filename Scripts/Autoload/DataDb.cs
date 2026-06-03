using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Godot;
using OccultShop.Models;

namespace OccultShop.Autoload;

public partial class DataDb : Node
{
	private const string DefaultAuthoredDataPath = "res://Data/authored_data.tres";

	[Export(PropertyHint.File, "*.tres,*.res")]
	public string AuthoredDataPath { get; set; } = DefaultAuthoredDataPath;

	public IReadOnlyDictionary<string, ItemDef> Items => _items;
	public IReadOnlyDictionary<string, RuleDef> Rules => _rules;
	public IReadOnlyList<EventCardDef> Events => _events;
	public IReadOnlyList<CustomerInteractionDef> CustomerInteractions => _customerInteractions;
	public IReadOnlyList<SynergyRule> Synergies => _synergies;
	public IReadOnlyList<PotionRecipeDef> PotionRecipes => _potionRecipes;

	private Dictionary<string, ItemDef> _items = new();
	private Dictionary<string, RuleDef> _rules = new();
	private List<EventCardDef> _events = new();
	private List<CustomerInteractionDef> _customerInteractions = new();
	private List<SynergyRule> _synergies = new();
	private List<PotionRecipeDef> _potionRecipes = new();

	public override void _Ready()
	{
		ReloadAll();
	}

	public void ReloadAll()
	{
		var authoredData = ResourceLoader.Load<AuthoredDataResource>(AuthoredDataPath);
		if (authoredData is null)
		{
			var exists = ResourceLoader.Exists(AuthoredDataPath);
			var genericResource = ResourceLoader.Load<Resource>(AuthoredDataPath);
			var genericTypeName = genericResource?.GetType().FullName ?? "<null>";
			GD.PushError($"DataDb: Failed to load authored data resource at '{AuthoredDataPath}'. Exists={exists}. GenericLoadType={genericTypeName}.");
			_items = new Dictionary<string, ItemDef>();
			_rules = new Dictionary<string, RuleDef>();
			_events = new List<EventCardDef>();
			_customerInteractions = new List<CustomerInteractionDef>();
			_synergies = new List<SynergyRule>();
			_potionRecipes = new List<PotionRecipeDef>();
			return;
		}

		var itemsResource = LoadSection<AuthoredItemsResource>(authoredData.ItemsPath, "items");
		var rulesResource = LoadSection<AuthoredRulesResource>(authoredData.RulesPath, "rules");
		var eventsResource = LoadSection<AuthoredEventsResource>(authoredData.EventsPath, "events");
		var customerInteractionsResource = LoadSection<AuthoredCustomerInteractionsResource>(authoredData.CustomerInteractionsPath, "customer interactions");
		var synergiesResource = LoadSection<AuthoredSynergiesResource>(authoredData.SynergiesPath, "synergies");
		var potionRecipesResource = LoadSection<AuthoredPotionRecipesResource>(authoredData.PotionRecipesPath, "potion recipes");

		_items = ParseItems(itemsResource?.Entries ?? new Godot.Collections.Array())
			.ToDictionary(x => x.Id, x => x, StringComparer.OrdinalIgnoreCase);
		_rules = ParseRules(rulesResource?.Entries ?? new Godot.Collections.Array())
			.ToDictionary(x => x.Id, x => x, StringComparer.OrdinalIgnoreCase);
		_events = ParseEvents(eventsResource?.Entries ?? new Godot.Collections.Array());
		_customerInteractions = ParseCustomerInteractions(customerInteractionsResource?.Entries ?? new Godot.Collections.Array());
		_synergies = ParseSynergies(synergiesResource?.Entries ?? new Godot.Collections.Array());
		_potionRecipes = ParsePotionRecipes(potionRecipesResource?.Entries ?? new Godot.Collections.Array());
	}

	private static TSection? LoadSection<TSection>(string path, string sectionName) where TSection : Resource
	{
		if (string.IsNullOrWhiteSpace(path))
		{
			GD.PushError($"DataDb: Missing path for authored {sectionName} section.");
			return null;
		}

		var section = ResourceLoader.Load<TSection>(path);
		if (section is null)
			GD.PushError($"DataDb: Failed to load authored {sectionName} section at '{path}'.");

		return section;
	}

	public bool TryGetItem(string itemId, out ItemDef item)
	{
		return _items.TryGetValue(itemId, out item!);
	}

	private static List<ItemDef> ParseItems(Godot.Collections.Array entries)
	{
		var items = new List<ItemDef>(entries.Count);
		foreach (var entryValue in entries)
		{
			if (!TryReadDictionary(entryValue, out var entry))
				continue;

			var id = ReadString(entry, "id");
			if (string.IsNullOrWhiteSpace(id))
				continue;

			var basePrice = ReadInt(entry, "price", 0);
			if (entry.ContainsKey("basePrice"))
				basePrice = ReadInt(entry, "basePrice", basePrice);

			items.Add(new ItemDef
			{
				Id = id,
				Name = ReadString(entry, "name"),
				IconPath = ReadNullableString(entry, "iconPath"),
				Description = ReadString(entry, "description"),
				Tags = ReadStringList(entry, "tags"),
				Quality = ReadInt(entry, "quality", 50),
				Traits = ReadStringIntDictionary(entry, "traits"),
				Risks = ReadStringIntDictionary(entry, "risks"),
				BasePrice = Math.Max(0, basePrice),
				ConsumableEffect = ParseConsumableEffect(ReadDictionary(entry, "consumableEffect")),
				ConsumableGate = ParseConsumableGate(ReadDictionary(entry, "consumableGate")),
				Treatment = ParseTreatment(ReadDictionary(entry, "treatment"))
			});
		}

		return items;
	}

	private static ConsumableEffectDef? ParseConsumableEffect(Godot.Collections.Dictionary? entry)
	{
		if (entry is null || entry.Count == 0)
			return null;

		var kind = ReadString(entry, "kind");
		if (string.IsNullOrWhiteSpace(kind))
			return null;

		return new ConsumableEffectDef
		{
			Kind = kind,
			RiskId = ReadString(entry, "riskId"),
			Description = ReadString(entry, "description")
		};
	}

	private static ConsumableGateDef? ParseConsumableGate(Godot.Collections.Dictionary? entry)
	{
		if (entry is null || entry.Count == 0)
			return null;

		var allowedTargetTags = ReadStringList(entry, "allowedTargetTags");
		if (allowedTargetTags.Count == 0)
			return null;

		return new ConsumableGateDef
		{
			AllowedTargetTags = allowedTargetTags
		};
	}

	private static ItemTreatmentDef? ParseTreatment(Godot.Collections.Dictionary? entry)
	{
		if (entry is null || entry.Count == 0)
			return null;

		var baseItemId = ReadString(entry, "baseItemId");
		var consumableItemId = ReadString(entry, "consumableItemId");
		if (string.IsNullOrWhiteSpace(baseItemId) && string.IsNullOrWhiteSpace(consumableItemId))
			return null;

		return new ItemTreatmentDef
		{
			BaseItemId = baseItemId,
			ConsumableItemId = consumableItemId,
			RemovedRisk = ReadString(entry, "removedRisk")
		};
	}

	private static List<RuleDef> ParseRules(Godot.Collections.Array entries)
	{
		var rules = new List<RuleDef>(entries.Count);
		foreach (var entryValue in entries)
		{
			if (!TryReadDictionary(entryValue, out var entry))
				continue;

			var id = ReadString(entry, "id");
			if (string.IsNullOrWhiteSpace(id))
				continue;

			rules.Add(new RuleDef
			{
				Id = id,
				Name = ReadString(entry, "name"),
				Desc = ReadString(entry, "desc")
			});
		}

		return rules;
	}

	private static List<EventCardDef> ParseEvents(Godot.Collections.Array entries)
	{
		var events = new List<EventCardDef>(entries.Count);
		foreach (var entryValue in entries)
		{
			if (!TryReadDictionary(entryValue, out var entry))
				continue;

			var id = ReadString(entry, "id");
			if (string.IsNullOrWhiteSpace(id))
				continue;

			events.Add(new EventCardDef
			{
				Id = id,
				Title = ReadString(entry, "title"),
				Text = ReadString(entry, "text"),
				CharacterImagePath = ReadNullableString(entry, "characterImagePath"),
				Requires = ParseRequirements(ReadDictionary(entry, "requires")),
				Weight = ReadInt(entry, "weight", 1),
				Choices = ParseEventChoices(ReadArray(entry, "choices"))
			});
		}

		return events;
	}

	private static List<CustomerInteractionDef> ParseCustomerInteractions(Godot.Collections.Array entries)
	{
		var interactions = new List<CustomerInteractionDef>(entries.Count);
		foreach (var entryValue in entries)
		{
			if (!TryReadDictionary(entryValue, out var entry))
				continue;

			var id = ReadString(entry, "id");
			if (string.IsNullOrWhiteSpace(id))
				continue;

			interactions.Add(new CustomerInteractionDef
			{
				Id = id,
				Title = ReadString(entry, "title"),
				Text = ReadString(entry, "text"),
				CharacterImagePath = ReadNullableString(entry, "characterImagePath"),
				Pool = ReadString(entry, "pool"),
				Difficulty = Math.Max(1, ReadInt(entry, "difficulty", 1)),
				StoryCharacterId = ReadString(entry, "storyCharacterId"),
				VisitId = ReadString(entry, "visitId"),
				DialogueStartNodeId = ReadString(entry, "dialogueStartNodeId"),
				DialogueNodes = ParseCustomerDialogueNodes(ReadArray(entry, "dialogueNodes")),
				Requires = ParseRequirements(ReadDictionary(entry, "requires")),
				Weight = ReadInt(entry, "weight", 1),
				DesiredTraits = ReadStringIntDictionary(entry, "desiredTraits"),
				BadTraits = ReadStringIntDictionary(entry, "badTraits"),
				OnSuccessEffects = ParseEffects(ReadArray(entry, "onSuccessEffects")),
				OnFailureEffects = ParseEffects(ReadArray(entry, "onFailureEffects")),
				OnSkipEffects = ParseEffects(ReadArray(entry, "onSkipEffects"))
			});
		}

		return interactions;
	}

	private static List<CustomerDialogueNodeDef> ParseCustomerDialogueNodes(Godot.Collections.Array entries)
	{
		var nodes = new List<CustomerDialogueNodeDef>(entries.Count);
		foreach (var entryValue in entries)
		{
			if (!TryReadDictionary(entryValue, out var entry))
				continue;

			var id = ReadString(entry, "id");
			if (string.IsNullOrWhiteSpace(id))
				continue;

			nodes.Add(new CustomerDialogueNodeDef
			{
				Id = id,
				Text = ReadString(entry, "text"),
				Options = ParseCustomerDialogueOptions(ReadArray(entry, "options"))
			});
		}

		return nodes;
	}

	private static List<CustomerDialogueOptionDef> ParseCustomerDialogueOptions(Godot.Collections.Array entries)
	{
		var options = new List<CustomerDialogueOptionDef>(entries.Count);
		foreach (var entryValue in entries)
		{
			if (!TryReadDictionary(entryValue, out var entry))
				continue;

			var label = ReadString(entry, "label");
			if (string.IsNullOrWhiteSpace(label))
				continue;

			options.Add(new CustomerDialogueOptionDef
			{
				Id = ReadString(entry, "id", label),
				Label = label,
				ResponseText = ReadString(entry, "responseText"),
				NextNodeId = ReadString(entry, "nextNodeId"),
				EndsInteraction = ReadBool(entry, "endsInteraction"),
				Effects = ParseEffects(ReadArray(entry, "effects"))
			});
		}

		return options;
	}

	private static List<SynergyRule> ParseSynergies(Godot.Collections.Array entries)
	{
		var synergies = new List<SynergyRule>(entries.Count);
		foreach (var entryValue in entries)
		{
			if (!TryReadDictionary(entryValue, out var entry))
				continue;

			var id = ReadString(entry, "id");
			if (string.IsNullOrWhiteSpace(id))
				continue;

			synergies.Add(new SynergyRule
			{
				Id = id,
				RequiredTraits = ReadStringList(entry, "requiredTraits"),
				RequiredRisks = ReadStringList(entry, "requiredRisks"),
				Modifier = ReadInt(entry, "modifier", 0),
				ResultTrait = ReadString(entry, "resultTrait"),
				Description = ReadString(entry, "description"),
				AddedRisk = ReadNullableString(entry, "addedRisk"),
				AddedRiskStrength = ReadInt(entry, "addedRiskStrength", 0)
			});
		}

		return synergies;
	}

	private static List<PotionRecipeDef> ParsePotionRecipes(Godot.Collections.Array entries)
	{
		var recipes = new List<PotionRecipeDef>(entries.Count);
		foreach (var entryValue in entries)
		{
			if (!TryReadDictionary(entryValue, out var entry))
				continue;

			var id = ReadString(entry, "id");
			var name = ReadString(entry, "name");
			if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(name))
				continue;

			var ingredientIds = ReadStringList(entry, "ingredientIds")
				.Select(x => x.Trim())
				.Where(x => !string.IsNullOrWhiteSpace(x))
				.ToList();
			if (ingredientIds.Count == 0)
				continue;

			var traits = ReadStringList(entry, "traits")
				.Select(x => x.Trim().ToLowerInvariant())
				.Where(x => !string.IsNullOrWhiteSpace(x))
				.ToList();

			recipes.Add(new PotionRecipeDef
			{
				Id = id,
				Name = name,
				IngredientIds = ingredientIds,
				Traits = traits
			});
		}

		return recipes;
	}

	private static List<EventChoiceDef> ParseEventChoices(Godot.Collections.Array entries)
	{
		var choices = new List<EventChoiceDef>(entries.Count);
		foreach (var entryValue in entries)
		{
			if (!TryReadDictionary(entryValue, out var entry))
				continue;

			var label = ReadString(entry, "label");
			if (string.IsNullOrWhiteSpace(label))
				continue;

			choices.Add(new EventChoiceDef
			{
				Label = label,
				Requires = ParseRequirements(ReadDictionary(entry, "requires")),
				Effects = ParseEffects(ReadArray(entry, "effects"))
			});
		}

		return choices;
	}

	private static List<EffectDef> ParseEffects(Godot.Collections.Array entries)
	{
		var effects = new List<EffectDef>(entries.Count);
		foreach (var entryValue in entries)
		{
			if (!TryReadDictionary(entryValue, out var entry))
				continue;

			effects.Add(new EffectDef
			{
				AddGold = ReadNullableInt(entry, "addGold"),
				AddDread = ReadNullableInt(entry, "addDread"),
				AddRule = ReadNullableString(entry, "addRule"),
				AddStoryFlag = ReadNullableString(entry, "addStoryFlag"),
				RemoveStoryFlag = ReadNullableString(entry, "removeStoryFlag"),
				AddItemId = ReadNullableString(entry, "addItemId"),
				AddItemQty = ReadNullableInt(entry, "addItemQty"),
				ConsumeItemId = ReadNullableString(entry, "consumeItemId"),
				ConsumeItemQty = ReadNullableInt(entry, "consumeItemQty"),
				ConsumeEachIngredientQty = ReadNullableInt(entry, "consumeEachIngredientQty")
			});
		}

		return effects;
	}

	private static RequirementsDef? ParseRequirements(Godot.Collections.Dictionary? entry)
	{
		if (entry is null || entry.Count == 0)
			return null;

		var requirements = new RequirementsDef
		{
			GoldMin = ReadNullableInt(entry, "goldMin"),
			DreadMin = ReadNullableInt(entry, "dreadMin"),
			DreadMax = ReadNullableInt(entry, "dreadMax"),
			DayMin = ReadNullableInt(entry, "dayMin"),
			DayMax = ReadNullableInt(entry, "dayMax"),
			DayExact = ReadNullableInt(entry, "dayExact"),
			HasItemId = ReadNullableString(entry, "hasItemId"),
			HasItemQty = ReadNullableInt(entry, "hasItemQty"),
			HasStoryFlag = ReadNullableString(entry, "hasStoryFlag"),
			MissingStoryFlag = ReadNullableString(entry, "missingStoryFlag")
		};

		if (requirements.GoldMin is null &&
			requirements.DreadMin is null &&
			requirements.DreadMax is null &&
			requirements.DayMin is null &&
			requirements.DayMax is null &&
			requirements.DayExact is null &&
			string.IsNullOrWhiteSpace(requirements.HasItemId) &&
			requirements.HasItemQty is null &&
			string.IsNullOrWhiteSpace(requirements.HasStoryFlag) &&
			string.IsNullOrWhiteSpace(requirements.MissingStoryFlag))
		{
			return null;
		}

		return requirements;
	}

	private static Godot.Collections.Dictionary? ReadDictionary(Godot.Collections.Dictionary source, string key)
	{
		if (!source.ContainsKey(key))
			return null;

		return TryReadDictionary(source[key], out var dictionary) ? dictionary : null;
	}

	private static Godot.Collections.Array ReadArray(Godot.Collections.Dictionary source, string key)
	{
		if (!source.ContainsKey(key))
			return new Godot.Collections.Array();

		return TryReadArray(source[key], out var array) ? array : new Godot.Collections.Array();
	}

	private static List<string> ReadStringList(Godot.Collections.Dictionary source, string key)
	{
		var list = new List<string>();
		if (!source.ContainsKey(key))
			return list;

		if (!TryReadArray(source[key], out var array))
			return list;

		foreach (var value in array)
		{
			var text = ReadVariantString(value);
			if (string.IsNullOrWhiteSpace(text))
				continue;

			list.Add(text);
		}

		return list;
	}

	private static Dictionary<string, int> ReadStringIntDictionary(Godot.Collections.Dictionary source, string key)
	{
		var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
		if (!source.ContainsKey(key))
			return result;

		if (!TryReadDictionary(source[key], out var dictionary))
			return result;

		foreach (var pair in dictionary)
		{
			var name = ReadVariantString(pair.Key);
			if (string.IsNullOrWhiteSpace(name))
				continue;

			if (!TryConvertToInt(pair.Value, out var amount))
				continue;

			result[name] = amount;
		}

		return result;
	}

	private static string ReadString(Godot.Collections.Dictionary source, string key, string fallback = "")
	{
		if (!source.ContainsKey(key))
			return fallback;

		var value = source[key];
		if (value.VariantType == Variant.Type.Nil)
			return fallback;

		var text = ReadVariantString(value);
		return string.IsNullOrWhiteSpace(text) ? fallback : text;
	}

	private static string? ReadNullableString(Godot.Collections.Dictionary source, string key)
	{
		var value = ReadString(source, key);
		return string.IsNullOrWhiteSpace(value) ? null : value;
	}

	private static int ReadInt(Godot.Collections.Dictionary source, string key, int fallback)
	{
		var value = ReadNullableInt(source, key);
		return value ?? fallback;
	}

	private static int? ReadNullableInt(Godot.Collections.Dictionary source, string key)
	{
		if (!source.ContainsKey(key))
			return null;

		return TryConvertToInt(source[key], out var value) ? value : null;
	}

	private static bool ReadBool(Godot.Collections.Dictionary source, string key, bool fallback = false)
	{
		if (!source.ContainsKey(key))
			return fallback;

		var value = source[key];
		if (value.VariantType == Variant.Type.Bool)
			return value.As<bool>();

		var text = ReadVariantString(value);
		if (bool.TryParse(text, out var boolValue))
			return boolValue;

		if (TryConvertToInt(value, out var intValue))
			return intValue != 0;

		return fallback;
	}

	private static bool TryReadDictionary(Variant value, out Godot.Collections.Dictionary dictionary)
	{
		if (value.VariantType == Variant.Type.Dictionary)
		{
			dictionary = value.As<Godot.Collections.Dictionary>();
			return true;
		}

		dictionary = new Godot.Collections.Dictionary();
		return false;
	}

	private static bool TryReadArray(Variant value, out Godot.Collections.Array array)
	{
		if (value.VariantType == Variant.Type.Array)
		{
			array = value.As<Godot.Collections.Array>();
			return true;
		}

		array = new Godot.Collections.Array();
		return false;
	}

	private static string ReadVariantString(Variant value)
	{
		if (value.VariantType == Variant.Type.Nil)
			return string.Empty;

		if (value.VariantType == Variant.Type.String)
			return value.As<string>();

		return value.ToString();
	}

	private static bool TryConvertToInt(Variant value, out int converted)
	{
		converted = 0;
		if (value.VariantType == Variant.Type.Nil)
			return false;

		switch (value.VariantType)
		{
			case Variant.Type.Int:
				converted = (int)value.As<long>();
				return true;
			case Variant.Type.Float:
				converted = (int)Math.Round(value.As<double>());
				return true;
		}

		var text = ReadVariantString(value);
		if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var integerValue))
		{
			converted = integerValue;
			return true;
		}

		if (double.TryParse(text, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var doubleValue))
		{
			converted = (int)Math.Round(doubleValue);
			return true;
		}

		return false;
	}
}
