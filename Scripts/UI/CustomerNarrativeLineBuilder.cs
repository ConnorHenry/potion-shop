using System.Collections.Generic;
using OccultShop.Dialogue;
using OccultShop.Models;

namespace OccultShop.UI;

public static class CustomerNarrativeLineBuilder
{
	public static List<NarrativeTextLine> BuildAuthoredNarrativeLines(
		IReadOnlyList<CustomerDialogueLineDef> lines,
		string fallbackText,
		string? fallbackSpeaker)
	{
		var dialogueLines = new List<DialogueLine>(lines.Count);
		foreach (var line in lines)
		{
			dialogueLines.Add(new DialogueLine
			{
				Speaker = line.Speaker,
				Text = line.Text,
				CharacterImageKey = line.CharacterImageKey
			});
		}

		return DialogueNarrativeLineBuilder.BuildNarrativeLines(dialogueLines, fallbackText, fallbackSpeaker);
	}
}
