using System;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;

using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Threading;

using Task = System.Threading.Tasks.Task;

namespace IKVM.Clang.Vsix.Clangd
{

    /// <summary>
    /// Shows the LLVM tools that Clang projects could not resolve as warnings in the Error List, with the SDK's
    /// guidance on correcting them. Opening one opens the project file, where the tool locations are set. The warnings
    /// are replaced whenever a design-time build reports the toolset again, so they go away once corrected.
    /// </summary>
    [Export]
    [PartCreationPolicy(CreationPolicy.Shared)]
    internal sealed class ClangToolsetErrorList
    {

        static readonly Guid ProviderGuid = new("8C2E5B8A-3F21-4D6B-9A3C-6E1F0B7D2A54");

        readonly IServiceProvider _serviceProvider;
        readonly JoinableTaskFactory _joinableTaskFactory;
        readonly ClangCompileDatabase _database;
        ErrorListProvider? _provider;

        /// <summary>
        /// Creates the reporter.
        /// </summary>
        [ImportingConstructor]
        public ClangToolsetErrorList(SVsServiceProvider serviceProvider, JoinableTaskContext joinableTaskContext, ClangCompileDatabase database)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _joinableTaskFactory = joinableTaskContext?.Factory ?? throw new ArgumentNullException(nameof(joinableTaskContext));
            _database = database ?? throw new ArgumentNullException(nameof(database));
            _database.ToolsetsChanged += (_, _) => _joinableTaskFactory.RunAsync(RefreshAsync).FileAndForget("IKVM.Clang/ToolsetErrorList");

            // toolsets may have been reported before this was created
            _joinableTaskFactory.RunAsync(RefreshAsync).FileAndForget("IKVM.Clang/ToolsetErrorList");
        }

        /// <summary>
        /// Replaces the warnings with those of the toolsets now reported.
        /// </summary>
        async Task RefreshAsync()
        {
            await _joinableTaskFactory.SwitchToMainThreadAsync();

            _provider ??= new ErrorListProvider(_serviceProvider) { ProviderName = "IKVM.Clang", ProviderGuid = ProviderGuid };
            _provider.SuspendRefresh();

            try
            {
                _provider.Tasks.Clear();

                foreach (var project in _database.GetToolsets().GroupBy(i => i.Context.ProjectPath, StringComparer.OrdinalIgnoreCase))
                {
                    var name = Path.GetFileNameWithoutExtension(project.Key);

                    if (project.All(i => i.IsReported == false))
                    {
                        Add(project.Key, $"{name}: this version of IKVM.Clang.Sdk does not report the LLVM tools it uses, so Visual Studio cannot start clangd for it. Update IKVM.Clang.Sdk to a newer version for code completion, navigation and diagnostics.");
                        continue;
                    }

                    // the same problem usually affects every target identifier; report it once
                    var problems = project
                        .SelectMany(i => i.Problems.Select(p => (Toolset: i, Problem: p)))
                        .GroupBy(i => (i.Problem.Code, i.Problem.Message));

                    foreach (var problem in problems)
                    {
                        var targets = problem.Select(i => i.Toolset.Context.TargetIdentifier).Where(i => i.Length > 0).Distinct().ToList();
                        var scope = targets.Count > 0 && targets.Count < project.Count() ? $" ({string.Join(", ", targets)})" : "";
                        Add(project.Key, $"{problem.Key.Code} {name}{scope}: {problem.Key.Message}");
                    }
                }
            }
            finally
            {
                _provider.ResumeRefresh();
            }
        }

        void Add(string projectPath, string text)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var task = new ErrorTask()
            {
                ErrorCategory = TaskErrorCategory.Warning,
                Category = TaskCategory.BuildCompile,
                Text = text,
                Document = projectPath,
            };

            task.Navigate += (_, _) =>
            {
                ThreadHelper.ThrowIfNotOnUIThread();
                VsShellUtilities.OpenDocument(_serviceProvider, projectPath);
            };

            _provider!.Tasks.Add(task);
        }

    }

}
