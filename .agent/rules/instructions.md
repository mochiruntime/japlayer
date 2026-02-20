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
    dotnet format
    ```

Always perform these steps before submitting or concluding a task.