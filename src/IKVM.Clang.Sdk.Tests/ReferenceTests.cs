using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;

using FluentAssertions;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace IKVM.Clang.Sdk.Tests;

/// <summary>
/// Tests project references: which target each is built for, relinking when a referenced library changes, and
/// copying the shared libraries an executable needs next to it.
/// </summary>
/// <remarks>
/// The Windows programs here are linked without the C runtime (with <c>-nostdlib</c> and an explicit entry point),
/// so that only LLVM is needed.
/// </remarks>
[TestClass]
public class ReferenceTests
{

    const string Target = "x86_64-pc-windows-msvc";

    public TestContext TestContext { get; set; }

    (int ExitCode, string Output) Run(string directory, params string[] arguments)
    {
        var result = TestProjects.MSBuild(directory, arguments);
        TestContext.WriteLine(result.Output);
        return result;
    }

    string Build(string directory, params string[] arguments)
    {
        var (exitCode, output) = Run(directory, arguments);
        exitCode.Should().Be(0, output);
        return output;
    }

    static void DllTool(string directory, string def, string lib)
    {
        var dlltool = new[] { Environment.GetEnvironmentVariable("PATH") ?? "" }
            .SelectMany(i => i.Split(Path.PathSeparator))
            .Append(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "LLVM", "bin"))
            .Where(i => i.Trim().Length > 0)
            .Select(i => Path.Combine(i.Trim(), OperatingSystem.IsWindows() ? "llvm-dlltool.exe" : "llvm-dlltool"))
            .FirstOrDefault(File.Exists);
        if (dlltool is null)
            Assert.Inconclusive("llvm-dlltool was not found to make an import library.");

        using var process = Process.Start(new ProcessStartInfo(dlltool, $"-m i386:x86-64 -d {def} -l {lib}") { UseShellExecute = false, WorkingDirectory = directory })!;
        process.WaitForExit();
        process.ExitCode.Should().Be(0);
    }

    static string Reference(string name) => $"<ProjectReference Include=\"..\\{name}\\{name}.clangproj\" />";

    [TestMethod]
    public void SingleTargetProjectUsesMatchingTargetOfMultiTargetReference()
    {
        // the library's header reaches the referencing project only if the reference is built for its target
        TestProjects.Create("multilib", "<OutputType>Lib</OutputType><TargetIdentifiers>wasm32-unknown-unknown;x86_64-pc-windows-msvc</TargetIdentifiers>", "",
            ("multilib.h", "int multilib(void);\n"),
            ("multilib.c", "#include \"multilib.h\"\nint multilib(void) { return 1; }\n"));
        var user = TestProjects.Create("singleuser", $"<OutputType>Lib</OutputType><TargetIdentifier>{Target}</TargetIdentifier>", Reference("multilib"),
            ("user.c", "#include \"multilib.h\"\nint user(void) { return multilib(); }\n"));

        Build(user, "-t:Build");
        TestProjects.Outputs(user, Target).Should().Contain("user.obj");
    }

    [TestMethod]
    public void HostProjectCannotChooseBetweenTargetsOfReference()
    {
        TestProjects.Create("multilib2", "<OutputType>Lib</OutputType><TargetIdentifiers>wasm32-unknown-unknown;x86_64-pc-windows-msvc</TargetIdentifiers>", "",
            ("multilib2.c", "int multilib2(void) { return 1; }\n"));
        var user = TestProjects.Create("hostuser", "<OutputType>Lib</OutputType>", Reference("multilib2"),
            ("user.c", "int user(void) { return 1; }\n"));

        var (exitCode, output) = Run(user, "-t:Build");
        exitCode.Should().NotBe(0);
        output.Should().Contain("ICLANG2001");
    }

    [TestMethod]
    public void ExecutableGetsSharedLibrariesAndRelinksWhenTheyChange()
    {
        static string NoRuntime(string entry) => $"<AdditionalLinkOptions>-nostdlib;{entry}</AdditionalLinkOptions>";

        // a shared library, used by the executable directly
        var math = TestProjects.Create("mathdll", $"<OutputType>Dll</OutputType><TargetIdentifier>{Target}</TargetIdentifier>{NoRuntime("-Wl,/noentry")}", "",
            ("math.c", "__declspec(dllexport) int add(int a, int b) { return a + b; }\n"));

        // a shared library reached only through a static library, which must still be copied
        TestProjects.Create("deepdll", $"<OutputType>Dll</OutputType><TargetIdentifier>{Target}</TargetIdentifier>{NoRuntime("-Wl,/noentry")}", "",
            ("deep.c", "__declspec(dllexport) int deep(void) { return 1; }\n"));
        TestProjects.Create("middlelib", $"<OutputType>Lib</OutputType><TargetIdentifier>{Target}</TargetIdentifier>", Reference("deepdll"),
            ("middle.c", "int middle(void) { return 1; }\n"));

        // returning from the entry point would end only its thread, leaving the process to the loader's threads
        // for a while, so it exits through kernel32, whose import library llvm-dlltool makes
        var app = TestProjects.Create("app", $"<OutputType>Exe</OutputType><TargetIdentifier>{Target}</TargetIdentifier>{NoRuntime("-Wl,/entry:main")}<LibraryDirectories>$(MSBuildProjectDirectory)</LibraryDirectories><Dependencies>kernel32</Dependencies>", Reference("mathdll") + Reference("middlelib"),
            ("main.c", "__declspec(dllimport) int add(int a, int b);\n__declspec(dllimport) void __stdcall ExitProcess(unsigned int code);\nint main(void) { ExitProcess(add(2, 3)); return 0; }\n"),
            ("kernel32.def", "LIBRARY kernel32.dll\nEXPORTS\nExitProcess\n"));
        DllTool(app, "kernel32.def", "kernel32.lib");

        Build(app, "-t:Build");

        var bin = Path.Combine(app, "bin", "Debug", Target);
        Directory.GetFiles(bin).Select(Path.GetFileName).Should().Contain(new[] { "app.exe", "mathdll.dll", "deepdll.dll" });

        if (OperatingSystem.IsWindows())
        {
            // it runs, finding the library next to it
            using var process = Process.Start(new ProcessStartInfo(Path.Combine(bin, "app.exe")) { UseShellExecute = false, WorkingDirectory = bin })!;
            if (process.WaitForExit(TimeSpan.FromSeconds(30)) == false)
            {
                process.Kill();
                Assert.Fail("app.exe did not exit.");
            }

            process.ExitCode.Should().Be(5);
        }

        var exe = Path.Combine(app, "obj", "Debug", Target, "app.exe");
        var linked = File.GetLastWriteTimeUtc(exe);

        // nothing changed: no relink
        Thread.Sleep(1100);
        Build(app, "-t:Build");
        File.GetLastWriteTimeUtc(exe).Should().Be(linked);

        // the library changes: relinked against it
        Thread.Sleep(1100);
        File.WriteAllText(Path.Combine(math, "math.c"), "__declspec(dllexport) int add(int a, int b) { return a + b; }\n__declspec(dllexport) int sub(int a, int b) { return a - b; }\n");
        Build(app, "-t:Build");
        var relinked = File.GetLastWriteTimeUtc(exe);
        relinked.Should().BeAfter(linked);

        // a link option changes: relinked
        Thread.Sleep(1100);
        var project = Path.Combine(app, "app.clangproj");
        File.WriteAllText(project, File.ReadAllText(project).Replace("-Wl,/entry:main", "-Wl,/entry:main;-Wl,/stack:4194304"));
        Build(app, "-t:Build");
        File.GetLastWriteTimeUtc(exe).Should().BeAfter(relinked);
    }

}
