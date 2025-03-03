/* 
 * 
 * https://github.com/BinToss/GroupBox.Avalonia
 * 
 * MIT License
 * 
 * Copyright (c) 2023 Noah Sherwin
 * 
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 * 
 * The above copyright notice and this permission notice shall be included in all
 * copies or substantial portions of the Software.
 * 
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
 * SOFTWARE.
 * 
*/

using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CollectiveModdingToolkit.Converters;

public static class GroupBoxConverters
{
	public static FuncMultiValueConverter<Rect?, CombinedGeometry> ConvertBoundsToOuterBorder { get; } =
	new(rects =>
	{
		List<Rect> arr = rects.Cast<Rect>().ToList() ?? throw new NullReferenceException();
		return arr.Count < 3
			? throw new ArgumentOutOfRangeException(nameof(rects), "Amount of given Bounds was less than 3")
			: new CombinedGeometry(
				GeometryCombineMode.Exclude,
				new RectangleGeometry(arr[0]),
				new RectangleGeometry(arr[1]),
				new TranslateTransform(-arr[2].Left, -arr[2].Top)
			);
	});

	/// <summary>
	/// Disguise a Thickness (e.g. Margin) as a Rect. The Rect's Left, Right, Top, and Bottom are equal to the Thickness's properties.
	/// </summary>
	public static FuncValueConverter<Thickness?, Rect> ThicknessRectConverter { get; } =
	new(thickness =>
	{
		return thickness is Thickness thick
			? new Rect(thick.Left, thick.Top, thick.Right - thick.Left, thick.Top - thick.Bottom)
			: throw new InvalidCastException();
	});
	public static FuncValueConverter<OperatingSystem?, CornerRadius> OsBorderCornerRadius { get; } = new((OperatingSystem? osVersion) =>
		new(osVersion?.Platform switch
		{
			PlatformID.Win32NT => osVersion.Version.Major switch
			{
				/* check radius via Windows' presentationsettings.exe */

				// XP and older. XP may have used 3, but older ones were probably 0.
				5 => 0,
				6 => osVersion.Version.Minor switch
				{
					// Vista, 7 are 3
					0 or 1 => 3,
					// 8, 8.1 are...unknown
					_ => 0,
				},
				// 10 and later. My copies of Win11 use 0.
				10 => osVersion.Version.Build switch
				{
					var win11 when win11 >= 22000 => 0,
					_ => 0,
				},
				_ => 0,
			},
			_ => 0
		}));

	public static FuncMultiValueConverter<object?, Thickness> ConvertDoublesToThickness { get; } = new(src =>
	{
		List<double> doubles = [];
		foreach (object? nd in src)
		{
			if (nd is double d)
				doubles.Add(d);
			else
				throw new InvalidCastException();
		}
		return doubles.Count switch
		{
			1 => new Thickness(doubles[0]),
			2 => new Thickness(doubles[0], doubles[1]),
			4 => new Thickness(doubles[0], doubles[1], doubles[2], doubles[3]),
			_ => throw new IndexOutOfRangeException("Source data must be doubles. Thickness' constructors only allow for 1, 2, or 4 arguments"),
		};
	});

	public static FuncValueConverter<Rect?, double> BoundsToHeaderHeightDividedByTwo { get; } = new(rect =>
	rect.HasValue ? rect.Value.Height / 2 : throw new InvalidCastException());
}
