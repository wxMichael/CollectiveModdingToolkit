namespace CollectiveModdingToolkit.Models;

/// <summary>How a value should be shown. Each level maps to the style class of the same name (see Styles/Text.axaml).</summary>
public enum StatusLevel
{
	/// <summary>No styling; the control keeps its default look.</summary>
	None,
	Neutral,
	Good,
	Caution,
	Warning,
	Bad,
	Blue,
}
