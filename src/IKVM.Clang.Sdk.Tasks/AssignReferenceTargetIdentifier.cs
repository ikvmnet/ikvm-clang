namespace IKVM.Clang.Sdk.Tasks
{

    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;

    using Microsoft.Build.Framework;
    using Microsoft.Build.Utilities;

    /// <summary>
    /// Decides which target identifier each project reference is built for. References are built for the referencing
    /// project's target identifier, whether or not they list it, so a library can be built for any target a project
    /// using it asks for. That is passed explicitly, since a TargetIdentifier set in the referencing project file,
    /// rather than given as a global property, would otherwise not reach the reference. A project that builds for the
    /// host, with no target identifier, cannot pick one of a reference's several, and that is an error.
    /// </summary>
    public class AssignReferenceTargetIdentifier : Task
    {

        /// <summary>
        /// The project references, with configuration already assigned.
        /// </summary>
        [Required]
        public ITaskItem[] References { get; set; } = Array.Empty<ITaskItem>();

        /// <summary>
        /// What each referenced project builds, as returned by its <c>GetTargetIdentifiers</c> target: items named by the
        /// project file, with <c>TargetIdentifiers</c> (the list, for a project that builds several) and
        /// <c>TargetIdentifier</c> (for one that builds a single target) metadata.
        /// </summary>
        public ITaskItem[] Reports { get; set; } = Array.Empty<ITaskItem>();

        /// <summary>
        /// The referencing project's target identifier; empty when it builds for the host.
        /// </summary>
        public string? TargetIdentifier { get; set; }

        /// <summary>
        /// The references, each with <c>SetTargetIdentifier</c> set.
        /// </summary>
        [Output]
        public ITaskItem[] AssignedReferences { get; set; } = Array.Empty<ITaskItem>();

        /// <inheritdoc />
        public override bool Execute()
        {
            var reports = new Dictionary<string, ITaskItem>(StringComparer.OrdinalIgnoreCase);
            foreach (var report in Reports)
                reports[Path.GetFullPath(report.ItemSpec)] = report;

            var current = (TargetIdentifier ?? "").Trim();
            var assigned = new List<ITaskItem>();

            foreach (var reference in References)
            {
                var result = new TaskItem(reference);
                var path = reference.GetMetadata("FullPath");

                if (reports.TryGetValue(path, out var report) == false)
                {
                    // not a Clang project, or one that does not report: leave it as it was
                    assigned.Add(result);
                    continue;
                }

                var list = Split(report.GetMetadata("TargetIdentifiers"));

                if (current.Length > 0)
                {
                    result.SetMetadata("SetTargetIdentifier", "TargetIdentifier=" + current);
                }
                else if (list.Count > 0)
                {
                    Log.LogError(null, "ICLANG2001", null, BuildEngine.ProjectFileOfTaskNode, 0, 0, 0, 0, "'{0}' builds several target identifiers ({1}), so the project referencing it must name the one it builds, with TargetIdentifier or TargetIdentifiers.", Path.GetFileName(path), string.Join(";", list));
                    continue;
                }
                else
                {
                    // both build for a single target of their own
                    result.SetMetadata("SetTargetIdentifier", "");
                }

                assigned.Add(result);
            }

            AssignedReferences = assigned.ToArray();
            return !Log.HasLoggedErrors;
        }

        static IReadOnlyList<string> Split(string value)
        {
            return value.Split(';').Select(i => i.Trim()).Where(i => i.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }

    }

}
