namespace IKVM.Clang.Sdk.Tasks;

using System;
using System.Collections.Generic;
using System.Text;

/// <summary>
/// Reads a JSON array of strings, such as a compile command's <c>CommandLine</c>.
/// </summary>
public static class JsonStrings
{

    /// <summary>
    /// Parses the array.
    /// </summary>
    public static IReadOnlyList<string> Parse(string json)
    {
        var result = new List<string>();
        var i = Skip(json, 0);

        if (i >= json.Length || json[i] != '[')
            throw new FormatException("Expected a JSON array.");

        i = Skip(json, i + 1);
        if (i < json.Length && json[i] == ']')
            return result;

        while (true)
        {
            if (i >= json.Length || json[i] != '"')
                throw new FormatException("Expected a JSON string.");

            var value = new StringBuilder();
            for (i++; ; i++)
            {
                if (i >= json.Length)
                    throw new FormatException("Unterminated JSON string.");

                var c = json[i];
                if (c == '"')
                    break;

                if (c != '\\')
                {
                    value.Append(c);
                    continue;
                }

                if (++i >= json.Length)
                    throw new FormatException("Unterminated JSON escape.");

                switch (json[i])
                {
                    case '"': value.Append('"'); break;
                    case '\\': value.Append('\\'); break;
                    case '/': value.Append('/'); break;
                    case 'b': value.Append('\b'); break;
                    case 'f': value.Append('\f'); break;
                    case 'n': value.Append('\n'); break;
                    case 'r': value.Append('\r'); break;
                    case 't': value.Append('\t'); break;
                    case 'u':
                        if (i + 4 >= json.Length)
                            throw new FormatException("Truncated JSON escape.");
                        value.Append((char)Convert.ToInt32(json.Substring(i + 1, 4), 16));
                        i += 4;
                        break;
                    default:
                        throw new FormatException("Unknown JSON escape.");
                }
            }

            result.Add(value.ToString());
            i = Skip(json, i + 1);

            if (i < json.Length && json[i] == ',')
            {
                i = Skip(json, i + 1);
                continue;
            }

            if (i < json.Length && json[i] == ']')
                return result;

            throw new FormatException("Expected ',' or ']'.");
        }
    }

    static int Skip(string json, int i)
    {
        while (i < json.Length && char.IsWhiteSpace(json[i]))
            i++;

        return i;
    }

}
