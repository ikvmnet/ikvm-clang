namespace IKVM.Clang.Sdk.Tasks;

using System;
using System.IO;
using System.Linq;
using System.Text;

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

/// <summary>
/// Describes how one source file is compiled: the arguments passed to clang, and the same command as a JSON
/// array (in the form of a <c>compile_commands.json</c> entry's <c>arguments</c>) for tools such as clangd.
/// </summary>
public class ClangCompileCommand : Task
{

    /// <summary>
    /// The source file being compiled.
    /// </summary>
    [Required]
    public string Source { get; set; } = "";

    /// <summary>
    /// The directory the compiler runs in, against which relative paths in the arguments resolve.
    /// </summary>
    [Required]
    public string Directory { get; set; } = "";

    /// <summary>
    /// The compiler, as the first element of the command line.
    /// </summary>
    [Required]
    public string Compiler { get; set; } = "";

    /// <summary>
    /// The argument items. See <see cref="ClangArguments.Expand"/>.
    /// </summary>
    [Required]
    public ITaskItem[] Arguments { get; set; } = Array.Empty<ITaskItem>();

    /// <summary>
    /// The command, with the expanded arguments as metadata.
    /// </summary>
    [Output]
    public ITaskItem Command { get; set; } = null!;

    /// <summary>
    /// The expanded arguments, one item per argument, excluding the compiler.
    /// </summary>
    [Output]
    public ITaskItem[] ArgumentList { get; set; } = Array.Empty<ITaskItem>();

    /// <inheritdoc />
    public override bool Execute()
    {
        var directory = Path.GetFullPath(Directory);
        var source = Path.GetFullPath(Path.Combine(directory, Source));
        var arguments = ClangArguments.Expand(Arguments).ToArray();

        var json = new StringBuilder("[");
        ClangArguments.AppendJsonString(json, Compiler);
        foreach (var argument in arguments)
            ClangArguments.AppendJsonString(json.Append(','), argument);
        json.Append(']');

        var command = new TaskItem(Escape(source));
        command.SetMetadata("WorkingDirectory", Escape(directory));
        command.SetMetadata("CommandLine", Escape(json.ToString()));
        Command = command;

        ArgumentList = arguments.Select(i => (ITaskItem)new TaskItem(Escape(i))).ToArray();
        return true;
    }

    /// <summary>
    /// Escapes the characters MSBuild treats specially, since <see cref="TaskItem"/> takes item specs and
    /// metadata in escaped form. Without this an argument such as <c>-DPCT=%d</c> or <c>-DLIST=a;b</c> would not
    /// survive the round trip.
    /// </summary>
    static string Escape(string value)
    {
        var sb = new StringBuilder(value.Length);

        foreach (var c in value)
        {
            if (c is '%' or '*' or '?' or '@' or '$' or '(' or ')' or ';' or '\'')
                sb.Append('%').Append(((int)c).ToString("X2"));
            else
                sb.Append(c);
        }

        return sb.ToString();
    }

}
