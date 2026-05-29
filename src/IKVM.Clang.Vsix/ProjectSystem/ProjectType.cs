using System;

namespace IKVM.Clang.Vsix.ProjectSystem
{

    /// <summary>
    /// Represents the managed project types in Visual Studio.
    /// </summary>
    internal static class ProjectType
    {

        /// <summary>
        /// A <see cref="string"/> representing the Clang project type based on the Common Project System (CPS).
        /// </summary>
        public const string Clang = "C16F8DA6-AF2B-48DF-A3C0-8B93BDB1BB12";

        /// <summary>
        /// A <see cref="Guid"/> representing the Clang project type based on the Common Project System (CPS).
        /// </summary>
        public static readonly Guid ClangGuid = new(Clang);

    }

}
