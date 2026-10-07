namespace IKVM.Clang.Sdk.Tasks;

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

/// <summary>
/// Reads the files listed in a make-style dependency file, as written by clang's <c>-MD</c>.
/// </summary>
public static class DependencyFile
{

    /// <summary>
    /// Returns the prerequisites the file lists, with escapes removed.
    /// </summary>
    public static IReadOnlyList<string> Read(string path)
    {
        try
        {
            return Parse(File.ReadAllText(path));
        }
        catch (IOException)
        {
            return Array.Empty<string>();
        }
    }

    /// <summary>
    /// Parses <c>target: prerequisite prerequisite \ (newline) prerequisite</c>, where spaces in names are escaped
    /// with a backslash.
    /// </summary>
    public static IReadOnlyList<string> Parse(string text)
    {
        var result = new List<string>();
        var current = new StringBuilder();
        var afterTarget = false;

        for (int i = 0; i < text.Length; i++)
        {
            var c = text[i];

            // line continuation
            if (c == '\\' && i + 1 < text.Length && (text[i + 1] == '\n' || text[i + 1] == '\r'))
            {
                i++;
                if (text[i] == '\r' && i + 1 < text.Length && text[i + 1] == '\n')
                    i++;

                Flush();
                continue;
            }

            // escaped space, hash or dollar
            if (c == '\\' && i + 1 < text.Length && (text[i + 1] == ' ' || text[i + 1] == '#'))
            {
                current.Append(text[++i]);
                continue;
            }

            if (c == '$' && i + 1 < text.Length && text[i + 1] == '$')
            {
                current.Append('$');
                i++;
                continue;
            }

            // the target ends at the first colon followed by white space, which a drive letter's colon is not
            if (afterTarget == false && c == ':' && (i + 1 == text.Length || char.IsWhiteSpace(text[i + 1])))
            {
                current.Clear();
                afterTarget = true;
                continue;
            }

            if (char.IsWhiteSpace(c))
            {
                Flush();
                continue;
            }

            current.Append(c);
        }

        Flush();
        return result;

        void Flush()
        {
            if (current.Length > 0 && afterTarget)
                result.Add(current.ToString());

            if (afterTarget)
                current.Clear();
        }
    }

}
