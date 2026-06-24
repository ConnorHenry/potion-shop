using System;
using System.Collections.Generic;

namespace OccultShop.Dialogue;

public sealed class DialogueSession
{
	private readonly DialogueGraph _graph;
	private readonly Func<DialogueOption, bool> _isOptionAvailable;
	private readonly int _maxVisibleOptions;
	private readonly List<DialogueOption> _visibleOptions = new();

	public DialogueSession(
		DialogueGraph graph,
		Func<DialogueOption, bool> isOptionAvailable,
		int maxVisibleOptions)
	{
		_graph = graph ?? throw new ArgumentNullException(nameof(graph));
		_isOptionAvailable = isOptionAvailable ?? throw new ArgumentNullException(nameof(isOptionAvailable));
		_maxVisibleOptions = Math.Max(1, maxVisibleOptions);
	}

	public DialogueNode? ActiveNode { get; private set; }
	public string ActiveNodeId => ActiveNode?.Id ?? string.Empty;
	public bool IsActive => ActiveNode is not null;
	public IReadOnlyList<DialogueOption> VisibleOptions => _visibleOptions;

	public bool TryStart(out DialogueNode? node)
	{
		if (!_graph.TryGetStartNode(out node) || node is null)
			return false;

		SetActiveNode(node);
		return true;
	}

	public bool TryMoveToNode(string nodeId, out DialogueNode? node, out string error)
	{
		error = string.Empty;
		if (!_graph.TryGetNode(nodeId, out node) || node is null)
		{
			error = $"Dialogue node '{nodeId}' was not found.";
			return false;
		}

		SetActiveNode(node);
		return true;
	}

	public bool TryMoveToNextNode(DialogueOption option, out DialogueNode? node, out string error)
	{
		node = null;
		error = string.Empty;
		if (option is null || string.IsNullOrWhiteSpace(option.NextNodeId))
			return false;

		return TryMoveToNode(option.NextNodeId, out node, out error);
	}

	public bool TryResolveReturnNode(
		DialogueOption option,
		string fallbackReturnNodeId,
		out DialogueNode? node,
		out string error)
	{
		node = null;
		error = string.Empty;
		if (option is null)
		{
			error = "Dialogue option was not provided.";
			return false;
		}

		var targetNodeId = !string.IsNullOrWhiteSpace(option.ReturnNodeId)
			? option.ReturnNodeId
			: option.NextNodeId;
		if (string.IsNullOrWhiteSpace(targetNodeId))
			targetNodeId = fallbackReturnNodeId;

		if (!string.IsNullOrWhiteSpace(targetNodeId) &&
			_graph.TryGetNode(targetNodeId, out var targetNode) &&
			targetNode is not null)
		{
			SetActiveNode(targetNode);
			node = targetNode;
			return true;
		}

		if (!string.IsNullOrWhiteSpace(targetNodeId))
		{
			error = $"Dialogue return node '{targetNodeId}' was not found.";
			return false;
		}

		if (ActiveNode is not null)
		{
			node = ActiveNode;
			return true;
		}

		if (_graph.TryGetStartNode(out var startNode) && startNode is not null)
		{
			SetActiveNode(startNode);
			node = startNode;
			return true;
		}

		error = "Dialogue session has no node to return to.";
		return false;
	}

	public IReadOnlyList<DialogueOption> RefreshVisibleOptions()
	{
		_visibleOptions.Clear();
		if (ActiveNode is null)
			return _visibleOptions;

		foreach (var option in ActiveNode.Options)
		{
			if (_visibleOptions.Count >= _maxVisibleOptions)
				break;
			if (!_isOptionAvailable(option))
				continue;

			_visibleOptions.Add(option);
		}

		return _visibleOptions;
	}

	public bool TrySelectVisibleOption(int optionIndex, out DialogueOption? option)
	{
		option = null;
		if (optionIndex < 0 || optionIndex >= _visibleOptions.Count)
			return false;

		option = _visibleOptions[optionIndex];
		return true;
	}

	private void SetActiveNode(DialogueNode node)
	{
		ActiveNode = node;
		_visibleOptions.Clear();
	}
}
