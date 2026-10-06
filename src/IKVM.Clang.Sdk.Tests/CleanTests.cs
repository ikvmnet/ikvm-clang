using System;
using System.Diagnostics;
using System.IO;
using System.Linq;

using FluentAssertions;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace IKVM.Clang.Sdk.Tests
{

    /// <summary>
    /// Tests Build, Clean, Rebuild and the removal of outputs that are no longer produced, for projects with one and
    /// with several target identifiers. The sources need no system headers, so only clang and llvm-ar are required.
    /// </summary>
    [TestClass]
    public class CleanTests
    {

        static string root = "";
        static string packageVersion = "";

        public TestContext TestContext { get; set; }

        [ClassInitialize]
        public static void Init(TestContext context)
        {
            packageVersion = File.ReadAllLines("IKVM.Clang.Sdk.Tests.properties").Select(i => i.Split('=', 2)).First(i => i[0] == "PackageVersion")[1];

            root = Path.Combine(Path.GetTempPath(), "IKVM.Clang.Sdk.Tests", nameof(CleanTests), Guid.NewGuid().ToString());
            Directory.CreateDirectory(root);

            File.WriteAllText(Path.Combine(root, "global.json"),
                "{ \"sdk\": { \"version\": \"10.0.0\", \"rollForward\": \"latestFeature\" }, \"msbuild-sdks\": { \"IKVM.Clang.Sdk\": \"" + packageVersion + "\" } }");

            var source = Path.Combine(Path.GetDirectoryName(typeof(CleanTests).Assembly.Location)!, "nuget");
            File.WriteAllText(Path.Combine(root, "nuget.config"), string.Join(Environment.NewLine,
                "<configuration>",
                $"  <config><add key=\"globalPackagesFolder\" value=\"{Path.Combine(root, "packages")}\" /></config>",
                $"  <packageSources><clear /><add key=\"dev\" value=\"{source}\" /></packageSources>",
                "</configuration>"));
        }

        [ClassCleanup]
        public static void Cleanup()
        {
            try
            {
                Directory.Delete(root, true);
            }
            catch (IOException)
            {

            }
        }

        /// <summary>
        /// Creates a static library project with a C and a C++ source.
        /// </summary>
        static string CreateProject(string name, string targets)
        {
            var directory = Path.Combine(root, name);
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, "a.c"), "int one(void) { return 1; }\n");
            File.WriteAllText(Path.Combine(directory, "b.cpp"), "int two() { return 2; }\n");
            File.WriteAllText(Path.Combine(directory, name + ".clangproj"), string.Join(Environment.NewLine,
                "<Project Sdk=\"IKVM.Clang.Sdk\">",
                "  <PropertyGroup>",
                "    <OutputType>Lib</OutputType>",
                $"    <TargetIdentifiers>{targets}</TargetIdentifiers>",
                "  </PropertyGroup>",
                "</Project>"));

            return directory;
        }

        void MSBuild(string directory, params string[] arguments)
        {
            var info = new ProcessStartInfo("dotnet")
            {
                WorkingDirectory = directory,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };

            info.ArgumentList.Add("msbuild");
            info.ArgumentList.Add("-nologo");
            info.ArgumentList.Add("-v:m");
            info.ArgumentList.Add("-restore");
            foreach (var argument in arguments)
                info.ArgumentList.Add(argument);

            using var process = Process.Start(info)!;
            var output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
            process.WaitForExit();
            TestContext.WriteLine(output);
            process.ExitCode.Should().Be(0, output);
        }

        static string[] Outputs(string directory, string target)
        {
            var obj = Path.Combine(directory, "obj", "Debug", target);
            if (Directory.Exists(obj) == false)
                return Array.Empty<string>();

            return Directory.GetFiles(obj).Select(Path.GetFileName).Where(i => i!.EndsWith(".FileListAbsolute.txt") == false).OrderBy(i => i).ToArray()!;
        }

        [TestMethod]
        public void SingleTargetBuildCleanRebuild()
        {
            var project = CreateProject("single", "");
            File.WriteAllText(Path.Combine(project, "single.clangproj"), File.ReadAllText(Path.Combine(project, "single.clangproj"))
                .Replace("<TargetIdentifiers></TargetIdentifiers>", "<TargetIdentifier>x86_64-pc-windows-msvc</TargetIdentifier>"));

            MSBuild(project, "-t:Build");
            Outputs(project, "x86_64-pc-windows-msvc").Should().BeEquivalentTo("a.obj", "b.obj", "single.lib");

            MSBuild(project, "-t:Clean");
            Outputs(project, "x86_64-pc-windows-msvc").Should().BeEmpty();

            MSBuild(project, "-t:Rebuild");
            Outputs(project, "x86_64-pc-windows-msvc").Should().BeEquivalentTo("a.obj", "b.obj", "single.lib");
        }

        [TestMethod]
        public void MultiTargetBuildCleanRebuild()
        {
            var project = CreateProject("multi", "x86_64-pc-windows-msvc;wasm32-unknown-unknown");

            MSBuild(project, "-t:Build");
            Outputs(project, "x86_64-pc-windows-msvc").Should().BeEquivalentTo("a.obj", "b.obj", "multi.lib");
            Outputs(project, "wasm32-unknown-unknown").Should().BeEquivalentTo("a.o", "b.o", "libmulti.a");

            MSBuild(project, "-t:Clean");
            Outputs(project, "x86_64-pc-windows-msvc").Should().BeEmpty();
            Outputs(project, "wasm32-unknown-unknown").Should().BeEmpty();

            var before = DateTime.UtcNow.AddSeconds(-1);
            MSBuild(project, "-t:Rebuild");
            Outputs(project, "x86_64-pc-windows-msvc").Should().BeEquivalentTo("a.obj", "b.obj", "multi.lib");
            Outputs(project, "wasm32-unknown-unknown").Should().BeEquivalentTo("a.o", "b.o", "libmulti.a");
            File.GetLastWriteTimeUtc(Path.Combine(project, "obj", "Debug", "wasm32-unknown-unknown", "a.o")).Should().BeAfter(before);
        }

        [TestMethod]
        public void RemovedSourceObjectIsDeleted()
        {
            var project = CreateProject("orphan", "x86_64-pc-windows-msvc");

            MSBuild(project, "-t:Build");
            Outputs(project, "x86_64-pc-windows-msvc").Should().Contain("b.obj");

            File.Delete(Path.Combine(project, "b.cpp"));
            MSBuild(project, "-t:Build");
            Outputs(project, "x86_64-pc-windows-msvc").Should().BeEquivalentTo("a.obj", "orphan.lib");
        }

    }

}
