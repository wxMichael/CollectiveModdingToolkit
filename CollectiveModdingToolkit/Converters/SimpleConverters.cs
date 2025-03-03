/*
 * 
 * Collective Modding Toolkit
 * Copyright (C) 2024, 2025  wxMichael
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
using Avalonia.Data;
using Avalonia.Data.Converters;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace CollectiveModdingToolkit.Converters;

internal class RemoveFileExtConverter : IValueConverter
{
	public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
		value == null ? BindingOperations.DoNothing : Path.GetFileNameWithoutExtension((string)value);

	public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
		BindingOperations.DoNothing;
}

public sealed class F4SEEmojiConverter : IMultiValueConverter
{
	public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture) => values?.Count != 2
			? throw new ArgumentException($"values.Count is {values?.Count}")
			: !values.All(x => x is bool or UnsetValueType or null)
			? throw new ArgumentException("Values are not bool")
			: values[0] is not bool supportsCurrent ||
			values[1] is not bool ogngCaution
			? BindingOperations.DoNothing
			: ogngCaution ? "⚠️" : supportsCurrent ? "✔" : "❌";
}
