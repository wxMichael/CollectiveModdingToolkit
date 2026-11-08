/*
 *
 * Collective Modding Toolkit
 * Copyright (C) 2024, 2025, 2026  wxMichael
 *
 * This program is free software; you can redistribute it and/or
 * modify it under the terms of the GNU General Public License
 * as published by the Free Software Foundation; either version 2
 * of the License, or (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program; if not, see <https://www.gnu.org/licenses/>.
 *
 */

using Avalonia;
using Avalonia.Controls;

namespace CollectiveModdingToolkit.Behaviors;

/// <summary>Attached behavior for read-only grids. Selection is cleared immediately.</summary>
public static class NoSelection
{
	public static readonly AttachedProperty<bool> IsEnabledProperty =
		AvaloniaProperty.RegisterAttached<DataGrid, bool>("IsEnabled", typeof(NoSelection));

	static NoSelection()
	{
		IsEnabledProperty.Changed.AddClassHandler<DataGrid>((grid, e) =>
		{
			if (e.NewValue is true)
				grid.SelectionChanged += Clear;
			else
				grid.SelectionChanged -= Clear;
		});
	}

	public static bool GetIsEnabled(DataGrid grid) => grid.GetValue(IsEnabledProperty);

	public static void SetIsEnabled(DataGrid grid, bool value) => grid.SetValue(IsEnabledProperty, value);

	private static void Clear(object? sender, SelectionChangedEventArgs e)
	{
		e.Handled = true;
		if (sender is not DataGrid { SelectedItem: not null } grid) return;

		grid.SelectedIndex = -1;
		grid.SelectedItem = null;
	}
}
