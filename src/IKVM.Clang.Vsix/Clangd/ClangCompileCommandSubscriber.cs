using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

using IKVM.Clang.Vsix.ProjectSystem;
using IKVM.Clang.Vsix.ProjectSystem.Configuration;

using Microsoft.VisualStudio.ProjectSystem;

using Newtonsoft.Json.Linq;

namespace IKVM.Clang.Vsix.Clangd
{

    /// <summary>
    /// Keeps <see cref="ClangCompileDatabase"/> up to date with the compile commands of one Clang project. It follows
    /// the active configuration group, which holds one configured project per target identifier, and reads the
    /// results of the <c>GetClangCompileCommands</c> target from each one's design-time builds.
    /// </summary>
    [Export(ExportContractNames.Scopes.UnconfiguredProject, typeof(IProjectDynamicLoadComponent))]
    [AppliesTo(ClangProjectCapabilities.AppliesTo)]
    internal sealed class ClangCompileCommandSubscriber : IProjectDynamicLoadComponent
    {

        const string RuleName = "ClangCompileCommand";

        readonly UnconfiguredProject _project;
        readonly IActiveConfigurationGroupService _activeConfigurationGroupService;
        readonly ClangCompileDatabase _database;
        readonly object _sync = new();
        readonly Dictionary<ConfiguredProject, IDisposable> _links = new();
        IDisposable? _groupLink;

        /// <summary>
        /// Creates the subscriber for the given project.
        /// </summary>
        [ImportingConstructor]
        public ClangCompileCommandSubscriber(UnconfiguredProject project, IActiveConfigurationGroupService activeConfigurationGroupService, ClangCompileDatabase database)
        {
            _project = project ?? throw new ArgumentNullException(nameof(project));
            _activeConfigurationGroupService = activeConfigurationGroupService ?? throw new ArgumentNullException(nameof(activeConfigurationGroupService));
            _database = database ?? throw new ArgumentNullException(nameof(database));
        }

        /// <inheritdoc />
        public Task LoadAsync()
        {
            var target = new ActionBlock<IProjectVersionedValue<IConfigurationGroup<ConfiguredProject>>>(i => OnActiveGroupChanged(i.Value));
            _groupLink = _activeConfigurationGroupService.ActiveConfiguredProjectGroupSource.SourceBlock.LinkTo(target, new DataflowLinkOptions() { PropagateCompletion = true });
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public Task UnloadAsync()
        {
            lock (_sync)
            {
                _groupLink?.Dispose();
                _groupLink = null;

                foreach (var link in _links.Values)
                    link.Dispose();

                _links.Clear();
            }

            _database.RemoveContexts(_project.FullPath);
            return Task.CompletedTask;
        }

        /// <summary>
        /// Subscribes to the configured projects that joined the active group and drops those that left it.
        /// </summary>
        void OnActiveGroupChanged(IConfigurationGroup<ConfiguredProject> group)
        {
            var configured = group.ToList();
            var contexts = configured.Select((i, n) => GetContext(i, n)).ToList();

            lock (_sync)
            {
                if (_groupLink is null)
                    return;

                foreach (var removed in _links.Keys.Except(configured).ToList())
                {
                    _links[removed].Dispose();
                    _links.Remove(removed);
                }

                for (int i = 0; i < configured.Count; i++)
                {
                    if (_links.ContainsKey(configured[i]))
                        continue;

                    var subscription = configured[i].Services.ProjectSubscription;
                    if (subscription is null)
                        continue;

                    var context = contexts[i];
                    var target = new ActionBlock<IProjectVersionedValue<IProjectSubscriptionUpdate>>(u => OnCompileCommandsChanged(context, u.Value));
                    _links[configured[i]] = subscription.ProjectBuildRuleSource.SourceBlock.LinkTo(
                        target,
                        new DataflowLinkOptions() { PropagateCompletion = true },
                        initialDataAsNew: true,
                        suppressVersionOnlyUpdates: true,
                        RuleName);
                }
            }

            _database.RemoveContexts(_project.FullPath, contexts);
        }

        /// <summary>
        /// Gets the context of a configured project: the project with the configuration's target identifier.
        /// </summary>
        ClangCompileContext GetContext(ConfiguredProject configured, int order)
        {
            configured.ProjectConfiguration.Dimensions.TryGetValue(ClangTargetIdentifierDimensionProvider.DimensionNameValue, out var targetIdentifier);
            return new ClangCompileContext(_project.FullPath, targetIdentifier ?? "", order);
        }

        /// <summary>
        /// Stores the compile commands from a design-time build.
        /// </summary>
        void OnCompileCommandsChanged(ClangCompileContext context, IProjectSubscriptionUpdate update)
        {
            if (update.CurrentState.TryGetValue(RuleName, out var snapshot) == false)
                return;

            var entries = new List<ClangCompileCommandEntry>();

            foreach (var item in snapshot.Items)
            {
                if (item.Value.TryGetValue("CommandLine", out var commandLine) == false || string.IsNullOrEmpty(commandLine))
                    continue;

                item.Value.TryGetValue("WorkingDirectory", out var workingDirectory);

                IReadOnlyList<string> arguments;
                try
                {
                    arguments = JArray.Parse(commandLine).Select(i => (string)i!).ToList();
                }
                catch (Exception)
                {
                    continue;
                }

                entries.Add(new ClangCompileCommandEntry(item.Key, context, workingDirectory ?? "", arguments));
            }

            _database.SetContext(context, entries);
        }

    }

}
