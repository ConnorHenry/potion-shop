using System.Collections.Generic;
using Godot;

namespace OccultShop.Tutorial.Presentation;

public sealed class TutorialInteractionGate
{
	private readonly Dictionary<BaseButton, bool> _disabledButtonStates = new();
	private readonly HashSet<BaseButton> _allowedButtons = new();

	public void Apply(Node?[] roots, params BaseButton?[] allowedButtons)
	{
		Restore();
		_allowedButtons.Clear();
		foreach (var allowedButton in allowedButtons)
		{
			if (allowedButton is null)
				continue;

			_allowedButtons.Add(allowedButton);
		}

		foreach (var root in roots)
			DisableButtonsExceptAllowed(root);
	}

	public void Restore()
	{
		foreach (var pair in _disabledButtonStates)
		{
			if (!GodotObject.IsInstanceValid(pair.Key))
				continue;

			pair.Key.Disabled = pair.Value;
		}

		_disabledButtonStates.Clear();
		_allowedButtons.Clear();
	}

	private void DisableButtonsExceptAllowed(Node? root)
	{
		if (root is null)
			return;

		foreach (var child in root.GetChildren())
		{
			if (child is BaseButton button)
				DisableButtonExceptAllowed(button);

			DisableButtonsExceptAllowed(child);
		}
	}

	private void DisableButtonExceptAllowed(BaseButton button)
	{
		if (_allowedButtons.Contains(button))
			return;

		if (!_disabledButtonStates.ContainsKey(button))
			_disabledButtonStates[button] = button.Disabled;

		button.Disabled = true;
	}
}
