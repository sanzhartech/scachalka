namespace YtDlpGui.Core.Arguments;

/// <summary>
/// Splits a user-supplied "custom yt-dlp arguments" string into tokens,
/// honoring double quotes (so paths with spaces work). Each token is passed to
/// the process via ArgumentList — the shell is never involved.
/// </summary>
public static class CommandLineTokenizer
{
    public static IReadOnlyList<string> Tokenize(string? commandLine)
    {
        if (string.IsNullOrWhiteSpace(commandLine))
        {
            return [];
        }

        var tokens = new List<string>();
        var current = new System.Text.StringBuilder();
        var inQuotes = false;

        foreach (var ch in commandLine)
        {
            if (ch == '"')
            {
                inQuotes = !inQuotes;
                continue;
            }

            if (char.IsWhiteSpace(ch) && !inQuotes)
            {
                if (current.Length > 0)
                {
                    tokens.Add(current.ToString());
                    current.Clear();
                }

                continue;
            }

            current.Append(ch);
        }

        if (current.Length > 0)
        {
            tokens.Add(current.ToString());
        }

        return tokens;
    }
}
