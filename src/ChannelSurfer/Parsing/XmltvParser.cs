using System.Globalization;
using System.Xml.Linq;
using ChannelSurfer.Models;

namespace ChannelSurfer.Parsing;

/// <summary>Parses an XMLTV document into channels and programmes.</summary>
public static class XmltvParser
{
    public static (IReadOnlyList<XmltvChannel> Channels, IReadOnlyList<Programme> Programmes) Parse(string xml)
    {
        var root = XDocument.Parse(xml).Root;
        if (root is null)
            return (Array.Empty<XmltvChannel>(), Array.Empty<Programme>());

        var channels = root.Elements("channel")
            .Select(c => new XmltvChannel(
                (string?)c.Attribute("id") ?? "",
                (string?)c.Element("display-name") ?? ""))
            .ToList();

        var programmes = root.Elements("programme")
            .Select(p => new Programme(
                (string?)p.Attribute("channel") ?? "",
                (string?)p.Element("title") ?? "",
                (string?)p.Element("desc"),
                ParseTime((string?)p.Attribute("start")),
                ParseTime((string?)p.Attribute("stop")),
                (string?)p.Element("category")))
            .ToList();

        return (channels, programmes);
    }

    /// <summary>XMLTV time is "YYYYMMDDHHMMSS [+/-HHMM]". The offset has no colon, so parse it by hand.</summary>
    private static DateTimeOffset ParseTime(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return default;
        s = s.Trim();

        var space = s.IndexOf(' ');
        var datePart = space < 0 ? s : s[..space];
        var offset = space < 0 ? TimeSpan.Zero : ParseOffset(s[(space + 1)..]);

        var fmt = datePart.Length == 12 ? "yyyyMMddHHmm" : "yyyyMMddHHmmss";
        var dt = DateTime.ParseExact(datePart, fmt, CultureInfo.InvariantCulture, DateTimeStyles.None);
        return new DateTimeOffset(dt, offset);
    }

    private static TimeSpan ParseOffset(string o)
    {
        o = o.Trim();
        var sign = o.StartsWith('-') ? -1 : 1;
        var digits = o.TrimStart('+', '-');
        var hours = int.Parse(digits[..2], CultureInfo.InvariantCulture);
        var minutes = digits.Length >= 4 ? int.Parse(digits[2..4], CultureInfo.InvariantCulture) : 0;
        return new TimeSpan(sign * hours, sign * minutes, 0);
    }
}
