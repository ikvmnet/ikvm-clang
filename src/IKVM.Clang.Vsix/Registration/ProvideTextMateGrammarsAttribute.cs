using System;
using System.IO;

using Microsoft.VisualStudio.Shell;

namespace IKVM.Clang.Vsix.Registration
{

    /// <summary>
    /// Registers a folder of TextMate grammars with Visual Studio's TextMate language service.
    /// The folder must contain a VS Code-compatible <c>package.json</c> grammar manifest.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    internal sealed class ProvideTextMateGrammarsAttribute : RegistrationAttribute
    {

        readonly string _relativeGrammarPath;

        /// <summary>
        /// Initializes a new instance of <see cref="ProvideTextMateGrammarsAttribute"/>.
        /// </summary>
        /// <param name="relativeGrammarPath">
        /// Path to the grammar folder, relative to the extension installation directory.
        /// The folder must contain a <c>package.json</c> manifest.
        /// </param>
        public ProvideTextMateGrammarsAttribute(string relativeGrammarPath)
        {
            _relativeGrammarPath = relativeGrammarPath ?? throw new ArgumentNullException(nameof(relativeGrammarPath));
        }

        public override void Register(RegistrationContext context)
        {
            using var key = context.CreateKey(@"TextMate\Repositories\IKVM.Clang");
            key.SetValue("InstallPath", Path.Combine("$PackageFolder$", _relativeGrammarPath));
        }

        public override void Unregister(RegistrationContext context)
        {
            context.RemoveKey(@"TextMate\Repositories\IKVM.Clang");
        }

    }

}
