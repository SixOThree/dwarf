# Phase B — Opcodes + tests (the fidelity gate)

**Status**: Not started
**Estimated effort**: 3–4 weeks single-engineer FTE
**Predecessor**: Phase A (foundation)
**Successor**: Phase C (engine completeness)

## Context recap

Phase A put the engine bones in place (Mem, Cpu, test fixture). Phase B fills in the **256+ Mesa instruction implementations** and ports the **608 JUnit tests** that prove them correct.

**This phase IS the fidelity gate.** Until all 608 tests pass, no agent or UI work should start. A failing test is louder than a perfect-looking design — trust the tests.

The Java code uses reflection-based dispatch over a fixed list of 8 classes; the C# port replaces this with explicit `Opcodes.Install(...)` calls from each chapter's static constructor. See [DECISIONS.md §1](DECISIONS.md).

## Goals

1. `Opcodes.cs` provides `OpImpl` delegate, `_opcTable[256]`, `_escTable[256]`, `Dispatch(int opcode)`, and `Install(int opcode, string name, OpImpl impl)` / `InstallEsc(...)` registration helpers
2. Each Ch0X chapter class is ported with all opcode bodies and a static constructor that registers them
3. The `OPCo_` / `OPCn_` (PrincOps pre-/post-4.0 global-frame architecture) split is preserved via `InstallOld` / `InstallNew`, dispatched by which PrincOps version is selected
4. All 608 JUnit tests are ported to xUnit and **pass**
5. BenchmarkDotNet baseline established for `Cpu.Dispatch` hot loop (optional but recommended)

## Java files to read

### Dispatch + opcode bodies

| Java path | LOC | What it contains |
|---|---:|---|
| `engine/Opcodes.java` | 400 | Dispatch table, reflection-based registration (replace with explicit calls), prepare/post tables, `prepareOpcodeTables` and `initializeInstructions` |
| `engine/opcodes/Ch03_Memory_Organization.java` | 190 | Memory-organization instructions (memory fetch, store, page handling) |
| `engine/opcodes/Ch05_Stack_Instructions.java` | 820 | Stack/arithmetic/logical instructions — start here, has the most tests (193) |
| `engine/opcodes/Ch06_Jump_Instructions.java` | 320 | Branches, jumps, comparisons |
| `engine/opcodes/Ch07_Assignment_Instructions.java` | 1,187 | Assignment, memory reference, field operations — has 212 tests |
| `engine/opcodes/Ch08_Block_Transfers.java` | 1,731 | Block transfers, BITBLT-like operations |
| `engine/opcodes/Ch09_Control_Transfers.java` | 382 | Control transfers, frame management |
| `engine/opcodes/Ch10_Processes.java` | 227 | Process scheduling primitives |
| `engine/opcodes/ChXX_Undocumented.java` | 299 | Fuji Xerox undocumented instructions |

### Tests (port these in parallel with the opcodes)

| Java path | LOC | Test count | Pair with |
|---|---:|---:|---|
| `unittest/AbstractInstructionTest.java` | 539 | (fixture) | (already done in Phase A) |
| `unittest/Ch03_MemoryOrganizationTest.java` | 135 | ~25 | Ch03 |
| `unittest/Ch05_StackInsnsTest.java` | 1,609 | 193 | Ch05 |
| `unittest/Ch06_JumpInsnsTest.java` | 1,164 | ~45 | Ch06 |
| `unittest/Ch07_AssignInsnsTest.java` | 2,478 | 212 | Ch07 |
| `unittest/Ch08_BlockTrfInsnsTest.java` | 1,730 | ~85 | Ch08 |
| `unittest/Misc_Tests.java` | 219 | ~48 | (misc, port last) |

Chapters 9 & 10 have **no unit tests by design** (see `unittest/package-info.java` — they need a real OS to drive). Port the implementations but verify them indirectly via Phase C smoke run.

## C# files to create

