using System.Collections.Generic;
using OccultShop.Dialogue;
using OccultShop.Models;

namespace OccultShop.Systems;

public sealed class CustomerDialogueGraphBuildResult
{
	public DialogueGraph Graph { get; set; } = new();
	public Dictionary<string, CustomerDialogueOptionDef> OptionsBySourceKey { get; set; } = new();
}

public static class CustomerDialogueGraphBuilder
{
	public static CustomerDialogueGraphBuildResult Build(CustomerInteractionDef interaction)
	{
		var result = new CustomerDialogueGraphBuildResult
		{
			Graph = new DialogueGraph
			{
				StartNodeId = interaction.DialogueStartNodeId
			}
		};

		foreach (var customerNode in interaction.DialogueNodes)
		{
			var node = new DialogueNode
			{
				Id = customerNode.Id,
				Text = customerNode.Text,
				Lines = BuildLines(customerNode.Lines)
			};

			for (var optionIndex = 0; optionIndex < customerNode.Options.Count; optionIndex += 1)
			{
				var customerOption = customerNode.Options[optionIndex];
				var sourceKey = BuildOptionSourceKey(customerNode.Id, customerOption.Id, optionIndex);
				node.Options.Add(new DialogueOption
				{
					SourceKey = sourceKey,
					Id = customerOption.Id,
					Label = customerOption.Label,
					ResponseText = customerOption.ResponseText,
					ResponseLines = BuildLines(customerOption.ResponseLines),
					NextNodeId = customerOption.NextNodeId,
					ReturnNodeId = customerOption.ReturnNodeId,
					EndsDialogue = customerOption.EndsInteraction
				});
				result.OptionsBySourceKey[sourceKey] = customerOption;
			}

			result.Graph.Nodes.Add(node);
		}

		return result;
	}

	public static List<DialogueLine> BuildLines(IReadOnlyList<CustomerDialogueLineDef> customerLines)
	{
		var lines = new List<DialogueLine>(customerLines.Count);
		foreach (var customerLine in customerLines)
		{
			lines.Add(new DialogueLine
			{
				Speaker = customerLine.Speaker,
				Text = customerLine.Text,
				CharacterImageKey = customerLine.CharacterImageKey
			});
		}

		return lines;
	}

	public static string BuildOptionSourceKey(string nodeId, string optionId, int optionIndex)
	{
		return $"{nodeId.Trim()}::{optionIndex}::{optionId.Trim()}";
	}
}
