using Microsoft.VisualStudio.Shell;

using System;

namespace IKVM.Clang.Vsix.Registration
{

    /// <summary>
    /// Registers a folder of TextMate grammars with Visual Studio's TextMate language service.
    /// The folder must contain a VS Code-compatible <c>package.json</c> grammar manifest.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    internal sealed class ProvideImageManifestAttribute : RegistrationAttribute
    {

        readonly string _relativeManifestPath;

        /// <summary>
        /// Initializes a new instance of <see cref="ProvideTextMateGrammarsAttribute"/>.
        /// </summary>
        /// <param name="relativeManifestPath">
        /// Path to the grammar folder, relative to the extension installation directory.
        /// The folder must contain a <c>package.json</c> manifest.
        /// </param>
        public ProvideImageManifestAttribute(string relativeManifestPath)
        {
            _relativeManifestPath = relativeManifestPath ?? throw new ArgumentNullException(nameof(relativeManifestPath));
        }

        public override void Register(RegistrationContext context)
        {
            using var key = context.CreateKey(@"ImageManifests");
            key.SetValue("$PackageFolder$\\" + _relativeManifestPath, "");
        }

        public override void Unregister(RegistrationContext context)
        {
            context.RemoveKey(@"ImageManifests");
        }

    }

}
