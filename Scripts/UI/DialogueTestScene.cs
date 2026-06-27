using System;
using System.Collections.Generic;
using System.Text;
using Godot;
using OccultShop.Dialogue;
using OccultShop.Models;

namespace OccultShop.UI;

public partial class DialogueTestScene : Control
{
	private const int MinimumStoryScore = 0;
	private const int MaximumStoryScore = 100;
	private const int StartingStoryScore = 50;
	private const int MaxVisibleOptions = 8;
	private const string TrustFlagId = "test_customer_trust";

	[Export] public NodePath StateLabelPath = new("CenterContainer/Panel/Margin/VBox/State");
	[Export] public NodePath ConversationPath = new("CenterContainer/Panel/Margin/VBox/Conversation");
	[Export] public NodePath OptionsPath = new("CenterContainer/Panel/Margin/VBox/Options");
	[Export] public NodePath ResetButtonPath = new("CenterContainer/Panel/Margin/VBox/Controls/Reset");
	[Export] public NodePath ReputationUpButtonPath = new("CenterContainer/Panel/Margin/VBox/Controls/ReputationUp");
	[Export] public NodePath ReputationDownButtonPath = new("CenterContainer/Panel/Margin/VBox/Controls/ReputationDown");
	[Export] public NodePath RelationshipUpButtonPath = new("CenterContainer/Panel/Margin/VBox/Controls/RelationshipUp");
	[Export] public NodePath RelationshipDownButtonPath = new("CenterContainer/Panel/Margin/VBox/Controls/RelationshipDown");
	[Export] public NodePath TrustButtonPath = new("CenterContainer/Panel/Margin/VBox/Controls/Trust");
	[Export] public NodePath QuestButtonPath = new("CenterContainer/Panel/Margin/VBox/Controls/Quest");

	private readonly Dictionary<string, int> _minimumReputationByOptionId = new(StringComparer.OrdinalIgnoreCase);
	private readonly Dictionary<string, int> _minimumRelationshipByOptionId = new(StringComparer.OrdinalIgnoreCase);
	private readonly Dictionary<string, string> _requiredFlagByOptionId = new(StringComparer.OrdinalIgnoreCase);
	private readonly Dictionary<string, QuestStatus> _requiredQuestStatusByOptionId = new(StringComparer.OrdinalIgnoreCase);
	private readonly HashSet<string> _seenOptionIds = new(StringComparer.OrdinalIgnoreCase);
	private readonly List<string> _conversationLines = new();

	private Label _stateLabel = default!;
	private RichTextLabel _conversation = default!;
	private VBoxContainer _options = default!;
	private Button _resetButton = default!;
	private Button _reputationUpButton = default!;
	private Button _reputationDownButton = default!;
	private Button _relationshipUpButton = default!;
	private Button _relationshipDownButton = default!;
	private Button _trustButton = default!;
	private Button _questButton = default!;
	private DialogueGraph _graph = default!;
	private DialogueSession _session = default!;
	private int _reputation = StartingStoryScore;
	private int _relationship = StartingStoryScore;
	private bool _hasTrustFlag;
	private bool _dialogueEnded;
	private QuestStatus _questStatus = QuestStatus.NotStarted;

	public override void _Ready()
	{
		if (!TryResolveNodes())
			return;

		_conversation.BbcodeEnabled = false;
		_resetButton.Pressed += ResetTest;
		_reputationUpButton.Pressed += IncreaseReputation;
		_reputationDownButton.Pressed += DecreaseReputation;
		_relationshipUpButton.Pressed += IncreaseRelationship;
		_relationshipDownButton.Pressed += DecreaseRelationship;
		_trustButton.Pressed += ToggleTrustFlag;
		_questButton.Pressed += StartQuest;

		_graph = BuildGraph();
		ResetTest();
	}

