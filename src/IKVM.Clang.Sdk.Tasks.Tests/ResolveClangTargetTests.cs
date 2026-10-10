using System;
using System.Collections;
using System.IO;
using System.Linq;

using FluentAssertions;

using IKVM.Clang.Sdk.Tasks;

using Microsoft.Build.Framework;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace IKVM.Clang.Sdk.Tasks.Tests;

/// <summary>
/// Tests what <see cref="ResolveClangTarget"/> makes of clang's answers, and, when clang is installed, what clang
/// answers for various triples.
/// </summary>
[TestClass]
public class ResolveClangTargetTests
{

    sealed class BuildEngine : IBuildEngine
    {

        public bool ContinueOnError => false;
        public int LineNumberOfTaskNode => 0;
        public int ColumnNumberOfTaskNode => 0;
        public string ProjectFileOfTaskNode => "";
        public bool BuildProjectFile(string projectFileName, string[] targetNames, IDictionary globalProperties, IDictionary targetOutputs) => throw new NotSupportedException();
        public void LogCustomEvent(CustomBuildEventArgs e) { }
        public void LogErrorEvent(BuildErrorEventArgs e) { }
        public void LogMessageEvent(BuildMessageEventArgs e) { }
        public void LogWarningEvent(BuildWarningEventArgs e) { }

    }

    static readonly Lazy<string?> clang = new(() => new[] { Environment.GetEnvironmentVariable("PATH") ?? "" }
        .SelectMany(i => i.Split(Path.PathSeparator))
        .Append(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "LLVM", "bin"))
        .Where(i => i.Trim().Length > 0)
        .Select(i => Path.Combine(i.Trim(), OperatingSystem.IsWindows() ? "clang.exe" : "clang"))
        .FirstOrDefault(File.Exists));

    [DataTestMethod]
    [DataRow(new byte[] { 0x7F, (byte)'E', (byte)'L', (byte)'F' }, "elf")]
    [DataRow(new byte[] { 0x00, (byte)'a', (byte)'s', (byte)'m' }, "wasm")]
    [DataRow(new byte[] { 0xCF, 0xFA, 0xED, 0xFE }, "macho")]
    [DataRow(new byte[] { 0xFE, 0xED, 0xFA, 0xCE }, "macho")]
    [DataRow(new byte[] { 0x64, 0x86, 0x02, 0x00 }, "coff")]
    [DataRow(new byte[] { 0x4C, 0x01, 0x02, 0x00 }, "coff")]
    [DataRow(new byte[] { 0x64, 0xAA, 0x02, 0x00 }, "coff")]
    [DataRow(new byte[] { 0x00, 0x00, 0xFF, 0xFF }, "coff")]
    [DataRow(new byte[] { 0x01, 0xF7, 0x00, 0x00 }, "xcoff")]
    [DataRow(new byte[] { 0x12, 0x34, 0x56, 0x78 }, "")]
    public void TellsObjectFormatFromHeader(byte[] header, string format)
    {
        ResolveClangTarget.GetObjectFormat(header, header.Length).Should().Be(format);
    }

    [TestMethod]
    public void ParsesPredefinedMacroNames()
    {
        ResolveClangTarget.ParseMacros("#define _MSC_VER 1933\r\n#define __pic__ 2\n#define __has_x(a) 1\nnot a define\n")
            .Should().BeEquivalentTo("_MSC_VER", "__pic__", "__has_x");
    }

    ResolveClangTarget Resolve(string triple)
    {
        if (clang.Value is null)
            Assert.Inconclusive("clang was not found.");

        var task = new ResolveClangTarget() { BuildEngine = new BuildEngine(), ClangPath = clang.Value, TargetTriple = triple };
        task.Execute().Should().BeTrue();
        return task;
    }

    [DataTestMethod]
    [DataRow("x86_64-pc-windows-msvc", "x86_64-pc-windows-msvc", "coff", true)]
    [DataRow("x86_64-w64-windows-gnu", "x86_64-w64-windows-gnu", "coff", false)]
    [DataRow("x86_64-unknown-uefi", "x86_64-unknown-uefi", "coff", false)]
    [DataRow("x86_64-linux-gnu", "x86_64-unknown-linux-gnu", "elf", false)]
    [DataRow("thumbv7em-none-eabi", "thumbv7em-unknown-none-eabi", "elf", false)]
    [DataRow("arm64-apple-tvos", "arm64-apple-tvos", "macho", false)]
    [DataRow("wasm32-unknown-unknown", "wasm32-unknown-unknown", "wasm", false)]
    public void AsksClangAboutTarget(string triple, string normalized, string format, bool msvc)
    {
        var task = Resolve(triple);
        task.Problem.Should().BeEmpty();
        task.ClangTargetTriple.Should().Be(normalized);
        task.ClangObjectFormat.Should().Be(format);
        task.ClangTargetIsMsvc.Should().Be(msvc);
    }

    [TestMethod]
    public void UsesAnswerKeptForSameClangAndTriple()
    {
        if (clang.Value is null)
            Assert.Inconclusive("clang was not found.");

        // an answer clang would never give shows the file was used rather than clang
        var info = new FileInfo(clang.Value);
        var cache = Path.Combine(Path.GetTempPath(), "IKVM.Clang.Sdk.Tests", Guid.NewGuid() + ".cache");
        Directory.CreateDirectory(Path.GetDirectoryName(cache)!);
        File.WriteAllText(cache, $"{clang.Value}|{info.LastWriteTimeUtc.Ticks}|{info.Length}|kept-triple\nkept-unknown-triple\nkept\ntrue\n");

        try
        {
            var task = new ResolveClangTarget() { BuildEngine = new BuildEngine(), ClangPath = clang.Value, TargetTriple = "kept-triple", CacheFile = cache };
            task.Execute().Should().BeTrue();
            task.ClangTargetTriple.Should().Be("kept-unknown-triple");
            task.ClangObjectFormat.Should().Be("kept");
            task.ClangTargetIsMsvc.Should().BeTrue();

            // a different triple is asked about, and kept instead
            task = new ResolveClangTarget() { BuildEngine = new BuildEngine(), ClangPath = clang.Value, TargetTriple = "riscv64-unknown-linux-gnu", CacheFile = cache };
            task.Execute().Should().BeTrue();
            task.ClangObjectFormat.Should().Be("elf");
            File.ReadAllLines(cache)[0].Should().EndWith("|riscv64-unknown-linux-gnu");
        }
        finally
        {
            File.Delete(cache);
        }
    }

    [TestMethod]
    public void ReportsTargetClangCannotBuildFor()
    {
        var task = Resolve("nonsense-unknown-nowhere");
        task.Problem.Should().Contain("nonsense-unknown-nowhere");
        task.ClangObjectFormat.Should().BeEmpty();
    }

    [TestMethod]
    public void AsksAboutDefaultTargetWithoutTriple()
    {
        var task = Resolve("");
        task.Problem.Should().BeEmpty();
        task.ClangTargetTriple.Should().NotBeEmpty();
        task.ClangObjectFormat.Should().Be(OperatingSystem.IsWindows() ? "coff" : OperatingSystem.IsMacOS() ? "macho" : "elf");
    }

}
