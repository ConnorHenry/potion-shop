using System;

namespace OccultShop.Persistence;

public sealed class SaveGameSummary
{
	public string FilePath { get; set; } = "";
	public string FileName { get; set; } = "";
	public string PlayerName { get; set; } = "";
	public DateTime SavedAtUtc { get; set; } = DateTime.UtcNow;
	public int Day { get; set; }
	public int Gold { get; set; }
	public int Dread { get; set; }

	public string BuildDisplayText()
	{
		var displayName = string.IsNullOrWhiteSpace(PlayerName) ? "Unnamed Player" : PlayerName.Trim();
		return $"{displayName} | {SavedAtUtc:yyyy-MM-dd HH:mm} UTC | Day {Day} | Gold {Gold} | Dread {Dread}";
	}
}
