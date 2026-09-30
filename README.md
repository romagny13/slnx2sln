# slnx2sln

A simple .NET tool that converts between modern `.slnx` and classic `.sln` solution files.

## Installation

### From NuGet.org

```bash
dotnet tool install --global slnx2sln
```

### From local package

```bash
dotnet tool install --global --add-source ./bin/Release slnx2sln
```

## Update

### From NuGet.org

```bash
dotnet tool update --global slnx2sln
```

### From local package

```bash
dotnet tool update --global --add-source ./bin/Release slnx2sln
```

## Uninstall

```bash
dotnet tool uninstall --global slnx2sln
```

## Usage

```bash
slnx2sln <file.slnx> [file.sln]          # Convert .slnx → .sln
slnx2sln <file.sln>  [file.slnx]         # Convert .sln  → .slnx
slnx2sln sync                            # Auto-sync the solution in the current directory (newer wins)
slnx2sln sync <file.sln|.slnx>           # Sync pair (newer wins)
slnx2sln sync <file.sln> <file.slnx>
```

### Examples

```bash
# .slnx → .sln
slnx2sln MySolution.slnx
slnx2sln MySolution.slnx Output.sln

# .sln → .slnx
slnx2sln MySolution.sln
slnx2sln MySolution.sln Output.slnx

# Sync (the newer file updates the older one)
slnx2sln sync MySolution.slnx
slnx2sln sync MySolution.sln
slnx2sln sync MySolution.sln MySolution.slnx

# Auto-sync: no file name needed
cd path/to/my/repo
slnx2sln sync
```

If no output file is specified, the tool uses the same name with the target extension.

### Auto-sync (`slnx2sln sync`)

When `sync` is called without any file name, the tool looks for `.sln` and `.slnx` files in the **current directory** (non-recursive):

- If a `.sln` / `.slnx` pair with the same name exists, the newer file overwrites the older one.
- If only one of the two exists (e.g. only `MySolution.slnx`), the other one is generated.
- If no solution file is found, the command fails.
- If several solutions with **different names** are found (e.g. `A.sln` and `B.slnx`), the command fails and asks you to specify the file explicitly, e.g. `slnx2sln sync A.sln`.

The explicit forms (`slnx2sln sync <file>` and `slnx2sln sync <file.sln> <file.slnx>`) keep working as before.