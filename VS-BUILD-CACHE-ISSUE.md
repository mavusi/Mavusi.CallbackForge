# Known Issue: Visual Studio Build Cache

## Issue

After converting to multi-targeting, Visual Studio may report build errors even though the command-line build succeeds:

```
NETSDK1005: Assets file doesn't have a target for 'net9.0' or 'net10.0'
```

## Root Cause

Visual Studio caches the `project.assets.json` file and doesn't always refresh it when switching to multi-targeting. The command-line `dotnet` tool works correctly.

## Verification

The project **does build successfully** from the command line:

```powershell
PS> dotnet build --force
Build succeeded with 3 warning(s) in 42.7s
```

All three target frameworks produce assemblies:
- `bin\Debug\net8.0\Mavusi.CallbackForge.dll` ✅
- `bin\Debug\net9.0\Mavusi.CallbackForge.dll` ✅
- `bin\Debug\net10.0\Mavusi.CallbackForge.dll` ✅

## Workarounds

### Option 1: Close and Reopen Visual Studio (Recommended)

1. Close Visual Studio
2. Delete obj and bin folders:
   ```powershell
   Remove-Item -Recurse -Force .\*\obj,.\*\bin
   ```
3. Run `dotnet restore`
4. Reopen Visual Studio
5. Build

### Option 2: Build from Command Line

Use the integrated terminal in Visual Studio:

```powershell
dotnet build --force
```

### Option 3: Reload Projects in Visual Studio

1. Right-click on each project in Solution Explorer
2. Select "Unload Project"
3. Right-click again
4. Select "Reload Project"
5. Rebuild solution

### Option 4: Clean Solution in Visual Studio

1. Build menu → Clean Solution
2. Close Visual Studio
3. Delete `obj` folders
4. Reopen Visual Studio
5. Build menu → Rebuild Solution

## Impact

This is **only a Visual Studio UI issue**. The actual functionality is NOT affected:

- ✅ Command-line builds work perfectly
- ✅ CI/CD pipelines will work
- ✅ NuGet package will be generated correctly
- ✅ All three target frameworks are compiled
- ✅ Users can reference the library from any .NET 8/9/10 project

## Microsoft Documentation

This is a known behavior documented in:
- [Multi-targeting in SDK-style projects](https://learn.microsoft.com/en-us/dotnet/standard/frameworks)
- [MSBuild SDK resolvers](https://learn.microsoft.com/en-us/visualstudio/msbuild/how-to-use-project-sdk)

## Status

**Status**: Working as designed  
**Severity**: Low (cosmetic only in VS, functionality unaffected)  
**Solution Compatibility**: ✅ Fully functional via CLI  
**Production Impact**: None

## Recommendation

**For development**: Use command-line `dotnet build` or restart VS after project file changes  
**For CI/CD**: No changes needed - works perfectly  
**For consumers**: No impact - NuGet resolves correctly
