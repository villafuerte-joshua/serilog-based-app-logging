# ApplicationLogging

[![NuGet](https://img.shields.io/nuget/v/Yuehanxin.ApplicationLogging.svg)](https://www.nuget.org/packages/Yuehanxin.ApplicationLogging)

A shared library that sets up Serilog for .NET host apps with one call, published as [`Yuehanxin.ApplicationLogging`](https://www.nuget.org/packages/Yuehanxin.ApplicationLogging).

```bash
dotnet add package Yuehanxin.ApplicationLogging
```

```csharp
builder.UseApplicationLogging(builder.Configuration);
```

- **Usage and configuration:** [ApplicationLogging/README.md](ApplicationLogging/README.md). This is the same README shown on nuget.org.
- **Release history:** [CHANGELOG.md](CHANGELOG.md)

## Releasing

1. Merge your changes into `main`, using conventional commit prefixes (`feat:`, `fix:`, `ref:`, `docs:`, `ci:` ...) so the changelog can group them.
2. Tag the release and push the tag:

   ```bash
   git tag v1.2.3
   git push origin v1.2.3
   ```

3. The `publish-package` workflow runs `.github/scripts/Generate-Changelog.ps1`. That script writes the changes between the new tag and the previous `v*` tag into the package release notes, regenerates `CHANGELOG.md` inside the package, and then publishes to nuget.org.

To refresh the committed `CHANGELOG.md` locally:

```powershell
./.github/scripts/Generate-Changelog.ps1                    # pending changes listed under "Unreleased"
./.github/scripts/Generate-Changelog.ps1 -Version 1.2.3     # pending changes listed under 1.2.3
```

## License

[MIT](LICENSE.txt)
