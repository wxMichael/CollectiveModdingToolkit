using Avalonia.Controls.Primitives;

namespace CollectiveModdingToolkit.Controls;
/// <summary>
/// <para>A thin wrapper for <see cref="HeaderedContentControl"/> intended for recreations of GroupBox and GroupBox-like Controls.</para>
/// This Control displays a frame around a <i>group</i> of controls with an optional caption.<br/>
/// </summary>
public partial class GroupBox : HeaderedContentControl
{
	public GroupBox() => InitializeComponent();
}
