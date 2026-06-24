using System;
using System.Collections.Generic;
using Godot;

namespace OccultShop.UI;

public readonly record struct ForestGatheringPlantDefinition(string ItemId, string Label, Vector2 Size, string TexturePath);

public readonly record struct ForestGatheringPlantEntry(
	string ItemId,
	string Label,
	Vector2 Center,
	Vector2 Size,
	string TexturePath,
	string InspectionTexturePath);

public static class ForestGatheringPlantCatalog
{
	private const string PlantTexturePathPrefix = "res://Assets/Gathering/Plants/";
	private const string InspectionPlantTexturePathPrefix = "res://Assets/Gathering/InspectionPlants/";
	private const string InspectionPlantTextureFilePrefix = "inspection_";

	private static readonly ForestGatheringPlantDefinition[] PlantDefinitions =
	{
		new("mint", "Specimen A", new Vector2(0.100f, 0.225f), "res://Assets/Gathering/Plants/mint_target_a.png"),
		new("mint", "Specimen B", new Vector2(0.092f, 0.210f), "res://Assets/Gathering/Plants/mint_target_b.png"),
		new("mint", "Specimen C", new Vector2(0.100f, 0.225f), "res://Assets/Gathering/Plants/mint_target_c.png"),
		new("heather", "Specimen D", new Vector2(0.118f, 0.170f), "res://Assets/Gathering/Plants/forest_flowering_stems.png"),
		new("gorse", "Specimen E", new Vector2(0.100f, 0.195f), "res://Assets/Gathering/Plants/forest_flowering_stems.png"),
		new("elder", "Specimen F", new Vector2(0.118f, 0.205f), "res://Assets/Gathering/Plants/forest_leaf_cluster.png"),
		new("rosemary", "Specimen G", new Vector2(0.085f, 0.205f), "res://Assets/Gathering/Plants/forest_slender_stems.png"),
		new("willow", "Specimen H", new Vector2(0.120f, 0.190f), "res://Assets/Gathering/Plants/forest_willow_stems.png"),
		new("juniper", "Specimen I", new Vector2(0.110f, 0.205f), "res://Assets/Gathering/Plants/forest_dark_cluster.png"),
		new("comfrey", "Specimen J", new Vector2(0.110f, 0.205f), "res://Assets/Gathering/Plants/forest_leaf_cluster.png"),
		new("thyme", "Specimen K", new Vector2(0.115f, 0.205f), "res://Assets/Gathering/Plants/forest_slender_stems.png"),
		new("yarrow", "Specimen L", new Vector2(0.105f, 0.170f), "res://Assets/Gathering/Plants/forest_flowering_stems.png"),
		new("thyme", "Specimen M", new Vector2(0.071f, 0.179f), "res://Assets/Gathering/Plants/mint_decoy_smooth_edge.png"),
		new("comfrey", "Specimen N", new Vector2(0.077f, 0.185f), "res://Assets/Gathering/Plants/forest_dark_cluster.png"),
		new("rosemary", "Specimen O", new Vector2(0.073f, 0.176f), "res://Assets/Gathering/Plants/forest_slender_stems.png"),
		new("elder", "Specimen P", new Vector2(0.067f, 0.166f), "res://Assets/Gathering/Plants/forest_leaf_cluster.png"),
		new("juniper", "Specimen Q", new Vector2(0.073f, 0.168f), "res://Assets/Gathering/Plants/forest_dark_cluster.png"),
		new("willow", "Specimen R", new Vector2(0.067f, 0.172f), "res://Assets/Gathering/Plants/forest_willow_stems.png"),
		new("yarrow", "Specimen S", new Vector2(0.062f, 0.149f), "res://Assets/Gathering/Plants/forest_flowering_stems.png"),
		new("heather", "Specimen T", new Vector2(0.061f, 0.159f), "res://Assets/Gathering/Plants/forest_flowering_stems.png"),
		new("elder", "Specimen U", new Vector2(0.062f, 0.157f), "res://Assets/Gathering/Plants/forest_smooth_cluster.png"),
		new("thyme", "Specimen V", new Vector2(0.060f, 0.153f), "res://Assets/Gathering/Plants/forest_slender_stems.png"),
		new("comfrey", "Specimen W", new Vector2(0.067f, 0.153f), "res://Assets/Gathering/Plants/forest_leaf_cluster.png"),
		new("willow", "Specimen X", new Vector2(0.063f, 0.159f), "res://Assets/Gathering/Plants/forest_willow_stems.png"),
		new("thyme", "Specimen Y", new Vector2(0.074f, 0.170f), "res://Assets/Gathering/Plants/mint_decoy_wrong_veins.png"),
		new("elder", "Specimen Z", new Vector2(0.070f, 0.160f), "res://Assets/Gathering/Plants/mint_decoy_smooth_edge.png"),
		new("rosemary", "Specimen AA", new Vector2(0.070f, 0.158f), "res://Assets/Gathering/Plants/mint_decoy_hidden_bud.png"),
		new("willow", "Specimen AB", new Vector2(0.074f, 0.168f), "res://Assets/Gathering/Plants/mint_decoy_alternate_pairs.png"),
		new("comfrey", "Specimen AC", new Vector2(0.068f, 0.155f), "res://Assets/Gathering/Plants/mint_decoy_rounder_leaf.png"),
		new("juniper", "Specimen AD", new Vector2(0.070f, 0.160f), "res://Assets/Gathering/Plants/mint_decoy_extra_tip.png"),
		new("yarrow", "Specimen AE", new Vector2(0.073f, 0.166f), "res://Assets/Gathering/Plants/mint_decoy_curved_stem.png"),
		new("heather", "Specimen AF", new Vector2(0.069f, 0.158f), "res://Assets/Gathering/Plants/mint_decoy_wrong_veins.png"),
		new("elder", "Specimen AG", new Vector2(0.067f, 0.151f), "res://Assets/Gathering/Plants/mint_decoy_smooth_edge.png"),
		new("thyme", "Specimen AH", new Vector2(0.069f, 0.156f), "res://Assets/Gathering/Plants/mint_decoy_offset_leaf.png"),
		new("comfrey", "Specimen AI", new Vector2(0.074f, 0.168f), "res://Assets/Gathering/Plants/mint_decoy_hidden_bud.png"),
		new("willow", "Specimen AJ", new Vector2(0.066f, 0.150f), "res://Assets/Gathering/Plants/mint_decoy_extra_tip.png")
	};

	public static IReadOnlyList<ForestGatheringPlantDefinition> Definitions => PlantDefinitions;

	public static string BuildInspectionTexturePath(string texturePath)
	{
		if (!texturePath.StartsWith(PlantTexturePathPrefix, StringComparison.OrdinalIgnoreCase))
			return texturePath;

		var textureFileName = texturePath.Substring(PlantTexturePathPrefix.Length);
		return $"{InspectionPlantTexturePathPrefix}{InspectionPlantTextureFilePrefix}{textureFileName}";
	}
}
