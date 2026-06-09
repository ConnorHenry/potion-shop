using Godot;

namespace OccultShop.Infrastructure;

public static class NodeLookup
{
	public static TNode? GetRequiredNodeOrNull<TNode>(
		Node owner,
		NodePath path,
		string ownerName,
		string exportName)
		where TNode : Node
	{
		if (path is null || path.IsEmpty)
		{
			GD.PushError($"{ownerName}: {exportName} is not assigned.");
			return null;
		}

		var resolvedNode = owner.GetNodeOrNull<TNode>(path);
		if (resolvedNode is null)
		{
			GD.PushError($"{ownerName}: {typeof(TNode).Name} was not found at '{path}'.");
			return null;
		}

		return resolvedNode;
	}

	public static TNode? GetOptionalNodeOrNull<TNode>(
		Node owner,
		NodePath path,
		string ownerName,
		string exportName,
		bool reportUnassigned = true,
		bool reportMissing = true)
		where TNode : Node
	{
		if (path is null || path.IsEmpty)
		{
			if (reportUnassigned)
				GD.PushError($"{ownerName}: {exportName} is not assigned.");

			return null;
		}

		var resolvedNode = owner.GetNodeOrNull<TNode>(path);
		if (resolvedNode is null && reportMissing)
			GD.PushError($"{ownerName}: {typeof(TNode).Name} was not found at '{path}'.");

		return resolvedNode;
	}

	public static bool TryGetRequiredNode<TNode>(
		Node owner,
		NodePath path,
		string ownerName,
		string exportName,
		out TNode node)
		where TNode : Node
	{
		node = default!;

		var resolvedNode = GetRequiredNodeOrNull<TNode>(owner, path, ownerName, exportName);
		if (resolvedNode is null)
			return false;

		node = resolvedNode;
		return true;
	}
}