	public override void _ExitTree()
	{
		if (_resetButton is not null)
			_resetButton.Pressed -= ResetTest;
		if (_reputationUpButton is not null)
			_reputationUpButton.Pressed -= IncreaseReputation;
		if (_reputationDownButton is not null)
			_reputationDownButton.Pressed -= DecreaseReputation;
		if (_relationshipUpButton is not null)
			_relationshipUpButton.Pressed -= IncreaseRelationship;
		if (_relationshipDownButton is not null)
			_relationshipDownButton.Pressed -= DecreaseRelationship;
		if (_trustButton is not null)
			_trustButton.Pressed -= ToggleTrustFlag;
		if (_questButton is not null)
			_questButton.Pressed -= StartQuest;
	}

	private bool TryResolveNodes()
	{
		_stateLabel = GetNodeOrNull<Label>(StateLabelPath);
		_conversation = GetNodeOrNull<RichTextLabel>(ConversationPath);
		_options = GetNodeOrNull<VBoxContainer>(OptionsPath);
		_resetButton = GetNodeOrNull<Button>(ResetButtonPath);
		_reputationUpButton = GetNodeOrNull<Button>(ReputationUpButtonPath);
		_reputationDownButton = GetNodeOrNull<Button>(ReputationDownButtonPath);
		_relationshipUpButton = GetNodeOrNull<Button>(RelationshipUpButtonPath);
		_relationshipDownButton = GetNodeOrNull<Button>(RelationshipDownButtonPath);
		_trustButton = GetNodeOrNull<Button>(TrustButtonPath);
		_questButton = GetNodeOrNull<Button>(QuestButtonPath);

		var hasRequiredNodes = true;
		hasRequiredNodes &= ReportMissingNode(_stateLabel, nameof(StateLabelPath), StateLabelPath);
		hasRequiredNodes &= ReportMissingNode(_conversation, nameof(ConversationPath), ConversationPath);
		hasRequiredNodes &= ReportMissingNode(_options, nameof(OptionsPath), OptionsPath);
		hasRequiredNodes &= ReportMissingNode(_resetButton, nameof(ResetButtonPath), ResetButtonPath);
		hasRequiredNodes &= ReportMissingNode(_reputationUpButton, nameof(ReputationUpButtonPath), ReputationUpButtonPath);
		hasRequiredNodes &= ReportMissingNode(_reputationDownButton, nameof(ReputationDownButtonPath), ReputationDownButtonPath);
		hasRequiredNodes &= ReportMissingNode(_relationshipUpButton, nameof(RelationshipUpButtonPath), RelationshipUpButtonPath);
		hasRequiredNodes &= ReportMissingNode(_relationshipDownButton, nameof(RelationshipDownButtonPath), RelationshipDownButtonPath);
		hasRequiredNodes &= ReportMissingNode(_trustButton, nameof(TrustButtonPath), TrustButtonPath);
		hasRequiredNodes &= ReportMissingNode(_questButton, nameof(QuestButtonPath), QuestButtonPath);
		return hasRequiredNodes;
	}

	private static bool ReportMissingNode(Node? node, string exportedPathName, NodePath path)
	{
		if (node is not null)
			return true;

		GD.PushError($"{nameof(DialogueTestScene)}: Missing node for {exportedPathName} at '{path}'.");
		return false;
	}

	private void ResetTest()
	{
		_reputation = StartingStoryScore;
		_relationship = StartingStoryScore;
		_hasTrustFlag = false;
		_questStatus = QuestStatus.NotStarted;
		_dialogueEnded = false;
		_seenOptionIds.Clear();
		_conversationLines.Clear();
		_session = new DialogueSession(_graph, IsOptionVisible, MaxVisibleOptions);
		if (!_session.TryStart(out var startNode) || startNode is null)
		{
			GD.PushError($"{nameof(DialogueTestScene)}: Could not start test dialogue graph.");
			return;
		}

		AppendNodeLines(startNode);
		RefreshPresentation();
	}

