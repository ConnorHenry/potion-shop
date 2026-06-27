using System;
using System.Collections.Generic;
using OccultShop.Models;

namespace OccultShop.Systems;

public sealed record MotherPostServeDialogueOption(string Label, string ResponseText);

public static class MotherPostServeDialogueFlow
{
	public const string OpeningMotherInteractionId = "customer_requests_opening_gravekeepers_balm";
	public const string MotherSpeakerName = "Mother";

	private const string QuestionOption = "Are you going to tell me what's wrong?";
	private const string QuestionResponse = "I told you not to worry about it. Everything is fine";
	private const string RestOption = "It's okay Ma. Here you need to get back to bed and rest.";
	private const string RestResponse = "Thank you dear.";

	public static bool ShouldBegin(CustomerInteractionDef interaction, bool saleSucceeded)
	{
		return saleSucceeded &&
			interaction is not null &&
			string.Equals(interaction.Id, OpeningMotherInteractionId, StringComparison.OrdinalIgnoreCase);
	}

	public static string BuildThankYouText(string playerName)
	{
		var normalizedPlayerName = string.IsNullOrWhiteSpace(playerName)
			? "there"
			: playerName.Trim();

		return $"Thank you so much {normalizedPlayerName}.";
	}

	public static List<MotherPostServeDialogueOption> BuildOptions()
	{
		return new List<MotherPostServeDialogueOption>
		{
			new(QuestionOption, QuestionResponse),
			new(RestOption, RestResponse)
		};
	}
}
