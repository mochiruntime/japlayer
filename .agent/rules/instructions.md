---
trigger: always_on
---

# Building and formatting

After making changes to the codebase:

1.  Run `dotnet build` in the solution root after any significant code modification. This verifies that the changes do not break the build.
    ```bash
    dotnet build
    ```

2.  After a successful build, run `dotnet format` to ensure that the code adheres to the project's style guidelines as defined in `.editorconfig`.
    ```bash
    dotnet format
    ```

Always perform these steps before submitting or concluding a task.
