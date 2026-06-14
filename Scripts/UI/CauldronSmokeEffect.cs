using System;
using System.Collections.Generic;
using Godot;

namespace OccultShop.UI;

public partial class CauldronSmokeEffect : Control
{
	private const float FadeInPortion = 0.18f;
	private const float FadeOutStart = 0.56f;
	private const float TwoPi = Mathf.Pi * 2.0f;
	private const float BurstDurationSeconds = 0.82f;
	private const int BurstPuffCount = 9;

	[Export] public int PuffCount = 12;
	[Export] public float CycleDurationSeconds = 5.6f;
	[Export] public float RiseDistance = 190.0f;
	[Export] public float BaseRadius = 18.0f;
	[Export] public float RadiusGrowth = 42.0f;
	[Export] public float MaxAlpha = 0.6f;
	[Export] public float SwayAmplitude = 22.0f;
	[Export] public Color SmokeColor = new(0.294118f, 0.117647f, 0.321569f, 1.0f);

	private static readonly Color[] BurstColors =
	{
		new(0.713725f, 0.258824f, 0.227451f, 1.0f),
		new(0.427451f, 0.121569f, 0.168627f, 1.0f),
		new(0.294118f, 0.117647f, 0.321569f, 1.0f),
		new(0.541176f, 0.384314f, 0.196078f, 1.0f),
		new(0.709804f, 0.541176f, 0.352941f, 1.0f),
		new(0.247059f, 0.372549f, 0.239216f, 1.0f)
	};

	private SmokePuff[] _puffs = Array.Empty<SmokePuff>();
	private readonly List<SmokeBurst> _bursts = new();
	private double _elapsedSeconds;

	public override void _Ready()
	{
		MouseFilter = MouseFilterEnum.Ignore;
		BuildPuffs();
		SetProcess(true);
	}

	public override void _Process(double delta)
	{
		_elapsedSeconds += delta;
		UpdateBursts(delta);
		QueueRedraw();
	}

	public override void _Draw()
	{
		if (_puffs.Length == 0 || Size.X <= 0.0f || Size.Y <= 0.0f)
			return;

		var origin = new Vector2(Size.X * 0.5f, Size.Y * 0.78f);
		var cycleDuration = Math.Max(0.01f, CycleDurationSeconds);
		var cyclePosition = Repeat01((float)(_elapsedSeconds / cycleDuration));

		foreach (var puff in _puffs)
		{
			var progress = cyclePosition + puff.PhaseOffset;
			progress -= MathF.Floor(progress);

			var fadeIn = SmoothStep01(progress / FadeInPortion);
			var fadeOut = 1.0f - SmoothStep01((progress - FadeOutStart) / (1.0f - FadeOutStart));
			var alpha = MaxAlpha * fadeIn * fadeOut;
			if (alpha <= 0.001f)
				continue;

			var wave = Mathf.Sin(((float)_elapsedSeconds * puff.SwaySpeed) + (puff.PhaseOffset * TwoPi));
			var x = puff.HorizontalOffset + (wave * SwayAmplitude * (0.35f + progress));
			var y = -RiseDistance * progress;
			var position = origin + new Vector2(x, y);
			var radius = BaseRadius + puff.RadiusOffset + (RadiusGrowth * progress);
			var innerOffset = new Vector2(puff.InnerOffsetX * (1.0f - progress), -radius * 0.08f);

			DrawCircle(position, radius * 1.65f, BuildSmokeColor(alpha * 0.16f));
			DrawCircle(position + innerOffset, radius * 1.02f, BuildSmokeColor(alpha * 0.28f));
			DrawCircle(position - innerOffset, radius * 0.56f, BuildSmokeColor(alpha * 0.18f));
		}

		DrawBursts(origin);
	}

