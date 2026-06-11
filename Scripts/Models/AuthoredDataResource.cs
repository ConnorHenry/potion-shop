using Godot;

namespace OccultShop.Models;

[GlobalClass]
public partial class AuthoredDataResource : Resource
{
	[Export]
	public string ItemsPath { get; set; } = "res://Data/items_data.tres";

	[Export]
	public string RulesPath { get; set; } = "res://Data/rules_data.tres";

	[Export]
	public string EventsPath { get; set; } = "res://Data/events_data.tres";

	[Export]
	public string CustomerInteractionsPath { get; set; } = "res://Data/customers_data.tres";

	[Export]
	public string PotionRecipesPath { get; set; } = "res://Data/potion_recipes_data.tres";
}
