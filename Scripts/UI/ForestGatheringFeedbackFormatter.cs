using System;

namespace OccultShop.UI;

public static class ForestGatheringFeedbackFormatter
{
	private const string MintDecoyTextureFilePrefix = "mint_decoy_";
	private const string PngTextureFileExtension = ".png";

	public static string BuildWrongPlantFeedback(
		ForestGatheringPlantEntry entry,
		string targetName,
		string plantName)
	{
		if (TryGetMintDecoyClueName(entry.TexturePath, out var decoyClueName))
			return BuildMintDecoyFeedback(decoyClueName, targetName);

		return $"That was {plantName}, not {targetName}.";
	}

	private static bool TryGetMintDecoyClueName(string texturePath, out string decoyClueName)
	{
		decoyClueName = string.Empty;
		var slashIndex = texturePath.LastIndexOf('/');
		var fileName = slashIndex >= 0 ? texturePath[(slashIndex + 1)..] : texturePath;
		if (!fileName.StartsWith(MintDecoyTextureFilePrefix, StringComparison.OrdinalIgnoreCase))
			return false;

		var clueName = fileName[MintDecoyTextureFilePrefix.Length..];
		if (clueName.EndsWith(PngTextureFileExtension, StringComparison.OrdinalIgnoreCase))
			clueName = clueName[..^PngTextureFileExtension.Length];

		decoyClueName = clueName.Replace('_', ' ');
		return !string.IsNullOrWhiteSpace(decoyClueName);
	}

	private static string BuildMintDecoyFeedback(string decoyClueName, string targetName)
	{
		return decoyClueName switch
		{
			"alternate pairs" => $"Wrong plant: {decoyClueName}; {targetName} leaves should be even and opposite.",
			"curved stem" => $"Wrong plant: {decoyClueName}; {targetName} stems should be crisp and straight.",
			"extra tip" => $"Wrong plant: {decoyClueName}; the stem tip does not match {targetName}.",
			"hidden bud" => $"Wrong plant: {decoyClueName}; {targetName} should not hide a bud.",
			"offset leaf" => $"Wrong plant: {decoyClueName}; {targetName} leaves should sit in even pairs.",
			"rounder leaf" => $"Wrong plant: {decoyClueName}; the leaves are too wide for {targetName}.",
			"smooth edge" => $"Wrong plant: {decoyClueName}; {targetName} leaves should be lightly toothed.",
			"wrong veins" => $"Wrong plant: {decoyClueName}; the leaf veins do not match {targetName}.",
			_ => $"Wrong plant: {decoyClueName}; it does not match {targetName}."
		};
	}
}
