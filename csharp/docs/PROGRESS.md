# Dwarf C# Port — Progress

**Current phase**: B (Opcodes + tests)
**Started**: 2026-05-12
**Last session**: 2026-05-12 (in-progress: Ch03 + Ch05 + Ch07 done — 416/608 tests passing; Ch06/Ch08/Ch09/Ch10/ChXX/Misc still pending)

## Phase status

- [x] **Phase 0**: Scaffolding + per-phase docs
- [x] **Phase A**: Foundation
- [ ] **Phase B**: Opcodes + tests          ← active
- [ ] **Phase C**: Engine completeness
- [ ] **Phase D**: Duchess agents
- [ ] **Phase E**: Avalonia UI for Duchess
- [ ] **Phase F**: Draco port
- [ ] **Phase G**: Polish

## Phase B sub-tasks (active)

See `02-phase-b-opcodes.md` for details.

- [x] Implement `OpImpl` delegate + `Opcodes.cs` dispatch + Install API
- [x] ~~Wire `RuntimeHelpers.RunClassConstructor` for each chapter class~~ (superseded — chapter classes expose explicit `RegisterAll()` static methods called from `Opcodes.initializeInstructionsPrincOps*40()`; cleaner than static-ctor + RunClassConstructor, and supports mode-switching)
- [x] Port `Processes` stub (just the helpers Ch03 needs: `psbHandle`, `psbIndex`, `resetPTC`)
- [x] Port `Ch03_Memory_Organization` (19 opcodes — 1 regular, 18 ESC)
- [x] Port `Ch03_MemoryOrganizationTest` (1 test method, loops ~1023 sub-iterations)
- [x] Add `OpcodeDispatchSmokeTest` (5 tests verifying dispatch pipeline end-to-end)
- [x] Port `Ch05_Stack_Instructions` + tests (193 tests — all pass first try)
- [x] Port `Ch07_Assignment_Instructions` + tests (212 tests — all pass first try)
- [ ] Port `Ch06_Jump_Instructions` + tests (~45 tests)          ← next
- [ ] Port `Ch08_BlockTransfers` + tests (~85 tests)
- [ ] Port `Ch09_ControlTransfers` (no tests)
- [ ] Port `Ch10_Processes` (no tests)
- [ ] Port `ChXX_Undocumented` (no tests)
- [ ] Port `MiscTests` (~48 tests)
- [ ] All 608 tests green
- [ ] (Optional) BenchmarkDotNet baseline captured

## Phase A sub-tasks (closed for reference)

- [x] Port `PrincOpsDefs.java` → `csharp/Dwarf.Engine/PrincOpsDefs.cs` (215 LOC)
- [x] Port `PilotDefs.java` → `csharp/Dwarf.Engine/PilotDefs.cs` (93 LOC + DisplayType sealed class)
- [x] Port `Config.java` → `csharp/Dwarf.Engine/Config.cs` (112 LOC, `const bool` for JIT DCE)
- [x] Define `MesaTrap` + `MesaTrapKind` (placeholder for trap unwind currency)
- [x] Define `AtomicInteger` wrapper (drop-in for `java.util.concurrent.atomic.AtomicInteger`)
- [x] Port `Mem.java` → `csharp/Dwarf.Engine/Mem.cs` (full memory layer; IORegion logging omitted for Phase F)
- [x] Port `Cpu.java` skeleton → `csharp/Dwarf.Engine/Cpu.cs` (registers, traps, stack, fault thrower abstraction)
- [x] Port `AbstractInstructionTest.java` → `csharp/Dwarf.Tests/AbstractInstructionTest.cs`
- [x] Add `TestAssemblyInfo.cs` to disable parallel test execution (engine has static state)
- [x] Add `FixtureSmokeTest.cs` — 5 smoke tests verifying the fixture wiring
- [x] Delete `Class1.cs` and `UnitTest1.cs` template stubs
- [x] Verify: `dotnet build` green, all 5 smoke tests pass

## Phase 0 sub-tasks (closed for reference)

- [x] 0a. Repoint git remotes (`origin` → SixOThree/dwarf, `upstream` → devhawala/dwarf)
- [x] 0a. Create and push `csharp-port` branch
- [x] 0b. Create `csharp/` + `Dwarf.slnx` + 9 projects
- [x] 0b. Wire project reference graph (15 references)
- [x] 0b. Add `Directory.Build.props` (nullable, implicit usings, warnings-as-errors)
- [x] 0b. Verify `dotnet build` succeeds — 0 warnings, 0 errors
- [x] 0c. Create 13 docs under `csharp/docs/`
- [x] 0d. Update root `.gitignore`, add `csharp/.editorconfig` + `csharp/BSD3-HEADER.txt`
- [x] 0e. Initial commit + push to `origin/csharp-port`

## Notes from recent sessions

(Append session-end notes here — discoveries, deviations from plan, things future sessions should know.)

