# Testing IKVM Clang Templates

## Local Installation

To test the templates locally before publishing:

1. **Pack the template project:**
   ```bash
   dotnet pack src\IKVM.Clang.Templates\IKVM.Clang.Templates.csproj
   ```

2. **Install the templates locally:**
   ```bash
   dotnet new install src\IKVM.Clang.Templates\bin\Debug\IKVM.Clang.Templates.1.0.0.nupkg
   ```

3. **Verify installation:**
   ```bash
   dotnet new list clang
   ```

   You should see:
   - `clang` - Clang Console Application
   - `clanglib` - Clang Shared Library
   - `clangstaticlib` - Clang Static Library

## Usage Examples

### Create a Console Application

```bash
# Create a new console app
dotnet new clang -n MyConsoleApp
cd MyConsoleApp

# Build the project
dotnet build

# Run (if build produces executable)
dotnet run
```

### Create a Shared Library

```bash
# Create a new shared library
dotnet new clanglib -n MySharedLib
cd MySharedLib

# Build the project
dotnet build
```

### Create a Static Library

```bash
# Create a new static library
dotnet new clangstaticlib -n MyStaticLib
cd MyStaticLib

# Build the project
dotnet build
```

## Uninstalling Templates

To uninstall the templates:

```bash
dotnet new uninstall IKVM.Clang.Templates
```

## Template Customization

Users can specify custom names and the project will be properly renamed:

```bash
dotnet new clang -n HelloWorld
# Creates: HelloWorld.clangproj with proper naming
```

## Publishing

Once tested and ready, publish to NuGet.org:

```bash
dotnet pack src\IKVM.Clang.Templates\IKVM.Clang.Templates.csproj -c Release
dotnet nuget push src\IKVM.Clang.Templates\bin\Release\IKVM.Clang.Templates.1.0.0.nupkg -s https://api.nuget.org/v3/index.json -k YOUR_API_KEY
```

Users can then install with:

```bash
dotnet new install IKVM.Clang.Templates
```
