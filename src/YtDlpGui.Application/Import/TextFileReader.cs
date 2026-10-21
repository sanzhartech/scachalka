using System.Text;

namespace YtDlpGui.Application.Import;

/// <summary>
/// Reads import files with encoding detection, shared by all importers.
/// Apple Music/iTunes exports are UTF-16 LE (usually with a BOM); hand-made
/// lists are typically UTF-8. A BOM wins; otherwise embedded NUL bytes
/// indicate BOM-less UTF-16.
/// </summary>
internal static class TextFileReader
{
    public static string ReadAllTextSmart(string path)
    {
        var bytes = File.ReadAllBytes(path);
        if (bytes.Length >= 2)
        {
            if (bytes[0] == 0xFF && bytes[1] == 0xFE)
            {
                return Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2);
            }

            if (bytes[0] == 0xFE && bytes[1] == 0xFF)
            {
                return Encoding.BigEndianUnicode.GetString(bytes, 2, bytes.Length - 2);
            }
        }

        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
        {
            return Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);
        }

        var probe = Math.Min(bytes.Length, 512);
        for (var i = 0; i < probe; i++)
        {
            if (bytes[i] == 0)
            {
                // Text files have no NULs unless they are UTF-16 without a BOM.
                return Encoding.Unicode.GetString(bytes);
            }
        }

        return Encoding.UTF8.GetString(bytes);
    }
}