### 2026-05-12 (Phase 0 kickoff)

- Confirmed `dotnet --version` 10.0.202 installed
- `.slnx` (XML solution format) replaces `.sln` in .NET 9+ — that's what `dotnet new sln` produces now
- The 7 pre-existing dirty files (`.project`, `.serena/`, `CLAUDE.md`) are user state, **not** port-related — they were excluded from the Phase 0 commit
- Initial reference graph: UI.Avalonia depends only on Engine (not Agents/Iop6085) — Duchess/Draco wire their agent sources to UI interfaces, mirroring Java's `iUiDataConsumer` boundary

### 2026-05-12 (Phase A close-out)

- **Naming convention decision**: preserve Java identifier names *verbatim* (constants, methods, fields). This violates .NET PascalCase norms but maximizes grep-ability with the Java source — across a 14-21-week port that diff-ability is worth more than convention. `.editorconfig` silences IDE1006 and CA1707/CA1051/CA2211/CA1710/CA1715/CA1716/CA1720/CA1309/CA1805/CA1859/CS0162.
- **Memory backing arrays**: `ushort[]` (vs Java `short[]`). The `& 0xFFFF` masking ceremony that pervades the Java opcodes will disappear in the C# port. Sign-extension is explicit at the call site via `(short)Cpu.pop()`.
- **Evaluation stack** is also `ushort[]` (consistent with `Mem`). `Cpu.push` / `Cpu.pop` use `ushort`.
- **`MesaTrap`** is the new exception type for trap unwind. `RealMesaFaultTrapThrower` currently throws `MesaERROR("requires Xfer — Phase C")` placeholders; tests install `ChkThrower` which records the expected trap kind without throwing the Phase C error.
- **`Cpu.MesaERROR`, `MesaStopped`, `MesaAbort`** are nested classes inside `Cpu` (mirroring Java). They live alongside the standalone `MesaTrap` enum.
- **IORegion logging blocks** inside `Mem._readLpWord` / `_writeLpWord` / `_readLengthenedMDSWord` / `_writeLengthenedMDSWord` are intentionally omitted. They land in Phase F when `IORegion` exists. Since `Config.IOR_LOG_MEM_ACCESS = false` is a const, dead-code elimination would have removed them anyway.
- **xUnit parallel execution disabled** via `[assembly: CollectionBehavior(DisableTestParallelization = true)]` in `TestAssemblyInfo.cs`. The engine has heavy static state — parallel execution would race.
- **`Cpu.initialize()`** is `NotImplementedException` for Phase A — it needs `Xfer.impl.xfer(bootLink, 0, XferType.xcall, false)` which is Phase C.
- **PilotDefs.DisplayType** is a sealed class with static instances (not a C# `enum`) so that callers can write `DisplayType.monochrome.bitDepth` identically to the Java code.
- **`AbstractInstructionTest.mkStack` typo preserved**: the Java code has a bug at line 283 (`Cpu.SP = newSavedSP` instead of `Cpu.savedSP = newSavedSP`). Kept for behavioral identity — the 608 Phase B tests must behave exactly as upstream.
- **`internalIT()`** for the interval timer uses `Environment.TickCount64 * 1_000_000` as a stand-in for Java's `System.nanoTime() & 0xFFFFFFFFFFFFC000L`. Resolution is millisecond-grained rather than 16μs — acceptable for Phase A; revisit if Mesa timeout logic in Phase C is sensitive.

**Phase A verification (close-out)**: `dotnet build csharp/Dwarf.slnx` succeeds with 0 warnings, 0 errors. `dotnet test` runs 5 tests, all pass.

### 2026-05-12 (Phase B vertical slice: dispatch infra + Ch03)

- **Chapter registration design**: dropped the static-constructor + `RuntimeHelpers.RunClassConstructor` plan in favor of an explicit `public static void RegisterAll()` method on each chapter class, called from `Opcodes.initializeInstructionsPrincOps*40()`. Simpler, no reflection, supports mode-switching without per-static-ctor-fires-only-once gotchas.
- **`OpImpl` is a `delegate void OpImpl()`** (no `.execute()` method) — call sites use `Ch03_Memory_Organization.ESC_x09_GMF()` rather than Java's `.execute()`. Faithful in behavior.
- **`Opcodes.InstallOld` / `InstallNew`** check `currentMode` and silently skip if mismatched. `Install` and `InstallEsc` are mode-agnostic and always land. This means a chapter's `RegisterAll()` can unconditionally call all three variants and the right ones win based on which `initializeInstructionsPrincOps*40()` was invoked.
- **`Processes` is a Phase B stub** — only `ProcessStateBlock_Size`, `psbHandle`, `psbIndex`, `resetPTC` are real; `faultOne` / `faultTwo` throw `NotImplementedException("Phase C")` (Ch03 tests don't call them; the real fault-dispatch path lands in Phase C). `checkforInterrupts`, `checkForTimeouts`, `reschedule`, `idle` are no-op stubs.
- **`Ch03_MemoryOrganizationTest`** is just 1 `[Fact]` method but loops `firstUnmappedPage`-many times (~1023 sub-iterations) — covers a huge surface of SM/SMF/GMF semantics. The Phase B doc's "~25 tests" estimate was off.
- **xUnit `@After` equivalent**: `AbstractInstructionTest` now implements `IDisposable` with a virtual `OnAfterTest()` hook. Ch03Test overrides it to call `Mem.createInitialPageMappingGuam()` (restores map after a test that mutated it).
- **Suppressed CA1711** (reserved-suffix names) — needed for `OpImpl` ('Impl') and `InstallNew`/`InstallOld` ('New').
- **All 11 tests pass** (5 Phase A smoke + 1 Ch03 + 5 dispatch smoke). Build 0 warnings 0 errors.

**Vertical slice complete** — the dispatch pipeline works end-to-end. Next session picks up at Ch05 (the largest chapter, 820 LOC, 193 tests) which will validate the sign-extension audit (RISKS R1).

### 2026-05-12 (Phase B: Ch05_Stack_Instructions — 193 tests)

- **All 193 Ch05 tests passed first try.** The sign-extension audit (RISKS R1) survives contact with reality on the chapter most exposed to signed/unsigned drift. Total: 204/608.
- **Sign-extension pattern that works**: Java `short` → C# `(short)Cpu.pop()` cast at the signed-arithmetic call site. Examples: NEG, SDIV, SHIFT count, LINT, ROTATE, DCMP. Java's signed `& 0xFFFF` masking ceremony falls away — C#'s `ushort` pop is already 0..65535.
- **One C# syntax gotcha**: shadowing a local in nested-then-outer scope is rejected by CS0136 (Java allows it). Renamed second `tmp` to `tmp2` in `rotateShort`.
- **`>>>` operator** (unsigned right shift, C# 11+) is a direct stand-in for Java's `>>>`. Used in shiftShort, shiftLong, rotateShort, LUDIV bounds check, SDDIV/UDDIV test helpers.
- **Floating-point**: `BitConverter.Int32BitsToSingle` / `SingleToInt32Bits` ports Java's `Float.intBitsToFloat` / `floatToRawIntBits` cleanly. FADD/FSUB/FMUL/FDIV/FCOMP/FLOAT implemented; ten less-used float ops (FIX, FIXI, FIXC, FSTICKY, FREM, FROUND, FROUNDI, FROUNDC, FSQRT, FSC) raise `signalEscOpcodeTrap` to delegate to Pilot's software emulation, matching Java.
- **`AbstractInstructionTest.mkStack` typo preserved** correctly — `mkStack(33, ..., SP, ...)` tests like `test_REC` pass. The Java bug where `Cpu.SP = newSavedSP` (should be savedSP) doesn't affect Ch05 tests because the typo only hits when both SP *and* savedSP sentinels are present, which Ch05 tests don't do.
- **Suppressed CA1711** for `OpImpl`. No new analyzer suppressions in this iteration.
- Total opcodes ported through this checkpoint: 19 (Ch03) + ~60 (Ch05) = 79 of ~256.

### 2026-05-12 (Phase B: Ch07_Assignment_Instructions — 212 tests)

- **All 212 Ch07 tests passed first try.** Total: 416/608.
- Ch07 is the biggest opcode set (~148 opcodes counting old/new variants): load/store immediate, local frame, global frame (old + new GF), direct pointer, indirect pointer, string, field access. The naming convention preservation paid off — direct grep-correspondence with Java made the port mechanical.
- **Test data factored into class-level `static readonly int[]` arrays** (`LF_FRAME`, `GF_FRAME`, `FIVE_MEM`, `EIGHT_MEM`, `FOURTEEN_MEM`, `STR_MEM`, `STR_MEM_HBIT`) to avoid repeating ~15-element literals across 100+ tests. Java's inline literals work but C# benefits from named arrays for readability.
- **One C# literal gotcha hit**: `(short)0x8000` in Java compiles (truncating cast); C# rejects with CS0221 "constant value 32768 cannot be converted to short". Switched to `(ushort)0x8000` in LINI — matches the ushort-stack convention anyway.
- **`unchecked((int)0xFFFF8000)` pattern** for tests where the Java literal `0xFFFF8000` is signed-int -32768. C# treats `0xFFFF8000` as uint by default; explicit unchecked cast preserves bit-pattern.
- **`Assert.NotEqual(0, Cpu.LF)`** is the xUnit equivalent of JUnit's `assertNotEquals("LF", 0, Cpu.LF)` (xUnit signature is `(expected, actual)`, no message param).
- Total opcodes ported through this checkpoint: 19 (Ch03) + ~60 (Ch05) + ~148 (Ch07) = ~227 of ~256.
