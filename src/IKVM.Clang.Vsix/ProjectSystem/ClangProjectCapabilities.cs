namespace IKVM.Clang.Vsix.ProjectSystem
{

    /// <summary>
    /// Project capabilities that identify Clang projects and that the Clang project type declares up front.
    /// </summary>
    static class ClangProjectCapabilities
    {

        /// <summary>
        /// Capability unique to Clang projects. Declared by the project type registration, so it is present from the
        /// start of project load, before MSBuild evaluation contributes any capabilities.
        /// </summary>
        public const string ClangCapability = "Clang";

        public const string AppliesTo = ClangCapability;

        /// <summary>
        /// Capabilities fixed for every project of the Clang project type. These are consulted before evaluation, so
        /// they cannot be left to the <c>ProjectCapability</c> items contributed by IKVM.Clang.Sdk.
        /// </summary>
        public const string Default =
            ClangCapability + "; " +
            HandlesOwnReload + "; " +
            OpenProjectFile + "; " +
            PreserveFormatting + "; " +
            ProjectConfigurationsDeclaredDimensions + "; " +
            UseProjectEvaluationCache;

        const string OpenProjectFile = nameof(OpenProjectFile);
        const string HandlesOwnReload = Microsoft.VisualStudio.ProjectSystem.ProjectCapabilities.HandlesOwnReload;
        const string PreserveFormatting = nameof(PreserveFormatting);
        // the dimensions, Configuration and Platform included, come from the providers in ProjectSystem\Configuration
        const string ProjectConfigurationsDeclaredDimensions = Microsoft.VisualStudio.ProjectSystem.ProjectCapabilities.ProjectConfigurationsDeclaredDimensions;
        const string UseProjectEvaluationCache = Microsoft.VisualStudio.ProjectSystem.ProjectCapabilities.UseProjectEvaluationCache;

    }

}
