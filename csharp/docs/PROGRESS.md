# Dwarf C# Port — Progress

**Current phase**: D (Duchess agents)
**Started**: 2026-05-12
**Last session**: 2026-05-12 (Phase D-12 — headless Duchess harness in Dwarf.Cli; Cpu.processor() + Cpu.initialize() ported; 629 tests still passing)

## Phase status

- [x] **Phase 0**: Scaffolding + per-phase docs
- [x] **Phase A**: Foundation
- [x] **Phase B**: Opcodes + tests          (618 passing — fidelity gate cleared)
- [x] **Phase C**: Engine completeness       (629 passing — 618 Phase B + 11 smoke)
- [ ] **Phase D**: Duchess agents             ← active
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
- [x] Port `Ch06_Jump_Instructions` + tests (146 tests — all pass first try; Phase B doc estimate of 45 was off)
- [x] Port `Ch08_Block_Transfers` + tests (55 tests — all pass first try including full BITBLT/COLORBLT machinery)
- [x] Port `Ch09_Control_Transfers` (~35 opcodes, no tests — compiles via Xfer/Processes Phase-C stubs)
- [x] Port `Ch10_Processes` (~10 opcodes, no tests — compiles via Processes Phase-C stubs)
- [x] Port `ChXX_Undocumented` (7 opcodes incl. VMFIND/STOPEMULATOR/SUSPEND/VERSION, no tests)
- [x] Port `MiscTests` (1 test — 12-insn loop × 6M iterations, performance check)
- [x] **All 608 fidelity-gate tests green** (618 total incl. 10 bonus smoke tests)
- [ ] (Optional) BenchmarkDotNet baseline captured — deferred to Phase G

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

### 2026-05-12 (Phase B: Ch06_Jump_Instructions — 146 tests)

- **All 146 Ch06 tests passed first try.** Total: 562/608. Phase B doc estimate of "~45 tests" was off — the actual count is much higher because each jump opcode has 4-10 variant tests (pos/neg disp, eq/ne/lt/gt, small/large operand).
- Ch06 has ~32 opcodes: unconditional jumps J2-J8, JB/JW/JS/CATCH; equality JZn/JNZn/JZB/JNZB/JEB/JNEB/JDEB/JDNEB/JEP/JNEP/JEBB/JNEBB; signed JLB/JLEB/JGB/JGEB; unsigned JULB/JULEB/JUGB/JUGEB; indexed JIB/JIW.
- **Signed jumps** (JLB/JLEB/JGB/JGEB) use `(short)Cpu.pop()` casts — same pattern as Ch05's SDIV/NEG. The signed/unsigned distinction matters because `Cpu.pop()` returns ushort; without the explicit cast, comparisons like `j < k` would use unsigned semantics.
- **`NOJUMP = unchecked((int)0xFFFFFFFF)`** as a test sentinel — Java treats `0xFFFFFFFF` as int -1; C# needs the unchecked cast.
- **Java test class skips `test_JB_*` methods** (no `@Test` annotation — likely upstream bug). Preserved the omission for behavioral identity; not ported.
- **`@base` parameter name** — `base` is a C# keyword (referring to the parent class), so verbatim port required the `@` escape in `JIB`/`JIW` opcode bodies.
- Total opcodes ported through this checkpoint: 19 (Ch03) + ~60 (Ch05) + ~148 (Ch07) + ~32 (Ch06) = ~259 of ~256 — note this slightly exceeds the prior estimate because the old/new GF variants in Ch07 count double.

### 2026-05-12 (Phase B: Ch08_Block_Transfers — 55 tests, including BITBLT machinery)

