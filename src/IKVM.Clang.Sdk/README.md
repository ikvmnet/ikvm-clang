# IKVM.Clang.Sdk

MSBuild SDK for compiling C, C++, Objective-C, and assembly source files with the **Clang** compiler. Produces executables, shared libraries, and static libraries targeting any LLVM triple — Windows, Linux, macOS, WebAssembly, and more.

---

## Requirements

- [LLVM / Clang](https://releases.llvm.org/) with `clang` and `llvm-ar` accessible on `PATH`
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
| `TargetIdentifiers` | *(required)* | Semicolon-separated LLVM target triples |
| `OutputType` | `dll` | Output kind: `exe`, `dll`, or `lib` |
| `TargetName` | project name | Base name for the output file |
| `ClangToolExe` | `clang` / `clang.exe` | Clang executable path or name |
| `LlvmArToolExe` | `llvm-ar` / `llvm-ar.exe` | LLVM archiver executable path or name |
| `DebugSymbols` | `true` in Debug | Emit debug symbols |
| `LanguageStandard` | *(clang default)* | C/C++ language standard, e.g. `c17`, `c++20` |
| `PositionIndependentCode` | *(false)* | Pass `-fPIC` to the compiler |
| `MsCompatibility` | *(false)* | Pass `-fms-compatibility` |
| `UseLd` | `lld` | Linker driver for `exe`/`dll` targets |
| `Subsystem` | `console` (Windows exe) | Windows subsystem (`console` or `windows`) |
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
