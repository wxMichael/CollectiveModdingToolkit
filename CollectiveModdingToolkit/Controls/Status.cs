using System;
using Avalonia;
using Avalonia.Controls;
using CollectiveModdingToolkit.Models;

namespace CollectiveModdingToolkit.Controls;

/// <summary>
///     Maps a <see cref="StatusLevel" /> onto the shared Neutral/Good/Caution/Warning/Bad style classes.<br />
///     See: Styles/Text.axaml
/// </summary>
public static class Status
{
	public static readonly AttachedProperty<StatusLevel> LevelProperty =
		AvaloniaProperty.RegisterAttached<Control, Control, StatusLevel>("Level");

	static Status()
	{
		LevelProperty.Changed.AddClassHandler<Control>((c, e) =>
		{
			foreach (var level in Enum.GetValues<StatusLevel>()) c.Classes.Remove(level.ToString());

			var next = (StatusLevel)e.NewValue!;
			if (next != StatusLevel.None) c.Classes.Add(next.ToString());
		});
	}

	public static StatusLevel GetLevel(Control c) => c.GetValue(LevelProperty);

	public static void SetLevel(Control c, StatusLevel value) => c.SetValue(LevelProperty, value);

	/// <summary>Warning at 95% of the limit, Bad once exceeded.</summary>
	public static StatusLevel ForLimit(int count, int max) =>
		count > max ? StatusLevel.Bad
		: count >= max * 0.95 ? StatusLevel.Warning
		: StatusLevel.Good;
}
