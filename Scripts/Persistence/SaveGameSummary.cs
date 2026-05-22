using System;

namespace OccultShop.Persistence;

public sealed class SaveGameSummary
{
	public string FilePath { get; set; } = "";
	public string FileName { get; set; } = "";
	public DateTime SavedAtUtc { get; set; } = DateTime.UtcNow;
	public int Day { get; set; }
	public int Gold { get; set; }
	public int Dread { get; set; }

	public string BuildDisplayText()
	{
		return $"{SavedAtUtc:yyyy-MM-dd HH:mm} UTC | Day {Day} | Gold {Gold} | Dread {Dread}";
	}
}
