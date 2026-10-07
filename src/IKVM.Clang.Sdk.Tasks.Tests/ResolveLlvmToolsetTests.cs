using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using FluentAssertions;

using IKVM.Clang.Sdk.Tasks;

using Microsoft.Build.Framework;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using Newtonsoft.Json.Linq;

namespace IKVM.Clang.Sdk.Tasks.Tests;

/// <summary>
/// Tests how <see cref="ResolveLlvmToolset"/> finds the LLVM tools, using directories of empty stand-in files so no
/// LLVM installation is needed, and without looking in the platform's usual installation directories.
/// </summary>
[TestClass]
public class ResolveLlvmToolsetTests
{

    sealed class BuildEngine : IBuildEngine
    {

        public List<string> Messages { get; } = new();

        public bool ContinueOnError => false;
        public int LineNumberOfTaskNode => 0;
        public int ColumnNumberOfTaskNode => 0;
        public string ProjectFileOfTaskNode => "";
        public bool BuildProjectFile(string projectFileName, string[] targetNames, IDictionary globalProperties, IDictionary targetOutputs) => throw new NotSupportedException();
        public void LogCustomEvent(CustomBuildEventArgs e) => Messages.Add(e.Message ?? "");
        public void LogErrorEvent(BuildErrorEventArgs e) => Messages.Add("error " + e.Message);
        public void LogMessageEvent(BuildMessageEventArgs e) => Messages.Add(e.Message ?? "");
        public void LogWarningEvent(BuildWarningEventArgs e) => Messages.Add("warning " + e.Message);

    }

    static readonly string Exe = OperatingSystem.IsWindows() ? ".exe" : "";

    string root = "";

    [TestInitialize]
    public void Setup()
    {
        root = Path.Combine(Path.GetTempPath(), "IKVM.Clang.Sdk.Tests", nameof(ResolveLlvmToolsetTests), Guid.NewGuid().ToString());
        Directory.CreateDirectory(root);
    }

    [TestCleanup]
    public void Cleanup()
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
    /// Creates a directory holding stand-ins for the named tools.
    /// </summary>
    string Install(string name, params string[] tools)
    {
        var directory = Path.Combine(root, name);
        Directory.CreateDirectory(directory);
        foreach (var tool in tools)
            File.WriteAllBytes(Path.Combine(directory, tool + Exe), Array.Empty<byte>());

        return directory;
    }

    static string Tool(string directory, string tool) => Path.Combine(directory, tool + Exe);

    static ResolveLlvmToolset Create(params string[] searchPath)
    {
        return new ResolveLlvmToolset()
        {
            BuildEngine = new BuildEngine(),
            SearchPath = string.Join(Path.PathSeparator, searchPath),
            SearchPlatformLocations = false,
            QueryResourceDirectory = false,
        };
    }

    static Dictionary<string, string> Problems(ResolveLlvmToolset task)
    {
        return task.Problems.ToDictionary(i => i.ItemSpec, i => i.GetMetadata("Code"));
    }

    [TestMethod]
    public void FindsWholeSuiteOnSearchPath()
    {
        var llvm = Install("llvm", "clang", "clang++", "llvm-ar", "clangd");
        var task = Create(llvm);

        task.Execute().Should().BeTrue();
        task.ClangPath.Should().Be(Tool(llvm, "clang"));
        task.ClangCxxPath.Should().Be(Tool(llvm, "clang++"));
        task.LlvmArPath.Should().Be(Tool(llvm, "llvm-ar"));
        task.ClangdPath.Should().Be(Tool(llvm, "clangd"));
        task.LlvmToolsDirectory.Should().Be(llvm);
        task.Problems.Should().BeEmpty();
        JArray.Parse(task.ProblemsJson).Should().BeEmpty();
    }

    [TestMethod]
    public void PrefersInstallationWithWholeSuiteOverClangAlone()
    {
        // like Apple's /usr/bin, which has clang but none of the other LLVM tools, ahead of a full LLVM
        var clangOnly = Install("clang-only", "clang", "clang++");
        var llvm = Install("llvm", "clang", "clang++", "llvm-ar", "clangd");
        var task = Create(clangOnly, llvm);

        task.Execute().Should().BeTrue();
        task.ClangPath.Should().Be(Tool(llvm, "clang"));
        task.ClangCxxPath.Should().Be(Tool(llvm, "clang++"));
        task.LlvmArPath.Should().Be(Tool(llvm, "llvm-ar"));
    }

    [TestMethod]
    public void TakesOtherToolsFromClangInstallationFirst()
    {
        var early = Install("early", "clangd", "llvm-ar");
        var llvm = Install("llvm", "clang", "clang++", "llvm-ar", "clangd");
        var task = Create(early, llvm);

        task.Execute().Should().BeTrue();
        task.ClangdPath.Should().Be(Tool(llvm, "clangd"));
        task.LlvmArPath.Should().Be(Tool(llvm, "llvm-ar"));
    }

