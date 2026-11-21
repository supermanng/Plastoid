using System.Globalization;
using System.Text;

namespace Plastoids.Modules.Identity.Jobs;

/// <summary>
/// Dependency-free PDF 1.4 writer for plain-text documents (Helvetica, A4). Enough for the NDPA export's
/// human-readable copy; a real report engine can replace it later without touching the job.
/// </summary>
internal static class MinimalPdf
{
    private const int LinesPerPage = 60;

    public static byte[] FromLines(string title, IReadOnlyList<string> lines)
    {
        var pages = Chunk(lines, LinesPerPage).ToList();
        if (pages.Count == 0)
        {
            pages.Add([]);
        }

        var objects = new List<string>();
        // 1: catalog, 2: pages, 3: font, then page + content pairs.
        objects.Add("<< /Type /Catalog /Pages 2 0 R >>");
        var pageIds = Enumerable.Range(0, pages.Count).Select(i => 4 + i * 2).ToList();
        objects.Add($"<< /Type /Pages /Kids [{string.Join(' ', pageIds.Select(id => $"{id} 0 R"))}] /Count {pages.Count} >>");
        objects.Add("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>");

        for (var p = 0; p < pages.Count; p++)
        {
            var content = new StringBuilder();
            content.Append("BT /F1 10 Tf 12 TL 40 800 Td ");
            content.Append('(').Append(Escape($"{title} — page {p + 1}/{pages.Count}")).Append(") Tj T* T* ");
            foreach (var line in pages[p])
            {
                content.Append('(').Append(Escape(line)).Append(") Tj T* ");
            }

            content.Append("ET");
            var stream = content.ToString();
            objects.Add($"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Resources << /Font << /F1 3 0 R >> >> /Contents {pageIds[p] + 1} 0 R >>");
            objects.Add($"<< /Length {Encoding.Latin1.GetByteCount(stream)} >>\nstream\n{stream}\nendstream");
        }

        var output = new StringBuilder("%PDF-1.4\n");
        var offsets = new List<int>();
        for (var i = 0; i < objects.Count; i++)
        {
            offsets.Add(Encoding.Latin1.GetByteCount(output.ToString()));
            output.Append(CultureInfo.InvariantCulture, $"{i + 1} 0 obj\n{objects[i]}\nendobj\n");
        }

        var xref = Encoding.Latin1.GetByteCount(output.ToString());
        output.Append(CultureInfo.InvariantCulture, $"xref\n0 {objects.Count + 1}\n0000000000 65535 f \n");
        foreach (var offset in offsets)
        {
            output.Append(CultureInfo.InvariantCulture, $"{offset:D10} 00000 n \n");
        }

        output.Append(CultureInfo.InvariantCulture, $"trailer\n<< /Size {objects.Count + 1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n");
        return Encoding.Latin1.GetBytes(output.ToString());
    }

    private static string Escape(string text)
    {
        var sb = new StringBuilder(text.Length + 8);
        foreach (var c in text)
        {
            switch (c)
            {
                case '\\': sb.Append("\\\\"); break;
                case '(': sb.Append("\\("); break;
                case ')': sb.Append("\\)"); break;
                default: sb.Append(c < 32 || c > 126 ? '?' : c); break;
            }
        }

        return sb.ToString();
    }

    private static IEnumerable<IReadOnlyList<string>> Chunk(IReadOnlyList<string> lines, int size)
    {
        for (var i = 0; i < lines.Count; i += size)
        {
            yield return lines.Skip(i).Take(size).ToList();
        }
    }
}
