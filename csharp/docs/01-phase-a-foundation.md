# Phase A — Foundation

**Status**: Not started (active phase after Phase 0)
**Estimated effort**: 1–2 weeks single-engineer FTE
**Predecessor**: Phase 0 (scaffolding)
**Successor**: Phase B (opcodes + tests)

## Context recap

The C# port has bones (9 projects, references wired). Nothing inside the projects yet besides the `Class1.cs` and `Program.cs` stubs that `dotnet new` creates. Phase A puts the **non-opcode** parts of the Mesa engine in place — pure data definitions (`PrincOpsDefs`, `PilotDefs`, `Config`), memory model skeleton (`Mem`), CPU state skeleton (`Cpu`), and the xUnit test fixture base. After Phase A, the project compiles cleanly and xUnit discovers tests, but the tests all fail predictably because no opcodes exist yet.

The 608-test fidelity gate in Phase B requires a working test harness here.

## Goals

1. All Mesa data constants are available in C# (no magic numbers in opcode code later)
2. `Mem.Initialize(...)` allocates the memory backing store, page map, page flags; provides word-level read/write accessors that perform page-fault and protection checks
3. `Cpu` exposes the register fields (`MDS`, `GF`, `LF`, `CB`, `PC`, `SP`, evaluation stack array), trap methods (`OpcodeTrap`, `EscOpcodeTrap`, `PointerTrap`, `BoundsTrap`, etc.), and basic push/pop helpers
4. `AbstractInstructionTest` (xUnit) replicates the Java fixture: initializes Mem + Cpu, captures faults/traps via a chk-thrower, exposes assertion helpers
5. Solution builds clean; xUnit discovers tests; tests all fail predictably (no opcode dispatch table yet)

## Java files to read

| Java path | LOC | Read order |
|---|---:|---|
| `src/dev/hawala/dmachine/engine/PrincOpsDefs.java` | 215 | 1st — pure constants, sets vocabulary |
| `src/dev/hawala/dmachine/engine/PilotDefs.java` | 93 | 2nd — pure constants (DisplayType etc.) |
| `src/dev/hawala/dmachine/engine/Config.java` | 112 | 3rd — debug/logging flags |
| `src/dev/hawala/dmachine/engine/Mem.java` | 1,223 | 4th — focus on the skeleton: lines 1–250 (init), 599–700 (accessors). Skip the IOP-region helpers; those land in Phase F |
| `src/dev/hawala/dmachine/engine/Cpu.java` | 1,739 | 5th — focus on lines 1–300 (registers + traps), 300–500 (stack push/pop), 500–800 (logging). Skip dispatch loop (Phase B) |
| `src/dev/hawala/dmachine/unittest/AbstractInstructionTest.java` | 539 | 6th — the test fixture (Java `@Before`, `ChkThrower`, assertion helpers) |

## C# files to create

| C# path | Responsibility | Approx LOC |
|---|---|---:|
| `csharp/Dwarf.Engine/PrincOpsDefs.cs` | Pure constants: `PAGES_PER_SEGMENT`, `WORDS_PER_PAGE`, `ADDRESSBITS_IN_PAGE`, `MAPFLAGS_*`, etc. Static class. | ~200 |
| `csharp/Dwarf.Engine/PilotDefs.cs` | `DisplayType` enum + Pilot OS constants | ~80 |
| `csharp/Dwarf.Engine/Config.cs` | Static config flags (`LOG_OPCODES`, `FLIGHTRECORDER_WITH_STACK`, etc.) — keep as static fields, settable from CLI | ~110 |
| `csharp/Dwarf.Engine/Mem.cs` | `static class Mem`; `Initialize(int virtBits, int realBits, DisplayType, int w, int h)`; `_mem`, `_pageMap`, `_pageFlags`; `GetByte`, `PutByte`, `ReadWord`, `WriteWord`, `ReadInt32`, `WriteInt32`; `GetRealAddress(int lp, bool forWrite)`; page-fault routing to `Cpu`. **Skeleton only — opcode-specific helpers come in Phase B.** | ~350 |
| `csharp/Dwarf.Engine/Cpu.cs` | `static class Cpu`; register fields (`MDS`, `GF16`, `GF32`, `LF`, `CB`, `PC`, `SP`); `_stack` array; `Push(ushort)`, `Pop()`, `PushLong(int)`, `PopLong()`; trap methods (`OpcodeTrap`, `EscOpcodeTrap`, `PointerTrap`, `BoundsTrap`, `SignalPageFault`); logging helpers (`LogOpcode`, `LogOpcode_alpha`, etc. — bodies can be stubs for now); `FaultTrapThrower` abstraction. **Skeleton only — dispatch loop comes in Phase B.** | ~600 |
| `csharp/Dwarf.Tests/AbstractInstructionTest.cs` | xUnit fixture: `ChkThrower` (custom `FaultTrapThrower` that records traps); `[SetUp]`-equivalent via constructor; `AssertStackTop`, `AssertNoTrap`, etc. | ~500 |

Also delete the stub `Class1.cs` files in the affected projects (`Dwarf.Engine`, `Dwarf.Tests`).

