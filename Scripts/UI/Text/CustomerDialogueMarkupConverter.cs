using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace OccultShop.UI;

public enum NarrativeTextCommandKind
{
	Pause,
	Speed
}

public sealed class NarrativeTextCommand
{
	public NarrativeTextCommand(NarrativeTextCommandKind kind, int visibleCharacterIndex, double numericValue)
	{
		Kind = kind;
		VisibleCharacterIndex = Math.Max(0, visibleCharacterIndex);
		NumericValue = numericValue;
	}

	public NarrativeTextCommandKind Kind { get; }
	public int VisibleCharacterIndex { get; }
	public double NumericValue { get; }
}

public sealed class NarrativeMarkupDocument
{
	public NarrativeMarkupDocument(
		string bbCode,
		string plainText,
		int visibleCharacterCount,
		IReadOnlyList<NarrativeTextCommand> commands,
		IReadOnlyList<string> warnings)
	{
		BbCode = bbCode;
		PlainText = plainText;
		VisibleCharacterCount = Math.Max(0, visibleCharacterCount);
		Commands = commands;
		Warnings = warnings;
	}

	public string BbCode { get; }
	public string PlainText { get; }
	public int VisibleCharacterCount { get; }
	public IReadOnlyList<NarrativeTextCommand> Commands { get; }
	public IReadOnlyList<string> Warnings { get; }
}

public static class CustomerDialogueMarkupConverter
{
	private static readonly Dictionary<string, string> NamedColors = new(StringComparer.OrdinalIgnoreCase)
	{
		["customer"] = CustomerDialogueTextFormatter.CustomerSpeakerColorHex,
		["player"] = CustomerDialogueTextFormatter.PlayerSpeakerColorHex,
		["gold"] = CustomerDialogueTextFormatter.CustomerSpeakerColorHex,
		["success"] = CustomerDialogueTextFormatter.MatchedDesiredColorHex,
		["danger"] = CustomerDialogueTextFormatter.MatchedRiskColorHex,
		["risk"] = CustomerDialogueTextFormatter.MatchedRiskColorHex,
		["quiet"] = "#A8A093",
		["pale"] = "#F2E9C9"
	};

	public static NarrativeMarkupDocument ConvertToBbCode(string? source)
	{
		var bbCode = new StringBuilder();
		var plainText = new StringBuilder();
		var commands = new List<NarrativeTextCommand>();
		var warnings = new List<string>();
		var text = source ?? string.Empty;

		AppendConverted(text, 0, text.Length, bbCode, plainText, commands, warnings);

		return new NarrativeMarkupDocument(
			bbCode.ToString(),
			plainText.ToString(),
			CountVisibleCharacters(plainText.ToString()),
			commands,
			warnings);
	}

	public static NarrativeMarkupDocument ConvertPlainText(string? source)
	{
		var text = source ?? string.Empty;
		var bbCode = new StringBuilder();
		AppendEscapedLiteral(text, bbCode);

		return new NarrativeMarkupDocument(
			bbCode.ToString(),
			text,
			CountVisibleCharacters(text),
			Array.Empty<NarrativeTextCommand>(),
			Array.Empty<string>());
	}

	public static int CountVisibleCharacters(string? text)
	{
		if (string.IsNullOrEmpty(text))
			return 0;

		var count = 0;
		for (var index = 0; index < text.Length; index += 1)
		{
			if (char.IsHighSurrogate(text[index]) &&
				index + 1 < text.Length &&
				char.IsLowSurrogate(text[index + 1]))
			{
				index += 1;
			}

			count += 1;
		}

		return count;
	}