    [TestMethod]
    public void ClangOverrideDecidesInstallationOfOtherTools()
    {
        var onPath = Install("on-path", "clang", "clang++", "llvm-ar", "clangd");
        var chosen = Install("chosen", "clang", "clang++", "llvm-ar", "clangd");
        var task = Create(onPath);
        task.ClangPath = Tool(chosen, "clang");

        task.Execute().Should().BeTrue();
        task.ClangPath.Should().Be(Tool(chosen, "clang"));
        task.ClangCxxPath.Should().Be(Tool(chosen, "clang++"));
        task.LlvmArPath.Should().Be(Tool(chosen, "llvm-ar"));
        task.ClangdPath.Should().Be(Tool(chosen, "clangd"));
    }

    [TestMethod]
    public void PerToolOverrideWins()
    {
        var llvm = Install("llvm", "clang", "clang++", "llvm-ar", "clangd");
        var other = Install("other", "clangd");
        var task = Create(llvm);
        task.ClangdPath = Tool(other, "clangd");

        task.Execute().Should().BeTrue();
        task.ClangdPath.Should().Be(Tool(other, "clangd"));
        task.ClangPath.Should().Be(Tool(llvm, "clang"));
    }

    [TestMethod]
    public void MissingOverrideIsReportedNotReplaced()
    {
        var llvm = Install("llvm", "clang", "clang++", "llvm-ar", "clangd");
        var task = Create(llvm);
        task.LlvmArPath = Path.Combine(root, "nope", "llvm-ar" + Exe);

        task.Execute().Should().BeTrue();
        task.LlvmArPath.Should().BeEmpty();
        Problems(task).Should().Equal(new Dictionary<string, string>() { ["LlvmAr"] = "ICLANG1002" });
        task.Problems[0].GetMetadata("Message").Should().Contain("LlvmArPath").And.Contain("does not exist");
    }

    [TestMethod]
    public void LlvmToolsPathIsTheOnlyPlaceLookedAt()
    {
        var tools = Install("tools", "clang", "clangd");
        var onPath = Install("on-path", "clang", "clang++", "llvm-ar", "clangd");
        var task = Create(onPath);
        task.LlvmToolsPath = tools;

        task.Execute().Should().BeTrue();
        task.ClangPath.Should().Be(Tool(tools, "clang"));
        task.ClangdPath.Should().Be(Tool(tools, "clangd"));
        task.ClangCxxPath.Should().BeEmpty();
        task.LlvmArPath.Should().BeEmpty();
        Problems(task).Should().Equal(new Dictionary<string, string>() { ["ClangCxx"] = "ICLANG1001", ["LlvmAr"] = "ICLANG1001" });
        task.Problems[0].GetMetadata("Message").Should().Contain("LlvmToolsPath");
    }

    [TestMethod]
    public void MissingLlvmToolsPathIsReportedForEveryTool()
    {
        var onPath = Install("on-path", "clang", "clang++", "llvm-ar", "clangd");
        var task = Create(onPath);
        task.LlvmToolsPath = Path.Combine(root, "nope");

        task.Execute().Should().BeTrue();
        task.ClangPath.Should().BeEmpty();
        Problems(task).Values.Should().AllBe("ICLANG1003");
        Problems(task).Keys.Should().BeEquivalentTo("Clang", "ClangCxx", "LlvmAr", "Clangd");
    }

    [TestMethod]
    public void NothingFoundSaysWhereItLooked()
    {
        var empty = Install("empty");
        var task = Create(empty);

        task.Execute().Should().BeTrue();
        task.ClangPath.Should().BeEmpty();
        Problems(task)["Clang"].Should().Be("ICLANG1001");
        var message = task.Problems.First(i => i.ItemSpec == "Clang").GetMetadata("Message");
        message.Should().Contain(empty).And.Contain("LlvmToolsPath").And.Contain("ClangPath");

        var json = JArray.Parse(task.ProblemsJson);
        json.Select(i => (string)i["tool"]!).Should().Contain("Clang");
        ((string)json.First(i => (string)i["tool"]! == "Clang")["message"]!).Should().Be(message);
    }

    [TestMethod]
    public void LinkerPathMustExist()
    {
        var llvm = Install("llvm", "clang", "clang++", "llvm-ar", "clangd", "ld.lld");

        var good = Create(llvm);
        good.LinkerPath = Tool(llvm, "ld.lld");
        good.Execute().Should().BeTrue();
        good.LinkerPath.Should().Be(Tool(llvm, "ld.lld"));
        good.Problems.Should().BeEmpty();

        var bad = Create(llvm);
        bad.LinkerPath = Path.Combine(root, "nope", "ld.lld" + Exe);
        bad.Execute().Should().BeTrue();
        bad.LinkerPath.Should().BeEmpty();
        Problems(bad).Should().Equal(new Dictionary<string, string>() { ["Linker"] = "ICLANG1002" });
    }

}
