using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using OccultShop.Autoload;
using OccultShop.Models;

namespace OccultShop.UI;

public partial class Garden : Control
{
	private const string MainScenePath = "res://Main.tscn";

	[Export] public NodePath PotsContainerPath = default!;
	[Export] public NodePath SeedsContainerPath = default!;
	[Export] public NodePath StatusLabelPath = default!;
	[Export] public NodePath BackButtonPath = default!;
	[Export] public NodePath GameStatePath = new("/root/GameState");
	[Export] public NodePath ItemCatalogPath = new("/root/ItemCatalog");
	[Export] public NodePath SaveGameManagerPath = new("/root/SaveGameManager");

	private GridContainer _potsContainer = default!;
	private VBoxContainer _seedsContainer = default!;
	private Label _statusLabel = default!;
	private Button _backButton = default!;
	private GameState _gameState = default!;
	private ItemCatalogService _itemCatalog = default!;
	private SaveGameManager _saveGameManager = default!;

	public override void _Ready()
	{
		var gameState = GetNodeOrNull<GameState>(GameStatePath);
		if (gameState is null)
		{
			GD.PushError($"Garden: GameState was not found at '{GameStatePath}'.");
			return;
		}

		var itemCatalog = GetNodeOrNull<ItemCatalogService>(ItemCatalogPath);
		if (itemCatalog is null)
		{
			GD.PushError($"Garden: ItemCatalog was not found at '{ItemCatalogPath}'.");
			return;
		}

		var saveGameManager = GetNodeOrNull<SaveGameManager>(SaveGameManagerPath);
		if (saveGameManager is null)
		{
			GD.PushError($"Garden: SaveGameManager was not found at '{SaveGameManagerPath}'.");
			return;
		}

		_gameState = gameState;
		_itemCatalog = itemCatalog;
		_saveGameManager = saveGameManager;
		_potsContainer = GetNode<GridContainer>(PotsContainerPath);
		_seedsContainer = GetNode<VBoxContainer>(SeedsContainerPath);
		_statusLabel = GetNode<Label>(StatusLabelPath);
		_backButton = GetNode<Button>(BackButtonPath);

		_backButton.Pressed += OnBackPressed;
		_gameState.Changed += Refresh;
		TryAutoSave("entering the garden");
		Refresh();
	}

	public override void _ExitTree()
	{
		if (_backButton is not null)
			_backButton.Pressed -= OnBackPressed;
		if (_gameState is not null)
			_gameState.Changed -= Refresh;
	}

	private void Refresh()
	{
		if (_gameState is null)
			return;

		ClearChildren(_potsContainer);
		ClearChildren(_seedsContainer);

		foreach (var pot in _gameState.GardenPots)
			_potsContainer.AddChild(CreatePotCard(pot));

		var seedEntries = BuildAvailableSeedEntries();
		if (seedEntries.Count == 0)
		{
			_seedsContainer.AddChild(new Label
			{
				Text = "No seeds available."
			});
			return;
		}

		foreach (var seedEntry in seedEntries)
		{
			_seedsContainer.AddChild(new Label
			{
				Text = $"{seedEntry.Name} Seed x{seedEntry.Quantity}"
			});
		}
	}

	private Control CreatePotCard(GardenPotState pot)
	{
		var panel = new PanelContainer
		{
			CustomMinimumSize = new Vector2(260, 190)
		};
		var margin = new MarginContainer();
		margin.AddThemeConstantOverride("margin_left", 12);
		margin.AddThemeConstantOverride("margin_top", 12);
		margin.AddThemeConstantOverride("margin_right", 12);
		margin.AddThemeConstantOverride("margin_bottom", 12);
		var column = new VBoxContainer();
		column.AddThemeConstantOverride("separation", 8);

		panel.AddChild(margin);
		margin.AddChild(column);

		column.AddChild(new Label
		{
			Text = $"Pot {pot.PotIndex + 1}"
		});

		if (pot.IsEmpty)
		{
			AddEmptyPotControls(column, pot);
			return panel;
		}

		var ingredientName = GetIngredientName(pot.IngredientId);
		column.AddChild(new Label
		{
			Text = ingredientName
		});
		column.AddChild(new Label
		{
			Text = pot.IsReady
				? "Ready to harvest"
				: $"Growing: {pot.DaysGrown}/{pot.RequiredGrowthDays} days"
		});

		var harvestButton = new Button
		{
			Text = "Harvest",
			Disabled = !pot.IsReady
		};
		harvestButton.Pressed += () => OnHarvestPressed(pot.PotIndex);
		column.AddChild(harvestButton);
		return panel;
	}

