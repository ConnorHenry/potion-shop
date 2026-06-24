using System;
using System.Collections.Generic;

namespace OccultShop.Dialogue;

public sealed class DialogueGraph
{
	public string StartNodeId { get; set; } = "";
	public List<DialogueNode> Nodes { get; set; } = new();

	public bool HasNodes => Nodes.Count > 0;

	public bool TryGetStartNode(out DialogueNode? node)
	{
		return TryGetNode(string.Empty, out node);
	}

	public bool TryGetNode(string? nodeId, out DialogueNode? node)
	{
		node = null;
		var resolvedNodeId = string.IsNullOrWhiteSpace(nodeId) ? StartNodeId : nodeId;
		if (string.IsNullOrWhiteSpace(resolvedNodeId) && Nodes.Count > 0)
		{
			node = Nodes[0];
			return true;
		}

		foreach (var candidate in Nodes)
		{
			if (string.Equals(candidate.Id, resolvedNodeId, StringComparison.OrdinalIgnoreCase))
			{
				node = candidate;
				return true;
			}
		}

		return false;
	}
}
