using System;
using System.ComponentModel.Composition;
using System.Threading.Tasks;

using IKVM.Clang.Vsix.ProjectSystem;

using Microsoft.VisualStudio;
using Microsoft.VisualStudio.ProjectSystem;

namespace IKVM.Clang.Vsix.Content
{

    /// <summary>
    /// Opens the source files of a Clang project in <see cref="ClangEditorFactory"/>, so they take the Clang content
    /// types. Files outside Clang projects are unaffected.
    /// </summary>
    [Export(typeof(IProjectSpecificEditorProvider))]
    [AppliesTo(ClangProjectCapabilities.AppliesTo)]
    [Order(1000)]
    internal sealed class ClangProjectSpecificEditorProvider : IProjectSpecificEditorProvider
    {

        static readonly Task<IProjectSpecificEditorInfo?> None = Task.FromResult<IProjectSpecificEditorInfo?>(null);
        static readonly Task<IProjectSpecificEditorInfo?> Clang = Task.FromResult<IProjectSpecificEditorInfo?>(new EditorInfo());

        /// <inheritdoc />
        public Task<IProjectSpecificEditorInfo?> GetSpecificEditorAsync(string documentMoniker)
        {
            return ContentTypeNames.TryGetForFile(documentMoniker, out _) ? Clang : None;
        }

        /// <inheritdoc />
        public Task<bool> SetUseGlobalEditorAsync(string documentMoniker, bool useGlobalEditor)
        {
            // the choice is not persisted; the Clang editor stays the project default
            return Task.FromResult(false);
        }

        /// <summary>
        /// Describes <see cref="ClangEditorFactory"/>.
        /// </summary>
        sealed class EditorInfo : IProjectSpecificEditorInfo
        {

            public Guid EditorFactory => ClangEditorFactory.EditorFactoryGuid;

            public bool IsDefaultEditor => true;

            public string DisplayName => "Clang Source Editor";

            public Guid DefaultView => VSConstants.LOGVIEWID.Primary_guid;

        }

    }

}
