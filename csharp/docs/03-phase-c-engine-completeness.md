# Phase C — Engine completeness

**Status**: Complete (closed 2026-05-12 — 629 tests passing)
**Estimated effort**: 1–2 weeks single-engineer FTE
**Predecessor**: Phase B (608 tests passing)
**Successor**: Phase D (Duchess agents)

## Context recap

Phase B locked in instruction fidelity. Phase C ports the remaining engine pieces — control transfers between procedures (`Xfer`), the process scheduler (`Processes`), and the boot-time microcode loader (`InitialMesaMicrocode`). After Phase C the C# engine can boot a germ image and execute Mesa code; tests still pass; agents are not yet wired.

## Goals

1. `Xfer` provides control-transfer operations (frame allocation, return links, fault frames)
2. `Processes` provides process scheduling primitives (ready queue, monitors, condition variables)
3. `InitialMesaMicrocode` loads the boot germ (`base144.raw`) into Mesa memory and sets up initial register state
4. A smoke test boots the germ and runs the first thousands of instructions without trap explosion
5. All 608 Phase B tests still pass (no regression)

## Java files to read

| Java path | LOC | What it contains |
|---|---:|---|
| `engine/Xfer.java` | 597 | Control transfer (`XferType` enum, `procXfer`, `returnXfer`, frame setup, fault frame logic) |
| `engine/Processes.java` | 1,065 | PSB array, ready/wait queues, monitor entry/exit, condition wait/notify, timeout handling |
| `engine/InitialMesaMicrocode.java` | 412 | Loads `base144.raw` germ image (BCD format), patches initial Mesa state, hands control to Cpu |

Resource:
- `src/resources/base144.raw` (1.5 MiB binary, 3.5" floppy template) — needs to be embedded as a resource in `Dwarf.Engine`

## C# files to create

| C# path | Source | Approx LOC |
|---|---|---:|
| `csharp/Dwarf.Engine/Xfer.cs` | `Xfer.java` | ~620 |
| `csharp/Dwarf.Engine/Processes.cs` | `Processes.java` | ~1,100 |
| `csharp/Dwarf.Engine/InitialMesaMicrocode.cs` | `InitialMesaMicrocode.java` | ~430 |
| `csharp/Dwarf.Engine/Resources/base144.raw` | Copy from `src/resources/base144.raw`; mark as `EmbeddedResource` in csproj | (binary) |
| `csharp/Dwarf.Engine/Resources.cs` | Resource loading helpers (`Resources.LoadGermImage()` returns `byte[]`) | ~30 |
| `csharp/Dwarf.Tests/SmokeTests.cs` | Boots germ image; asserts engine runs ≥10k instructions without uncaught trap | ~80 |

## Implementation notes

### Embedded resource for `base144.raw`

In `Dwarf.Engine.csproj`, add:

```xml
<ItemGroup>
  <EmbeddedResource Include="Resources/base144.raw" />
</ItemGroup>
```

Then load via `typeof(Resources).Assembly.GetManifestResourceStream("Dwarf.Engine.Resources.base144.raw")`.

Copy from the Java tree:

```powershell
Copy-Item src/resources/base144.raw csharp/Dwarf.Engine/Resources/base144.raw
```

### Processes scheduling — keep single-threaded

The Java `Processes` class implements Mesa's *cooperative* multi-process scheduler within the single emulator thread. **Do not** introduce real C# Tasks or threads — that would change semantics. Mesa processes are scheduled by Mesa instructions (`MW`, `SP`, `RR`, etc.); the C# port simulates this on top of the same single emulator thread.

### Xfer fault frames

Mesa control transfers can fault during execution (e.g., page fault during frame allocation). The Java code uses Java exceptions (the `MesaTrap` mechanism from Phase A) to unwind cleanly. C# port uses the same exception-based control flow. **Test trap paths explicitly** — the Phase B tests don't cover Xfer faults.

### Smoke test

The smoke test in `SmokeTests.cs` should:

1. `Mem.Initialize(24, 22, DisplayType.Monochrome, 1024, 768)`
2. `Cpu.Initialize()`
3. `Opcodes.InitializeInstructionsPrincOps40()` (or post-40 depending on which germ)
4. `InitialMesaMicrocode.LoadGermImage(Resources.LoadGermImage())`
5. Run a fixed number of dispatch cycles (say 10,000), expecting no uncaught trap

This isn't a *correctness* test (we don't know what the germ should produce), it's a *liveness* test — proves the engine can execute meaningful code without exploding.

## Verification

```powershell
# Regression: Phase B tests still green
dotnet test csharp/Dwarf.slnx --filter "FullyQualifiedName~Ch0" --nologo --verbosity quiet
# Expect: 608 tests passed

# Smoke test
dotnet test csharp/Dwarf.slnx --filter "FullyQualifiedName~SmokeTests" --nologo --verbosity quiet
# Expect: smoke test passes

# Full suite
dotnet test csharp/Dwarf.slnx --nologo
# Expect: 608 + new smoke tests passed, 0 failed

# Resource confirmation
dotnet build csharp/Dwarf.slnx --verbosity normal | Select-String 'base144.raw'
# Expect: log line showing the resource was embedded
```

## Sub-tasks

- [x] Copy `base144.raw` into `Dwarf.Engine/Resources/` and wire as `EmbeddedResource`
- [x] Port `Xfer.java` → `csharp/Dwarf.Engine/Xfer.cs`
- [x] Port `Processes.java` → `csharp/Dwarf.Engine/Processes.cs`
- [x] Port `InitialMesaMicrocode.java` → `csharp/Dwarf.Engine/InitialMesaMicrocode.cs`
- [x] Write `SmokeTests.cs` — 11 liveness tests (see PROGRESS.md for rationale on the scope vs the original "boot germ, run 10k" plan)
- [x] Verify: 618 Phase B tests + 11 smoke tests pass (629 total)
- [x] Commit: `feat(engine): Phase C — Xfer, Processes, InitialMesaMicrocode; germ image loads`

## Hand-off

Phase C closed. Active phase is D (Duchess agents). See PROGRESS.md "2026-05-12 (Phase C close-out)" for the detailed session log.
