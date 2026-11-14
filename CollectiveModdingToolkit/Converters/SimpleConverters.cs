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
using System.Collections;
using System.Globalization;
using System.IO;
using Avalonia.Data;
using Avalonia.Data.Converters;
using CollectiveModdingToolkit.Models;
using CollectiveModdingToolkit.Models.FileTypes;

namespace CollectiveModdingToolkit.Converters;

internal class RemoveFileExtConverter : IValueConverter
{
	public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
		value == null ? BindingOperations.DoNothing : Path.GetFileNameWithoutExtension((string)value);

	public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
		BindingOperations.DoNothing;
}

/// <summary>
///     Binds an enum to a group of radio buttons: each button passes its value's name as the converter parameter.
/// </summary>
public sealed class EnumIsConverter : IValueConverter
{
	public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
		value != null && parameter is string name &&
		string.Equals(value.ToString(), name, StringComparison.Ordinal);

	public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
		value is true && parameter is string name && Enum.TryParse(targetType, name, out var result)
			? result
			: BindingOperations.DoNothing;
}

public sealed class PatchFailConverter : IValueConverter
{
	public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		if (value == null) return BindingOperations.DoNothing;

		return $"Patching failed: {Archive.Describe((FailReason)value)}";
	}

	public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
		BindingOperations.DoNothing;
}

internal class SeverityConverter : IValueConverter
{
	public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		if (value == null) return BindingOperations.DoNothing;

		var severity = (Severity)value;

		return severity switch
		{
			Severity.NoImpact => "Gray",
			Severity.Low => "#E0E03F",
			Severity.Medium => "#E6AC40",
			Severity.Critical => "#C23636",
			_ => "#C0C0C0",
		};
	}

	public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
		BindingOperations.DoNothing;
}

internal class IsNotNullOrEmptyConverter : IValueConverter
{
	public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
		value is ICollection collection && collection.Count != 0;

	public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
		BindingOperations.DoNothing;
}
