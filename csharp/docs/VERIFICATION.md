# Verification commands

Per-phase commands to verify completeness. Run from the repo root (`C:\Development\_github\dwarf`).

## Always-run (every session, before and after work)

```powershell
# Confirm branch
git branch --show-current        # expect: csharp-port

# Build everything
dotnet build csharp/Dwarf.slnx --nologo

# Run all tests
dotnet test csharp/Dwarf.slnx --nologo --verbosity quiet
```

If any of these fail before you start, **fix them first** — don't start new work on a broken tree.

## Phase 0 — Scaffolding

```powershell
# Solution structure
Get-ChildItem csharp -Directory | Select-Object Name
# Expect: 9 project dirs + docs

# Remotes
git remote -v
# Expect: origin = SixOThree/dwarf, upstream = devhawala/dwarf

# Branch tracking
git branch -vv
# Expect: csharp-port tracks origin/csharp-port

# Build empty solution
dotnet build csharp/Dwarf.slnx --nologo
# Expect: 9 projects, 0 warnings, 0 errors

# Test discovery
dotnet test csharp/Dwarf.slnx --nologo --verbosity quiet --no-build
# Expect: 0 tests run, 0 failed
```

## Phase A — Foundation

```powershell
# Build
dotnet build csharp/Dwarf.slnx --nologo
# Expect: green

# Tests should now be discovered (abstract base class + at least one concrete test class)
dotnet test csharp/Dwarf.slnx --nologo --verbosity quiet --filter FullyQualifiedName~Dwarf.Tests
# Expect: tests discovered, all expected to fail (no opcodes registered yet)
```

Specific checks:
- `Dwarf.Engine/PrincOpsDefs.cs` exists and has all constants from Java `PrincOpsDefs.java`
- `Dwarf.Engine/Cpu.cs` exists with register fields + stack array + trap methods
- `Dwarf.Engine/Mem.cs` exists with `Initialize(int virtBits, int realBits, ...)` method and `_mem` / `_pageMap` / `_pageFlags` arrays
- `Dwarf.Tests/AbstractInstructionTest.cs` exists as base fixture

## Phase B — Opcodes + tests (the fidelity gate)

```powershell
dotnet test csharp/Dwarf.slnx --nologo --verbosity quiet
# Expect: 608 tests passed, 0 failed
```

Sub-phase checks during Phase B:
```powershell
# After porting Ch05_StackInstructions
dotnet test csharp/Dwarf.slnx --filter FullyQualifiedName~Ch05 --verbosity quiet
# Expect: 193 tests passed
```

Performance baseline at end of Phase B (optional but recommended):
```powershell
# Once BenchmarkDotNet harness exists
dotnet run -c Release --project csharp/Dwarf.Tests --filter "*Dispatch*"
# Compare ns/op to a Java baseline run
```

## Phase C — Engine completeness

```powershell
dotnet test csharp/Dwarf.slnx --nologo --verbosity quiet
# Expect: 608 tests still passing (no regression from Phase B)
```

Plus a smoke run:
```powershell
# Once a minimal harness exists in Dwarf.Cli or Dwarf.Tests
dotnet run --project csharp/Dwarf.Cli -- --smoke-test
# Expect: loads base144.raw germ image; executes some thousand instructions; exits cleanly
```

## Phase D — Duchess agents

```powershell
dotnet test csharp/Dwarf.slnx --nologo --verbosity quiet
# Expect: 608 + new agent tests passing
```

Side-by-side disk integrity check:
```powershell
# Use the same canonical base disk; boot Pilot in Java Dwarf and C# Dwarf;
# diff the framebuffers at known checkpoints
java -jar dwarf.jar -duchess <config>      # Java (in a separate window)
dotnet run --project csharp/Dwarf.Cli -- -duchess <config>   # C#
```

NetHub round-trip:
```powershell
# Boot Java NetHub server; both Java + C# Dwarf instances connect;
# verify packets flow byte-identically (compare pcap captures)
```

## Phase E — Avalonia UI for Duchess

End-to-end smoke:
```powershell
dotnet run --project csharp/Dwarf.Cli -- -duchess <config>
# Expect: window appears; display renders; keyboard + mouse interactive;
# fullscreen toggle works; closing window cleanly shuts down engine
```

Rendering perf:
```powershell
# Open the running emulator with a busy display workload (XDE booting)
# Use Windows Performance Recorder or dotMemory to confirm UI thread isn't pegged
```

## Phase F — Draco port

```powershell
dotnet test csharp/Dwarf.slnx --nologo --verbosity quiet
# Expect: all tests passing (no regression)

# End-to-end
dotnet run --project csharp/Dwarf.Cli -- -draco <config>
# Expect: ViewPoint 2.0 or XDE 5.0 boots
```

## Phase G — Polish

```powershell
# CI pipeline
gh workflow run ci.yml
# Watch the run pass on Windows + Linux + macOS

# Performance regression baseline
dotnet run -c Release --project csharp/Dwarf.Benchmarks  # or similar
# Compare against Phase B baseline; target ≥100% of Java throughput
```

Migration tool docs:
- README in csharp/ explains how to run `java -jar dwarf.jar -merge <config>` once before switching to C#
- The Java JAR is shipped alongside the C# build artifact
