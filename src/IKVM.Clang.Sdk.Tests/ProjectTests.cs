﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

using Buildalyzer;
using Buildalyzer.Environment;

using FluentAssertions;

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

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

        public static Dictionary<string, string> Properties { get; set; }

        public static string TestRoot { get; set; }

        public static string TempRoot { get; set; }

        public static string WorkRoot { get; set; }

        public static string NuGetPackageRoot { get; set; }

        [ClassInitialize]
        public static void Init(TestContext context)
        {
            // properties to load into test build
            Properties = File.ReadAllLines("IKVM.Clang.Sdk.Tests.properties").Select(i => i.Split('=', 2)).ToDictionary(i => i[0], i => i[1]);

            // root of the project collection itself
            TestRoot = Path.Combine(Path.GetDirectoryName(typeof(ProjectTests).Assembly.Location), "Project");
            context.WriteLine("TestRoot: {0}", TestRoot);

            // temporary directory
            TempRoot = Path.Combine(Path.GetTempPath(), "IKVM.Clang.Sdk.Tests", Guid.NewGuid().ToString());
            if (Directory.Exists(TempRoot))
                Directory.Delete(TempRoot, true);
            Directory.CreateDirectory(TempRoot);
            context.WriteLine("TempRoot: {0}", TempRoot);

            // work directory
            WorkRoot = Path.Combine(context.TestRunDirectory, "IKVM.Clang.Sdk.Tests", "ProjectTests");
            if (Directory.Exists(WorkRoot))
                Directory.Delete(WorkRoot, true);
            Directory.CreateDirectory(WorkRoot);
            context.WriteLine("WorkRoot: {0}", WorkRoot);

            // other required sub directories
            NuGetPackageRoot = Path.Combine(TempRoot, "nuget", "packages");

            // nuget.config file that defines package sources
            using (var globalJson = new JsonTextWriter(File.CreateText(Path.Combine(TestRoot, "global.json"))) { Formatting = Formatting.Indented, CloseOutput = true })
            {
                new JObject()
                {
                    ["sdk"] = new JObject()
                    {
                        ["version"] = "8.0.0",
                        ["rollForward"] = "latestFeature"
                    },
                    ["msbuild-sdks"] = new JObject()
                    {
                        ["IKVM.Clang.Sdk"] = Properties["PackageVersion"],
                    }
                }.WriteTo(globalJson);
            }

            // nuget.config file that defines package sources
            new XDocument(
                new XElement("configuration",
                    new XElement("config",
                        new XElement("add",
                            new XAttribute("key", "globalPackagesFolder"),
                            new XAttribute("value", NuGetPackageRoot))),
                    new XElement("packageSources",
                        new XElement("clear"),
                        new XElement("add",
                            new XAttribute("key", "dev"),
                            new XAttribute("value", Path.Combine(Path.GetDirectoryName(typeof(ProjectTests).Assembly.Location), @"nuget"))),
                        new XElement("add",
                            new XAttribute("key", "nuget.org"),
                            new XAttribute("value", "https://api.nuget.org/v3/index.json")))))
                .Save(Path.Combine(TestRoot, "nuget.config"));

            var manager = new AnalyzerManager();
            var analyzer = manager.GetProject(Path.Combine(TestRoot, "Executable", "Executable.clangproj"));
            analyzer.AddBuildLogger(new TargetLogger(context));
            analyzer.AddBinaryLogger(Path.Combine(WorkRoot, "msbuild.binlog"));
            analyzer.SetEnvironmentVariable("NUGET_PACKAGES", "");
            analyzer.SetGlobalProperty("ImportDirectoryBuildProps", "false");
            analyzer.SetGlobalProperty("ImportDirectoryBuildTargets", "false");
            analyzer.SetGlobalProperty("PackageVersion", Properties["PackageVersion"]);
            analyzer.SetGlobalProperty("RestorePackagesPath", NuGetPackageRoot + Path.DirectorySeparatorChar);
            analyzer.SetGlobalProperty("CreateHardLinksForAdditionalFilesIfPossible", "true");
            analyzer.SetGlobalProperty("CreateHardLinksForCopyAdditionalFilesIfPossible", "true");
            analyzer.SetGlobalProperty("CreateHardLinksForCopyFilesToOutputDirectoryIfPossible", "true");
            analyzer.SetGlobalProperty("CreateHardLinksForCopyLocalIfPossible", "true");
            analyzer.SetGlobalProperty("CreateHardLinksForPublishFilesIfPossible", "true");
            analyzer.SetGlobalProperty("Configuration", "Debug");

            var options = new EnvironmentOptions();
            options.WorkingDirectory = TestRoot;
            options.DesignTime = false;
            options.Restore = false;
            options.TargetsToBuild.Clear();
            options.TargetsToBuild.Add("Restore");
            options.Arguments.Add("/v:diag");

            var result = analyzer.Build(options);
            context.AddResultFile(Path.Combine(WorkRoot, "msbuild.binlog"));
            result.OverallSuccess.Should().Be(true);
        }

        [DataTestMethod]
        [DataRow(EnvironmentPreference.Core, "win-x64", "{0}.dll", "{0}.lib", "{0}.exe", "{0}.pdb")]
        [DataRow(EnvironmentPreference.Core, "win-x86", "{0}.dll", "{0}.lib", "{0}.exe", "{0}.pdb")]
        [DataRow(EnvironmentPreference.Core, "win-arm64", "{0}.dll", "{0}.lib", "{0}.exe", "{0}.pdb")]
        //[DataRow(EnvironmentPreference.Core, "linux-x64", "lib{0}.so", "lib{0}.a", "{0}", "_")]
        //[DataRow(EnvironmentPreference.Core, "linux-arm", "lib{0}.so", "lib{0}.a", "{0}", "_")]
        //[DataRow(EnvironmentPreference.Core, "linux-arm64", "lib{0}.so", "lib{0}.a", "{0}", "_")]
        [DataRow(EnvironmentPreference.Core, "browser-wasm", "_", "lib{0}.a", "{0}.wasm", "_")]
        public void CanBuildTestProject(EnvironmentPreference env, string tid, string dllName, string libName, string exeName, string symName)
        {
            TestContext.WriteLine("TestRoot: {0}", TestRoot);
            TestContext.WriteLine("TempRoot: {0}", TempRoot);
            TestContext.WriteLine("WorkRoot: {0}", WorkRoot);

            var manager = new AnalyzerManager();
            var analyzer = manager.GetProject(Path.Combine(TestRoot, "Executable", "Executable.clangproj"));
            analyzer.AddBuildLogger(new TargetLogger(TestContext));
            analyzer.AddBinaryLogger(Path.Combine(WorkRoot, $"{tid}-msbuild.binlog"));
            analyzer.SetEnvironmentVariable("NUGET_PACKAGES", "");
            analyzer.SetGlobalProperty("ImportDirectoryBuildProps", "false");
            analyzer.SetGlobalProperty("ImportDirectoryBuildTargets", "false");
            analyzer.SetGlobalProperty("PackageVersion", Properties["PackageVersion"]);
            analyzer.SetGlobalProperty("RestorePackagesPath", NuGetPackageRoot + Path.DirectorySeparatorChar);
            analyzer.SetGlobalProperty("CreateHardLinksForAdditionalFilesIfPossible", "true");
            analyzer.SetGlobalProperty("CreateHardLinksForCopyAdditionalFilesIfPossible", "true");
            analyzer.SetGlobalProperty("CreateHardLinksForCopyFilesToOutputDirectoryIfPossible", "true");
            analyzer.SetGlobalProperty("CreateHardLinksForCopyLocalIfPossible", "true");
            analyzer.SetGlobalProperty("CreateHardLinksForPublishFilesIfPossible", "true");
            analyzer.SetGlobalProperty("SkipCopyBuildProduct", "false");
            analyzer.SetGlobalProperty("CopyBuildOutputToOutputDirectory", "true");
            analyzer.SetGlobalProperty("Configuration", "Debug");

            var options = new EnvironmentOptions();
            options.WorkingDirectory = TestRoot;
            options.Preference = env;
            options.DesignTime = false;
            options.Restore = false;
            options.GlobalProperties["TargetIdentifier"] = tid;
            options.TargetsToBuild.Clear();
            options.TargetsToBuild.Add("Clean");
            options.TargetsToBuild.Add("Build");
            options.Arguments.Add("/v:diag");

            var result = analyzer.Build(options);
            TestContext.AddResultFile(Path.Combine(WorkRoot, $"{tid}-msbuild.binlog"));
            result.OverallSuccess.Should().BeTrue();

            var binDir = Path.Combine(TestRoot, "Executable", "obj", "Debug", tid);
            if (exeName != "_")
                File.Exists(Path.Combine(binDir, string.Format(exeName, "Executable"))).Should().BeTrue();
            if (symName != "_")
                File.Exists(Path.Combine(binDir, string.Format(symName, "Executable"))).Should().BeTrue();

            var dllDir1 = Path.Combine(TestRoot, "SharedLibrary1", "obj", "Debug", tid);
            if (dllName != "_")
                File.Exists(Path.Combine(dllDir1, string.Format(dllName, "SharedLibrary1"))).Should().BeTrue();
            if (symName != "_")
                File.Exists(Path.Combine(dllDir1, string.Format(symName, "SharedLibrary1"))).Should().BeTrue();

            var dllDir2 = Path.Combine(TestRoot, "SharedLibrary2", "obj", "Debug", tid);
            if (dllName != "_")
                File.Exists(Path.Combine(dllDir2, string.Format(dllName, "SharedLibrary2"))).Should().BeTrue();
            if (symName != "_")
                File.Exists(Path.Combine(dllDir2, string.Format(symName, "SharedLibrary2"))).Should().BeTrue();

            var libDir1 = Path.Combine(TestRoot, "StaticLibrary1", "obj", "Debug", tid);
            if (libName != "_")
                File.Exists(Path.Combine(libDir1, string.Format(libName, "StaticLibrary1"))).Should().BeTrue();
        }

    }

}
