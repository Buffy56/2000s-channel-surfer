using ChannelSurfer.Models;

namespace ChannelSurfer.Scheduling;

/// <summary>A grid view of the lineup: time-slot headers across the top, one row of cells per channel.</summary>
public sealed class GuideModel
{
    public IReadOnlyList<DateTimeOffset> Headers { get; }
    public IReadOnlyList<GuideRow> Rows { get; }

    private GuideModel(IReadOnlyList<DateTimeOffset> headers, IReadOnlyList<GuideRow> rows)
    {
        Headers = headers;
        Rows = rows;
    }

    public static GuideModel Build(Lineup lineup, DateTimeOffset start, int slotCount, TimeSpan slot)
    {
        var headers = new List<DateTimeOffset>(slotCount);
        for (var i = 0; i < slotCount; i++)
            headers.Add(start + i * slot);

        var rows = new List<GuideRow>(lineup.Channels.Count);
        foreach (var channel in lineup.Channels)
            rows.Add(new GuideRow(channel, BuildCells(lineup.Programmes(channel), start, slotCount, slot)));

        return new GuideModel(headers, rows);
    }

    private static IReadOnlyList<GuideCell> BuildCells(
        IReadOnlyList<Programme> programmes, DateTimeOffset start, int slotCount, TimeSpan slot)
    {
        var cells = new List<GuideCell>();
        var slotIndex = 0;
        while (slotIndex < slotCount)
        {
            var slotStart = start + slotIndex * slot;
            var p = programmes.FirstOrDefault(pr => pr.Start <= slotStart && slotStart < pr.Stop);
            if (p is not null)
            {
                var endSlot = (int)Math.Min(slotCount,
                    Math.Ceiling((p.Stop - start).TotalSeconds / slot.TotalSeconds));
                var span = Math.Max(1, endSlot - slotIndex);
                cells.Add(new GuideCell(p.Title, slotIndex, span, p));
                slotIndex += span;
            }
            else
            {
                cells.Add(new GuideCell("No info", slotIndex, 1, null));
                slotIndex++;
            }
        }
        return cells;
    }
}

public sealed record GuideRow(Channel Channel, IReadOnlyList<GuideCell> Cells);

public sealed record GuideCell(string Title, int StartSlotIndex, int SpanSlots, Programme? Programme);