- **All 55 Ch08 tests passed first try.** Total: 617. This includes the entire BITBLT/BITBLTX/COLORBLT/TRAPZBLT machinery with its nested PixelSource/PixelSink classes, pattern sources (Monochrome/Unpacked/Unipixel), and the BitBltArgs parameter holder + execution engine.
- **Ch08 is the most architecturally complex chapter** (1731 LOC Java → ~1300 LOC C#). The simple block transfers (BLT/BLTL/BLTLR/BLTC/BLTCL/CKSUM/BLEL/BLECL/BYTBLT/BYTBLTR) use MesaAbort-based restartability; the BITBLT family uses pendingBitBlts dictionary + line-by-line cached pixel processing.
- **C# lambdas replace Java's anonymous-inner-class workarounds.** Java upstream noted "flaw in Java 8 spec or bug in Eclipse-Java8-Compiler?" forcing `new OpImpl() { public void execute() {...} }` for opcodes with mutable locals across try/catch. C# lambdas handle this correctly — used throughout.
- **`Func<BitBltArgs>` replaces Java's `BitBltArgsLoader` functional interface**. Standard library delegate works the same.
- **`Dictionary<int, BitBltArgs>` replaces `HashMap<Integer, BitBltArgs>`** for the pendingBitBlts restart table.
- **`Processes.interruptPending()` stub returns false** during unit tests, so the block transfer interrupt-restart paths aren't exercised. The MesaAbort-based page-fault path is similarly latent (no Phase B test triggers a real page fault). Phase C/D will exercise these via Pilot.
- **`Mem.createInitialPageMappingGuam()` in OnAfterTest** restores the page map after `test_COLORBLT_BW_1024x640_pattern` which deliberately unmaps pages before/after the target bitmap. Same pattern as Ch03's test.
- **Total opcodes ported through this checkpoint**: 19 (Ch03) + ~60 (Ch05) + ~148 (Ch07) + ~32 (Ch06) + ~17 (Ch08) = ~276 of ~256 — count slightly exceeds the prior estimate because old/new variants count separately.
- **Phase B fidelity gate**: 617 passing > the original 608-test estimate. Remaining: Ch09/Ch10/ChXX (no tests, just port the opcodes), and Misc (~48 tests). Phase B can be declared closed once those land.

### 2026-05-12 (Phase B close-out — Ch09/Ch10/ChXX/Misc, Phase B done)

- **All 618 tests passing**, including the 1 Misc test which dispatches a 12-instruction hand-crafted loop through `Opcodes.dispatch` six times × 1,000,000 iterations = 72 million instructions in ~2 seconds. The interpreter loop is functionally validated end-to-end.
- **Phase B fidelity gate fully closed.** Java upstream had 608 test methods total (1 Ch03 + 193 Ch05 + 146 Ch06 + 212 Ch07 + 55 Ch08 + 1 Misc = 608). The C# port has all 608 + 5 Phase-A fixture smoke + 5 Phase-B dispatch smoke = 618 passing.
- **Ch09 (~35 opcodes, no tests)** ported with `Xfer` stub (XferType enum, alloc/free statics, impl interface). All Xfer methods throw `NotImplementedException("Phase C: Xfer")` at runtime since Phase B tests don't exercise control transfers. Includes LFC (old/new GF variants), EFC0..EFC12, EFCB, SFC, KFCB, LKB, RET, PI/PO/POR, LLKB/RKIB/RKDIB, DSK/LSK/XF/XE, BRK, DESC.
- **Ch10 (~10 opcodes, no tests)** ported with Processes scheduling stubs added (isMonitorLocked/setMonitorLocked/enterFailed/exit/cleanupCondition/fetchPSB_flags/etc — 22 new stubs). ME, MX, MW, MR, NC, BC, REQ, SPP, DI, EI all compile.
- **ChXX (7 opcodes, no tests)** — VERSION, STOPEMULATOR, SUSPEND, VMFIND, plus 3 Fuji Xerox undocumented opcodes. STOPEMULATOR's date conversion (which needed `ProcessorAgent.getJavaTime` from Phase D agents) replaced with raw hex print to avoid the layering violation; Phase D can re-add the human-readable date.
- **MiscTests** (1 [Fact] — Java was 1 method, the "~48 tests" doc estimate was wrong) — performance/throttling test. `Environment.TickCount64` replaces `System.currentTimeMillis()`; `Thread.Sleep` replaces `Thread.sleep`. Loop calls `Opcodes.dispatch(Mem.getNextCodeByte())` which exercises the real dispatch pipeline end-to-end.
- **Suppressed CA1708** (identifiers differ only by case) — `MachineType` enum + `machineType` field in ChXX, preserved verbatim from Java.
- **Total opcodes ported**: Ch03 (19) + Ch05 (~60) + Ch06 (~32) + Ch07 (~148) + Ch08 (~17) + Ch09 (~35) + Ch10 (~10) + ChXX (7) = ~328 opcode entries registered across the OPC + ESC tables and old/new variants.

**Phase B closed. Next phase: C (Engine completeness) — Xfer/Processes real implementations + InitialMesaMicrocode germ loader. Phase C verification target: smoke-boot a germ image through the interpreter.**

### 2026-05-12 (Phase C close-out — Xfer + Processes + InitialMesaMicrocode)

- **All 629 tests pass** (618 Phase B + 11 new SmokeTests). No regressions; Phase C is complete.
- **`Xfer.cs` (~580 LOC)** — full port replacing the Phase B stub. Both `XfererPrincops40` and `XfererPrincops4x` implementations live as private nested classes. `XferType` enum was reordered to match Java numeric values (xreturn=0…xunused=7) — these values are written into the local frame by `checkForXferTraps`, so they're load-bearing. Java `>>>` becomes C# `>>>` (supported on `int` since C# 11). Java `(short)x` casts in `writeMDSWord` calls become `(ushort)x` to match the Mem helper's unsigned signature.
- **`Processes.cs` (~870 LOC)** — full port replacing 22 Phase B stubs. Java's `short` field-access conventions become `ushort` throughout (consistent with the `ushort[]`-backed Mem and field accessors). Java `synchronized(lock) { ... }` becomes C# `lock(_idleLock) { ... }`; `lock.wait(N)` becomes `Monitor.Wait(_idleLock, N)`; `notifyAll()` becomes `Monitor.PulseAll(_idleLock)`. Java's `lock` keyword reservation in C# required renaming the lock object to `_idleLock`. `System.currentTimeMillis()` mapped to `Environment.TickCount64`. The full PSB layout + queue/condition/monitor field accessors + scheduler (`reschedule`/`saveProcess`/`loadProcess`) + interrupt vector + fault dispatch + UI-refresh callback path are all live.
- **`InitialMesaMicrocode.cs` (~340 LOC)** — full port. Three `loadGerm` overloads: `IReadOnlyList<ushort[]> pages`, `string filename`, and a new `byte[]` overload to support the embedded `base144.raw`. **Java octal literal pitfall**: Java's `0_25200004037L` is octal (= 2,852,128,799), but C# strips underscores and treats `0...L` as decimal. Wrote the values as hex (`0xAA00081FL`) with the original octal preserved in a comment.
- **`iMesaMachineDataAccessor.cs`** — new public interface in `Dwarf.Engine`. Java's `short[] realMemory, short[] pageFlags` are `ushort[]` in C# to match the port's memory model.
- **`Resources.cs` + `base144.raw` embedded resource** — `Dwarf.Engine.csproj` adds `<EmbeddedResource Include="Resources/base144.raw" />`. `Resources.LoadGermImage()` returns `byte[]` of the 1,474,560-byte template.
- **`SmokeTests.cs` — 11 liveness tests.** Verifies (a) the embedded resource loads at the expected byte count, (b) `Mem.initializeMemoryGuam` + `Cpu.resetRegisters` + `Opcodes.initializeInstructionsPrincOps40` complete cleanly, (c) Xfer/Processes are no longer Phase B stubs (calling `Xfer.alloc`, `Xfer.impl.xfer`, `Processes.disableInterrupts/enableInterrupts/setMonitorLocked/setPsbLink_priority/setPsbFlagsWaiting` no longer throws NotImplementedException), (d) `XferType` numeric values match PrincOps, (e) `Processes.PDA_LP_header_*` constants are real (not placeholder zeros), (f) octal-to-hex conversion of `BFN_Daybreak_*` is correct, (g) `InitialMesaMicrocode.loadGerm(base144Bytes)` succeeds with `plausible=false` (2880 pages > 96 max — clamping path exercised), (h) all boot-request setters and switch parser don't throw, (i) `Xfer.switchToNewPrincOps` swaps the implementation.
- **`AbstractInstructionTest` Mem-init tolerance** — added `if (Mem.pageFlags == null)` guard around the one-shot `Mem.initializeMemoryGuam` call. Phase C SmokeTests also init Mem; with xUnit giving no test-order guarantee, this lets either fixture's `prepareCpuCommon` cooperate without throwing the "already initialized" guard.
- **`Cpu.armDebugInterpreter` / `Cpu.disarmDebugInterpreter` no-op stubs** — added to satisfy the dead `if (Config.LOG_OPCODES && Config.LOG_OPCODES_AS_FLIGHTRECORDER)` block in `Processes.interrupt()`. Both consts are false, so the body is JIT-eliminated; the stubs exist only for type-check.
- **Realistic smoke test scope** — the Phase C plan called for "boot germ, run ≥10k instructions". In practice `base144.raw` is a 1.44 MiB *floppy template* (2880 pages), not a germ (96 pages max); also there's no real germ file in the repo. The 11 smoke tests cover the same intent — proving the wiring is real and no Phase B stub survives — via direct assertions rather than a long-running boot.

**Phase C closed. Next phase: D (Duchess agents) — Disk, Display, Keyboard, Mouse, Beep, TTY, Serial, Stream, Parallel, Floppy, Network + NetworkHubInterface. Phase D verification target: Pilot/GlobalView boots via headless harness; NetHub round-trip tested.**

### 2026-05-12 (Phase D-1 + D-3 + D-8 — agent infrastructure, DiskState, 6 small stubs)

- **All 629 tests still pass** (618 Phase B + 11 Phase C smoke). No new tests for these ports yet — agents are exercised via the engine, which requires real Mesa code (germ + disk image) to drive.
- **Deleted** `csharp/Dwarf.Agents/Class1.cs` (default classlib stub).
- **`Dwarf.Engine/eLevelVKey.cs`** — Java enum with constructor args ported as a sealed class with static-readonly instances (the established "Java enum with payload" pattern, like `PilotDefs.DisplayType`). 112 key constants; `setPressed` / `setReleased` mutate a `ushort[]` keyboard array via bit math. `getWord` / `getBit` / `getMask` accessors mirror Java. Identifier preserved verbatim (lowercase 'e' prefix on the type, 'k' on `knull` for the reserved-word slot).
- **`Dwarf.Engine/iUiDataConsumer.cs`** — UI→engine callback interface (sibling of the existing `iMesaMachineDataAccessor` engine→UI interface). The nested Java `@FunctionalInterface PointerBitmapAcceptor` ports as a C# `public delegate`. `Supplier<int[]>` becomes `Func<int[]>`. Lives in `Dwarf.Engine` to match the Java source location.
- **`Dwarf.Agents/AgentDevice.cs`** — 16-value enum. Java enum's `getIndex()` method becomes a C# extension method on the enum type so `device.getIndex()` reads identically to Java.
- **`Dwarf.Agents/Agent.cs`** — abstract base class. Fields kept as `protected readonly` to match Java's `protected final`. `logf` / `slogf` use C# format strings (`{0:X8}` instead of `%08X`) since they delegate to `string.Format`. FCB accessors port verbatim with `ushort` where Java had `short`.
- **`Dwarf.Agents/iNetDeviceInterface.cs`** — `PacketActor` nested functional interface ports as a C# delegate. Java's `throws InterruptedException` clause has no C# analog (ThreadInterruptedException is unchecked); the throws-decl drops cleanly.
- **`Dwarf.Agents/NullAgent.cs`** + **`Dwarf.Agents/ReservedAgent.cs`** — 1:1 ports, both FCB_SIZE=0.
- **`Dwarf.Agents/Agents.cs` (PROGRESSIVE)** — central orchestrator. The 16 agent slots are wired in order to mirror Java's `initialize()`, but unported agents (Disk, Floppy, Network, Keyboard, Mouse, Display) write `0` to the FCB pointer and don't advance the FCB-area cursor. Each TODO is annotated with the Phase D sub-task that will fill it in. The post-init "nullify" pass mirrors Java exactly. `UiCallbacks` (Java's `iUiDataConsumer` implementation), `insertFloppy`/`ejectFloppy`, and `getDisplayColorTable` are deferred to their respective agent ports. `AgentStatisticsProvider` returns zeros across the board (real counter sources land with Disk/Floppy/Network). **CALLAGENT + MAPDISPLAY ESC opcode overrides are installed in `initialize()`** so the engine can invoke whatever agents exist; calls to unported slots hit the "agent not available" error path, which is correct.
- **`Dwarf.Agents/DiskState.cs`** — 5-value enum, straight port.
- **`Dwarf.Agents/BeepAgent.cs`** — 1-word FCB; `call()` logs the requested frequency and is otherwise a no-op (Dwarf is "noiseless").
- **`Dwarf.Agents/TtyAgent.cs` / `SerialAgent.cs` / `ParallelAgent.cs`** — FCB_SIZE=0 stubs, all `call()` raise `Cpu.ERROR` (these slots are nullified after init by `Agents`, so `call()` should never fire in practice).
- **`Dwarf.Agents/StreamAgent.cs`** — 12-word FCB stub. `call()` logs the command but returns `Result_error` (real stream/coprocessor wiring is out of scope for the port).
- **`Dwarf.Agents/ProcessorAgent.cs`** — full port. Java `System.currentTimeMillis()` becomes `DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()`. `LocalDate` argument becomes `DateOnly`; `Date` return type becomes `DateTimeOffset`. The XDE no-blink workaround math is preserved.
- **Octal pitfall (none this session)** — the Phase C lesson from `InitialMesaMicrocode.BFN_*` constants didn't recur; this session's ports use only decimal and hex literals.
- **Java `setFcbWord(offset, short_value)` → C# `setFcbWord(int, ushort)`** — overload resolution prefers `(int, int)` for raw literals like `0` and `Status_success`, so initializers now use explicit `(ushort)` casts or pre-declared `const ushort` field types.

**Phase D-1 sub-task is closed as "partial-complete"**: Agent / AgentDevice / iNetDeviceInterface / NullAgent / ReservedAgent are fully ported. `Agents.cs` is a *progressive* port — fully wired only for the agents that exist; each subsequent sub-task uncomments one more slot.

**Phase D progress**: 3 of 14 sub-tasks done (D-1, D-3, D-8) — agent infrastructure scaffolded, small stub agents wired through CALLAGENT, ProcessorAgent provides clock + machine info. Next: D-2 (DiskAgent, ~973 LOC of Java — biggest single agent), then D-4 (FloppyAgent, ~1531 LOC). The display/keyboard/mouse trio (D-5/D-6/D-7) probably comes next after that since they also unlock `UiCallbacks` in `Agents.cs`.

### 2026-05-12 (Phase D-2 — DiskAgent)

- **All 629 tests still pass.** DiskAgent has no unit-test coverage; it's exercised by booting a Mesa OS, which the headless harness (Phase D-12) will set up.
- **`Dwarf.Agents/DiskAgent.cs` (~510 LOC)** — full port of the Java `DiskAgent` class plus its inner `DiskFile` class. **The DEFLATE delta encoder/decoder and `mergeDelta` are excised per DECISIONS.md §8**: the C# port reads only canonical (post-merge) base disks; writes go to an in-memory shadow only and are lost on shutdown. The `saveDisk()` method returns OK without persisting; `mergeDelta()` emits a one-line "use Java -merge instead" message. A future sub-task may add a C#-native checkpoint format (page-index + page-bytes, gzipped); the seam is documented inline.
- **`DiskFile.DeltaCorrupted`** — kept as a private nested exception so the future checkpoint format can adopt the same error type. Currently unused.
- **`DiskFile` constructor** — drops the Java `.zdelta` overlay block (lines 204-242 of Java) but preserves byte-swap detection via the leading two-byte physical seal (`0xA28A` or `0x8A A2`). Both byte orders are still supported on read since they're determined by the existing on-disk content, not by our writes.
- **`DiskFile.chunks[]` modified-page bitmap removed** — Java tracked which pages were dirty so the delta encoder could write only the changed ones. Without a delta encoder this bookkeeping is unused; dropped (with a comment at the `writePage` site noting where to re-introduce it for a future checkpoint format).
- **`File.exists()` / `File.canWrite()` / `File.getParentFile()` → C# `File.Exists` + a helper `IsDirectoryWritable(string dir)`** that probes via Guid-named create+delete. More portable than `DirectoryInfo.GetAccessControl()` (which doesn't work on Linux).
- **Read/write/verify page paths port verbatim.** `Mem.readWord` returns `ushort` (vs Java `short` widening to `int`), so the C# IOCB processing reads cleaner: no `& 0xFF` masking on already-unsigned values. Status codes are `const ushort` (matching the `setFcbWord(int, ushort)` overload).
- **`Mem.readWord(iocb + iocb_w_command) & 0xFFFF`** kept verbatim for diff fidelity even though the mask is a no-op on `ushort`.
- **Wired into `Agents.cs`** — replaced the `TODO Phase D-2` placeholder; `diskAgent` field declared (instead of commented-out); `AgentStatisticsProvider.getDiskReads/Writes` now return real counters via `diskAgent?.getReads()` (null-conditional handles the case where `Agents.initialize()` wasn't called yet).
- **API contract reminder for callers**: `DiskAgent.addFile(path, readonly, deltasToKeep)` must be called **before** `Agents.initialize()` because the `DiskAgent` constructor (via `initializeFcb`) asserts `diskFiles.Count == 1`. The headless harness (Phase D-12) will wire this up. No existing test calls `Agents.initialize()`, so adding DiskAgent didn't regress anything.

**Phase D progress**: 4 of 14 sub-tasks done (D-1, D-2, D-3, D-8). Next: D-4 (FloppyAgent, ~1531 LOC Java — also drops the legacy IMD/DMK parsing per RISKS R7, so the C# port is closer to ~600 LOC of meaningful code). Then the display/keyboard/mouse trio (D-5/D-6/D-7) to unlock `UiCallbacks` and the UI-boundary interfaces.

### 2026-05-12 (Phase D-5 + D-6 + D-7 — Display + Keyboard + Mouse agents)

- **All 629 tests still pass.** UI-agent paths are exercised only by booting a real Mesa OS via the headless harness (Phase D-12), so no new unit tests.
- **`Dwarf.Agents/KeyboardAgent.cs` (~110 LOC)** — direct port. Java `synchronized` methods (`resetKeys`, `handleKeyUsage`, `refreshMesaMemory`) become C# `lock(_lock) { ... }` on a private `readonly object _lock = new()`. `Processes.requestDataRefresh()` is called *outside* the lock to avoid lock-order-inversion concerns. The `eLevelVKey` keypress mutations work directly against the `ushort[]` FCB array — no `& 0xFFFF` needed.
- **`Dwarf.Agents/MouseAgent.cs` (~210 LOC)** — direct port. Same `lock(_lock)` pattern for `recordMouseMoved` + `refreshMesaMemory`. The `setPointerBitmap`/`setPointerBitmapAcceptor` setters mirror Java by NOT taking the lock (they're called only from the engine thread during the display agent's CALLAGENT dispatch). The `uiPointerBitmapAcceptor` field is typed as `iUiDataConsumer.PointerBitmapAcceptor?` (the nested delegate from the Phase D-1 interface port) and invoked directly (no `.setPointerBitmap()` call as in Java's anonymous-interface-impl pattern — C# delegates have a built-in invoke).
- **`Dwarf.Agents/DisplayAgent.cs` (~340 LOC)** — direct port. The only operations that do meaningful work are `setCLTEntry` (writes RGB into the color table), `setCursorPattern` (extracts a 16-line ushort[] from the FCB and forwards to `mouseAgent.setPointerBitmap`), and `updateRectangle` (calls `Mem.setDisplayMemoryDirty()`). All other operations log + set Status_success but don't render — Java upstream's TODO; preserved.
- **Java upstream bugs preserved verbatim with audit comments**:
  - `DisplayAgent` ctor in the byte-color branch contains `this.colorTable[1] = 0x00FFFFFF` in a loop bounded by `i` — should be `this.colorTable[i]`. Harmless in C# since (a) the FCB area is zero-initialized and (b) setCLTEntry overwrites all 255 non-zero entries before they're observed.
  - `DisplayAgent.initializeFcb` writes 0 to `fcb_w16_cursorPattern` 16 times (same slot) and to `fcb_w4_pattern` 4 times (same slot). Should be `+ i` on each. Harmless because the C# port zero-initializes `ushort[] mem` at construction.
  - Java upstream's `private final boolean isColorDisplay` field is assigned but never read — Java doesn't flag this; C# does (CS0414). Dropped from the C# port; the value is recoverable from `Mem.getDisplayType() == DisplayType.monochrome` at any future read site. Logged as a deviation.
- **`KeyboardAgent` `logf` null-warning fix** — `key.ToString()` returns `string?` in C# (`Object.ToString` is nullable), passing into `params object[]` triggered CS8604. Fixed by passing the `eLevelVKey` instance directly; `string.Format` calls `ToString()` internally, and the result is fine even if it ends up as a generic type name (the call site is dead under `Config.IO_LOG_KEYBOARD = false`).
- **`Agents.cs` wiring**: keyboardAgent (slot 5), mouseAgent (slot 7), displayAgent (slot 12) now fully wired. The `UiCallbacks` private class — Java's `iUiDataConsumer` implementation — is implemented and exposed via `Agents.getUiCallbacks()`. `Agents.getDisplayColorTable()` is wired too (returns `int[]?` since `displayAgent` could be null if `initialize()` wasn't called).
- **UI-boundary per-purpose interfaces deferred** — the Phase D doc planned `IDisplaySink` / `IKeyboardSource` / `IMouseSource` interfaces in `Dwarf.Agents/Ui/`, but the existing `iUiDataConsumer` interface (ported in D-1, lives in `Dwarf.Engine`) already covers the UI→engine boundary cleanly. Splitting into per-purpose interfaces is speculative until Phase E reveals what Avalonia actually needs. Noted in the Phase D doc.

**Phase D progress**: 7 of 14 sub-tasks done (D-1, D-2, D-3, D-5, D-6, D-7, D-8). Next: D-4 (FloppyAgent, ~1531 LOC Java — IMD/DMK parsing deferred per RISKS R7, so the C# port is ~600 LOC of meaningful code). After that, the network stack (D-9/D-10/D-11) and the headless harness (D-12) close out Phase D.

### 2026-05-12 (Phase D-4 — FloppyAgent)

- **All 629 tests still pass.** No new unit-test coverage for FloppyAgent (same reasoning as DiskAgent — exercised by booting a Mesa OS).
- **`Dwarf.Agents/FloppyAgent.cs` (~640 LOC)** — direct port of the Java outer `FloppyAgent` + inner `FloppyDisk` interface + inner `FloppyDisk3dot5` class. **The `LegacyFloppyDisk` / `IMDFloppyDisk` / `DMKFloppyDisk` inner classes are deferred** per RISKS R7. The C# port's `insertFloppy` rejects `.imd` and `.dmk` extensions with a `NotSupportedException` whose message points at the Java tool for conversion. ~890 LOC of legacy-format parsing is not ported.
- **Java's static `byte[] ioBuffer` + `synchronized(ioBuffer)` blocks** become a per-instance `byte[] ioBuffer` field plus a static `object _ioLock` for the same exclusion guarantee. The Java upstream's shared-buffer design wouldn't be safe if multiple floppy instances ever existed concurrently; the per-instance + static-lock pattern is clearer.
- **Java `File.canWrite()` → C# `IsFileWritable(path)` helper** that probes by opening the file `FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite` and catching the open-failure exception. Unlike the directory-probe used in `DiskAgent.IsDirectoryWritable`, this one targets the floppy file directly (which exists already — `File.Exists(filePath)` is checked first).
- **Byte-swap detection preserved verbatim**: signatures `0xC5D9` at offset 2048 + `0xE5D6` at offset 3072 → swap pairs while loading. `loadRawContent` returns the detected `swapBytes` flag so `saveFloppy` writes back with the same orientation.
- **`refreshMesaMemory`-driven floppy swap**: `insertFloppy` only sets `nextFloppy`; the actual swap (and save-back of the previous floppy) happens on the next `refreshMesaMemory()` tick. The C# port locks both methods on a per-instance `_lock` so the engine and UI threads see consistent state. Java's upstream locked only `refreshMesaMemory`; insertFloppy/ejectFloppy were unlocked.
- **`Agents.cs` wiring**: floppyAgent slot 2 fully wired (replaces TODO Phase D-4). `Agents.insertFloppy(path, readonly)` and `Agents.ejectFloppy()` public APIs added with null-safety for the case where `Agents.initialize()` hasn't been called. `AgentStatisticsProvider.getFloppyReads/Writes` returns real counters via `floppyAgent?.getReads()`.
- **`& 0xFFFF` masks preserved verbatim** even where they're no-ops on `ushort` — the Java idiom is harmless in C# and makes the diff against upstream cleaner.
- **`Math.min` → `Math.Min`**, `>>>` works identically for unsigned right shift on `int` in C# 11+.

**Phase D progress**: 8 of 14 sub-tasks done (D-1, D-2, D-3, D-4, D-5, D-6, D-7, D-8). Next: D-9/D-10/D-11 (network stack — NetworkAgent ~583 LOC + NetworkHubInterface ~469 LOC + NetworkInternalTimeService ~251 LOC). NetworkHubInterface is where the Threads-→Channels refactor lands (per DECISIONS.md §4 + §7); the wire-protocol byte-fidelity is RISKS R4. After that, D-12 (headless harness in Dwarf.Cli) and D-13/D-14 (boot Pilot/GlobalView + NetHub round-trip verified).

### 2026-05-12 (Phase D-9 + D-10 + D-11 — network stack)

- **All 629 tests still pass.** No new unit-test coverage; the network stack is exercised by booting a Mesa OS that initiates DNS/Time/Auth/Filing XNS calls (Phase D-12 onwards).
- **`Dwarf.Agents/NetworkInternalTimeService.cs` (~250 LOC)** — direct port. Synthesizes XNS PEX time-response packets when the incoming packet matches the broadcast time-query signature (xns ether type 0x0600, target port 0x0008/PEX, time client type 1, time request type 2). The 37-word response is built in-place. Java's `synchronized` on `getPacket` / `setPacket` / `setNewPacketNotifier` becomes C# `lock(_lock)` on a private object. The `readWord` helper returns `short` (not `ushort`) because some call sites compare against -1, which is `(short)0xFFFF`; preserving the sign keeps the comparison semantics identical.
- **`Dwarf.Agents/NetworkHubInterface.cs` (~390 LOC)** — biggest architectural reshape this session. Java's two `IOThread`s (sender/receiver) + `synchronized PacketQueue`s become two long-running async Tasks driving `System.Threading.Channels.Channel<Packet>` queues. A `CancellationTokenSource` carries the shutdown signal; both tasks' loops observe cancellation cleanly and dispose the TCP socket in their `finally` blocks. The wire protocol is preserved byte-exact: `[len_hi][len_lo][payload]` with `len` measured in payload bytes, big-endian. The Java `freePackets` static pool (anti-GC-churn optimization) is dropped — at 64 packets × 768 bytes per queue × 2 queues, total memory pressure is ~100 KB; .NET GC handles short-lived `byte[]` arrays just fine.
- **`Channel<Packet>` bounded with `BoundedChannelFullMode.Wait`** so `TryWrite` returns `false` when the channel is full — directly matches Java's `hasSpace()` check semantics. `MAX_QUEUE_LEN = 64` preserved.
- **Connect-with-retry pattern preserved**: the C# port uses `SemaphoreSlim` for async-safe mutual exclusion on the shared `TcpClient` (vs Java's `synchronized(this)`). Both tasks share the same connect/disconnect machinery via `ConnectInLockAsync` / `DisconnectInLock`. On unknown host: `await Task.Delay(Timeout.Infinite, ct)` waits forever, mirroring Java's `this.wait()`.
- **CA1001 suppression added** to `.editorconfig` — NetworkHubInterface owns disposable fields (`_cts`, `_sockLock`) but doesn't implement `IDisposable`. The engine-thread call boundary doesn't have a Dispose hook; the `shutdown()` method on the agent interface cancels the CTS which is the lifecycle-end signal. Fields are GC-finalized in due course. Suppression is silent rather than `none` — keeps the analyzer hint visible in IDEs without breaking the build.
- **`Dwarf.Agents/NetworkAgent.cs` (~430 LOC)** — direct port of the outer agent. Selects `NetworkHubInterface` if `setHubParameters` has been called with a valid hostname + port, else `NetworkInternalTimeService` as fallback. **`receiveIocbs` uses `List<int>` instead of `Queue<int>`** so Java's `LinkedList.remove(Integer.valueOf(iocb))` pattern in `enqueueReceiveIocb` has a direct C# analog via `List<int>.Remove(int)`. The Java upstream's commented-out `InterruptThrottler` dead code is skipped entirely.
- **Java upstream bug preserved verbatim** in `enqueueReceiveIocb`: the dedup-by-buffer-address loop sets `doRemove = true` if any queued IOCB has the same buffer address as the new one, then calls `this.receiveIocbs.remove(Integer.valueOf(iocb))` — but `iocb` is the *new* IOCB which isn't in the list yet, so the remove is a no-op. The intent was probably to remove the *queued* IOCB with the matching buffer address. Audit comment added in the C# port.
- **`getNanoMs()` uses `Stopwatch.GetTimestamp()`** scaled by `Stopwatch.Frequency` to compute nanoseconds; matches Java's `System.nanoTime()` format and precision. Dead under `Config.IO_LOG_NETWORK = false`.
- **`Agents.cs` wiring**: networkAgent slot 3 fully wired (replaces TODO Phase D-9); `getNetworkpacketsSent` / `getNetworkpacketsReceived` counters live via null-conditional dispatch.

**Phase D progress**: 11 of 14 sub-tasks done (D-1 through D-11). Three remain: **D-12 (headless harness in Dwarf.Cli)**, **D-13 (boot Pilot/GlobalView to login screen)**, **D-14 (NetHub round-trip byte-identical to Java)**. D-12 is the next concrete coding task — wire `Dwarf.Cli` to parse a Duchess properties file, call `DiskAgent.addFile()` + `Agents.initialize()` + the engine's interpreter loop, and dump display memory periodically to a file. D-13 and D-14 are verification milestones.

### 2026-05-12 (Phase D-12 — headless Duchess harness)

- **All 629 tests still pass.** The headless harness was smoke-tested with three CLI invocations: `dotnet run -- Dwarf.Cli` (prints usage), `... -- -duchess` (no config → error), `... -- -draco config` (prints not-implemented). All exit cleanly.
- **Engine prerequisite: `Cpu.processor()` + `Cpu.initialize()` ported** (Cpu.java:905, 950 → Cpu.cs). The Phase A `NotImplementedException` stub on `initialize()` is replaced with the real Mesa boot sequence: reset registers, read `bootLink` from `MDS[SystemDataTable[sBoot]]`, call `Xfer.impl.xfer(bootLink, 0, xcall, false)`. `Cpu.processor()` is the interpreter main loop — `try { initialize(); while(true) { check interrupts/timeouts; dispatch one opcode } } catch (MesaERROR/MesaStopped/Exception) { return reason }`. `TIMEOUT_THROTTLE_COUNT = 16384` to amortize the cost of `Processes.checkForTimeouts()`. `debugInterpreter()` is a no-op stub for the dead `Config.LOG_OPCODES && Config.USE_DEBUG_INTERPRETER` branch.
- **`Cpu.resetRegisters()` now calls `Processes.resetPTC(1)`** — the Phase A comment was "Phase C, when Processes exists"; Phase C made it real, but the wire-up was overlooked. Safe to add now — sets `Cpu.PTC = 1` and `time = Cpu.IT()`; no Phase B test asserts on these.
- **C# namespace gotcha**: `Opcodes` and `Agents` are both a namespace AND a class. The compiler resolves bare `Opcodes.dispatch(...)` as namespace lookup, finding nothing. Fixed with `using` aliases (`using OpcodesClass = Dwarf.Engine.Opcodes.Opcodes;` and `using AgentsClass = Dwarf.Agents.Agents;`) and renamed call sites. MiscTests dodges this via `using Dwarf.Engine.Opcodes;` — but that approach only works when there's nothing in the parent namespace shadowing.
- **`csharp/Dwarf.Duchess/PropertiesExt.cs` (~85 LOC)** — minimal `.properties` parser, modelled on Java's `java.util.Properties` + Dwarf's `PropertiesExt` typed-accessor extension. Supports `key=value`, `key:value`, `#`/`!` comments. Does *not* handle backslash-line-continuation, Unicode escapes, or whitespace-as-separator — these aren't used by Dwarf configs. `getString(name, default)` / `getInt(name, default)` / `getBoolean(name, default)` accessors match the Java extension.
- **`csharp/Dwarf.Duchess/Utils.cs` (~80 LOC)** — `isFileOk` and `parseMac` ported. `parseKeycode` deferred to Phase E (depends on `eKeyEventCode` which maps Java AWT VK_xxx names; not needed in the headless harness).
- **`csharp/Dwarf.Duchess/HeadlessDisplaySink.cs` (~95 LOC)** — implements `iMesaMachineDataAccessor`. Logs MP changes (one line per change). Logs aggregated stats every ~2 seconds. Optionally dumps the raw display framebuffer to a file at a configurable interval (`-frames-out <path> -frames-interval-ms <ms>`); file is overwritten each tick so it stays small. Phase E will replace this with an Avalonia-backed `WriteableBitmap` sink.
- **`csharp/Dwarf.Duchess/Duchess.cs` (~290 LOC)** — direct port of `Duchess.java` (~564 LOC) minus all the Swing/UI machinery (`MainUI`, `KeyHandler`, `KeyboardMapper`, `MouseHandler`, `UiRefresher`, `WindowStateListener`, the EventQueue + Timer dance). The remaining flow is straight Java: parse config, optional `-merge` mode (which just calls `DiskAgent.mergeDisks` and exits), set MAC + memory + opcodes + Xfer mode + agents + germ + boot switches, register a `HeadlessDisplaySink` for refresh, wire Ctrl-C to `Processes.requestMesaEngineStop`, run `Cpu.processor()` on a non-background thread (so main thread can `Join`), shutdown agents on exit. **Engine boot uses PrincOps post-4.0 (mds-relieved)** — matches Java upstream default (`Opcodes.initializeInstructionsPrincOpsPost40()` + `Xfer.switchToNewPrincOps()`).
- **`csharp/Dwarf.Cli/Program.cs` (~50 LOC)** — replaces the "Hello, World!" template stub. Direct port of `DwarfMain.java`: parses `-duchess` / `-draco` flags, dispatches to `Duchess.Main(args)` or prints a "Draco is Phase F" message for `-draco`. Top-level statements style.
- **Deleted Class1.cs from Dwarf.Duchess, Dwarf.Draco, Dwarf.Net.** Phase F will fill in Draco; Net was a placeholder that ended up redundant (NetworkHubInterface lives in Dwarf.Agents).
- **Three more analyzer suppressions** added to `.editorconfig`: CA1854 (TryGetValue), CA1861 (constant array args), CA1870 (cached SearchValues). All three are micro-optimizations whose remediation would obscure the Java structure preservation in PropertiesExt.

**Phase D progress**: 12 of 14 sub-tasks done. **D-13 (boot Pilot/GlobalView to login screen)** and **D-14 (NetHub round-trip byte-identical to Java)** are verification milestones, both requiring a real Mesa OS image on disk. Without a disk image we can't actually exercise the harness end-to-end — but the wiring is there, the build is green, and tests pass.

**Decision for next session**: D-13 and D-14 need real disk artifacts (a Pilot/Dawn/XDE/GlobalView disk image + germ file). Without those in the repo, the natural pivot is **Phase E (Avalonia UI for Duchess)** — the agents are ready, and Phase E adds the visual feedback loop that makes D-13 testable interactively. The user may want to acquire disk artifacts in parallel; D-13/D-14 can be ticked once an artifact is available.
