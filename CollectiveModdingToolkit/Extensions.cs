namespace CollectiveModdingToolkit;

internal static class Extensions
{
	public static int CountLines(this string text)
	{
		int count = 0;
		if (!string.IsNullOrEmpty(text))
		{
			count = text.Length - text.Replace("\n", string.Empty).Length;
			if (text[^1] != '\n')
				++count;
		}
		return count;
	}
}
