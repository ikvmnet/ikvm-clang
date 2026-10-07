namespace IKVM.Clang.Sdk.Tasks;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

using Task = Microsoft.Build.Utilities.Task;

/// <summary>
/// Compiles the out-of-date sources of a project, several at a time.
/// </summary>
/// <remarks>
/// Each command is a <c>ClangCompileCommand</c> item: the source, with <c>CommandLine</c> (a JSON array of the
/// compiler and its arguments), <c>WorkingDirectory</c> and <c>ObjectPath</c> metadata. An object is out of date
/// when it is missing or older than its source, or than any file clang listed as read when it was last compiled
/// (kept in <c>object.d</c>), or when the command line differs from the one it was last compiled with (kept in
/// <c>object.cmd</c>). So editing a header, or changing a definition or any other option, recompiles what it
/// affects.
/// </remarks>
public class ClangCompile : Task, ICancelableTask
{

    readonly ConcurrentDictionary<Process, bool> _running = new();
    volatile bool _cancelled;

    /// <summary>
    /// The commands to run.
    /// </summary>
    [Required]
    public ITaskItem[] Commands { get; set; } = Array.Empty<ITaskItem>();

    /// <summary>
    /// The compiler to run, in place of the first element of each command line.
    /// </summary>
    [Required]
    public string Compiler { get; set; } = "";

    /// <summary>
    /// How many compilers to run at once; zero or less means one per processor.
    /// </summary>
    public int MaxParallelism { get; set; }

    /// <summary>
    /// Environment variables for the compiler, as <c>NAME=value</c>.
    /// </summary>
    public ITaskItem[] EnvironmentVariables { get; set; } = Array.Empty<ITaskItem>();

    /// <summary>
    /// The files this task writes or may write: the object, dependency and command files of every command.
    /// </summary>
    [Output]
    public ITaskItem[] FileWrites { get; set; } = Array.Empty<ITaskItem>();

    /// <summary>
    /// Whether any source was out of date and compiled.
    /// </summary>
    [Output]
    public bool Compiled { get; set; }

    /// <inheritdoc />
    public void Cancel()
    {
        _cancelled = true;

        foreach (var process in _running.Keys)
        {
            try
            {
                process.Kill();
            }
            catch (Exception)
            {
                // already gone
            }
        }
    }

    /// <inheritdoc />
    public override bool Execute()
    {
        var work = new List<(string Source, string Directory, string Object, string CommandFile, string DependencyFile, string CommandLine, IReadOnlyList<string> Arguments)>();
        var writes = new List<ITaskItem>();

        foreach (var command in Commands)
        {
            var directory = command.GetMetadata("WorkingDirectory");
            if (string.IsNullOrEmpty(directory))
                directory = Directory.GetCurrentDirectory();

            var source = Path.GetFullPath(Path.Combine(directory, command.ItemSpec));
            var objectPath = Path.GetFullPath(Path.Combine(directory, command.GetMetadata("ObjectPath")));
            var commandFile = objectPath + ".cmd";
            var dependencyFile = objectPath + ".d";
            var commandLine = command.GetMetadata("CommandLine");

            writes.Add(new TaskItem(objectPath));
            writes.Add(new TaskItem(commandFile));
            writes.Add(new TaskItem(dependencyFile));

            IReadOnlyList<string> arguments;
            try
            {
                arguments = JsonStrings.Parse(commandLine).Skip(1).ToList();
            }
            catch (FormatException e)
            {
                Log.LogError("The compile command of '{0}' could not be read: {1}", source, e.Message);
                continue;
            }

            if (IsUpToDate(source, objectPath, commandFile, dependencyFile, commandLine))
            {
                Log.LogMessage(MessageImportance.Low, "Skipping '{0}': '{1}' is up to date.", source, objectPath);
                continue;
            }

            work.Add((source, directory, objectPath, commandFile, dependencyFile, commandLine, arguments));
        }

        FileWrites = writes.ToArray();

        if (Log.HasLoggedErrors || work.Count == 0)
            return !Log.HasLoggedErrors;

        Compiled = true;

        var parallelism = MaxParallelism > 0 ? MaxParallelism : Environment.ProcessorCount;
        using var gate = new SemaphoreSlim(parallelism);

        var tasks = work.Select(async item =>
        {
            await gate.WaitAsync();
            try
            {
                if (_cancelled == false)
                    await CompileAsync(item.Source, item.Directory, item.Object, item.CommandFile, item.DependencyFile, item.CommandLine, item.Arguments);
            }
            finally
            {
                gate.Release();
            }
        }).ToArray();

        System.Threading.Tasks.Task.WaitAll(tasks);
        return !Log.HasLoggedErrors && _cancelled == false;
    }