	public void PlayRandomBurst()
	{
		var color = BurstColors[Random.Shared.Next(BurstColors.Length)];
		var burst = new SmokeBurst
		{
			Color = color,
			Puffs = new BurstPuff[BurstPuffCount]
		};

		for (var i = 0; i < burst.Puffs.Length; i++)
		{
			var angle = -Mathf.Pi * 0.5f + RandomRange(-0.95f, 0.95f);
			var speed = RandomRange(54.0f, 132.0f);
			burst.Puffs[i] = new BurstPuff
			{
				Direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)),
				Speed = speed,
				StartRadius = RandomRange(10.0f, 22.0f),
				EndRadius = RandomRange(36.0f, 68.0f),
				AlphaScale = RandomRange(0.42f, 0.74f),
				StartOffset = new Vector2(RandomRange(-20.0f, 20.0f), RandomRange(-10.0f, 8.0f))
			};
		}

		_bursts.Add(burst);
	}

	private void BuildPuffs()
	{
		var count = Math.Max(1, PuffCount);
		_puffs = new SmokePuff[count];

		for (var i = 0; i < count; i++)
		{
			var side = i % 2 == 0 ? -1.0f : 1.0f;
			_puffs[i] = new SmokePuff
			{
				PhaseOffset = i / (float)count,
				HorizontalOffset = side * (8.0f + ((i % 4) * 7.0f)),
				InnerOffsetX = side * (5.0f + ((i % 3) * 3.0f)),
				RadiusOffset = (i % 5) * 2.6f,
				SwaySpeed = 0.72f + ((i % 4) * 0.09f)
			};
		}
	}

	private Color BuildSmokeColor(float alpha)
	{
		return new Color(SmokeColor.R, SmokeColor.G, SmokeColor.B, alpha);
	}

	private void UpdateBursts(double delta)
	{
		for (var i = _bursts.Count - 1; i >= 0; i--)
		{
			var burst = _bursts[i];
			burst.ElapsedSeconds += delta;
			if (burst.ElapsedSeconds < BurstDurationSeconds)
				continue;

			_bursts.RemoveAt(i);
		}
	}

	private void DrawBursts(Vector2 origin)
	{
		foreach (var burst in _bursts)
		{
			var progress = Clamp01((float)(burst.ElapsedSeconds / BurstDurationSeconds));
			var easedDistance = 1.0f - ((1.0f - progress) * (1.0f - progress));
			var alpha = 1.0f - SmoothStep01(progress);

			foreach (var puff in burst.Puffs)
			{
				var position = origin + puff.StartOffset + (puff.Direction * puff.Speed * easedDistance);
				var radius = Mathf.Lerp(puff.StartRadius, puff.EndRadius, progress);
				var color = BuildBurstColor(burst.Color, alpha * puff.AlphaScale);

				DrawCircle(position, radius * 1.4f, BuildBurstColor(burst.Color, color.A * 0.18f));
				DrawCircle(position, radius, color);
				DrawCircle(position + new Vector2(radius * 0.22f, -radius * 0.12f), radius * 0.56f, BuildBurstColor(burst.Color, color.A * 0.42f));
			}
		}
	}

	private static Color BuildBurstColor(Color color, float alpha)
	{
		return new Color(color.R, color.G, color.B, alpha);
	}

	private static float Clamp01(float value)
	{
		return Mathf.Clamp(value, 0.0f, 1.0f);
	}

	private static float Repeat01(float value)
	{
		return value - MathF.Floor(value);
	}

	private static float SmoothStep01(float value)
	{
		var clamped = Clamp01(value);
		return clamped * clamped * (3.0f - (2.0f * clamped));
	}

	private static float RandomRange(float min, float max)
	{
		return min + ((float)Random.Shared.NextDouble() * (max - min));
	}

	private readonly struct SmokePuff
	{
		public float PhaseOffset { get; init; }
		public float HorizontalOffset { get; init; }
		public float InnerOffsetX { get; init; }
		public float RadiusOffset { get; init; }
		public float SwaySpeed { get; init; }
	}

	private sealed class SmokeBurst
	{
		public Color Color { get; init; }
		public BurstPuff[] Puffs { get; init; } = Array.Empty<BurstPuff>();
		public double ElapsedSeconds { get; set; }
	}

	private readonly struct BurstPuff
	{
		public Vector2 Direction { get; init; }
		public Vector2 StartOffset { get; init; }
		public float Speed { get; init; }
		public float StartRadius { get; init; }
		public float EndRadius { get; init; }
		public float AlphaScale { get; init; }
	}
}
