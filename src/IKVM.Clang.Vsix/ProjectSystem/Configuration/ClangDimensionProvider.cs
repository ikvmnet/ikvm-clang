using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Threading.Tasks;

using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;
using Microsoft.VisualStudio.ProjectSystem;

namespace IKVM.Clang.Vsix.ProjectSystem.Configuration
{

    /// <summary>
    /// Base of the providers of the configuration dimensions of a Clang project. Each dimension takes its values from a
    /// semicolon separated MSBuild property, such as <c>Configurations</c>, in the way the .NET project system takes
    /// <c>TargetFramework</c> values from <c>TargetFrameworks</c>.
    /// </summary>
    /// <remarks>
    /// Clang projects declare <see cref="ProjectCapabilities.ProjectConfigurationsDeclaredDimensions"/>, which makes the
    /// project system responsible for every dimension, so Configuration and Platform need providers as much as
    /// TargetIdentifier does.
    /// </remarks>
    internal abstract class ClangDimensionProvider : IProjectConfigurationDimensionsProvider5
    {

        /// <summary>
        /// Creates the provider.
        /// </summary>
        protected ClangDimensionProvider(string dimensionName, string propertyName, string? defaultValues)
        {
            DimensionName = dimensionName;
            PropertyName = propertyName;
            DefaultValues = defaultValues;
        }

        /// <summary>
        /// Name of the dimension, which is also the global property each configuration sets.
        /// </summary>
        public string DimensionName { get; }

        /// <summary>
        /// Name of the property listing the values of the dimension.
        /// </summary>
        public string PropertyName { get; }

        /// <summary>
        /// Values to use when the property is not set, or <see langword="null"/> if the dimension is then absent.
        /// </summary>
        public string? DefaultValues { get; }

