using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace IKVM.Clang.Sdk.Tests
{

    /// <summary>
    /// Writes small Clang projects that use the IKVM.Clang.Sdk package built alongside these tests, and builds them
    /// with <c>dotnet msbuild</c>. The projects need clang and llvm-ar, but no system headers or libraries.
    /// </summary>
    static class TestProjects
    {

        static readonly Lazy<string> root = new(CreateRoot);

        /// <summary>
        /// Directory the projects are written under, with the global.json and nuget.config that select the SDK.
        /// </summary>
        public static string Root => root.Value;

        static string CreateRoot()
        {
            var packageVersion = File.ReadAllLines("IKVM.Clang.Sdk.Tests.properties").Select(i => i.Split('=', 2)).First(i => i[0] == "PackageVersion")[1];

            var directory = Path.Combine(Path.GetTempPath(), "IKVM.Clang.Sdk.Tests", "Projects", Guid.NewGuid().ToString());
            Directory.CreateDirectory(directory);

            File.WriteAllText(Path.Combine(directory, "global.json"),
                "{ \"sdk\": { \"version\": \"10.0.0\", \"rollForward\": \"latestFeature\" }, \"msbuild-sdks\": { \"IKVM.Clang.Sdk\": \"" + packageVersion + "\" } }");

            var source = Path.Combine(Path.GetDirectoryName(typeof(TestProjects).Assembly.Location)!, "nuget");
            File.WriteAllText(Path.Combine(directory, "nuget.config"), string.Join(Environment.NewLine,
                "<configuration>",
                $"  <config><add key=\"globalPackagesFolder\" value=\"{Path.Combine(directory, "packages")}\" /></config>",
                $"  <packageSources><clear /><add key=\"dev\" value=\"{source}\" /></packageSources>",
                "</configuration>"));

            return directory;
        }

        /// <summary>
        /// Creates a project directory with the given files, and a project file of the given name with the given
        /// property and item XML.
        /// </summary>
        public static string Create(string name, string properties, string items = "", params (string Path, string Text)[] files)
        {
            var directory = Path.Combine(Root, name);
            Directory.CreateDirectory(directory);

            foreach (var (path, text) in files)
            {
                var full = Path.Combine(directory, path);
                Directory.CreateDirectory(Path.GetDirectoryName(full)!);
                File.WriteAllText(full, text);
            }

            File.WriteAllText(Path.Combine(directory, name + ".clangproj"), string.Join(Environment.NewLine,
                "<Project Sdk=\"IKVM.Clang.Sdk\">",
                "  <PropertyGroup>",
                "    " + properties,
                "  </PropertyGroup>",
                "  <ItemGroup>",
                "    " + items,
                "  </ItemGroup>",
                "</Project>"));

            return directory;
        }

        /// <summary>
        /// Runs <c>dotnet msbuild</c> on the project in the given directory, returning its exit code and output.
        /// </summary>
        public static (int ExitCode, string Output) MSBuild(string directory, params string[] arguments)
        {
            var info = new ProcessStartInfo("dotnet")
            {
                WorkingDirectory = directory,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };

            // worker nodes left running would hold the output pipes open
            info.Environment["MSBUILDDISABLENODEREUSE"] = "1";

            info.ArgumentList.Add("msbuild");
            info.ArgumentList.Add("-nologo");
            info.ArgumentList.Add("-v:n");
            info.ArgumentList.Add("-restore");
            info.ArgumentList.Add("-nodeReuse:false");
            foreach (var argument in arguments)
                info.ArgumentList.Add(argument);

            using var process = Process.Start(info)!;
            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();
            if (process.WaitForExit(TimeSpan.FromMinutes(5)) == false)
            {
                process.Kill(true);
                throw new TimeoutException($"MSBuild did not finish in {directory}.");
            }

            return (process.ExitCode, stdout.Result + stderr.Result);
        }

        /// <summary>
        /// The files in a project's intermediate directory for a target identifier, without the build's bookkeeping.
        /// </summary>
        public static string[] Outputs(string directory, string target)
        {
            var obj = Path.Combine(directory, "obj", "Debug", target);
            if (Directory.Exists(obj) == false)
                return Array.Empty<string>();

            return Directory.GetFiles(obj)
                .Select(Path.GetFileName)
                .Where(i => i!.EndsWith(".FileListAbsolute.txt") == false && i.EndsWith(".cmd") == false && i.EndsWith(".d") == false && i.EndsWith(".cache") == false)
                .OrderBy(i => i)
                .ToArray()!;
        }

        /// <summary>
        /// Gets the last write times of the given files in a project directory.
        /// </summary>
        public static Dictionary<string, DateTime> Times(string directory, params string[] paths)
        {
            return paths.ToDictionary(i => i, i => File.GetLastWriteTimeUtc(Path.Combine(directory, i)));
        }

    }

}
