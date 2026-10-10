namespace IKVM.Clang.Sdk.Tasks
{

    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Text;

    using Microsoft.Build.Framework;
    using Microsoft.Build.Utilities;

    /// <summary>
    /// Asks clang what a target triple means. A triple is whatever clang makes of it, so rather than reading meaning
    /// into the text, this has clang normalize it, lists the macros clang predefines for it, and compiles an empty file
    /// for it to see which object file format it produces. The answers are kept for each compiler, version of it and
    /// triple, for as long as the build process lives, and in <see cref="CacheFile"/> for later builds.
    /// </summary>
    public class ResolveClangTarget : Task
    {

        /// <summary>
        /// What clang said about a triple.
        /// </summary>
        sealed class Answer
        {

            public string Triple { get; set; } = "";

            public string ObjectFormat { get; set; } = "";

            public bool IsMsvc { get; set; }

            public string Problem { get; set; } = "";

        }

        static readonly ConcurrentDictionary<string, Answer> Answers = new(StringComparer.Ordinal);

        /// <summary>
        /// Full path of clang.
        /// </summary>
        [Required]
        public string ClangPath { get; set; } = "";

        /// <summary>
        /// The triple to ask about, as given to clang's <c>--target</c>; empty for the target clang builds for by default.
        /// </summary>
        public string? TargetTriple { get; set; }

        /// <summary>
        /// Environment variables for clang, as <c>NAME=VALUE</c>.
        /// </summary>
        public ITaskItem[] EnvironmentVariables { get; set; } = Array.Empty<ITaskItem>();

        /// <summary>
        /// The triple as clang normalizes it, such as <c>x86_64-unknown-linux-gnu</c> for <c>x86_64-linux-gnu</c>.
        /// </summary>
        [Output]
        public string ClangTargetTriple { get; set; } = "";

        /// <summary>
        /// The object file format clang produces for the target: <c>elf</c>, <c>coff</c>, <c>macho</c>, <c>wasm</c>,
        /// <c>xcoff</c> or <c>goff</c>; empty if it could not be told.
        /// </summary>
        [Output]
        public string ClangObjectFormat { get; set; } = "";

        /// <summary>
        /// Whether the target is the MSVC environment, in which clang defines <c>_MSC_VER</c>.
        /// </summary>
        [Output]
        public bool ClangTargetIsMsvc { get; set; }

        /// <summary>
        /// Why clang could not say, if it could not; the targets that need the answer fail with it.
        /// </summary>
        [Output]
        public string Problem { get; set; } = "";

        /// <summary>
        /// File to keep the answer in between builds, which is used again while clang and the triple are the same.
        /// </summary>
        public string? CacheFile { get; set; }

        /// <inheritdoc />
        public override bool Execute()
        {
            var triple = (TargetTriple ?? "").Trim();
            var clang = new FileInfo(ClangPath);
            var key = ClangPath + "|" + (clang.Exists ? clang.LastWriteTimeUtc.Ticks + "|" + clang.Length : "") + "|" + triple;

            if (Answers.TryGetValue(key, out var answer) == false)
            {
                answer = ReadCache(key) ?? Ask(triple);

                // a problem may be passing, such as a timeout, so ask again next time
                if (answer.Problem.Length == 0)
                {
                    Answers[key] = answer;
                    WriteCache(key, answer);
                }
            }

            ClangTargetTriple = answer.Triple;
            ClangObjectFormat = answer.ObjectFormat;
            ClangTargetIsMsvc = answer.IsMsvc;
            Problem = answer.Problem;

            Log.LogMessage(MessageImportance.Low, "Target '{0}': triple={1}; object format={2}; MSVC={3}", triple, ClangTargetTriple, ClangObjectFormat, ClangTargetIsMsvc);
            if (Problem.Length > 0)
                Log.LogMessage(MessageImportance.Low, Problem);

            return true;
        }

        Answer Ask(string triple)
        {
            var target = triple.Length > 0 ? new[] { "--target=" + triple } : Array.Empty<string>();
            var name = triple.Length > 0 ? $"the target '{triple}'" : "its default target";

            var objectPath = Path.Combine(Path.GetTempPath(), "ikvm-clang-" + Guid.NewGuid().ToString("N") + ".o");
            try
            {
                // at once: what clang makes of the triple, the macros it predefines for the target, and an empty object
                // file for it, to see its format
                var normalize = System.Threading.Tasks.Task.Run(() => Run(Concat(target, "-print-target-triple")));
                var predefine = System.Threading.Tasks.Task.Run(() => Run(Concat(target, "-x", "c", "-dM", "-E", "-")));
                var compile = System.Threading.Tasks.Task.Run(() => Run(Concat(target, "-x", "c", "-c", "-", "-o", objectPath)));
                System.Threading.Tasks.Task.WaitAll(normalize, predefine, compile);

                // the compile says best why the target cannot be built for
                if (compile.Result.ExitCode != 0 || File.Exists(objectPath) == false)
                    return Failed(name, compile.Result.Error);
                if (normalize.Result.ExitCode != 0)
                    return Failed(name, normalize.Result.Error);
                if (predefine.Result.ExitCode != 0)
                    return Failed(name, predefine.Result.Error);

                return new Answer()
                {
                    Triple = normalize.Result.Output.Trim(),
                    ObjectFormat = GetObjectFormat(objectPath),
                    IsMsvc = ParseMacros(predefine.Result.Output).Contains("_MSC_VER"),
                };
            }
            finally
            {
                try
                {
                    File.Delete(objectPath);
                }
                catch (IOException)
                {

                }
                catch (UnauthorizedAccessException)
                {

                }
            }
        }

        /// <summary>
        /// Reads the answer kept in <see cref="CacheFile"/>: a line with the key, then the triple, object format and
        /// whether it is MSVC, one to a line.
        /// </summary>
        Answer? ReadCache(string key)
        {
            if (string.IsNullOrEmpty(CacheFile) || File.Exists(CacheFile) == false)
                return null;

            try
            {
                var lines = File.ReadAllLines(CacheFile);
                if (lines.Length < 4 || lines[0] != key)
                    return null;

                return new Answer() { Triple = lines[1], ObjectFormat = lines[2], IsMsvc = lines[3] == "true" };
            }
            catch (IOException)
            {
                return null;
            }
            catch (UnauthorizedAccessException)
            {
                return null;
            }
        }

        void WriteCache(string key, Answer answer)
        {
            if (string.IsNullOrEmpty(CacheFile))
                return;

            var text = string.Join("\n", key, answer.Triple, answer.ObjectFormat, answer.IsMsvc ? "true" : "false") + "\n";

            try
            {
                if (File.Exists(CacheFile) && File.ReadAllText(CacheFile) == text)
                    return;

                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(CacheFile))!);
                File.WriteAllText(CacheFile, text);
            }
            catch (IOException e)
            {
                Log.LogMessage(MessageImportance.Low, "Could not write {0}: {1}", CacheFile, e.Message);
            }
            catch (UnauthorizedAccessException e)
            {
                Log.LogMessage(MessageImportance.Low, "Could not write {0}: {1}", CacheFile, e.Message);
            }
        }

        static Answer Failed(string name, string error)
        {
            error = error.Trim();
            return new Answer() { Problem = $"clang could not build for {name}, so the names of the files the build produces are not known." + (error.Length > 0 ? " clang said: " + error : "") };
        }

        static string[] Concat(string[] first, params string[] rest)
        {
            var result = new string[first.Length + rest.Length];
            first.CopyTo(result, 0);
            rest.CopyTo(result, first.Length);
            return result;
        }

        /// <summary>
        /// Runs clang with the given arguments and no input, returning its exit code and output.
        /// </summary>
        (int ExitCode, string Output, string Error) Run(string[] arguments)
        {
            var info = new ProcessStartInfo(ClangPath)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };

            var commandLine = new StringBuilder();
            foreach (var argument in arguments)
            {
                if (commandLine.Length > 0)
                    commandLine.Append(' ');
                ClangArguments.AppendQuoted(commandLine, argument);
            }

            info.Arguments = commandLine.ToString();

            foreach (var variable in EnvironmentVariables)
            {
                var text = variable.ItemSpec;
                var equals = text.IndexOf('=');
                if (equals > 0)
                    info.Environment[text.Substring(0, equals)] = text.Substring(equals + 1);
            }

            try
            {
                using var process = Process.Start(info);
                if (process is null)
                    return (-1, "", "");

                process.StandardInput.Close();
                var output = process.StandardOutput.ReadToEndAsync();
                var error = process.StandardError.ReadToEndAsync();

                if (process.WaitForExit(60000) == false)
                {
                    try
                    {
                        process.Kill();
                    }
                    catch (InvalidOperationException)
                    {

                    }

                    return (-1, "", "clang did not finish within a minute.");
                }

                System.Threading.Tasks.Task.WaitAll(output, error);
                return (process.ExitCode, output.Result, error.Result);
            }
            catch (Exception e)
            {
                return (-1, "", e.Message);
            }
        }

        /// <summary>
        /// Gets the names of the macros in the output of <c>clang -dM -E</c>.
        /// </summary>
        public static HashSet<string> ParseMacros(string output)
        {
            var macros = new HashSet<string>(StringComparer.Ordinal);

            foreach (var line in output.Split('\n'))
            {
                var text = line.Trim();
                if (text.StartsWith("#define ", StringComparison.Ordinal) == false)
                    continue;

                var name = text.Substring(8);
                var end = name.IndexOfAny(new[] { ' ', '(' });
                macros.Add(end < 0 ? name : name.Substring(0, end));
            }

            return macros;
        }

        /// <summary>
        /// Tells the format of an object file from its first bytes.
        /// </summary>
        public static string GetObjectFormat(string path)
        {
            var header = new byte[4];
            int count;
            using (var stream = File.OpenRead(path))
                count = stream.Read(header, 0, header.Length);

            return GetObjectFormat(header, count);
        }

        public static string GetObjectFormat(byte[] header, int count)
        {
            if (count >= 4 && header[0] == 0x7F && header[1] == (byte)'E' && header[2] == (byte)'L' && header[3] == (byte)'F')
                return "elf";

            if (count >= 4 && header[0] == 0x00 && header[1] == (byte)'a' && header[2] == (byte)'s' && header[3] == (byte)'m')
                return "wasm";

            if (count >= 4)
            {
                var magic = (uint)(header[0] << 24 | header[1] << 16 | header[2] << 8 | header[3]);
                if (magic is 0xFEEDFACE or 0xFEEDFACF or 0xCEFAEDFE or 0xCFFAEDFE)
                    return "macho";

                // COFF with more sections than the ordinary header allows, which starts with an anonymous object header
                if (magic == 0x0000FFFF)
                    return "coff";
            }

            if (count >= 2)
            {
                var bigEndian = header[0] << 8 | header[1];
                if (bigEndian is 0x01DF or 0x01F7)
                    return "xcoff";

                if (header[0] == 0x03)
                    return "goff";

                // otherwise COFF, which starts with the machine type
                var machine = header[1] << 8 | header[0];
                if (machine is 0x014C or 0x8664 or 0xAA64 or 0xA641 or 0x01C4 or 0x01C0 or 0x0200 or 0x5064)
                    return "coff";
            }

            return "";
        }

    }

}
