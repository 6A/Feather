Feather.Fody
============

Feather strips all references to [FSharp.Core](https://www.nuget.org/packages/FSharp.Core)
from any .NET assembly, by either removing useless attributes and methods or by
replacing them with built-in equivalents.

Feather works fully at compile-time (using [Fody](https://github.com/Fody/Fody)), and thus
does not require any runtime dependency.

Therefore, Feather can be used to code in F#, while having outputs assembly that do not
require any extra dependencies, and can thus be used directly within C# projects without
requiring [FSharp.Core](https://www.nuget.org/packages/FSharp.Core).

**It is currently a work in progress.** Right now, all references to FSharp.Core are merely
removed; no equivalent function calls are used.

## Installing
> The NuGet package has not been published yet. However, as soon as it will be online, the
> following will apply to the installation process of Feather.

Add a dependency to your project:
```xml
<ItemGroup>
  <PackageReference Include="Feather.Fody" Version="0.1.0" />
</ItemGroup>
```

Then, install the Feather weaver by creating a `FodyWeavers.xml` file and setting its content
to:
```xml
<?xml version="1.0" encoding="utf-8" ?>
<Weavers>
  <Feather /> 
</Weavers>
```

## Process
See the [implementation issue](https://github.com/6A/Feather/issues/1) for progress details.

## Testing
A test assembly is available in the [Feather.TestAssembly](./Feather.TestAssembly) directory,
and contains many declarations that make use of F#-specific features.

This assembly is modified by [Feather.Fody](./Feather.Fody) on compilation, and should then
lose all its references to [FSharp.Core](https://www.nuget.org/packages/FSharp.Core).

The [Feather.Tests](./Feather.Tests) projects finally ensures that both these statements are true:
- `Feather.TestAssembly.dll` does not contain **any** reference to `FSharp.Core`.
- `Feather.TestAssembly.dll` can be loaded, and works flawlessly.

## Contributing
### Adding tests
1. Add test cases to [Feather.TestAssembly](./Feather.TestAssembly).
2. Make sure they work after removal of all F# references in [Feather.Tests](./Feather.Tests).

### Adding replacements
1. Grab [dnSpy](https://github.com/0xd4d/dnSpy) to analyze output assemblies.
2. Find a common pattern or specific use.
3. Implement a replacement in [ModuleWeaver.cs](./Feather.Fody/ModuleWeaver.cs).