        /// <summary>
        /// Splits a property value into the distinct dimension values it lists.
        /// </summary>
        IReadOnlyList<string> Parse(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                value = DefaultValues;

            if (string.IsNullOrWhiteSpace(value))
                return Array.Empty<string>();

            return value!.Split(';')
                .Select(i => i.Trim())
                .Where(i => i.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        /// <summary>
        /// Reads the values from the project file itself, for when no evaluation is available. Only an unconditioned
        /// literal value is understood; anything else falls back to the default values.
        /// </summary>
        async Task<IReadOnlyList<string>> GetValuesFromXmlAsync(UnconfiguredProject project)
        {
            var value = await project.ProjectService.Services.ProjectLockService.ReadLockAsync(async access =>
            {
                var xml = await access.GetProjectXmlAsync(project.FullPath);
                return GetLiteralProperty(xml, PropertyName);
            });

            return Parse(value);
        }

        /// <summary>
        /// Gets the value of the last unconditioned definition of the property in the project file, if it contains no
        /// MSBuild expressions.
        /// </summary>
        static string? GetLiteralProperty(ProjectRootElement xml, string name)
        {
            var property = xml.Properties
                .Where(i => string.Equals(i.Name, name, StringComparison.OrdinalIgnoreCase))
                .Where(i => string.IsNullOrEmpty(i.Condition) && string.IsNullOrEmpty(i.Parent.Condition))
                .LastOrDefault();

            if (property is null || property.Value.Contains("$(") || property.Value.Contains("@(") || property.Value.Contains("%("))
                return null;

            return property.Value;
        }

        IEnumerable<KeyValuePair<string, IEnumerable<string>>> ToDimensions(IReadOnlyList<string> values)
        {
            Clangd.ClangdTrace.Write($"dimension {DimensionName}: {string.Join(";", values)}");

            if (values.Count == 0)
                return Enumerable.Empty<KeyValuePair<string, IEnumerable<string>>>();

            return new[] { new KeyValuePair<string, IEnumerable<string>>(DimensionName, values) };
        }

        IEnumerable<KeyValuePair<string, string>> ToDefaults(IReadOnlyList<string> values)
        {
            if (values.Count == 0)
                return Enumerable.Empty<KeyValuePair<string, string>>();

            return new[] { new KeyValuePair<string, string>(DimensionName, values[0]) };
        }

        /// <inheritdoc />
        public async Task<IEnumerable<KeyValuePair<string, IEnumerable<string>>>> GetProjectConfigurationDimensionsAsync(UnconfiguredProject project)
        {
            return ToDimensions(await GetValuesFromXmlAsync(project));
        }

        /// <inheritdoc />
        public Task<IEnumerable<KeyValuePair<string, IEnumerable<string>>>> GetProjectConfigurationDimensionsAsync(Project project)
        {
            return Task.FromResult(ToDimensions(Parse(project.GetPropertyValue(PropertyName))));
        }

        /// <inheritdoc />
        public async Task<IEnumerable<KeyValuePair<string, string>>> GetDefaultValuesForDimensionsAsync(UnconfiguredProject project)
        {
            return ToDefaults(await GetValuesFromXmlAsync(project));
        }

        /// <inheritdoc />
        public async Task<IEnumerable<KeyValuePair<string, string>>> GetBestGuessDefaultValuesForDimensionsAsync(UnconfiguredProject project)
        {
            return ToDefaults(await GetValuesFromXmlAsync(project));
        }

        /// <inheritdoc />
        public Task<IEnumerable<KeyValuePair<string, string>>> GetBestGuessDefaultValuesForDimensionsAsync(UnconfiguredProject project, string solutionConfiguration)
        {
            return GetBestGuessDefaultValuesForDimensionsAsync(project);
        }

        /// <inheritdoc />
        public Task OnDimensionValueChangedAsync(ProjectConfigurationDimensionValueChangedEventArgs args)
        {
            return Task.CompletedTask;
        }

    }

    /// <summary>
    /// The Configuration dimension, from the <c>Configurations</c> property.
    /// </summary>
    [Export(typeof(IProjectConfigurationDimensionsProvider))]
    [AppliesTo(ClangProjectCapabilities.AppliesTo)]
    [ConfigurationDimensionDescription(ConfigurationGeneral.Configuration, isVariantDimension: false)]
    [Order(Order)]
    internal sealed class ClangConfigurationDimensionProvider : ClangDimensionProvider
    {

        public const int Order = 1000;

        [ImportingConstructor]
        public ClangConfigurationDimensionProvider() :
            base(ConfigurationGeneral.Configuration, "Configurations", "Debug;Release")
        {

        }

    }

    /// <summary>
    /// The Platform dimension, from the <c>Platforms</c> property. The platform does not select what is built, which
    /// is the target identifier, so the default is the single AnyCPU platform.
    /// </summary>
    [Export(typeof(IProjectConfigurationDimensionsProvider))]
    [AppliesTo(ClangProjectCapabilities.AppliesTo)]
    [ConfigurationDimensionDescription(ConfigurationGeneral.Platform, isVariantDimension: false)]
    [Order(Order)]
    internal sealed class ClangPlatformDimensionProvider : ClangDimensionProvider
    {

        public const int Order = 990;

        [ImportingConstructor]
        public ClangPlatformDimensionProvider() :
            base(ConfigurationGeneral.Platform, "Platforms", "AnyCPU")
        {

        }

    }

    /// <summary>
    /// The TargetIdentifier dimension, from the <c>TargetIdentifiers</c> property. It is a variant dimension: every
    /// target identifier of the active configuration and platform is loaded at once, each as its own configured
    /// project with its own design-time build, as .NET does for <c>TargetFrameworks</c>. A project that sets only
    /// <c>TargetIdentifier</c> has no such dimension.
    /// </summary>
    [Export(typeof(IProjectConfigurationDimensionsProvider))]
    [AppliesTo(ClangProjectCapabilities.AppliesTo)]
    [ConfigurationDimensionDescription(DimensionNameValue, isVariantDimension: true)]
    [Order(Order)]
    internal sealed class ClangTargetIdentifierDimensionProvider : ClangDimensionProvider
    {

        public const string DimensionNameValue = "TargetIdentifier";

        public const int Order = 980;

        [ImportingConstructor]
        public ClangTargetIdentifierDimensionProvider() :
            base(DimensionNameValue, "TargetIdentifiers", null)
        {

        }

    }

    /// <summary>
    /// Names of the standard dimensions.
    /// </summary>
    internal static class ConfigurationGeneral
    {

        public const string Configuration = nameof(Configuration);

        public const string Platform = nameof(Platform);

    }

}
