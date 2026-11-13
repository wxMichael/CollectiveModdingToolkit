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

using System;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace CollectiveModdingToolkit.Views;

public partial class TabAbout : UserControl
{
	public TabAbout()
	{
		InitializeComponent();
		AttachedToVisualTree += (_, _) => RestartIconAnimation();
	}

	// Style animations are canceled when the control leaves the visual tree (tab switch)
	// and aren't restarted on return. Re-applying the style class restarts it.
	private void RestartIconAnimation()
	{
		RotatingIcon.Classes.Remove("Rotating");
		Dispatcher.UIThread.Post(() => RotatingIcon.Classes.Add("Rotating"), DispatcherPriority.Render);
	}

	private static void OpenFlyout(object? sender)
	{
		if (sender is not Control control || FlyoutBase.GetAttachedFlyout(control) is not { } fo) return;
		control.IsEnabled = false;
		fo.Closed += (_, _) => { control.IsEnabled = true; };
		fo.ShowAt(control);
		DispatcherTimer.RunOnce(fo.Hide, new TimeSpan(0, 0, 2));
	}

	private void Button_Click_ShowCopied(object? sender, RoutedEventArgs e) => OpenFlyout(sender);

	private void Button_Click_ShowLicenses(object? sender, RoutedEventArgs e) =>
		LicensesWindow.Open(TopLevel.GetTopLevel(this) as Window);
}
