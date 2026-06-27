using Godot;

namespace OccultShop.UI;

public readonly record struct HudAudioSettings(
	bool AmbientSoundsEnabled,
	double RainfallVolume,
	bool MusicEnabled,
	double MusicVolume);

public static class HudAudioSettingsStore
{
	private const string AmbientSettingsPath = "user://settings.cfg";
	private const string AmbientSettingsSection = "audio";
	private const string AmbientSoundsEnabledKey = "ambient_sounds_enabled";
	private const string RainfallVolumeKey = "rainfall_volume";
	private const string MusicEnabledKey = "music_enabled";
	private const string MusicVolumeKey = "music_volume";

	public const bool DefaultAmbientSoundsEnabled = true;
	public const double DefaultRainfallVolume = 0.7;
	public const bool DefaultMusicEnabled = true;
	public const double DefaultMusicVolume = 0.55;

	public static HudAudioSettings Load()
	{
		var config = new ConfigFile();
		var error = config.Load(AmbientSettingsPath);
		if (error != Error.Ok && error != Error.FileNotFound)
			GD.PushError($"Hud: Could not load audio settings. Error: {error}");

		return new HudAudioSettings(
			(bool)config.GetValue(AmbientSettingsSection, AmbientSoundsEnabledKey, DefaultAmbientSoundsEnabled),
			ClampNormalizedVolume((double)config.GetValue(AmbientSettingsSection, RainfallVolumeKey, DefaultRainfallVolume)),
			(bool)config.GetValue(AmbientSettingsSection, MusicEnabledKey, DefaultMusicEnabled),
			ClampNormalizedVolume((double)config.GetValue(AmbientSettingsSection, MusicVolumeKey, DefaultMusicVolume)));
	}

	public static void Save(HudAudioSettings settings)
	{
		var config = new ConfigFile();
		config.SetValue(AmbientSettingsSection, AmbientSoundsEnabledKey, settings.AmbientSoundsEnabled);
		config.SetValue(AmbientSettingsSection, RainfallVolumeKey, ClampNormalizedVolume(settings.RainfallVolume));
		config.SetValue(AmbientSettingsSection, MusicEnabledKey, settings.MusicEnabled);
		config.SetValue(AmbientSettingsSection, MusicVolumeKey, ClampNormalizedVolume(settings.MusicVolume));

		var error = config.Save(AmbientSettingsPath);
		if (error != Error.Ok)
			GD.PushError($"Hud: Could not save audio settings. Error: {error}");
	}

	public static float GetVolumeDb(double normalizedVolume)
	{
		var clampedVolume = ClampNormalizedVolume(normalizedVolume);
		return clampedVolume <= 0.0
			? -80.0f
			: Mathf.LinearToDb((float)clampedVolume);
	}

	public static double ClampNormalizedVolume(double value)
	{
		if (double.IsNaN(value) || double.IsInfinity(value))
			return 0.0;

		return Mathf.Clamp(value, 0.0, 1.0);
	}
}
