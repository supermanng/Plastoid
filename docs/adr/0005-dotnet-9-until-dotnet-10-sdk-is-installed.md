# ADR 0005 — Target .NET 9 for now

**Status:** accepted (temporary) · **Date:** 2026-11-21

The build prompt asks for .NET 10 LTS; only the .NET 9.0.203 SDK is installed on the development machine and CI is pinned to match. All package versions are pinned to the newest releases that still target net9 (EF Core 9.0.23, Quartz 3.22, OpenTelemetry pinned to 1.13). The OTLP exporter package is left out entirely: 1.14+ needs .NET 10 and 1.13 has published CVEs, so traces and metrics are instrumented but not exported yet.

`SuppressTfmSupportBuildWarnings` is on because StackExchange.Redis 2.13 and AWSSDK.Core 4.1 resolve System.IO.Hashing 11.0.0, which only warns about net9 and ships netstandard assets. Remove the suppression when moving to .NET 10.

**To move to .NET 10:** install the SDK, change `TargetFramework` in `Directory.Build.props`, bump the Microsoft.* package versions in `Directory.Packages.props` to 10.x, Quartz to 4.x, OpenTelemetry to latest, and update `global.json` and the Dockerfile base images.
