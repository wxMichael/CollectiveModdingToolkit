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

using System.Collections.Specialized;
using Avalonia.Controls;
using Avalonia.Threading;
using CollectiveModdingToolkit.ViewModels;

namespace CollectiveModdingToolkit.Views;

public partial class TabDowngrader : UserControl
{
	private INotifyCollectionChanged? _messages;

	public TabDowngrader()
	{
		InitializeComponent();
		DataContextChanged += (_, _) =>
		{
			if (_messages != null) _messages.CollectionChanged -= ScrollToNewestMessage;

			_messages = (DataContext as DowngraderViewModel)?.Messages;
			if (_messages != null) _messages.CollectionChanged += ScrollToNewestMessage;
		};
	}

	private void ScrollToNewestMessage(object? sender, NotifyCollectionChangedEventArgs e)
	{
		if (e.Action == NotifyCollectionChangedAction.Add)
			Dispatcher.UIThread.Post(() => LogScroll.ScrollToEnd(), DispatcherPriority.Background);
	}
}
