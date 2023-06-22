using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;

using Buildalyzer;
using Buildalyzer.Environment;

using FluentAssertions;

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace IKVM.Clang.Sdk.Tests
{

    [TestClass]
    public class ProjectTests
    {

        /// <summary>
        /// Forwards MSBuild events to the test context.
        /// </summary>
        class TargetLogger : Logger
        {

            readonly TestContext context;

            /// <summary>
            /// Initializes a new instance.
            /// </summary>
            /// <param name="context"></param>
            /// <exception cref="ArgumentNullException"></exception>
            public TargetLogger(TestContext context)
            {
                this.context = context ?? throw new ArgumentNullException(nameof(context));
            }

            public override void Initialize(IEventSource eventSource)
            {
                eventSource.AnyEventRaised += (sender, evt) => context.WriteLine($"{evt.Message}");
            }

        }

        public TestContext TestContext { get; set; }

        [TestMethod]
        public void CanBuildTestProject()
        {
            var properties = File.ReadAllLines("IKVM.Clang.Sdk.Tests.properties").Select(i => i.Split('=', 2)).ToDictionary(i => i[0], i => i[1]);

            var nugetPackageRoot = Path.Combine(Path.GetTempPath(), "IKVM.Clang.Sdk.Tests", "nuget", "packages");
            if (Directory.Exists(nugetPackageRoot))
                Directory.Delete(nugetPackageRoot, true);
            Directory.CreateDirectory(nugetPackageRoot);

            new XDocument(
                new XElement("configuration",
                    new XElement("config",
                        new XElement("add",
                            new XAttribute("key", "globalPackagesFolder"),
                            new XAttribute("value", nugetPackageRoot))),
                    new XElement("packageSources",
                        new XElement("clear"),
                        new XElement("add",
                            new XAttribute("key", "nuget.org"),
                            new XAttribute("value", "https://api.nuget.org/v3/index.json")),
                        new XElement("add",
                            new XAttribute("key", "dev"),
                            new XAttribute("value", Path.Combine(Path.GetDirectoryName(typeof(ProjectTests).Assembly.Location), @"nuget"))),
                        new XElement("add",
                            new XAttribute("key", "nuget.org"),
                            new XAttribute("value", "https://api.nuget.org/v3/index.json")))))
                .Save(Path.Combine(@"Project", "nuget.config"));

            var manager = new AnalyzerManager();
            var analyzer = manager.GetProject(Path.Combine(@"Project", "Executable", "Executable.clangproj"));
            analyzer.AddBuildLogger(new TargetLogger(TestContext) { Verbosity = LoggerVerbosity.Detailed });
            analyzer.AddBinaryLogger("msbuild.binlog");

            analyzer.SetGlobalProperty("ImportDirectoryBuildProps", "false");
            analyzer.SetGlobalProperty("ImportDirectoryBuildTargets", "false");
            analyzer.SetGlobalProperty("PackageVersion", properties["PackageVersion"]);
            analyzer.SetGlobalProperty("RestorePackagesPath", nugetPackageRoot + Path.DirectorySeparatorChar);
            analyzer.SetGlobalProperty("CreateHardLinksForAdditionalFilesIfPossible", "true");
            analyzer.SetGlobalProperty("CreateHardLinksForCopyAdditionalFilesIfPossible", "true");
            analyzer.SetGlobalProperty("CreateHardLinksForCopyFilesToOutputDirectoryIfPossible", "true");
            analyzer.SetGlobalProperty("CreateHardLinksForCopyLocalIfPossible", "true");
            analyzer.SetGlobalProperty("CreateHardLinksForPublishFilesIfPossible", "true");
            analyzer.SetGlobalProperty("SkipCopyBuildProduct", "false");
            analyzer.SetGlobalProperty("CopyBuildOutputToOutputDirectory", "true");


            var o = new EnvironmentOptions();

            // first restore and clean
            o.TargetsToBuild.Clear();
            o.TargetsToBuild.Add("Restore");
            o.TargetsToBuild.Add("Clean");
            analyzer.Build(o).OverallSuccess.Should().BeTrue();

            // do a global build
            o.TargetsToBuild.Clear();
            o.TargetsToBuild.Add("Build");
            analyzer.Build(o).OverallSuccess.Should().BeTrue();

            // do individual target builds
            var targets = new[]
            {
                "x86_64-pc-windows-msvc",
                "i686-pc-windows-msvc",
                "aarch64-pc-windows-msvc",
            };

            foreach (var target in targets)
            {
                TestContext.WriteLine("Building with TargetIdentifier {0}.", target);
                var options = new EnvironmentOptions();
                options.DesignTime = false;
                options.Restore = false;
                options.GlobalProperties["TargetIdentifier"] = target;
                options.TargetsToBuild.Clear();
                options.TargetsToBuild.Add("Build");
                analyzer.Build(o).OverallSuccess.Should().Be(true);
            }
        }

    }

}
