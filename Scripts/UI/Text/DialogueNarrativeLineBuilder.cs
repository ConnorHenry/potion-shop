using System.Collections.Generic;
using OccultShop.Dialogue;

namespace OccultShop.UI;

public static class DialogueNarrativeLineBuilder
{
	public static List<NarrativeTextLine> BuildNarrativeLines(
		IReadOnlyList<DialogueLine> lines,
		string fallbackText,
		string? fallbackSpeaker)
	{
		var narrativeLines = new List<NarrativeTextLine>();
		if (lines.Count > 0)
		{
			foreach (var line in lines)
			{
				if (string.IsNullOrWhiteSpace(line.Text))
					continue;

				var speaker = string.IsNullOrWhiteSpace(line.Speaker) ? fallbackSpeaker : line.Speaker;
				narrativeLines.Add(new NarrativeTextLine(
					speaker,
					line.Text,
					line.AllowMarkup,
					line.CharacterImageKey));
			}
		}
		else if (!string.IsNullOrWhiteSpace(fallbackText))
		{
			narrativeLines.Add(new NarrativeTextLine(fallbackSpeaker, fallbackText));
		}

		if (narrativeLines.Count == 0)
			narrativeLines.Add(new NarrativeTextLine(null, "...", allowMarkup: false));

		return narrativeLines;
	}
}
