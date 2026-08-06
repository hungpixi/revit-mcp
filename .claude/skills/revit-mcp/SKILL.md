```markdown
# revit-mcp Development Patterns

> Auto-generated skill from repository analysis

## Overview
This skill covers the core development conventions and workflows used in the `revit-mcp` repository, a C# codebase with no detected framework. It documents file organization, code style, import/export patterns, and testing approaches to help contributors maintain consistency and quality.

## Coding Conventions

### File Naming
- **PascalCase** is used for all file names.
  - **Example:** `MyClass.cs`, `ProjectUtilities.cs`

### Import Style
- **Mixed imports** are used, meaning both `using` statements at the top and inline imports may appear.
  - **Example:**
    ```csharp
    using System;
    using Autodesk.Revit.DB;
    ```

### Export Style
- **Named exports** are used, where classes and methods are explicitly declared and exported.
  - **Example:**
    ```csharp
    public class WallCreator
    {
        public void CreateWall(Document doc) { ... }
    }
    ```

### General Code Style
- Classes, methods, and properties use **PascalCase**.
- Variables use **camelCase**.
- Indentation is typically 4 spaces.

## Workflows

### Adding a New Feature
**Trigger:** When implementing a new feature or module.
**Command:** `/add-feature`

1. Create a new `.cs` file using PascalCase for the feature name.
2. Add necessary `using` statements at the top.
3. Implement the feature as a public class with named exports.
4. Write or update corresponding test files (`*.test.*`).
5. Commit changes with a clear, descriptive message.

### Fixing a Bug
**Trigger:** When resolving a bug or issue.
**Command:** `/fix-bug`

1. Locate the relevant file(s) using PascalCase naming.
2. Make the necessary code changes, following code style conventions.
3. Update or add test cases to cover the fix.
4. Commit with a message describing the bug and the fix.

### Writing Tests
**Trigger:** When verifying new or existing functionality.
**Command:** `/write-test`

1. Create or update a test file matching the pattern `*.test.*`.
2. Write test methods using descriptive method names.
3. Follow the project's code style and import conventions.
4. Run tests to ensure correctness.

## Testing Patterns

- **Framework:** Unknown (no specific testing framework detected).
- **File Pattern:** Test files follow the `*.test.*` naming convention.
  - **Example:** `WallCreator.test.cs`
- Test methods are typically named to describe the scenario being tested.
- Tests should be updated or added whenever new features or bug fixes are implemented.

## Commands
| Command       | Purpose                                 |
|---------------|-----------------------------------------|
| /add-feature  | Scaffold and implement a new feature    |
| /fix-bug      | Apply and document a bug fix            |
| /write-test   | Add or update tests for code changes    |
```
