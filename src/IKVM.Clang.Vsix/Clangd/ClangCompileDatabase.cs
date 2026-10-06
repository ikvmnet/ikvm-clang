using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;

namespace IKVM.Clang.Vsix.Clangd
{

    /// <summary>
    /// The compile command of one source file in one context: a Clang project built for one target identifier.
    /// </summary>
    internal sealed class ClangCompileCommandEntry
    {

        /// <summary>
        /// Creates the entry.
        /// </summary>
        public ClangCompileCommandEntry(string file, ClangCompileContext context, string workingDirectory, IReadOnlyList<string> arguments)
        {
            File = file ?? throw new ArgumentNullException(nameof(file));
            Context = context ?? throw new ArgumentNullException(nameof(context));
            WorkingDirectory = workingDirectory ?? throw new ArgumentNullException(nameof(workingDirectory));
            Arguments = arguments ?? throw new ArgumentNullException(nameof(arguments));
        }

        /// <summary>
        /// Full path of the source file.
        /// </summary>
        public string File { get; }

        /// <summary>
        /// The project and target identifier the command belongs to.
        /// </summary>
        public ClangCompileContext Context { get; }

        /// <summary>
        /// Directory the compiler runs in.
        /// </summary>
        public string WorkingDirectory { get; }

        /// <summary>
        /// The compiler followed by its arguments.
        /// </summary>
        public IReadOnlyList<string> Arguments { get; }

    }

    /// <summary>
    /// A Clang project built for one target identifier: what the user picks between in the editor's project context
    /// list.
    /// </summary>
    internal sealed class ClangCompileContext : IEquatable<ClangCompileContext>
    {

        /// <summary>
        /// Creates the context.
        /// </summary>
        public ClangCompileContext(string projectPath, string targetIdentifier, int order)
        {
            ProjectPath = projectPath ?? throw new ArgumentNullException(nameof(projectPath));
            TargetIdentifier = targetIdentifier ?? "";
            Order = order;
        }

        /// <summary>
        /// Full path of the project file.
        /// </summary>
        public string ProjectPath { get; }

        /// <summary>
        /// The target identifier, or an empty string for a project that builds a single target.
        /// </summary>
        public string TargetIdentifier { get; }

        /// <summary>
        /// Position of the target identifier in the project's list; the first is the default context.
        /// </summary>
        public int Order { get; }

        /// <summary>
        /// Identifies the context in the language server protocol.
        /// </summary>
        public string Id => TargetIdentifier.Length == 0 ? ProjectPath : ProjectPath + "|" + TargetIdentifier;

        /// <summary>
        /// Text shown for the context in the editor.
        /// </summary>
        public string Label
        {
            get
            {
                var name = Path.GetFileNameWithoutExtension(ProjectPath);
                return TargetIdentifier.Length == 0 ? name : $"{name} ({TargetIdentifier})";
            }
        }

        /// <inheritdoc />
        public bool Equals(ClangCompileContext? other)
        {
            return other is not null && StringComparer.OrdinalIgnoreCase.Equals(Id, other.Id);
        }

        /// <inheritdoc />
        public override bool Equals(object? obj) => Equals(obj as ClangCompileContext);

        /// <inheritdoc />
        public override int GetHashCode() => StringComparer.OrdinalIgnoreCase.GetHashCode(Id);

    }

    /// <summary>
    /// Arguments of <see cref="ClangCompileDatabase.Changed"/>.
    /// </summary>
    internal sealed class ClangCompileDatabaseChangedEventArgs : EventArgs
    {

        /// <summary>
        /// Creates the arguments.
        /// </summary>
        public ClangCompileDatabaseChangedEventArgs(IReadOnlyCollection<string> files)
        {
            Files = files;
        }

        /// <summary>
        /// Full paths of the files whose commands were added, changed or removed.
        /// </summary>
        public IReadOnlyCollection<string> Files { get; }

    }

    /// <summary>
    /// The compile commands of every source file of every loaded Clang project, in every context. Filled from the
    /// design-time builds of the projects and read by the clangd language client.
    /// </summary>
    [Export]
    [PartCreationPolicy(CreationPolicy.Shared)]
    internal sealed class ClangCompileDatabase
    {

        readonly object _sync = new();
        readonly Dictionary<ClangCompileContext, IReadOnlyList<ClangCompileCommandEntry>> _byContext = new();
        readonly Dictionary<ClangCompileContext, ClangToolset> _toolsets = new();
        Dictionary<string, IReadOnlyList<ClangCompileCommandEntry>> _byFile = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Raised, on an arbitrary thread, after commands change.
        /// </summary>
        public event EventHandler<ClangCompileDatabaseChangedEventArgs>? Changed;

        /// <summary>
        /// Raised, on an arbitrary thread, after the toolset of a context is reported, changes or is removed.
        /// </summary>
        public event EventHandler? ToolsetsChanged;