	private void IncreaseReputation()
	{
		AddReputation(5);
	}

	private void DecreaseReputation()
	{
		AddReputation(-5);
	}

	private void IncreaseRelationship()
	{
		AddRelationship(5);
	}

	private void DecreaseRelationship()
	{
		AddRelationship(-5);
	}

	private void ToggleTrustFlag()
	{
		_hasTrustFlag = !_hasTrustFlag;
		AppendSystemLine(_hasTrustFlag
			? $"Flag '{TrustFlagId}' granted."
			: $"Flag '{TrustFlagId}' removed.");
		RefreshPresentation();
	}

	private void StartQuest()
	{
		_questStatus = QuestStatus.InProgress;
		AppendSystemLine("Quest 'test_customer_order' set to InProgress.");
		RefreshPresentation();
	}

	private void AddReputation(int change)
	{
		_reputation = Math.Clamp(_reputation + change, MinimumStoryScore, MaximumStoryScore);
		AppendSystemLine($"Reputation {(change >= 0 ? "+" : string.Empty)}{change}.");
		RefreshPresentation();
	}

	private void AddRelationship(int change)
	{
		_relationship = Math.Clamp(_relationship + change, MinimumStoryScore, MaximumStoryScore);
		AppendSystemLine($"Relationship {(change >= 0 ? "+" : string.Empty)}{change}.");
		RefreshPresentation();
	}

	private void SelectOption(DialogueOption option)
	{
		if (_dialogueEnded)
			return;
		if (option is null)
			return;

		var wasSeen = !string.IsNullOrWhiteSpace(option.Id) && _seenOptionIds.Contains(option.Id);
		if (!MeetsRequirements(option))
		{
			AppendSystemLine($"'{option.Label}' is visible because it was seen, but its current requirements are not met.");
			RefreshPresentation();
			return;
		}

		if (!string.IsNullOrWhiteSpace(option.Id))
			_seenOptionIds.Add(option.Id);

		AppendLine("You", option.Label);
		AppendOptionResponse(option);
		if (!wasSeen)
			ApplyFirstSelectionEffect(option);

		if (option.EndsDialogue)
		{
			_dialogueEnded = true;
			AppendSystemLine("Dialogue ended. Use Reset to start over.");
			ClearOptionButtons();
			RefreshStateLabel();
			_conversation.Text = string.Join("\n\n", _conversationLines);
			return;
		}

		if (_session.TryMoveToNextNode(option, out var nextNode, out var error) && nextNode is not null)
		{
			AppendNodeLines(nextNode);
		}
		else if (!string.IsNullOrWhiteSpace(error))
		{
			GD.PushError($"{nameof(DialogueTestScene)}: {error}");
			AppendSystemLine(error);
		}

		RefreshPresentation();
	}

	private void ApplyFirstSelectionEffect(DialogueOption option)
	{
		switch (option.Id)
		{
			case "offer_sample":
				_hasTrustFlag = true;
				_reputation = Math.Clamp(_reputation + 2, MinimumStoryScore, MaximumStoryScore);
				_relationship = Math.Clamp(_relationship + 5, MinimumStoryScore, MaximumStoryScore);
				AppendSystemLine($"Applied effects: reputation +2, relationship +5, flag '{TrustFlagId}'.");
				break;

			case "start_order":
				_questStatus = QuestStatus.InProgress;
				AppendSystemLine("Applied effect: quest 'test_customer_order' set to InProgress.");
				break;

			case "complete_order":
				_questStatus = QuestStatus.Complete;
				_reputation = Math.Clamp(_reputation + 2, MinimumStoryScore, MaximumStoryScore);
				_relationship = Math.Clamp(_relationship + 5, MinimumStoryScore, MaximumStoryScore);
				AppendSystemLine("Applied effects: quest Complete, reputation +2, relationship +5.");
				break;
		}
	}

