namespace IKVM.Clang.Sdk.Tasks;

using System;
using System.Collections.Generic;
using System.Text;

using Microsoft.Build.Framework;

/// <summary>
/// Expands argument items into the individual command line arguments they stand for.
/// </summary>
/// <remarks>
/// An argument item is its item spec, optionally followed by <c>Value</c>, <c>Value2</c>, ... metadata, each
/// preceded by the matching <c>Separator</c>, <c>Separator2</c>, ... metadata. A separator made only of white
/// space starts a new argument, so <c>-o</c> with a value and a space separator becomes two arguments; any other
/// separator joins the value to the argument, as in <c>--target=x86_64-pc-windows-msvc</c> or <c>-DNAME=1</c>.
/// </remarks>
public static class ClangArguments
{

    /// <summary>
    /// Returns the individual arguments described by the given items.
    /// </summary>
    public static IEnumerable<string> Expand(IEnumerable<ITaskItem> items)
    {
        if (items is null)
            throw new ArgumentNullException(nameof(items));

        foreach (var item in items)
        {
            if (item.ItemSpec.Length == 0)
                continue;

            var current = new StringBuilder(item.ItemSpec);

            for (int i = 1; i < 128; i++)
            {
                var suffix = i == 1 ? "" : i.ToString();
                var value = item.GetMetadata("Value" + suffix);
                if (string.IsNullOrEmpty(value))
                    break;

                var separator = item.GetMetadata("Separator" + suffix) ?? "";
                if (separator.Length > 0 && separator.Trim().Length == 0)
                {
                    yield return current.ToString();
                    current.Clear();
                }
                else
                {
                    current.Append(separator);
                }

                current.Append(value);
            }

            yield return current.ToString();
        }
    }

    /// <summary>
    /// Appends the argument to a response file line, quoting and escaping it as needed by the GNU response file
    /// syntax that clang uses.
    /// </summary>
    public static StringBuilder AppendQuoted(StringBuilder sb, string argument)
    {
        var quote = argument.Length == 0;
        foreach (var c in argument)
            quote |= char.IsWhiteSpace(c);

        if (quote)
            sb.Append('"');

        foreach (var c in argument)
        {
            if (c == '"' || c == '\\')
                sb.Append('\\');

            sb.Append(c);
        }

        if (quote)
            sb.Append('"');

        return sb;
    }

    /// <summary>
    /// Appends the given value as a JSON string literal.
    /// </summary>
    public static StringBuilder AppendJsonString(StringBuilder sb, string value)
    {
        sb.Append('"');

        foreach (var c in value)
        {
            switch (c)
            {
                case '"':
                    sb.Append("\\\"");
                    break;
                case '\\':
                    sb.Append("\\\\");
                    break;
                case '\n':
                    sb.Append("\\n");
                    break;
                case '\r':
                    sb.Append("\\r");
                    break;
                case '\t':
                    sb.Append("\\t");
                    break;
                default:
                    if (c < ' ')
                        sb.Append("\\u").Append(((int)c).ToString("x4"));
                    else
                        sb.Append(c);
                    break;
            }
        }

        return sb.Append('"');
    }

}