## Implementation notes

### Static init order

C# is stricter about static-init dependencies than Java. **Do not** rely on static-field initializers in `Cpu` and `Mem` to reference each other implicitly. Instead, define an explicit boot sequence:

```csharp
PrincOpsDefs.<consts loaded automatically>;
PilotDefs.<consts loaded automatically>;
Config.LoadFromCommandLine(args);   // or whatever
Mem.Initialize(virtBits: 24, realBits: 22, DisplayType.Monochrome, 1024, 768);
Cpu.Initialize();                   // resets register state, registers trap thrower
// Phase B: Opcodes.Initialize(princOpsVersion: PrincOpsVersion.V40);
```

This is referenced in [DECISIONS.md §5](DECISIONS.md).

### Memory backing arrays

```csharp
internal static ushort[] _mem = null!;
internal static int[] _pageMap = null!;
internal static ushort[] _pageFlags = null!;
```

The `!` is a deliberate null-forgiveness — these are populated in `Initialize`. Make `Initialize` idempotent-or-throw (Java throws `IllegalStateException` if called twice; do the same).

### Trap methods

Java throws control flow via `Cpu.signalPageFault(lp)` etc. — these methods throw a `MesaTrap` runtime exception (the `RealMesaFaultTrapThrower`). C# port can use the same pattern: `MesaTrap : Exception` with a discriminator field for the trap kind. Each trap method does:

```csharp
public static void PointerTrap()
{
    throw new MesaTrap(MesaTrapKind.Pointer);
}
```

The interpreter loop (Phase B) catches `MesaTrap` and dispatches to the appropriate trap handler.

### xUnit fixture differences from JUnit

| Java JUnit 4 | C# xUnit |
|---|---|
| `@Before` method | constructor |
| `@After` method | `IDisposable.Dispose` |
| `@Test` method | `[Fact]` (or `[Theory]` + `[InlineData]`) |
| `assertEquals(a, b)` | `Assert.Equal(a, b)` |
| `fail("msg")` | `Assert.Fail("msg")` (xUnit ≥ 2.5.3) |
| `expected = Exception.class` | `Assert.Throws<T>(() => ...)` |

For test classes that share heavy setup (allocating Mem), use xUnit's `IClassFixture<T>` pattern to share fixture instances per class. **Do not** make Mem a singleton that bleeds state across tests — re-initialize per test.

### BSD-3 license header

Every new `.cs` file gets the BSD-3 header (see `csharp/BSD3-HEADER.txt`). Add it at the top of each new file. The header preserves Dr. Hans-Walter Latz's copyright and adds the port copyright.

## Verification

```powershell
# Build
dotnet build csharp/Dwarf.slnx --nologo
# Expect: green, 0 warnings, 0 errors

# Test discovery
dotnet test csharp/Dwarf.slnx --nologo --verbosity quiet --no-build
# Expect: tests discovered (the AbstractInstructionTest base + at least one stub concrete class
#         in Dwarf.Tests/Ch03Test_Skeleton.cs that just verifies fixture init).
#         Tests can FAIL predictably with "no opcodes registered" — that's the Phase B work.

# License headers
Get-ChildItem csharp/Dwarf.Engine, csharp/Dwarf.Tests -Filter *.cs -Recurse `
    | Where-Object { -not (Select-String -Path $_.FullName -Pattern 'BSD' -Quiet) } `
    | ForEach-Object FullName
# Expect: empty output (all new files have a BSD header)
```

## Sub-tasks

- [ ] Port `PrincOpsDefs.java` → `csharp/Dwarf.Engine/PrincOpsDefs.cs`
- [ ] Port `PilotDefs.java` → `csharp/Dwarf.Engine/PilotDefs.cs`
- [ ] Port `Config.java` → `csharp/Dwarf.Engine/Config.cs`
- [ ] Port `Mem.java` skeleton → `csharp/Dwarf.Engine/Mem.cs` (init + accessors only)
- [ ] Port `Cpu.java` skeleton → `csharp/Dwarf.Engine/Cpu.cs` (registers + stack + traps)
- [ ] Define `MesaTrap : Exception` + `MesaTrapKind` enum
- [ ] Port `AbstractInstructionTest.java` → `csharp/Dwarf.Tests/AbstractInstructionTest.cs`
- [ ] Add one stub concrete test (e.g., `Ch03Test_Skeleton.cs`) that verifies the fixture works
- [ ] Delete the `Class1.cs` stubs in affected projects
- [ ] Verify build + test discovery per "Verification" section
- [ ] Commit: `feat(engine): Phase A — port PrincOpsDefs, PilotDefs, Config, Mem/Cpu skeletons, xUnit fixture`

## Hand-off

When this phase is done:
1. Tick every sub-task box above
2. Run all verification commands; paste results into the bottom of PROGRESS.md under a "Phase A close-out" note
3. In PROGRESS.md, tick Phase A in the top-level checklist, set `Current phase: B`, copy Phase B's sub-task list into the active section
4. Commit `chore(docs): close Phase A, start Phase B`
5. The next session opens `02-phase-b-opcodes.md` and begins porting the 256+ opcode bodies and 608 tests