| C# path | Source | Approx LOC |
|---|---|---:|
| `csharp/Dwarf.Engine/Opcodes/OpImpl.cs` | `delegate void OpImpl()` + `MesaTrap` integration | ~30 |
| `csharp/Dwarf.Engine/Opcodes/Opcodes.cs` | `Opcodes.java` dispatch + `Install` API | ~250 |
| `csharp/Dwarf.Engine/Opcodes/Ch03_MemoryOrganization.cs` | `Ch03_Memory_Organization.java` | ~200 |
| `csharp/Dwarf.Engine/Opcodes/Ch05_StackInstructions.cs` | `Ch05_Stack_Instructions.java` | ~850 |
| `csharp/Dwarf.Engine/Opcodes/Ch06_JumpInstructions.cs` | `Ch06_Jump_Instructions.java` | ~330 |
| `csharp/Dwarf.Engine/Opcodes/Ch07_AssignmentInstructions.cs` | `Ch07_Assignment_Instructions.java` | ~1,200 |
| `csharp/Dwarf.Engine/Opcodes/Ch08_BlockTransfers.cs` | `Ch08_Block_Transfers.java` | ~1,750 |
| `csharp/Dwarf.Engine/Opcodes/Ch09_ControlTransfers.cs` | `Ch09_Control_Transfers.java` | ~400 |
| `csharp/Dwarf.Engine/Opcodes/Ch10_Processes.cs` | `Ch10_Processes.java` | ~250 |
| `csharp/Dwarf.Engine/Opcodes/ChXX_Undocumented.cs` | `ChXX_Undocumented.java` | ~310 |
| `csharp/Dwarf.Tests/Ch03MemoryOrganizationTest.cs` | `Ch03_MemoryOrganizationTest.java` | ~150 |
| `csharp/Dwarf.Tests/Ch05StackInstructionsTest.cs` | `Ch05_StackInsnsTest.java` | ~1,700 |
| `csharp/Dwarf.Tests/Ch06JumpInstructionsTest.cs` | `Ch06_JumpInsnsTest.java` | ~1,250 |
| `csharp/Dwarf.Tests/Ch07AssignmentInstructionsTest.cs` | `Ch07_AssignInsnsTest.java` | ~2,600 |
| `csharp/Dwarf.Tests/Ch08BlockTransfersTest.cs` | `Ch08_BlockTrfInsnsTest.java` | ~1,800 |
| `csharp/Dwarf.Tests/MiscTests.cs` | `Misc_Tests.java` | ~250 |

## Implementation notes

### Opcode delegate

```csharp
public delegate void OpImpl();
```

Same shape as Java's `@FunctionalInterface OpImpl { void execute(); }`. Each opcode body becomes a lambda assigned to a `static readonly OpImpl` field, or — if you prefer fewer allocations — a static method whose method-group is converted to an `OpImpl` at registration.

### Naming convention

Java field name → C# field name:

| Java | C# |
|---|---|
| `OPC_xA2_REC` | `OPC_xA2_REC` (verbatim — preserves searchability against Java source) |
| `OPCo_x5D_RGILP_pair` | `OPCo_x5D_RGILP_pair` |
| `OPCn_x5D_RGILP_pair` | `OPCn_x5D_RGILP_pair` |
| `ESC_x24_BNDCKL` | `ESC_x24_BNDCKL` |

Keep the underscores and hex prefixes. The name parses cleanly: `<scope>_<x##>_<MNEMONIC>[_arglogspec]`. This is internal to chapter classes — public API surface is just `Dispatch(int opcode)`.

### Static-constructor registration

```csharp
public static partial class Ch05_StackInstructions
{
    public static readonly OpImpl OPC_xA2_REC = () => { Cpu.Recover(); };
    public static readonly OpImpl OPC_xA3_REC2 = () => { Cpu.Recover(); Cpu.Recover(); };
    // ... 100s more ...

    static Ch05_StackInstructions()
    {
        // PrincOps-version-agnostic opcodes
        Opcodes.Install(0xA2, "REC",  OPC_xA2_REC);
        Opcodes.Install(0xA3, "REC2", OPC_xA3_REC2);
        // ...
        // PrincOps pre-4.0 (old global-frame) variants
        Opcodes.InstallOld(0x5D, "RGILP", OPCo_x5D_RGILP_pair);
        // PrincOps post-4.0 (new global-frame) variants
        Opcodes.InstallNew(0x5D, "RGILP", OPCn_x5D_RGILP_pair);
        // ESC instructions
        Opcodes.InstallEsc(0x24, "BNDCKL", ESC_x24_BNDCKL);
    }
}
```

Triggering the static constructor happens via `RuntimeHelpers.RunClassConstructor(typeof(Ch05_StackInstructions).TypeHandle);` from `Opcodes.InitializeInstructionsPrincOps40()`. The chapter classes are *referenced* (via `typeof(...)`) so the .NET runtime knows to load them.

### Sign-extension audit checklist

Watch for these patterns in the Java source. Every occurrence is a place where the C# port can easily drift:

