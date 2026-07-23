# Contributing to Scachalka

Thanks for your interest in improving Scachalka! This project is a clean-architecture
WPF front-end for yt-dlp and FFmpeg.

## Prerequisites

- Windows 10/11 x64
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- yt-dlp and FFmpeg on `PATH` (only needed to run the app, not to build)

## Getting started

```powershell
git clone https://github.com/<your-user>/scachalka.git
cd scachalka
dotnet build
dotnet test
```

## Project layout

| Project | Responsibility |
|---|---|
| `YtDlpGui.Abstractions` | Interfaces, models, enums, localization contract — zero dependencies |
| `YtDlpGui.Core` | Pure domain logic: validation, argument builders, progress parsing, errors |
| `YtDlpGui.Infrastructure` | Process execution, tool discovery, settings, logging, localization catalog |
| `YtDlpGui.Application` | Download queue coordinator, executor, job model |
| `YtDlpGui.UI` | WPF views, view models (MVVM), themes, converters |
| `YtDlpGui.App` | Composition root (DI) and app lifecycle |

**Dependency rule:** dependencies point downward only
(`App → UI → Application → Core → Abstractions`, `Infrastructure → Abstractions`).
`Core` and `Application` must never reference WPF or `Infrastructure`.

## Coding standards

- C# 12 / .NET 8, nullable enabled, warnings clean.
- Small, single-responsibility files (target < 300 lines).
- No `TODO`, placeholders, or dead code in merged PRs.
- Add unit tests for new domain logic in `tests/YtDlpGui.Core.Tests`.
- Run `dotnet format`, `dotnet build`, and `dotnet test` before opening a PR.

## Adding a new output format

Formats are strategies — you do **not** touch existing code:

1. Add the value to `MediaFormat` and its mapping in `MediaFormatExtensions`.
2. Handle it in `VideoArgumentBuilder` or `AudioArgumentBuilder` (or add a new builder).
3. Register any new `IArgumentBuilder` in `ServiceRegistration`.
4. Add tests in `ArgumentBuilderTests`.

## Adding a UI language

1. Add a `StringCatalog.<Lang>.cs` catalog in `YtDlpGui.Infrastructure/Localization`.
2. Register it in `Localizer` and add the code to `AvailableLanguages`.
3. Add a `LanguageOption` entry (endonym label).

## Commit messages

Conventional commits are appreciated: `feat:`, `fix:`, `refactor:`, `docs:`, `test:`, `chore:`.

## Legal / scope

Scachalka is a GUI wrapper. It must not add functionality whose only purpose is to
circumvent site access controls (anti-bot protections, DRM) or to facilitate copyright
infringement. Keep contributions to general-purpose downloader features.