        /// <summary>
        /// Records the LLVM toolset the SDK resolved for a context.
        /// </summary>
        public void SetToolset(ClangToolset toolset)
        {
            lock (_sync)
            {
                _toolsets.Remove(toolset.Context);
                _toolsets[toolset.Context] = toolset;
            }

            ToolsetsChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Gets the toolsets of every context, ordered by project and then default context first.
        /// </summary>
        public IReadOnlyList<ClangToolset> GetToolsets()
        {
            lock (_sync)
                return _toolsets.Values
                    .OrderBy(i => i.Context.ProjectPath, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(i => i.Context.Order)
                    .ToList();
        }

        /// <summary>
        /// Replaces the commands of one context.
        /// </summary>
        public void SetContext(ClangCompileContext context, IReadOnlyList<ClangCompileCommandEntry> entries)
        {
            IReadOnlyCollection<string> changed;

            lock (_sync)
            {
                _byContext.TryGetValue(context, out var previous);
                _byContext.Remove(context);
                _byContext[context] = entries;
                changed = Diff(previous, entries);
                if (changed.Count > 0)
                    Reindex();
            }

            if (changed.Count > 0)
                Changed?.Invoke(this, new ClangCompileDatabaseChangedEventArgs(changed));
        }

        /// <summary>
        /// Removes the commands of the contexts of the given project that are not in <paramref name="keep"/>.
        /// </summary>
        public void RemoveContexts(string projectPath, IEnumerable<ClangCompileContext>? keep = null)
        {
            var retain = new HashSet<ClangCompileContext>(keep ?? Enumerable.Empty<ClangCompileContext>());
            var changed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var toolsetsChanged = false;

            lock (_sync)
            {
                foreach (var context in _toolsets.Keys.ToList())
                {
                    if (StringComparer.OrdinalIgnoreCase.Equals(context.ProjectPath, projectPath) && retain.Contains(context) == false)
                    {
                        _toolsets.Remove(context);
                        toolsetsChanged = true;
                    }
                }

                foreach (var context in _byContext.Keys.ToList())
                {
                    if (StringComparer.OrdinalIgnoreCase.Equals(context.ProjectPath, projectPath) && retain.Contains(context) == false)
                    {
                        foreach (var entry in _byContext[context])
                            changed.Add(entry.File);

                        _byContext.Remove(context);
                    }
                }

                if (changed.Count > 0)
                    Reindex();
            }

            if (changed.Count > 0)
                Changed?.Invoke(this, new ClangCompileDatabaseChangedEventArgs(changed));

            if (toolsetsChanged)
                ToolsetsChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Gets the commands of a file in every context, default context first.
        /// </summary>
        public IReadOnlyList<ClangCompileCommandEntry> GetEntries(string file)
        {
            lock (_sync)
                return _byFile.TryGetValue(file, out var entries) ? entries : Array.Empty<ClangCompileCommandEntry>();
        }

        /// <summary>
        /// Gets the command of every file in its default context.
        /// </summary>
        public IReadOnlyList<ClangCompileCommandEntry> GetDefaultEntries()
        {
            lock (_sync)
                return _byFile.Values.Select(i => i[0]).ToList();
        }

        /// <summary>
        /// Gets every context of the project that contains the given directory most closely, default first. Used for
        /// files with no command of their own, such as headers.
        /// </summary>
        public IReadOnlyList<ClangCompileCommandEntry> GetNearestEntries(string file)
        {
            lock (_sync)
            {
                var directory = Path.GetDirectoryName(file) ?? "";

                return _byContext
                    .Where(i => i.Value.Count > 0)
                    .Select(i => (Context: i.Key, Entry: Closest(i.Value, directory)))
                    .OrderByDescending(i => CommonPrefixLength(Path.GetDirectoryName(i.Entry.File) ?? "", directory))
                    .ThenBy(i => i.Context.ProjectPath, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(i => i.Context.Order)
                    .Select(i => i.Entry)
                    .ToList();
            }
        }

        static ClangCompileCommandEntry Closest(IReadOnlyList<ClangCompileCommandEntry> entries, string directory)
        {
            return entries.OrderByDescending(i => CommonPrefixLength(Path.GetDirectoryName(i.File) ?? "", directory)).First();
        }

        static int CommonPrefixLength(string a, string b)
        {
            var n = Math.Min(a.Length, b.Length);
            var i = 0;
            while (i < n && char.ToUpperInvariant(a[i]) == char.ToUpperInvariant(b[i]))
                i++;

            return i;
        }

        static IReadOnlyCollection<string> Diff(IReadOnlyList<ClangCompileCommandEntry>? previous, IReadOnlyList<ClangCompileCommandEntry> current)
        {
            var before = (previous ?? Array.Empty<ClangCompileCommandEntry>()).ToDictionary(i => i.File, StringComparer.OrdinalIgnoreCase);
            var changed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var entry in current)
            {
                if (before.TryGetValue(entry.File, out var old) == false ||
                    old.WorkingDirectory != entry.WorkingDirectory ||
                    old.Arguments.SequenceEqual(entry.Arguments) == false)
                    changed.Add(entry.File);

                before.Remove(entry.File);
            }

            foreach (var file in before.Keys)
                changed.Add(file);

            return changed;
        }

        void Reindex()
        {
            _byFile = _byContext.Values
                .SelectMany(i => i)
                .GroupBy(i => i.File, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    i => i.Key,
                    i => (IReadOnlyList<ClangCompileCommandEntry>)i
                        .OrderBy(j => j.Context.ProjectPath, StringComparer.OrdinalIgnoreCase)
                        .ThenBy(j => j.Context.Order)
                        .ToList(),
                    StringComparer.OrdinalIgnoreCase);
        }

    }

}
