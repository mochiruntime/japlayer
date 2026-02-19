# Agent Instructions

When making changes to the codebase, always ensure quality and consistency by following these steps:

1.  **Check for Compile-Time Correctness**:
    Run `dotnet build` in the solution root after any significant code modification. This verifies that the changes do not break the build.
    ```bash
    dotnet build
    ```

2.  **Format Code**:
    After a successful build, run `dotnet format` to ensure that the code adheres to the project's style guidelines as defined in `.editorconfig`.
    ```bash
    dotnet format
    ```

Always perform these steps before submitting or concluding a task.