	private static void AppendConverted(
		string source,
		int start,
		int end,
		StringBuilder bbCode,
		StringBuilder plainText,
		List<NarrativeTextCommand> commands,
		List<string> warnings)
	{
		var index = start;
		while (index < end)
		{
			if (source[index] != '{')
			{
				AppendEscapedCharacter(source[index], bbCode);
				plainText.Append(source[index]);
				index += 1;
				continue;
			}

			var closingBraceIndex = FindMatchingBrace(source, index, end);
			if (closingBraceIndex < 0)
			{
				AppendEscapedCharacter(source[index], bbCode);
				plainText.Append(source[index]);
				index += 1;
				continue;
			}

			var tagContentStart = index + 1;
			var tagContentLength = closingBraceIndex - tagContentStart;
			var tagContent = source.Substring(tagContentStart, tagContentLength);
			var topLevelPipeIndex = FindTopLevelPipe(tagContent);
			if (topLevelPipeIndex >= 0)
			{
				var tagSpec = tagContent[..topLevelPipeIndex].Trim();
				var nestedContent = tagContent[(topLevelPipeIndex + 1)..];
				if (!TryAppendStyledContent(
					tagSpec,
					nestedContent,
					bbCode,
					plainText,
					commands,
					warnings))
				{
					var literal = source.Substring(index, closingBraceIndex - index + 1);
					AppendUnknownLiteral(literal, bbCode, plainText);
					warnings.Add($"Unknown dialogue text style '{tagSpec}'.");
				}
			}
			else if (!TryAppendInlineCommand(tagContent.Trim(), plainText, commands, warnings))
			{
				var literal = source.Substring(index, closingBraceIndex - index + 1);
				AppendUnknownLiteral(literal, bbCode, plainText);
				warnings.Add($"Unknown dialogue text command '{tagContent.Trim()}'.");
			}

			index = closingBraceIndex + 1;
		}
	}

	private static bool TryAppendStyledContent(
		string tagSpec,
		string content,
		StringBuilder bbCode,
		StringBuilder plainText,
		List<NarrativeTextCommand> commands,
		List<string> warnings)
	{
		if (!TryBuildStyleTags(tagSpec, out var openingTag, out var closingTag, out var warning))
		{
			if (!string.IsNullOrWhiteSpace(warning))
				warnings.Add(warning);
			return false;
		}

		bbCode.Append(openingTag);
		AppendConverted(content, 0, content.Length, bbCode, plainText, commands, warnings);
		bbCode.Append(closingTag);
		return true;
	}

	private static bool TryBuildStyleTags(
		string tagSpec,
		out string openingTag,
		out string closingTag,
		out string warning)
	{
		openingTag = string.Empty;
		closingTag = string.Empty;
		warning = string.Empty;

		if (string.IsNullOrWhiteSpace(tagSpec))
			return false;

		var colonIndex = tagSpec.IndexOf(':', StringComparison.Ordinal);
		var tagName = colonIndex >= 0 ? tagSpec[..colonIndex].Trim() : tagSpec.Trim();
		var tagValue = colonIndex >= 0 ? tagSpec[(colonIndex + 1)..].Trim() : string.Empty;

		if (string.Equals(tagName, "i", StringComparison.OrdinalIgnoreCase) ||
			string.Equals(tagName, "italic", StringComparison.OrdinalIgnoreCase))
		{
			openingTag = "[i]";
			closingTag = "[/i]";
			return true;
		}

		if (string.Equals(tagName, "b", StringComparison.OrdinalIgnoreCase) ||
			string.Equals(tagName, "bold", StringComparison.OrdinalIgnoreCase))
		{
			openingTag = "[b]";
			closingTag = "[/b]";
			return true;
		}

		if (string.Equals(tagName, "shake", StringComparison.OrdinalIgnoreCase))
		{
			openingTag = "[shake rate=18.0 level=3 connected=1]";
			closingTag = "[/shake]";
			return true;
		}

		if (string.Equals(tagName, "wave", StringComparison.OrdinalIgnoreCase))
		{
			openingTag = "[wave amp=12.0 freq=4.0 connected=1]";
			closingTag = "[/wave]";
			return true;
		}

		if (!string.Equals(tagName, "color", StringComparison.OrdinalIgnoreCase))
			return false;

		if (!TryResolveColor(tagValue, out var colorHex))
		{
			warning = $"Unknown dialogue text color '{tagValue}'.";
			return false;
		}

		openingTag = $"[color={colorHex}]";
		closingTag = "[/color]";
		return true;
	}

