using System.Collections.Generic;

namespace OccultShop.Dialogue;

public sealed class DialogueNode
{
	public string Id { get; set; } = "";
	public string Text { get; set; } = "";
	public List<DialogueLine> Lines { get; set; } = new();
	public List<DialogueOption> Options { get; set; } = new();
}
