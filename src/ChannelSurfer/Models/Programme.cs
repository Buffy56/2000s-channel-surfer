namespace ChannelSurfer.Models;

/// <summary>A &lt;channel&gt; entry from XMLTV (its id and first display-name).</summary>
public record XmltvChannel(string Id, string DisplayName);

/// <summary>A &lt;programme&gt; entry from XMLTV, linked to a channel by ChannelId.</summary>
public record Programme(string ChannelId, string Title, string? Desc, DateTimeOffset Start, DateTimeOffset Stop, string? Category);
