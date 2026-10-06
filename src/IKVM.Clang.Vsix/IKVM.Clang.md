# IKVM.Clang for Visual Studio

Visual Studio extension that provides IDE support for building C, C++, Objective-C, and Assembly projects using the Clang compiler.

## Features

**Solution Explorer Integration**
- Project and file management
- File type icons for C, C++, Objective-C, Assembly, and header files
- In-place project file editing

**Build Integration**
- Build, Rebuild, and Clean commands
- Compiler output in the Output Window

**Property Pages**
- Output type and LLVM target triples
- Per-file language and language standard overrides

**Project System**
- Based on the Common Project System (CPS)
- SDK-style `.clangproj` files
- Automatic file discovery
- Supports LLVM target triples for cross-platform builds
- Source files opened from a Clang project use Clang content types; files in other projects are unaffected

**Syntax Highlighting**
- Objective-C / Objective-C++ (`.m`, `.mm`)
- Assembly (`.s`, `.asm`)

## Requirements

- LLVM/Clang installed and on PATH
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