| Java | C# |
|---|---|
| `(short)(b \| 0xFFFFFF00)` | `(short)(sbyte)b` |
| `Cpu.pop() & 0xFFFF` | `Cpu.Pop()` (already `ushort`, no mask) |
| `(int)(value & 0xFFFFFFFFL)` | `(uint)value` |
| `signExtendByte(b)` | `(int)(sbyte)b` |
| `signExtendShort(s)` | `(int)(short)s` |

The `Cpu.Pop()` / `Cpu.Push(...)` API should use `ushort` for word-width and `uint` for long-width to make widening unambiguous. **Doing this consistently is the single biggest defense against R1 in RISKS.md.**

### Test conversion — JUnit → xUnit mechanical map

| JUnit | xUnit |
|---|---|
| `class Ch05Test extends AbstractInstructionTest { ... }` | `class Ch05Test : AbstractInstructionTest { ... }` |
| `@Test public void foo() { ... }` | `[Fact] public void Foo() { ... }` |
| `assertEquals(expected, actual)` | `Assert.Equal(expected, actual)` |
| `assertTrue(b)` | `Assert.True(b)` |
| `fail("msg")` | `Assert.Fail("msg")` |
| `@Test(expected = T.class)` | wrap in `Assert.Throws<T>(() => ...)` |

Use ripgrep / sed to do the first-pass conversion, then hand-fix. **Don't auto-rename test method names** to PascalCase — keep them verbatim for searchability against the Java source.

### Order of work within Phase B

1. **Opcodes dispatch + Install API** (lay the table)
2. **Ch03** — smallest, gets you over the "first chapter ported" hump
3. **Ch05 + Ch05Test** — biggest test count (193), validates the sign-extension audit early
4. **Ch07 + Ch07Test** — biggest opcode set, big test count (212)
5. **Ch06 + Ch06Test**
6. **Ch08 + Ch08Test** — block transfers, large and tricky
7. **Ch09**, **Ch10**, **ChXX** — no unit tests; port carefully and rely on Phase C smoke
8. **MiscTests** — port last

For each chapter, follow strict TDD: port the test class first, watch it fail, port the implementation, watch it pass. The `superpowers:test-driven-development` skill applies here.

## Verification

```powershell
# After Ch03 ported
dotnet test csharp/Dwarf.slnx --filter FullyQualifiedName~Ch03 --verbosity quiet
# Expect: ~25 tests passed

# After Ch05 ported
dotnet test csharp/Dwarf.slnx --filter FullyQualifiedName~Ch05 --verbosity quiet
# Expect: 193 tests passed

# End-of-phase: full suite
dotnet test csharp/Dwarf.slnx --nologo
# Expect: 608 tests passed, 0 failed
```

Performance baseline (optional but valuable):

```powershell
# Once you wire BenchmarkDotNet
dotnet run -c Release --project csharp/Dwarf.Tests --filter "*Dispatch*"
# Capture ns/op; this becomes the baseline for Phase G perf tuning
```

## Sub-tasks

- [ ] Implement `OpImpl` delegate + `Opcodes.cs` dispatch + Install API
- [ ] Wire `RuntimeHelpers.RunClassConstructor` for each chapter class
- [ ] Port `Ch03_MemoryOrganization` + tests (~25 tests)
- [ ] Port `Ch05_StackInstructions` + tests (193 tests)
- [ ] Port `Ch07_AssignmentInstructions` + tests (212 tests)
- [ ] Port `Ch06_JumpInstructions` + tests (~45 tests)
- [ ] Port `Ch08_BlockTransfers` + tests (~85 tests)
- [ ] Port `Ch09_ControlTransfers` (no tests)
- [ ] Port `Ch10_Processes` (no tests)
- [ ] Port `ChXX_Undocumented` (no tests)
- [ ] Port `MiscTests` (~48 tests)
- [ ] All 608 tests green
- [ ] (Optional) BenchmarkDotNet baseline captured
- [ ] Commit: `feat(engine): Phase B — 256+ Mesa opcodes ported, 608 unit tests passing`

## Hand-off

When this phase is done:
1. Run `dotnet test csharp/Dwarf.slnx --nologo` and **paste the test count into PROGRESS.md** — this is the fidelity gate; future sessions must trust this number
2. Tick every sub-task above
3. Tick Phase B in PROGRESS.md top-level; set `Current phase: C`
4. If BenchmarkDotNet was run, record the baseline ns/op figures in a `csharp/docs/benchmarks/phase-b-baseline.md` file
5. Commit `chore(docs): close Phase B, start Phase C`
