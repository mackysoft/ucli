# MackySoft.Ucli.Contracts

[![NuGet](https://img.shields.io/nuget/v/MackySoft.Ucli.Contracts?label=MackySoft.Ucli.Contracts)](https://www.nuget.org/packages/MackySoft.Ucli.Contracts) [![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](https://github.com/mackysoft/ucli/blob/master/LICENSE)

**Created by Hiroya Aramaki ([Makihiro](https://twitter.com/makihiro_dev))**

`MackySoft.Ucli.Contracts` contains the shared IPC protocol and data contract types used by uCLI runtime components.

This is an advanced integration package for uCLI runtime, Unity plugin integration, and tooling that needs to exchange uCLI protocol messages directly. Users who only run the `ucli` command or install `MackySoft.Ucli.Unity` usually do not need to reference this package directly.

## Installation

Install a pinned version from nuget.org:

```bash
dotnet add package MackySoft.Ucli.Contracts --version <version>
```

## What This Package Provides

- IPC request and response contracts.
- Typed primitive operation Args/Result contract types.
- Attributes used to describe operation inputs, results, and generated validation schemas.
- `UcliNoResult` for operations that intentionally omit `opResults[].result`.
- Protocol constants and shared protocol metadata.
- Configuration and storage contract models.
- JSON serialization helpers for uCLI contract types.
- Shared data shapes used by the CLI, Unity plugin, and infrastructure package.

## Operation Contracts

Primitive operation contracts are authored as CLR Args/Result types plus operation metadata. Args/Result types define the public JSON structure. Reusable operation values such as scene asset paths, prefab asset paths, hierarchy paths, GlobalObjectId strings, asset GUIDs, request-local aliases, and Unity type identifiers use semantic string value types in the CLR contract, but they serialize as JSON strings on the IPC boundary. Args properties and semantic value types carry input descriptions and semantic constraints through attributes such as `UcliDescriptionAttribute` and `UcliInputConstraintAttribute`; `ops describe` derives `inputs[]` from those attributes. Operation metadata defines the operation-level description and assurance metadata. uCLI exposes `description`, `inputs`, `resultContract`, and `assurance` through `ops describe`, and also generates `argsSchema` / `resultSchema` for JSON structure validation.

`argsSchema` and `resultSchema` validate only the JSON structure of `steps[].args` and `opResults[].result`; they are not the primary agent UX contract. Operation selection, input construction, and result interpretation should use the higher-level describe contract. Semantic constraints are exposed as `inputs[].constraints`, not as JSON Schema constraint keywords.

Selectors are also contract types, such as `GameObjectReferenceArgs`, `ComponentReferenceArgs`, and `AssetReferenceArgs`. Operation authors consume those typed references and keep resolved Unity objects inside the Unity implementation layer.

JSON remains the IPC wire format. Operation implementations and command builders should use the typed contract model before crossing the IPC boundary, and should avoid treating raw `JsonElement` as the authoring surface.

## Related Packages

| Package | Role |
| --- | --- |
| `MackySoft.Ucli` | .NET global tool that provides the `ucli` command. |
| `MackySoft.Ucli.Unity` | Unity Editor plugin for uCLI IPC and automation. |
| `MackySoft.Ucli.Infrastructure` | Shared infrastructure services that use the contract model. |

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
