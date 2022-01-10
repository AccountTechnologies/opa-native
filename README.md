# opa-native
[OPA](https://github.com/open-policy-agent/opa) server native binaries packaged as NuGet.

# Why?

So it can be easily used in integration tests.

# Usage

Reference in a proj file like:

```xml
  <ItemGroup>
    <PackageReference Include="Opa.Native" Version="0.*" PrivateAssets="Compile" IncludeAssets="Compile;Runtime;Native" />
  </ItemGroup>
```

Then in code :

```csharp
await using OpaHandle h = await OpaProcess.StartServerAsync();
```
