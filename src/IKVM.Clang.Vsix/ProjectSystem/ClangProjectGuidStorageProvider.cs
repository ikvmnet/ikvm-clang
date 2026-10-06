using System;
using System.ComponentModel.Composition;
using System.Linq;
using System.Threading.Tasks;

using Microsoft.VisualStudio.ProjectSystem;
using Microsoft.VisualStudio.ProjectSystem.VS;

namespace IKVM.Clang.Vsix.ProjectSystem
{

    /// <summary>
    /// Keeps the project GUID out of the project file. Visual Studio otherwise writes a <c>ProjectGuid</c> property
    /// into every Clang project it opens; the solution already records the GUID, so SDK-style projects do not need it.
    /// A GUID the project file already has is still used, so projects that have one keep it. The .NET project system
    /// does the same.
    /// </summary>
    [Export(typeof(IProjectGuidStorageProvider))]
    [AppliesTo(ClangProjectCapabilities.AppliesTo)]
    [Order(1000)]
    internal sealed class ClangProjectGuidStorageProvider : IProjectGuidStorageProvider
    {

        readonly UnconfiguredProject _project;
        Guid? _guid;

        /// <summary>
        /// Creates the provider for the given project.
        /// </summary>
        [ImportingConstructor]
        public ClangProjectGuidStorageProvider(UnconfiguredProject project)
        {
            _project = project ?? throw new ArgumentNullException(nameof(project));
        }

        /// <inheritdoc />
        public async Task<Guid> GetProjectGuidAsync()
        {
            if (_guid is Guid guid)
                return guid;

            // a GUID written into the project file earlier, if any; otherwise none, and the solution's is used
            var value = await _project.ProjectService.Services.ProjectLockService.ReadLockAsync(async access =>
            {
                var xml = await access.GetProjectXmlAsync(_project.FullPath);
                return xml.Properties.LastOrDefault(i => string.Equals(i.Name, "ProjectGuid", StringComparison.OrdinalIgnoreCase))?.Value;
            });

            return Guid.TryParse(value, out var existing) ? existing : Guid.Empty;
        }

        /// <inheritdoc />
        public Task SetProjectGuidAsync(Guid guid)
        {
            // remembered for this session only; never written to the project file
            _guid = guid;
            return Task.CompletedTask;
        }

    }

}
