---
trigger: always_on
---

# Environment

Assume that the default shell is PowerShell

# Building and formatting

After making changes to the codebase:

1.  Run `dotnet build` in the solution root after any significant code modification (or Visual Studio `MSBuild.exe` with `/p:Platform=x64 /nr:false` if `dotnet build` fails in WinUI `XamlCompiler.exe` `MarkupCompilePass2`). This verifies that the changes do not break the build.
    ```powershell
    dotnet build /p:ForceNoColor=true 2>&1
    ```

2.  After a successful build, run `dotnet format` to ensure that the code adheres to the project's style guidelines as defined in `.editorconfig`.
    ```powershell
    dotnet format --no-restore && dotnet format style --no-restore --severity info
    ```

Always perform these steps before submitting or concluding a task.

# Coding style

Avoid abbreviating variable names to maintain readability and explicitness

# Architecture & Best Practices

Always prefer standard, common, established platform patterns over ad-hoc workarounds:

- **EF Core & Database**: Model all persisted tables as EF Core entities in `Japlayer.Data` and evolve schemas using EF Core Migrations (`dotnet ef migrations add` + `Database.Migrate()`). Avoid raw SQL DDL or ad-hoc `ExecuteSqlRaw` queries when standard LINQ and EF Core change tracking apply. Inject `IDbContextFactory<DatabaseContext>` (via `AddDbContextFactory`) into singleton providers and scope each operation with `await using var context = await _contextFactory.CreateDbContextAsync();` instead of holding a long-lived `DatabaseContext`. Batch-load related data in providers rather than triggering N+1 queries per item, and avoid duplicating presentation heuristics between `Japlayer.Data` and ViewModels.
- **WinUI 3 Styling & Controls**: Use built-in WinUI 3 `{ThemeResource ...}` brushes (such as `CardBackgroundFillColorDefaultBrush`, `CardStrokeColorDefaultBrush`, and `TextFillColorSecondaryBrush`) so both Light and Dark themes work automatically. Never hardcode theme-specific hex colors outside media/video overlays, and only use layout panels within their supported control types (e.g., `ItemsWrapGrid` only inside `ListViewBase`, never `ItemsControl`).
- **WinUI 3 `AutoSuggestBox` with Custom Items**: When binding `ItemsSource` to custom types/records, set `TextMemberPath`, override `ToString()` on the item type, set `UpdateTextOnSelect="False"` if the input clears on submit, and perform async submission in `QuerySubmitted` rather than `SuggestionChosen`.
- **WinUI 3 Collections & Image Decoding**: Keep `ItemsSource` collection instances stable and apply bulk updates with a single `Reset` (short-circuiting no-op sequences) instead of replacing `ObservableCollection` instances or mutating items in a loop. Always set `DecodePixelWidth` with `DecodePixelType.Logical` on `BitmapImage` and key shared image caches by `(path, decodePixelWidth)`.
- **WinUI 3 `SizeChanged` Layout Guard**: Whenever a `SizeChanged` handler sets `Width`, `Height`, or updates layout-affecting collections, floor computed dimensions (`Math.Floor`) and guard updates with a hysteresis threshold (`> 1.0` DIPs) to prevent sub-pixel DPI rounding `LayoutCycleException` (`0xc000027b`) crashes.
- **General & Testing**: Reuse existing project helpers, MVVM patterns (`CommunityToolkit.Mvvm`), and standard .NET/WinUI APIs before introducing custom abstractions. Inject `IMessenger` (defaulting to / registered as `WeakReferenceMessenger.Default`) rather than referencing `WeakReferenceMessenger.Default` directly in ViewModels so unit tests can run with isolated messenger instances. Ensure unit tests cover concurrency, coalescing, and end-to-end ViewModel messaging rather than only manually injected events.