    /// <summary>
    /// Whether the object is newer than everything it was compiled from, with the same command line.
    /// </summary>
    bool IsUpToDate(string source, string objectPath, string commandFile, string dependencyFile, string commandLine)
    {
        if (File.Exists(objectPath) == false || File.Exists(commandFile) == false || File.Exists(dependencyFile) == false)
            return false;

        if (File.ReadAllText(commandFile) != commandLine)
            return false;

        var built = File.GetLastWriteTimeUtc(objectPath);

        foreach (var input in new[] { source, commandFile }.Concat(DependencyFile.Read(dependencyFile)))
        {
            // a dependency that is gone, such as a removed header, needs the source compiled again to know
            if (File.Exists(input) == false || File.GetLastWriteTimeUtc(input) > built)
                return false;
        }

        return true;
    }

    async System.Threading.Tasks.Task CompileAsync(string source, string directory, string objectPath, string commandFile, string dependencyFile, string commandLine, IReadOnlyList<string> arguments)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(objectPath)!);

        // the command file changes only with the command line, so its time says when that last changed
        if (File.Exists(commandFile) == false || File.ReadAllText(commandFile) != commandLine)
            File.WriteAllText(commandFile, commandLine);

        // clang lists every file it reads, for the next build to check
        var all = arguments.Concat(new[] { "-MD", "-MF", dependencyFile }).ToList();

        var responseFile = Path.GetTempFileName();
        var rsp = new StringBuilder();
        foreach (var argument in all)
            ClangArguments.AppendQuoted(rsp, argument).AppendLine();
        File.WriteAllText(responseFile, rsp.ToString());

        try
        {
            var info = new ProcessStartInfo(Compiler, "@\"" + responseFile + "\"")
            {
                WorkingDirectory = directory,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };

            foreach (var variable in EnvironmentVariables)
            {
                var text = variable.ItemSpec;
                var equals = text.IndexOf('=');
                if (equals > 0)
                    info.Environment[text.Substring(0, equals)] = text.Substring(equals + 1);
            }

            Log.LogMessage(MessageImportance.Normal, "{0} {1}", Compiler, string.Join(" ", all.Select(i => i.IndexOf(' ') >= 0 ? "\"" + i + "\"" : i)));

            var output = new List<string>();
            int exitCode;

            using (var process = new Process() { StartInfo = info })
            {
                process.OutputDataReceived += (_, e) => { if (e.Data is not null) lock (output) output.Add(e.Data); };
                process.ErrorDataReceived += (_, e) => { if (e.Data is not null) lock (output) output.Add(e.Data); };

                try
                {
                    process.Start();
                }
                catch (Exception e)
                {
                    Log.LogError("Could not run '{0}': {1}", Compiler, e.Message);
                    return;
                }

                _running[process] = true;
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                await System.Threading.Tasks.Task.Run(() => process.WaitForExit());
                _running.TryRemove(process, out _);
                exitCode = process.ExitCode;
            }

            // logged together, so the output of compilers running at once does not interleave
            var hadErrors = false;
            lock (output)
            {
                foreach (var line in output)
                {
                    Log.LogMessageFromText(line, MessageImportance.High);
                    hadErrors |= line.Contains(": error:") || line.Contains(": fatal error:");
                }
            }

            if (exitCode != 0)
            {
                // the compile failed, so the next build must try again
                TryDelete(objectPath);
                if (hadErrors == false && _cancelled == false)
                    Log.LogError("{0} exited with code {1} compiling '{2}'.", Path.GetFileName(Compiler), exitCode, source);
            }
        }
        finally
        {
            TryDelete(responseFile);
        }
    }

    static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {

        }
        catch (UnauthorizedAccessException)
        {

        }
    }

}
