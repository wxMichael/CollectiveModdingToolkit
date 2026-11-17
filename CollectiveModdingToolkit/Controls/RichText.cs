using System;
using System.Collections.Generic;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;

namespace CollectiveModdingToolkit.Controls;

/// <summary>
///     Colored/emphasized text from a plain string, so it survives translation (.po msgstr).<br />
///     Usage: <c>&lt;TextBlock c:RichText.Markup="{Binding Message}" /&gt;</c><br />
///     Markup: <c>Found [bad]3[/bad] invalid files in [b]Data[/b].</c><br />
///     Tags: b, i, plus any color class from Styles/Text.axaml
///     (neutral, neutral2, good, caution, warning, bad, blue, purple, accent). Tags nest.<br />
///     Write <c>[[</c> for a literal <c>[</c>. Unknown or unbalanced tags are kept as literal text.<br />
///     Translators may move/reorder tags freely, they only wrap text.
/// </summary>
public static class RichText
{
	private static readonly Dictionary<string, string> ColorClasses = new()
	{
		["neutral"] = "Neutral",
		["neutral2"] = "Neutral2",
		["good"] = "Good",
		["caution"] = "Caution",
		["warning"] = "Warning",
		["bad"] = "Bad",
		["blue"] = "Blue",
		["purple"] = "Purple",
		["accent"] = "Accent",
	};

	public static readonly AttachedProperty<string?> MarkupProperty =
		AvaloniaProperty.RegisterAttached<TextBlock, TextBlock, string?>("Markup");

	static RichText() =>
		MarkupProperty.Changed.AddClassHandler<TextBlock>((tb, e) => Apply(tb, e.NewValue as string));

	public static string? GetMarkup(TextBlock t) => t.GetValue(MarkupProperty);

	public static void SetMarkup(TextBlock t, string? value) => t.SetValue(MarkupProperty, value);

	/// <summary>Remove markup tags, e.g. for logs or clipboard.</summary>
	public static string Strip(string? markup)
	{
		var text = new StringBuilder();
		foreach (var (s, _) in Parse(markup)) text.Append(s);
		return text.ToString();
	}

	private static void Apply(TextBlock tb, string? markup)
	{
		var inlines = tb.Inlines ??= [];
		inlines.Clear();
		foreach (var (text, tags) in Parse(markup))
		{
			var run = new Run(text);
			foreach (var tag in tags)
				switch (tag)
				{
					case "b": run.FontWeight = FontWeight.Bold; break;
					case "i": run.FontStyle = FontStyle.Italic; break;
					default: run.Classes.Add(ColorClasses[tag]); break;
				}

			inlines.Add(run);
		}
	}

	private static List<(string Text, List<string> Tags)> Parse(string? markup)
	{
		var result = new List<(string, List<string>)>();
		if (string.IsNullOrEmpty(markup)) return result;

		var open = new List<string>();
		var buf = new StringBuilder();

		for (var i = 0; i < markup.Length; i++)
		{
			var c = markup[i];
			if (c != '[')
			{
				// Plain text
				buf.Append(c);
				continue;
			}

			if (i + 1 < markup.Length && markup[i + 1] == '[')
			{
				// Escaped square bracket, "[["
				buf.Append('[');
				i++;
				continue;
			}

			var end = markup.IndexOf(']', i + 1);
			if (end == -1)
			{
				// Unpaired bracket; treat as plain text
				buf.Append(c);
				continue;
			}

			var name = markup.AsSpan(i + 1, end - i - 1);
			var closing = name.StartsWith("/");
			var key = (closing ? name[1..] : name).ToString().ToLowerInvariant();

			if (key is not ("b" or "i") && !ColorClasses.ContainsKey(key))
			{
				// Unknown tag; treat as plain text
				buf.Append(c);
				continue;
			}

			if (closing)
			{
				var idx = open.LastIndexOf(key);
				if (idx < 0)
				{
					buf.Append(markup, i, end - i + 1);
					i = end;
					continue;
				}

				Flush();
				open.RemoveAt(idx);
			}
			else
			{
				Flush();
				open.Add(key);
			}

			i = end;
		}

		Flush();
		return result;

		void Flush()
		{
			if (buf.Length == 0) return;
			result.Add((buf.ToString(), [.. open]));
			buf.Clear();
		}
	}
}
