namespace OccultShop.Dialogue;

public sealed class DialogueLine
{
	public string Speaker { get; set; } = "";
	public string Text { get; set; } = "";
	public string CharacterImageKey { get; set; } = "";
	public bool AllowMarkup { get; set; } = true;
}