	private void RefreshPresentation()
	{
		RefreshStateLabel();
		RefreshOptions();
		_conversation.Text = string.Join("\n\n", _conversationLines);
	}

	private void RefreshStateLabel()
	{
		_stateLabel.Text =
			$"Reputation: {_reputation}/100    Relationship: {_relationship}/100    " +
			$"Quest: {_questStatus}    Flag {TrustFlagId}: {(_hasTrustFlag ? "set" : "unset")}";
	}

	private void RefreshOptions()
	{
		ClearOptionButtons();
		if (_dialogueEnded)
			return;

		foreach (var option in _session.RefreshVisibleOptions())
		{
			var isSeen = !string.IsNullOrWhiteSpace(option.Id) && _seenOptionIds.Contains(option.Id);
			var requirementsMet = MeetsRequirements(option);
			var button = new Button
			{
				Text = isSeen ? $"[seen] {option.Label}" : option.Label,
				Disabled = !requirementsMet,
				SizeFlagsHorizontal = SizeFlags.ExpandFill,
				TooltipText = BuildRequirementText(option)
			};
			button.Pressed += () => SelectOption(option);
			_options.AddChild(button);
		}
	}

	private void ClearOptionButtons()
	{
		foreach (var child in _options.GetChildren())
			child.QueueFree();
	}

	private bool IsOptionVisible(DialogueOption option)
	{
		if (option is null)
			return false;
		if (!string.IsNullOrWhiteSpace(option.Id) && _seenOptionIds.Contains(option.Id))
			return true;

		return MeetsRequirements(option);
	}

	private bool MeetsRequirements(DialogueOption option)
	{
		if (option is null || string.IsNullOrWhiteSpace(option.Id))
			return true;

		if (_minimumReputationByOptionId.TryGetValue(option.Id, out var minimumReputation) &&
			_reputation < minimumReputation)
		{
			return false;
		}

		if (_minimumRelationshipByOptionId.TryGetValue(option.Id, out var minimumRelationship) &&
			_relationship < minimumRelationship)
		{
			return false;
		}

		if (_requiredFlagByOptionId.TryGetValue(option.Id, out var flagId) &&
			string.Equals(flagId, TrustFlagId, StringComparison.OrdinalIgnoreCase) &&
			!_hasTrustFlag)
		{
			return false;
		}

		if (_requiredQuestStatusByOptionId.TryGetValue(option.Id, out var requiredQuestStatus) &&
			_questStatus != requiredQuestStatus)
		{
			return false;
		}

		return true;
	}

	private string BuildRequirementText(DialogueOption option)
	{
		if (option is null || string.IsNullOrWhiteSpace(option.Id))
			return string.Empty;

		var builder = new StringBuilder();
		if (_minimumReputationByOptionId.TryGetValue(option.Id, out var minimumReputation))
			builder.AppendLine($"Requires reputation >= {minimumReputation}.");
		if (_minimumRelationshipByOptionId.TryGetValue(option.Id, out var minimumRelationship))
			builder.AppendLine($"Requires relationship >= {minimumRelationship}.");
		if (_requiredFlagByOptionId.TryGetValue(option.Id, out var flagId))
			builder.AppendLine($"Requires flag '{flagId}'.");
		if (_requiredQuestStatusByOptionId.TryGetValue(option.Id, out var requiredQuestStatus))
			builder.AppendLine($"Requires quest status {requiredQuestStatus}.");

		if (!MeetsRequirements(option) && _seenOptionIds.Contains(option.Id))
			builder.AppendLine("Seen choices stay visible but are disabled while gated.");

		return builder.ToString().Trim();
	}

	private void AppendNodeLines(DialogueNode node)
	{
		if (node is null)
			return;

		if (node.Lines.Count > 0)
		{
			foreach (var line in node.Lines)
				AppendLine(line.Speaker, line.Text);
			return;
		}

		if (!string.IsNullOrWhiteSpace(node.Text))
			AppendLine("Customer", node.Text);
	}

