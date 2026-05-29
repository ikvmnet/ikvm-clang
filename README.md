# IKVM.Clang

**MSBuild SDK and Visual Studio extension for building native projects with the Clang compiler.**

[![NuGet](https://img.shields.io/nuget/v/IKVM.Clang.Sdk.svg)](https://www.nuget.org/packages/IKVM.Clang.Sdk)
[![Visual Studio Marketplace](https://img.shields.io/visual-studio-marketplace/v/IKVM.IKVM.Clang)](https://marketplace.visualstudio.com/items?itemName=IKVM.IKVM.Clang)
[![GitHub license](https://img.shields.io/github/license/ikvmnet/ikvm-clang)](LICENSE)

---

## Overview

IKVM.Clang provides two components:

| Component | Description |
|-----------|-------------|
| **`IKVM.Clang.Sdk`** | MSBuild SDK that drives `clang` / `llvm-ar` to compile C, C++, Objective-C, and assembly source files into executables, shared libraries, or static libraries. Works on Windows, Linux, and macOS from the command line or CI. |
| **IKVM.Clang VSIX** | Visual Studio extension that integrates the SDK with the IDE: CPS-based project system, Solution Explorer icons, syntax highlighting for C/C++/Objective-C/Assembly, and property pages. |

---

## Requirements

- [LLVM / Clang](https://releases.llvm.org/) installed and `clang` / `llvm-ar` on `PATH` (or configured via MSBuild properties)
- .NET SDK (for MSBuild) — .NET 6 or later recommended
- Visual Studio 2022 17.0+ (for the VSIX extension, optional)

---

## Getting Started

### 1 — Install the Visual Studio Extension (optional)

If you are using Visual Studio, install the **IKVM.Clang** extension from the Visual Studio Marketplace. When you open a project that uses `IKVM.Clang.Sdk`, Visual Studio will offer to install the extension automatically if it is not already present.

### 2 — Create a project

Add the SDK to your project file (`.clangproj` or any MSBuild project extension):

```xml
<Project>
    <Import Project="Sdk.props" Sdk="IKVM.Clang.Sdk" />

    <PropertyGroup>
        <!-- LLVM target triple -->
        <TargetIdentifiers>x86_64-pc-windows-msvc</TargetIdentifiers>
        <!-- exe | dll | lib -->
        <OutputType>exe</OutputType>
    </PropertyGroup>

    <ItemGroup>
        <Compile Include="src\*.c" />
    </ItemGroup>

    <Import Project="Sdk.targets" Sdk="IKVM.Clang.Sdk" />
</Project>
```

### 3 — Build

```shell
dotnet build
```

or from Visual Studio: **Build → Build Solution**.

---

## Supported Source File Types

| Item Type | Extensions |
|-----------|------------|
| C source | `.c` |
| C++ source | `.cpp` `.cc` `.cxx` `.c++` `.cppm` `.ixx` |
| C/C++ header | `.h` `.hpp` `.hh` `.hxx` `.h++` `.ipp` |
| Objective-C / Objective-C++ | `.m` `.mm` |
| Assembly | `.s` `.asm` |

---

## Output Types

| `OutputType` | Description | Output |
|---|---|---|
| `exe` | Executable | `.exe` / *(no extension)* / `.wasm` |
| `dll` | Shared library | `.dll` / `.so` / `.dylib` |
| `lib` | Static library | `.lib` / `.a` |

---

## Key MSBuild Properties

| Property | Default | Description |
|----------|---------|-------------|
| `TargetIdentifiers` | *(required)* | Semicolon-separated LLVM target triples, e.g. `x86_64-pc-windows-msvc` |
| `OutputType` | `dll` | Output kind: `exe`, `dll`, or `lib` |
| `ClangToolExe` | `clang` / `clang.exe` | Path or name of the Clang executable |
| `LlvmArToolExe` | `llvm-ar` / `llvm-ar.exe` | Path or name of the LLVM archiver |
| `DebugSymbols` | `true` in Debug | Emit debug symbols |
| `UseLd` | `lld` | Linker driver (`lld`, `bfd`, `gold`, etc.) |

---

## Repository Structure

```
src/
  IKVM.Clang.Sdk/           MSBuild SDK package (targets, props, CPS rules)
  IKVM.Clang.Sdk.Tasks/     Custom MSBuild tasks
  IKVM.Clang.Vsix/          Visual Studio extension (VSIX)
samples/
  IKVM.Clang.Sample1/       Minimal C project example
```

---

## Contributing

Pull requests are welcome. Please open an issue first to discuss significant changes.

## License

This project is licensed under the [MIT License](LICENSE).
