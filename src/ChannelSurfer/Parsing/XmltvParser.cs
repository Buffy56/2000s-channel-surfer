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
            .Select(TryProgramme)
            .Where(p => p is not null)
            .Select(p => p!)
            .ToList();

        return (channels, programmes);
    }

    // One malformed programme must not drop the whole guide: skip it, keep the rest.
    private static Programme? TryProgramme(XElement p)
    {
        var start = ParseTime((string?)p.Attribute("start"));
        var stop = ParseTime((string?)p.Attribute("stop"));
        if (start is null || stop is null) return null;

        return new Programme(
            (string?)p.Attribute("channel") ?? "",
            (string?)p.Element("title") ?? "",
            (string?)p.Element("desc"),
            start.Value,
            stop.Value,
            (string?)p.Element("category"));
    }

    /// <summary>XMLTV time is "YYYYMMDDHHMMSS [+/-HHMM]". The offset has no colon, so parse it by hand. Null if unparseable.</summary>
    private static DateTimeOffset? ParseTime(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        s = s.Trim();

        try
        {
            var space = s.IndexOf(' ');
            var datePart = space < 0 ? s : s[..space];
            var offset = space < 0 ? TimeSpan.Zero : ParseOffset(s[(space + 1)..]);

            var fmt = datePart.Length == 12 ? "yyyyMMddHHmm" : "yyyyMMddHHmmss";
            if (!DateTime.TryParseExact(datePart, fmt, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
                return null;
            return new DateTimeOffset(dt, offset);
        }
        catch
        {
            return null; // malformed offset or out-of-range value
        }
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
