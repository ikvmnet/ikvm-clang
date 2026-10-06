using System;
using System.IO;

using FluentAssertions;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace IKVM.Clang.Sdk.Tests
{

    /// <summary>
    /// Tests Build, Clean, Rebuild and the removal of outputs that are no longer produced, for projects with one and
    /// with several target identifiers.
    /// </summary>
    [TestClass]
    public class CleanTests
    {

        public TestContext TestContext { get; set; }

        static readonly (string, string)[] Sources = { ("a.c", "int one(void) { return 1; }\n"), ("b.cpp", "int two() { return 2; }\n") };

        void Build(string directory, params string[] arguments)
        {
            var (exitCode, output) = TestProjects.MSBuild(directory, arguments);
            TestContext.WriteLine(output);
            exitCode.Should().Be(0, output);
        }

        [TestMethod]
        public void SingleTargetBuildCleanRebuild()
        {
            var project = TestProjects.Create("single", "<OutputType>Lib</OutputType><TargetIdentifier>x86_64-pc-windows-msvc</TargetIdentifier>", "", Sources);

            Build(project, "-t:Build");
            TestProjects.Outputs(project, "x86_64-pc-windows-msvc").Should().BeEquivalentTo("a.obj", "b.obj", "single.lib");

            Build(project, "-t:Clean");
            TestProjects.Outputs(project, "x86_64-pc-windows-msvc").Should().BeEmpty();

            Build(project, "-t:Rebuild");
            TestProjects.Outputs(project, "x86_64-pc-windows-msvc").Should().BeEquivalentTo("a.obj", "b.obj", "single.lib");
        }

        [TestMethod]
        public void MultiTargetBuildCleanRebuild()
        {
            var project = TestProjects.Create("multi", "<OutputType>Lib</OutputType><TargetIdentifiers>x86_64-pc-windows-msvc;wasm32-unknown-unknown</TargetIdentifiers>", "", Sources);

            Build(project, "-t:Build");
            TestProjects.Outputs(project, "x86_64-pc-windows-msvc").Should().BeEquivalentTo("a.obj", "b.obj", "multi.lib");
            TestProjects.Outputs(project, "wasm32-unknown-unknown").Should().BeEquivalentTo("a.o", "b.o", "libmulti.a");

            Build(project, "-t:Clean");
            TestProjects.Outputs(project, "x86_64-pc-windows-msvc").Should().BeEmpty();
            TestProjects.Outputs(project, "wasm32-unknown-unknown").Should().BeEmpty();

            var before = DateTime.UtcNow.AddSeconds(-1);
            Build(project, "-t:Rebuild");
            TestProjects.Outputs(project, "x86_64-pc-windows-msvc").Should().BeEquivalentTo("a.obj", "b.obj", "multi.lib");
            TestProjects.Outputs(project, "wasm32-unknown-unknown").Should().BeEquivalentTo("a.o", "b.o", "libmulti.a");
            File.GetLastWriteTimeUtc(Path.Combine(project, "obj", "Debug", "wasm32-unknown-unknown", "a.o")).Should().BeAfter(before);
        }

        [TestMethod]
        public void RemovedSourceObjectIsDeleted()
        {
            var project = TestProjects.Create("orphan", "<OutputType>Lib</OutputType><TargetIdentifier>x86_64-pc-windows-msvc</TargetIdentifier>", "", Sources);

            Build(project, "-t:Build");
            TestProjects.Outputs(project, "x86_64-pc-windows-msvc").Should().Contain("b.obj");

            File.Delete(Path.Combine(project, "b.cpp"));
            Build(project, "-t:Build");
            TestProjects.Outputs(project, "x86_64-pc-windows-msvc").Should().BeEquivalentTo("a.obj", "orphan.lib");
        }

    }

}
