using System.Globalization;
using System.Text;

namespace Plastoids.Modules.Sponsorship.Services;

/// <summary>A minimal single-font PDF writer for the MVP impact report (blueprint §10 step 14). Text only; narrative and charts are Phase 2.</summary>
internal static class SimplePdf
{
    public static byte[] FromLines(IReadOnlyList<string> lines)
    {
        var content = new StringBuilder();
        content.Append("BT /F1 11 Tf 50 800 Td 15 TL\n");
        foreach (var line in lines)
        {
            content.Append('(').Append(Escape(line)).Append(") Tj T*\n");
        }

        content.Append("ET\n");
        var stream = Encoding.ASCII.GetBytes(content.ToString());

        var objects = new List<string>
        {
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Contents 4 0 R /Resources << /Font << /F1 5 0 R >> >> >>",
            $"<< /Length {stream.Length} >>\nstream\n{Encoding.ASCII.GetString(stream)}endstream",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>",
        };

        var pdf = new StringBuilder("%PDF-1.4\n");
        var offsets = new List<int>();
        for (var i = 0; i < objects.Count; i++)
        {
            offsets.Add(Encoding.ASCII.GetByteCount(pdf.ToString()));
            pdf.Append(CultureInfo.InvariantCulture, $"{i + 1} 0 obj\n{objects[i]}\nendobj\n");
        }

        var xref = Encoding.ASCII.GetByteCount(pdf.ToString());
        pdf.Append(CultureInfo.InvariantCulture, $"xref\n0 {objects.Count + 1}\n0000000000 65535 f \n");
        foreach (var offset in offsets)
        {
            pdf.Append(CultureInfo.InvariantCulture, $"{offset:D10} 00000 n \n");
        }

        pdf.Append(CultureInfo.InvariantCulture, $"trailer\n<< /Size {objects.Count + 1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n");
        return Encoding.ASCII.GetBytes(pdf.ToString());
    }

    private static string Escape(string text)
    {
        var ascii = new StringBuilder(text.Length);
        foreach (var ch in text.Replace("₦", "NGN ", StringComparison.Ordinal))
        {
            ascii.Append(ch switch
            {
                '(' => "\\(",
                ')' => "\\)",
                '\\' => "\\\\",
                < (char)32 or > (char)126 => "?",
                _ => ch.ToString(),
            });
        }

        return ascii.ToString();
    }
}
