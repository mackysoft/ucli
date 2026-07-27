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
- Consumer-owned annotations for uCLI-specific operation-input meaning.
- `UcliNoResult` for operations that intentionally omit `opResults[].result`.
- Protocol constants and shared protocol metadata.
- Configuration and storage contract models.
- JSON serialization helpers for uCLI contract types.
- uCLI vocabulary declarations backed by `MackySoft.Text.Vocabularies` and its JSON adapter.
- Shared data shapes used by the CLI, Unity plugin, and infrastructure package.

## Operation Contracts

Primitive operation contracts are authored as CLR Args/Result types plus operation metadata. Args/Result types and the effective `System.Text.Json` contract define the public JSON structure. Reusable operation values such as scene asset paths, prefab asset paths, hierarchy paths, GlobalObjectId strings, asset GUIDs, and Unity type identifiers use semantic CLR value types even when they serialize as JSON strings. Finite string values use `MackySoft.Text.Vocabularies` types and the runtime vocabulary converter. Closed selector alternatives use the IPC serializer's polymorphic `JsonTypeInfo` configuration.

Descriptions and string or collection bounds use the common `MackySoft.JsonSchema.Generation.Annotations` attributes. Requiredness, nullability, arbitrary JSON values, finite vocabularies, and polymorphic alternatives come from the actual CLR and serializer contract rather than duplicate schema annotations. uCLI-specific Unity meaning uses one concrete annotation per constraint, with typed vocabulary arguments where a finite value is required.

Public raw `op` args must not use request-local alias references. The Unity runtime accepts that reference variant only while compiling higher-level request steps.

Operation schema and describe metadata are projections of the same provider generation result. uCLI does not rebuild a separate input table from reflection attributes.

Selectors are also contract types, such as `GameObjectReferenceArgs`, `ComponentReferenceArgs`, and `AssetReferenceArgs`. Operation authors consume those typed references and keep resolved Unity objects inside the Unity implementation layer.

JSON remains the IPC wire format. Operation implementations and command builders should use the typed contract model before crossing the IPC boundary, and should avoid treating raw `JsonElement` as the authoring surface.

## Related Packages

| Package | Role |
| --- | --- |
| `MackySoft.Ucli` | .NET global tool that provides the `ucli` command. |
| `MackySoft.Ucli.Unity` | Unity Editor plugin for uCLI IPC and automation. |
| `MackySoft.Ucli.Infrastructure` | Shared infrastructure services that use the contract model. |
| [`MackySoft.Text.Vocabularies`](https://github.com/mackysoft/dotnet-foundations) | Product-independent finite typed text vocabulary definitions and resolution. |
| [`MackySoft.Text.Vocabularies.Json`](https://github.com/mackysoft/dotnet-foundations) | `System.Text.Json` string and property-name adapters for those vocabularies. |

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
