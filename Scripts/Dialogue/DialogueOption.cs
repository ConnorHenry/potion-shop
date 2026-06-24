using System.Collections.Generic;

namespace OccultShop.Dialogue;

public sealed class DialogueOption
{
	public string SourceKey { get; set; } = "";
	public string Id { get; set; } = "";
	public string Label { get; set; } = "";
	public string ResponseText { get; set; } = "";
	public List<DialogueLine> ResponseLines { get; set; } = new();
	public string NextNodeId { get; set; } = "";
	public string ReturnNodeId { get; set; } = "";
	public bool EndsDialogue { get; set; }

	public bool HasResponse => ResponseLines.Count > 0 || !string.IsNullOrWhiteSpace(ResponseText);
}
