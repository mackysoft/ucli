# MackySoft.Ucli.Infrastructure

[![NuGet](https://img.shields.io/nuget/v/MackySoft.Ucli.Infrastructure?label=MackySoft.Ucli.Infrastructure)](https://www.nuget.org/packages/MackySoft.Ucli.Infrastructure) [![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](https://github.com/mackysoft/ucli/blob/master/LICENSE)

**Created by Hiroya Aramaki ([Makihiro](https://twitter.com/makihiro_dev))**

`MackySoft.Ucli.Infrastructure` contains shared infrastructure services used by uCLI runtime components.

This is an advanced integration package, not the protocol contract layer. It builds on `MackySoft.Ucli.Contracts` and provides boundary services for runtime code that needs filesystem, process, fingerprint, and transport helpers. Users who only run the `ucli` command or install `MackySoft.Ucli.Unity` usually do not need to reference this package directly.

## Installation

Install a pinned version from nuget.org:

```bash
dotnet add package MackySoft.Ucli.Infrastructure --version <version>
```

## What This Package Provides

- Filesystem and storage path helpers.
- Process liveness probing utilities.
- Project and index fingerprint helpers.
- IPC transport path utilities.
- Shared runtime services used by the CLI and Unity plugin.

## Related Packages

| Package | Role |
| --- | --- |
| `MackySoft.Ucli` | .NET global tool that provides the `ucli` command. |
| `MackySoft.Ucli.Unity` | Unity Editor plugin for uCLI IPC and automation. |
| `MackySoft.Ucli.Contracts` | Shared IPC protocol and data contract types. |

## Repository

Source and issue tracking:

<https://github.com/mackysoft/ucli>

## Support

Use GitHub Issues for bugs, questions, and package problems:

<https://github.com/mackysoft/ucli/issues>

Include the package name and version when reporting package-specific problems.

## Sponsor

If uCLI or other MackySoft projects are useful to you, please support MackySoft through GitHub Sponsors:

<https://github.com/sponsors/mackysoft>

## Author

- Website: <https://mackysoft.net/>
- GitHub: <https://github.com/mackysoft>
- Sponsors: <https://github.com/sponsors/mackysoft>

## License

This package is under the [MIT License](https://github.com/mackysoft/ucli/blob/master/LICENSE).
