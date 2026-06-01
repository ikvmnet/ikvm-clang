# IKVM Clang Project Templates

This package provides .NET project templates for creating native C/C++ projects using the IKVM.Clang.Sdk.

## Installation

Install the templates package:

```bash
dotnet new install IKVM.Clang.Templates
```

## Available Templates

### Clang Console Application
Creates a console executable project.

```bash
dotnet new clang -n MyApp
```

### Clang Shared Library
Creates a shared library (DLL/SO) project.

```bash
dotnet new clanglib -n MyLibrary
```

### Clang Static Library
Creates a static library project.

```bash
dotnet new clangstaticlib -n MyStaticLib
```

## Building

Build your project using the standard .NET CLI:

```bash
dotnet build
```

## Options

- `-n, --name`: Name for the generated project (default: current directory name)
- `--Framework`: Target framework (default: net10.0)

## Examples

Create a console application:
```bash
dotnet new clang -n HelloWorld
cd HelloWorld
dotnet build
```

Create a shared library:
```bash
dotnet new clanglib -n MathLib
cd MathLib
dotnet build
```

Create a static library:
```bash
dotnet new clangstaticlib -n UtilsLib
cd UtilsLib
dotnet build
```

## Requirements

- IKVM.Clang.Sdk package
- Clang/LLVM toolchain

## Learn More

- [IKVM.NET GitHub](https://github.com/ikvmnet/ikvm-clang)