	private void AddEmptyPotControls(VBoxContainer column, GardenPotState pot)
	{
		column.AddChild(new Label
		{
			Text = "Empty"
		});

		var seedEntries = BuildAvailableSeedEntries();
		var seedPicker = new OptionButton();
		foreach (var seedEntry in seedEntries)
			seedPicker.AddItem($"{seedEntry.Name} Seed x{seedEntry.Quantity}");

		var plantButton = new Button
		{
			Text = "Plant",
			Disabled = seedEntries.Count == 0
		};
		plantButton.Pressed += () =>
		{
			var selectedIndex = seedPicker.Selected;
			if (selectedIndex < 0 || selectedIndex >= seedEntries.Count)
			{
				SetStatus("Select a seed first.");
				return;
			}

			OnPlantPressed(pot.PotIndex, seedEntries[selectedIndex].SeedId);
		};

		column.AddChild(seedPicker);
		column.AddChild(plantButton);
	}

	private List<SeedEntry> BuildAvailableSeedEntries()
	{
		var entries = new List<SeedEntry>();
		foreach (var pair in _gameState.SeedInventory)
		{
			if (pair.Value <= 0)
				continue;
			if (!_gameState.TryGetGardenCropBySeedId(pair.Key, out var crop))
				continue;

			entries.Add(new SeedEntry(pair.Key, GetIngredientName(crop.IngredientId), pair.Value));
		}

		return entries
			.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
			.ThenBy(x => x.SeedId, StringComparer.OrdinalIgnoreCase)
			.ToList();
	}

	private void OnPlantPressed(int potIndex, string seedId)
	{
		if (_gameState.TryPlantSeed(potIndex, seedId, out var error))
		{
			TryAutoSave("planting a seed");
			SetStatus("Seed planted.");
			return;
		}

		SetStatus(error);
	}

	private void OnHarvestPressed(int potIndex)
	{
		if (_gameState.TryHarvestGardenPot(potIndex, out var error))
		{
			TryAutoSave("harvesting a crop");
			SetStatus("Ingredient harvested.");
			return;
		}

		SetStatus(error);
	}

	private void OnBackPressed()
	{
		TryAutoSave("leaving the garden");
		Error error = GetTree().ChangeSceneToFile(MainScenePath);
		if (error != Error.Ok)
		{
			GD.PushError($"Garden: Failed to load main scene. Error: {error}");
		}
	}

	private bool TryAutoSave(string context)
	{
		var saveSucceeded = _saveGameManager.SaveGame();
		if (!saveSucceeded)
			GD.PushError($"Garden: Auto-save failed while {context}.");

		return saveSucceeded;
	}

	private string GetIngredientName(string ingredientId)
	{
		return _itemCatalog.TryGetItem(ingredientId, out var item) && !string.IsNullOrWhiteSpace(item.Name)
			? item.Name
			: ingredientId;
	}

	private void SetStatus(string message)
	{
		_statusLabel.Text = message;
	}

	private static void ClearChildren(Node parent)
	{
		foreach (var child in parent.GetChildren())
		{
			parent.RemoveChild(child);
			child.QueueFree();
		}
	}

	private readonly record struct SeedEntry(string SeedId, string Name, int Quantity);
}
