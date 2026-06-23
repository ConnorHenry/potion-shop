using System.Text.Json.Serialization;

namespace OccultShop.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum BoilingStirringRhythm
{
	ClockwiseSlow,
	ClockwiseFast,
	AntiClockwiseSlow,
	AntiClockwiseFast
}

public sealed class BoilingMiniGameDef
{
	[JsonPropertyName("temperatureTargetMin")]
	public float TemperatureTargetMin { get; set; }

	[JsonPropertyName("temperatureTargetMax")]
	public float TemperatureTargetMax { get; set; }

	[JsonPropertyName("temperatureHoldSeconds")]
	public float TemperatureHoldSeconds { get; set; }

	[JsonPropertyName("heatLockSeconds")]
	public float HeatLockSeconds { get; set; } = 3.0f;

	[JsonPropertyName("heatRiseRate")]
	public float HeatRiseRate { get; set; }

	[JsonPropertyName("heatFallRate")]
	public float HeatFallRate { get; set; }

	[JsonPropertyName("donenessDurationSeconds")]
	public float DonenessDurationSeconds { get; set; }

	[JsonPropertyName("donenessWindowStart")]
	public float DonenessWindowStart { get; set; }

	[JsonPropertyName("donenessWindowEnd")]
	public float DonenessWindowEnd { get; set; }

	[JsonPropertyName("stirringRhythm")]
	public BoilingStirringRhythm StirringRhythm { get; set; }

	[JsonPropertyName("stirringHoldSeconds")]
	public float StirringHoldSeconds { get; set; }

	[JsonPropertyName("failureRiskId")]
	public string FailureRiskId { get; set; } = "";
}
