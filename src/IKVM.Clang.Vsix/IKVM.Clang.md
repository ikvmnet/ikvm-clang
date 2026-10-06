# IKVM.Clang for Visual Studio

Visual Studio extension that provides IDE support for building C, C++, Objective-C, and Assembly projects using the Clang compiler.

## Features

**Solution Explorer Integration**
- Project and file management
- File type icons for C, C++, Objective-C, Assembly, and header files
- In-place project file editing

**Build Integration**
- Build, Rebuild, and Clean commands
- Compiler errors and warnings in the Error List, with their file and line
- Build output in the Output Window

**Property Pages**
- Output type and LLVM target triples
- Per-file language and language standard overrides

**Language Support (clangd)**
- Completion, hover, go to definition, find references, rename and diagnostics from clangd
- clangd gets each file's real compile command from the project: include directories, definitions, target
- Projects with several `TargetIdentifiers` show each target as a context in the editor's project list; switching
  it re-parses the file for that target, like switching target frameworks in a .NET project
- Headers use the command of the nearest source file of their project

**Project System**
- Based on the Common Project System (CPS)
- SDK-style `.clangproj` files
- Automatic file discovery
- Supports LLVM target triples for cross-platform builds
- Source files opened from a Clang project use Clang content types; files in other projects are unaffected

**Syntax Highlighting**
- C, C++, Objective-C and Objective-C++ through Visual Studio's built-in grammars, including C++ module and
  header extensions (`.cppm`, `.ixx`, `.hxx`, `.ipp`)
- Assembly (`.s`, `.asm`)

## Requirements

- LLVM/Clang installed and on PATH
- clangd for language support: found on PATH, in the default LLVM installation, in Visual Studio's C++ Clang tools,
  or at the path in the `IKVM_CLANG_CLANGD_PATH` environment variable
- Visual Studio 2022 version 17.14 or later

## Example Project

```xml
<Project Sdk="IKVM.Clang.Sdk">
  <PropertyGroup>
    <TargetIdentifiers>x86_64-pc-windows-msvc</TargetIdentifiers>
    <OutputType>Exe</OutputType>
  </PropertyGroup>
</Project>
```

Open the `.clangproj` file in Visual Studio to get started. Add source files through Solution Explorer and build using the Build menu.

## More Information

- [GitHub Repository](https://github.com/ikvmnet/ikvm-clang)
- [Report Issues](https://github.com/ikvmnet/ikvm-clang/issues)
- [NuGet Package: IKVM.Clang.Sdk](https://www.nuget.org/packages/IKVM.Clang.Sdk)