	private static bool TryAppendInlineCommand(
		string tagContent,
		StringBuilder plainText,
		List<NarrativeTextCommand> commands,
		List<string> warnings)
	{
		if (string.IsNullOrWhiteSpace(tagContent))
			return false;

		var colonIndex = tagContent.IndexOf(':', StringComparison.Ordinal);
		if (colonIndex <= 0 || colonIndex >= tagContent.Length - 1)
			return false;

		var commandName = tagContent[..colonIndex].Trim();
		var commandValue = tagContent[(colonIndex + 1)..].Trim();
		var visibleIndex = CountVisibleCharacters(plainText.ToString());

		if (string.Equals(commandName, "pause", StringComparison.OrdinalIgnoreCase))
		{
			if (!double.TryParse(commandValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var pauseSeconds) ||
				pauseSeconds < 0.0)
			{
				warnings.Add($"Invalid dialogue pause duration '{commandValue}'.");
				return true;
			}

			commands.Add(new NarrativeTextCommand(NarrativeTextCommandKind.Pause, visibleIndex, pauseSeconds));
			return true;
		}

		if (string.Equals(commandName, "speed", StringComparison.OrdinalIgnoreCase))
		{
			if (!int.TryParse(commandValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var charactersPerSecond) ||
				charactersPerSecond <= 0)
			{
				warnings.Add($"Invalid dialogue speed '{commandValue}'.");
				return true;
			}

			commands.Add(new NarrativeTextCommand(NarrativeTextCommandKind.Speed, visibleIndex, charactersPerSecond));
			return true;
		}

		return false;
	}

	private static bool TryResolveColor(string value, out string colorHex)
	{
		colorHex = string.Empty;
		if (string.IsNullOrWhiteSpace(value))
			return false;

		var trimmed = value.Trim();
		if (NamedColors.TryGetValue(trimmed, out var namedColor))
		{
			colorHex = namedColor;
			return true;
		}

		if (!IsHexColor(trimmed))
			return false;

		colorHex = trimmed;
		return true;
	}

	private static bool IsHexColor(string value)
	{
		if (value.Length != 7 && value.Length != 9)
			return false;

		if (value[0] != '#')
			return false;

		for (var index = 1; index < value.Length; index += 1)
		{
			var c = value[index];
			var isHex =
				(c >= '0' && c <= '9') ||
				(c >= 'a' && c <= 'f') ||
				(c >= 'A' && c <= 'F');
			if (!isHex)
				return false;
		}

		return true;
	}

	private static int FindMatchingBrace(string source, int openBraceIndex, int end)
	{
		var depth = 0;
		for (var index = openBraceIndex; index < end; index += 1)
		{
			if (source[index] == '{')
			{
				depth += 1;
				continue;
			}

			if (source[index] != '}')
				continue;

			depth -= 1;
			if (depth == 0)
				return index;
		}

		return -1;
	}

	private static int FindTopLevelPipe(string content)
	{
		var depth = 0;
		for (var index = 0; index < content.Length; index += 1)
		{
			if (content[index] == '{')
			{
				depth += 1;
				continue;
			}

			if (content[index] == '}')
			{
				depth = Math.Max(0, depth - 1);
				continue;
			}

			if (content[index] == '|' && depth == 0)
				return index;
		}

		return -1;
	}

	private static void AppendUnknownLiteral(string literal, StringBuilder bbCode, StringBuilder plainText)
	{
		AppendEscapedLiteral(literal, bbCode);
		plainText.Append(literal);
	}

	private static void AppendEscapedLiteral(string literal, StringBuilder bbCode)
	{
		foreach (var c in literal)
			AppendEscapedCharacter(c, bbCode);
	}

	private static void AppendEscapedCharacter(char c, StringBuilder bbCode)
	{
		if (c == '[')
		{
			bbCode.Append("[lb]");
			return;
		}

		if (c == ']')
		{
			bbCode.Append("[rb]");
			return;
		}

		bbCode.Append(c);
	}
}