	private void AppendOptionResponse(DialogueOption option)
	{
		if (option.ResponseLines.Count > 0)
		{
			foreach (var line in option.ResponseLines)
				AppendLine(line.Speaker, line.Text);
			return;
		}

		if (!string.IsNullOrWhiteSpace(option.ResponseText))
			AppendLine("Customer", option.ResponseText);
	}

	private void AppendLine(string speaker, string text)
	{
		if (string.IsNullOrWhiteSpace(text))
			return;

		_conversationLines.Add(string.IsNullOrWhiteSpace(speaker)
			? text
			: $"{speaker}: {text}");
	}

	private void AppendSystemLine(string text)
	{
		AppendLine("System", text);
	}

	private DialogueGraph BuildGraph()
	{
		_minimumReputationByOptionId.Clear();
		_minimumRelationshipByOptionId.Clear();
		_requiredFlagByOptionId.Clear();
		_requiredQuestStatusByOptionId.Clear();

		_minimumReputationByOptionId["ask_reputation"] = 55;
		_requiredFlagByOptionId["ask_secret"] = TrustFlagId;
		_minimumRelationshipByOptionId["ask_secret"] = 55;
		_requiredQuestStatusByOptionId["complete_order"] = QuestStatus.InProgress;

		return new DialogueGraph
		{
			StartNodeId = "intro",
			Nodes =
			{
				new DialogueNode
				{
					Id = "intro",
					Lines =
					{
						new DialogueLine
						{
							Speaker = "Mara",
							Text = "You are testing a branching dialogue tree. Some options are hidden until the test state changes."
						}
					},
					Options =
					{
						new DialogueOption
						{
							Id = "ask_work",
							Label = "Ask what Mara needs.",
							ResponseText = "A discreet tonic, something useful for a customer who cannot sleep.",
							NextNodeId = "hub"
						},
						new DialogueOption
						{
							Id = "offer_sample",
							Label = "Offer a free sample to build trust.",
							ResponseText = "That is generous. I will remember it.",
							NextNodeId = "hub"
						},
						new DialogueOption
						{
							Id = "ask_reputation",
							Label = "Mention your shop's good reputation.",
							ResponseText = "If your reputation is already that strong, perhaps this is safer than I feared.",
							NextNodeId = "hub"
						},
						new DialogueOption
						{
							Id = "leave_intro",
							Label = "End the conversation.",
							ResponseText = "Mara nods and steps away from the counter.",
							EndsDialogue = true
						}
					}
				},
				new DialogueNode
				{
					Id = "hub",
					Lines =
					{
						new DialogueLine
						{
							Speaker = "Mara",
							Text = "Now choose a branch, change the state controls, or return to earlier options."
						}
					},
					Options =
					{
						new DialogueOption
						{
							Id = "ask_secret",
							Label = "Ask why the order must stay quiet.",
							ResponseText = "Because the mayor's household will deny the illness unless the remedy works.",
							NextNodeId = "hub"
						},
						new DialogueOption
						{
							Id = "start_order",
							Label = "Accept Mara's potion order.",
							ResponseText = "Then the order begins. Bring back something gentle and precise.",
							NextNodeId = "hub"
						},
						new DialogueOption
						{
							Id = "complete_order",
							Label = "Resolve the order as a successful sale.",
							ResponseText = "The tonic works. Mara's trust in the shop improves.",
							NextNodeId = "hub"
						},
						new DialogueOption
						{
							Id = "return_intro",
							Label = "Return to the opening choices.",
							ResponseText = "You circle back to the beginning of the exchange.",
							NextNodeId = "intro"
						},
						new DialogueOption
						{
							Id = "leave_hub",
							Label = "End the conversation.",
							ResponseText = "Mara gathers her notes and leaves.",
							EndsDialogue = true
						}
					}
				}
			}
		};
	}
}
