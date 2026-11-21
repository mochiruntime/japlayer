---
trigger: always_on
---

# Environment

Assume that the default shell is PowerShell

# Building and formatting

After making changes to the codebase:

1.  Run `dotnet build` in the solution root after any significant code modification. This verifies that the changes do not break the build.
    ```powershell
    dotnet build /p:ForceNoColor=true 2>&1
    ```

2.  After a successful build, run `dotnet format` to ensure that the code adheres to the project's style guidelines as defined in `.editorconfig`.
    ```powershell
    dotnet format && dotnet format style --severity info
    ```

Always perform these steps before submitting or concluding a task.

# Coding style

Avoid abbreviating variable names to maintain readability and explicitness

# Architecture & Best Practices

Always prefer standard, common, established platform patterns over ad-hoc workarounds:

- **EF Core & Database**: Model all persisted tables as EF Core entities in `Japlayer.Data` and evolve schemas using EF Core Migrations (`dotnet ef migrations add` + `Database.Migrate()`). Avoid raw SQL DDL or ad-hoc `ExecuteSqlRaw` queries when standard LINQ and EF Core change tracking apply.
- **WinUI 3 Styling & Controls**: Use built-in WinUI 3 `{ThemeResource ...}` brushes (such as `CardBackgroundFillColorDefaultBrush`, `CardStrokeColorDefaultBrush`, and `TextFillColorSecondaryBrush`) so both Light and Dark themes work automatically. Never hardcode theme-specific hex colors outside media/video overlays, and only use layout panels within their supported control types (e.g., `ItemsWrapGrid` only inside `ListViewBase`, never `ItemsControl`).
- **WinUI 3 `SizeChanged` Layout Guard**: Whenever a `SizeChanged` handler sets `Width`, `Height`, or updates layout-affecting collections, floor computed dimensions (`Math.Floor`) and guard updates with a hysteresis threshold (`> 1.0` DIPs) to prevent sub-pixel DPI rounding `LayoutCycleException` (`0xc000027b`) crashes.
- **General**: Reuse existing project helpers, MVVM patterns (`CommunityToolkit.Mvvm`), and standard .NET/WinUI APIs before introducing custom abstractions or one-off workarounds.
