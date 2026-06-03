using Godot;

namespace OccultShop.Infrastructure;

public static class NodeLookup
{
	public static bool TryGetRequiredNode<TNode>(
		Node owner,
		NodePath path,
		string ownerName,
		string exportName,
		out TNode node)
		where TNode : Node
	{
		node = default!;

		if (path is null || path.IsEmpty)
		{
			GD.PushError($"{ownerName}: {exportName} is not assigned.");
			return false;
		}

		var resolvedNode = owner.GetNodeOrNull<TNode>(path);
		if (resolvedNode is null)
		{
			GD.PushError($"{ownerName}: {typeof(TNode).Name} was not found at '{path}'.");
			return false;
		}

		node = resolvedNode;
		return true;
	}
}
