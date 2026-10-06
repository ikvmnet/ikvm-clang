using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;

using FluentAssertions;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace IKVM.Clang.Sdk.Tests
{

    /// <summary>
    /// Tests that builds redo exactly what changed, and the outputs they produce.
    /// </summary>
    [TestClass]
    public class BuildTests
    {

        const string Target = "x86_64-pc-windows-msvc";

        public TestContext TestContext { get; set; }

        string Build(string directory, params string[] arguments)
        {
            var (exitCode, output) = TestProjects.MSBuild(directory, arguments);
            TestContext.WriteLine(output);
            exitCode.Should().Be(0, output);
            return output;
        }

        /// <summary>
        /// Waits long enough for a file written next to have a later time than one written before.
        /// </summary>
        static void Tick() => Thread.Sleep(1100);

        static string Obj(string name) => Path.Combine("obj", "Debug", Target, name);

        [TestMethod]
        public void RecompilesWhatAnEditedHeaderAffects()
        {
            var project = TestProjects.Create("header-edit", $"<OutputType>Lib</OutputType><TargetIdentifier>{Target}</TargetIdentifier>", "",
                ("v.h", "#define VALUE 1\n"),
                ("a.c", "#include \"v.h\"\nint a(void) { return VALUE; }\n"),
                ("b.c", "int b(void) { return 2; }\n"));

            Build(project, "-t:Build");
            var built = TestProjects.Times(project, Obj("a.obj"), Obj("b.obj"));

            Tick();
            Build(project, "-t:Build");
            TestProjects.Times(project, Obj("a.obj"), Obj("b.obj")).Should().Equal(built, "nothing changed");

            Tick();
            File.WriteAllText(Path.Combine(project, "v.h"), "#define VALUE 2\n");
            Build(project, "-t:Build");
            var after = TestProjects.Times(project, Obj("a.obj"), Obj("b.obj"));
            after[Obj("a.obj")].Should().BeAfter(built[Obj("a.obj")], "a.c includes the edited header");
            after[Obj("b.obj")].Should().Be(built[Obj("b.obj")], "b.c does not include it");
        }

        [TestMethod]
        public void RecompilesWhenOptionsChange()
        {
            var project = TestProjects.Create("options-change", $"<OutputType>Lib</OutputType><TargetIdentifier>{Target}</TargetIdentifier>", "",
                ("a.c", "int a(void) { return 1; }\n"));

            Build(project, "-t:Build");
            var built = TestProjects.Times(project, Obj("a.obj"));

            Tick();
            Build(project, "-t:Build", "-p:PreprocessorDefinitions=EXTRA");
            TestProjects.Times(project, Obj("a.obj"))[Obj("a.obj")].Should().BeAfter(built[Obj("a.obj")], "a definition was added");

            var defined = TestProjects.Times(project, Obj("a.obj"));
            Tick();
            Build(project, "-t:Build", "-p:PreprocessorDefinitions=EXTRA");
            TestProjects.Times(project, Obj("a.obj")).Should().Equal(defined, "the options are the same as last time");
        }

        [TestMethod]
        public void ArchiveIsRecreatedWithoutRemovedSources()
        {
            var llvmAr = new[] { Environment.GetEnvironmentVariable("PATH") ?? "" }
                .SelectMany(i => i.Split(Path.PathSeparator))
                .Append(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "LLVM", "bin"))
                .Select(i => Path.Combine(i.Trim(), OperatingSystem.IsWindows() ? "llvm-ar.exe" : "llvm-ar"))
                .FirstOrDefault(File.Exists);
            if (llvmAr is null)
                Assert.Inconclusive("llvm-ar was not found to list the archive.");

            var project = TestProjects.Create("archive", $"<OutputType>Lib</OutputType><TargetIdentifier>{Target}</TargetIdentifier>", "",
                ("a.c", "int a(void) { return 1; }\n"),
                ("b.c", "int b(void) { return 2; }\n"));

            Build(project, "-t:Build");
            Members(llvmAr, Path.Combine(project, Obj("archive.lib"))).Should().BeEquivalentTo("a.obj", "b.obj");

            File.Delete(Path.Combine(project, "b.c"));
            Build(project, "-t:Build");
            Members(llvmAr, Path.Combine(project, Obj("archive.lib"))).Should().BeEquivalentTo("a.obj");
        }

        static string[] Members(string llvmAr, string archive)
        {
            using var process = Process.Start(new ProcessStartInfo(llvmAr, $"t \"{archive}\"") { RedirectStandardOutput = true, UseShellExecute = false })!;
            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            return output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(Path.GetFileName).ToArray()!;
        }

        [TestMethod]
        public void BuildsForTheHostWithoutATargetIdentifier()
        {
            var project = TestProjects.Create("host", "<OutputType>Lib</OutputType>", "",
                ("a.c", "int a(void) { return 1; }\n"));

            Build(project, "-t:Build");

            var obj = Path.Combine(project, "obj", "Debug");
            var expected = OperatingSystem.IsWindows() ? new[] { "a.obj", "host.lib" } : new[] { "a.o", "libhost.a" };
            Directory.GetFiles(obj).Select(Path.GetFileName).Where(i => i!.EndsWith(".cmd") == false && i.EndsWith(".d") == false && i.EndsWith(".txt") == false && i.EndsWith(".cache") == false)
                .Should().BeEquivalentTo(expected);
        }

        [TestMethod]
        public void ReleaseOptimizesAndPositionIndependenceIsLeftToClang()
        {
            var project = TestProjects.Create("defaults", $"<OutputType>Lib</OutputType><TargetIdentifier>{Target}</TargetIdentifier>", "",
                ("a.c", "int a(void) { return 1; }\n"));

            string Command(string configuration) => File.ReadAllText(Path.Combine(project, "obj", configuration, Target, "a.obj.cmd"));

            Build(project, "-t:Build", "-p:Configuration=Debug");
            Build(project, "-t:Build", "-p:Configuration=Release");

            Command("Debug").Should().NotContain("-O").And.NotContain("NDEBUG").And.Contain("-g");
            Command("Release").Should().Contain("-O2").And.Contain("-DNDEBUG").And.NotContain("-g");

            // clang knows which targets want position independent code
            Command("Debug").Should().NotContain("-fPIC").And.NotContain("-fno-pic");

            // and each can be changed
            Build(project, "-t:Build", "-p:Configuration=Release", "-p:Optimization=s", "-p:Assertions=true");
            Command("Release").Should().Contain("-Os").And.NotContain("-O2").And.NotContain("NDEBUG");

            // Windows does not take -fPIC, so this is for an ELF target
            const string Elf = "x86_64-unknown-linux-gnu";
            Build(project, "-t:Build", $"-p:TargetIdentifier={Elf}", "-p:PositionIndependentCode=true");
            File.ReadAllText(Path.Combine(project, "obj", "Debug", Elf, "a.o.cmd")).Should().Contain("-fPIC");
            Build(project, "-t:Build", $"-p:TargetIdentifier={Elf}", "-p:PositionIndependentCode=false");
            File.ReadAllText(Path.Combine(project, "obj", "Debug", Elf, "a.o.cmd")).Should().Contain("-fno-pic");
        }

        [TestMethod]
        public void NamesOutputsByWhatClangBuilds()
        {
            // UEFI builds COFF objects, as Windows does, though its triple says nothing of Windows
            var project = TestProjects.Create("formats", "<OutputType>Lib</OutputType><TargetIdentifiers>x86_64-unknown-uefi;x86_64-linux-gnu</TargetIdentifiers>", "",
                ("a.c", "int a(void) { return 1; }\n"));

            Build(project, "-t:Build");
            TestProjects.Outputs(project, "x86_64-unknown-uefi").Should().BeEquivalentTo("a.obj", "formats.lib");
            TestProjects.Outputs(project, "x86_64-linux-gnu").Should().BeEquivalentTo("a.o", "libformats.a");
        }

        [TestMethod]
        public void TargetClangCannotBuildForFailsTheBuild()
        {
            var project = TestProjects.Create("nonsense", "<OutputType>Lib</OutputType><TargetIdentifier>nonsense-unknown-nowhere</TargetIdentifier>", "",
                ("a.c", "int a(void) { return 1; }\n"));

            var (exitCode, output) = TestProjects.MSBuild(project, "-t:Build");
            TestContext.WriteLine(output);
            exitCode.Should().NotBe(0);
            output.Should().Contain("ICLANG3001").And.Contain("nonsense-unknown-nowhere");

            // but it can still be cleaned
            TestProjects.MSBuild(project, "-t:Clean").ExitCode.Should().Be(0);
        }

        [TestMethod]
        public void CopiesOnlyHeadersMarkedForTheIncludeDirectory()
        {
            var project = TestProjects.Create("headers", $"<OutputType>Lib</OutputType><TargetIdentifier>{Target}</TargetIdentifier>",
                "<Header Update=\"private.h\" CopyToIncludeDirectory=\"false\" />",
                ("public.h", "int a(void);\n"),
                ("private.h", "int hidden(void);\n"),
                ("a.c", "#include \"public.h\"\nint a(void) { return 1; }\n"));

            Build(project, "-t:Build");

            Directory.GetFiles(Path.Combine(project, Obj("headers"))).Select(Path.GetFileName).Should().BeEquivalentTo("public.h");
        }

    }

}
