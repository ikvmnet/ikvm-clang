# IKVM.Clang for Visual Studio

Visual Studio extension that provides IDE support for building C, C++, Objective-C, and Assembly projects using the Clang compiler.

## Features

**Solution Explorer Integration**
- Project and file management
- File type icons for C, C++, Objective-C, Assembly, and header files
- In-place project file editing

**Build Integration**
- Build, Rebuild, and Clean commands
- Compiler errors and warnings in the Error List
- Click-to-navigate from errors to source
- Build output in the Output Window

**Property Pages**
- Configure compiler settings
- Set linker options
- Manage build properties

**Project System**
- Based on the Common Project System (CPS)
- SDK-style `.clangproj` files
- Automatic file discovery
- Supports LLVM target triples for cross-platform builds

**Syntax Highlighting**
- C (`.c`)
- C++ (`.cpp`, `.cc`, `.cxx`, `.c++`, `.cppm`, `.ixx`)
- Objective-C / Objective-C++ (`.m`, `.mm`)
- Assembly (`.s`, `.asm`)
- Headers (`.h`, `.hpp`, `.hh`, `.hxx`, `.h++`, `.ipp`)

## Requirements

- LLVM/Clang installed and on PATH
- Visual Studio 2022 version 17.0 or later

## Example Project

```xml
<Project Sdk="IKVM.Clang.Sdk">
  <PropertyGroup>
    <TargetIdentifiers>x86_64-pc-windows-msvc</TargetIdentifiers>
    <OutputType>exe</OutputType>
  </PropertyGroup>
</Project>
```

Open the `.clangproj` file in Visual Studio to get started. Add source files through Solution Explorer and build using the Build menu.

## More Information

- [GitHub Repository](https://github.com/ikvmnet/ikvm-clang)
- [Report Issues](https://github.com/ikvmnet/ikvm-clang/issues)
- [NuGet Package: IKVM.Clang.Sdk](https://www.nuget.org/packages/IKVM.Clang.Sdk)