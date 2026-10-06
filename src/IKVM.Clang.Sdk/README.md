# IKVM.Clang.Sdk

MSBuild SDK for compiling C, C++, Objective-C, and assembly source files with the **Clang** compiler. Produces executables, shared libraries, and static libraries targeting any LLVM triple — Windows, Linux, macOS, WebAssembly, and more.

---

## Requirements

- [LLVM / Clang](https://releases.llvm.org/): `clang`, `clang++` and `llvm-ar`, plus `clangd` for language support in Visual Studio. They are found automatically; see [LLVM Tools](#llvm-tools)
- MSBuild 17+ (ships with Visual Studio 2022, or via the .NET SDK)
- Visual Studio 2022 17.0+ with the [IKVM.Clang](https://marketplace.visualstudio.com/items?itemName=IKVM.IKVM.Clang) extension for IDE support (optional)

---

## Quick Start

Create a project file (conventionally `.clangproj`, but any extension works):

```xml
<Project>
    <Import Project="Sdk.props" Sdk="IKVM.Clang.Sdk" />

    <PropertyGroup>
        <TargetIdentifiers>x86_64-pc-windows-msvc</TargetIdentifiers>
        <OutputType>exe</OutputType>
    </PropertyGroup>

    <ItemGroup>
        <Compile Include="src\*.c" />
    </ItemGroup>

    <Import Project="Sdk.targets" Sdk="IKVM.Clang.Sdk" />
</Project>
```

Then build:

```shell
dotnet build
```

---

## LLVM Tools

The SDK finds the LLVM tools it runs, all from the same LLVM installation so that they match:

| Tool | Used for |
|------|----------|
| `clang` | Compiling, and linking projects with only C, Objective-C and assembly sources |
| `clang++` | Linking projects with C++ or Objective-C++ sources, so the C++ standard library is linked |
| `llvm-ar` | Creating static libraries |
| `clangd` | Language support in Visual Studio |
| `lld` | Linking; found by clang itself, next to it |

Without any settings, clang is looked for on `PATH` and in the usual installation directories: `C:\Program Files\LLVM\bin` and Visual Studio's C++ Clang tools on Windows, Homebrew's LLVM on macOS, and `/usr/lib/llvm-*/bin` on Linux. An installation with the whole suite is preferred over one with clang alone (such as Apple's `/usr/bin`). The other tools are then taken from clang's installation first.

These properties override that:

| Property | Meaning |
|----------|---------|
| `LlvmToolsPath` | Directory to take every tool from, such as the `bin` directory of an LLVM installation. Nothing else is searched. |
| `ClangPath` | Full path of `clang`. The other tools are then looked for next to it first. |
| `ClangCxxPath` | Full path of `clang++`. |
| `LlvmArPath` | Full path of `llvm-ar`. |
| `ClangdPath` | Full path of `clangd`. |
| `LinkerPath` | Full path of the linker for clang to run (passed as `--ld-path`). |
| `LinkWithClangCxx` | `true` or `false` to choose whether `clang++` links; by default it does when there are C++ or Objective-C++ sources. |

The older `ClangToolPath`/`ClangToolExe` and `LlvmArToolPath`/`LlvmArToolExe` properties still work.

A tool that cannot be found, or an override that names a file that does not exist, fails the build when that tool is needed, with an `ICLANG1001`, `ICLANG1002` or `ICLANG1003` error that says what to set. In Visual Studio the same problems appear as warnings in the Error List.

The `GetLlvmToolset` target returns what was found, and `GetClangCompileCommands` the command line of each source file.

---

## Output Types

Set `<OutputType>` in your project to one of:

| Value | Produces | Windows | Linux | macOS | WASM |
|-------|----------|---------|-------|-------|------|
| `exe` | Executable | `.exe` | *(none)* | *(none)* | `.wasm` |
| `dll` | Shared library | `.dll` | `.so` | `.dylib` | `.so` |
| `lib` | Static library | `.lib` | `.a` | `.a` | `.a` |

---

## Target Triples

`TargetIdentifiers` accepts one or more semicolon-separated LLVM target triples.

```xml
<!-- Single target -->
<TargetIdentifiers>x86_64-pc-windows-msvc</TargetIdentifiers>

<!-- Multi-target (cross-compile) -->
<TargetIdentifiers>x86_64-pc-linux-gnu;aarch64-pc-linux-gnu</TargetIdentifiers>
```

Common triples:

| Triple | Platform |
|--------|----------|
| `x86_64-pc-windows-msvc` | Windows x64 |
| `aarch64-pc-windows-msvc` | Windows ARM64 |
| `x86_64-pc-linux-gnu` | Linux x64 |
| `aarch64-pc-linux-gnu` | Linux ARM64 |
| `x86_64-apple-macosx` | macOS x64 |
| `aarch64-apple-macosx` | macOS ARM64 (Apple Silicon) |
| `wasm32-unknown-unknown` | WebAssembly |

---

## Supported Source File Types

| Item type | Extensions | Language |
|-----------|------------|----------|
| `Compile` | `.c` | C |
| `Compile` | `.cpp` `.cc` `.cxx` `.c++` `.cppm` `.ixx` | C++ |
| `Compile` | `.m` `.mm` | Objective-C / Objective-C++ |
| `Compile` | `.s` `.asm` | Assembly |
| `Header` | `.h` `.hpp` `.hh` `.hxx` `.h++` `.ipp` | C/C++ headers (copied to include output) |

---

## Key MSBuild Properties

| Property | Default | Description |
|----------|---------|-------------|
| `TargetIdentifier` | *(host)* | The LLVM target triple to build for; without one, clang builds for the host |
| `TargetIdentifiers` | *(empty)* | Semicolon-separated LLVM target triples, each built in turn |
| `OutputType` | `dll` | Output kind: `exe`, `dll`, or `lib` |
| `TargetName` | project name | Base name for the output file |
| `ClangToolExe` | `clang` / `clang.exe` | Clang executable path or name |
| `LlvmArToolExe` | `llvm-ar` / `llvm-ar.exe` | LLVM archiver executable path or name |
| `DebugSymbols` | `true` in Debug | Emit debug symbols |
| `Optimization` | `2` in Release | Optimization level, passed as `-O<level>`: `0`, `1`, `2`, `3`, `s`, `z` or `g` |
| `Assertions` | `false` in Release | When `false`, define `NDEBUG`, which removes `assert` |
| `LanguageStandard` | *(clang default)* | C/C++ language standard, e.g. `c17`, `c++20` |
| `PositionIndependentCode` | `true` on ELF targets (Linux and other Unix) | Pass `-fPIC` to the compiler |
| `MsCompatibility` | *(false)* | Pass `-fms-compatibility` |
| `UseLd` | `lld` | Linker driver for `exe`/`dll` targets |
| `Subsystem` | *(linker default)* | Windows subsystem passed to the MSVC linker (`console` or `windows`) |
| `AdditionalCompileOptions` | *(empty)* | Extra flags forwarded to every `clang` invocation |
| `AdditionalLinkOptions` | *(empty)* | Extra flags forwarded to the linker invocation |

---

## Item Metadata

### `Compile` items

| Metadata | Description |
|----------|-------------|
| `Language` | Override language: `c`, `c++`, `objective-c`, `objective-c++` |
| `LanguageStandard` | Per-file language standard override |
| `DebugSymbols` | Per-file debug symbol override |
| `Optimization` | Per-file optimization level override |
| `PositionIndependentCode` | Per-file `-fPIC` override |
| `IncludeDirectories` | Semicolon-separated extra include search paths |
| `PreprocessorDefinitions` | Semicolon-separated `NAME` or `NAME=VALUE` defines |
| `AdditionalCompileOptions` | Extra flags for this file only |

### `Header` items

Headers with `<CopyToIncludeDirectory>true</CopyToIncludeDirectory>` (the default) are copied to the intermediate `headers\` output folder and automatically added to the include path of dependent projects.

---

## Project References

Reference another `IKVM.Clang.Sdk` project to automatically link its library and expose its headers:

```xml
<ItemGroup>
    <ProjectReference Include="..\MyLib\MyLib.csproj" />
</ItemGroup>
```

The referenced project's output library and `headers\` folder are wired up automatically.

---

## Visual Studio Integration

Install the **[IKVM.Clang](https://marketplace.visualstudio.com/items?itemName=IKVM.IKVM.Clang)** extension to get:

- Full CPS-based project system (Solution Explorer, property pages)
- Syntax highlighting for C, C++, Objective-C, and Assembly
- Custom project and file icons

When you open a project that uses this SDK, Visual Studio will offer to install the extension automatically if it is not already present.

---

## Source and Issues

[https://github.com/ikvmnet/ikvm-clang](https://github.com/ikvmnet/ikvm-clang)
