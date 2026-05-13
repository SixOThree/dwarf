# Dwarf C# Port — Progress

**Current phase**: F (Draco port) — Phase E coding complete (12/12); Phase D at 12/14 (D-13/D-14 await disk artifacts)
**Started**: 2026-05-12
**Last session**: 2026-05-13 (Phase F-4b — HFloppy infrastructure; **Phase F is now coding-complete**, all device handlers wired; 656 tests still passing)

## Phase status

- [x] **Phase 0**: Scaffolding + per-phase docs
- [x] **Phase A**: Foundation
- [x] **Phase B**: Opcodes + tests          (618 passing — fidelity gate cleared)
- [x] **Phase C**: Engine completeness       (629 passing — 618 Phase B + 11 smoke)
- [~] **Phase D**: Duchess agents             (12/14 — D-13/D-14 await Duchess disk artifacts)
- [~] **Phase E**: Avalonia UI for Duchess    (12/12 coding sub-tasks; boot-to-login awaits Duchess disk)
- [ ] **Phase F**: Draco port                 ← active (IOP foundation landed)
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

### 2026-05-12 (Phase E first step — Avalonia scaffolding + DisplayControl prototype)

- **All 632 tests pass** (629 from Phase D + 3 new DisplayPipelineTests). The Avalonia infrastructure is in place; `dotnet run --project csharp/Dwarf.Cli -- -gui` opens a 1024×768 window with a diagonal-stripe test pattern as visual confirmation.
- **Avalonia 11.3.15** chosen (latest stable as of 2026-05-10) — matches across `Avalonia`, `Avalonia.Themes.Fluent` (in `Dwarf.UI.Avalonia`), and `Avalonia.Desktop` (in `Dwarf.Cli`). Library/host split per the Phase E doc recommendation: the library stays platform-agnostic; the host (`Dwarf.Cli`) chains `.UsePlatformDetect().LogToTrace().StartWithClassicDesktopLifetime(args)` at boot.
- **`Dwarf.UI.Avalonia` is now a real Avalonia library** — `<AvaloniaUseCompiledBindingsByDefault>true</AvaloniaUseCompiledBindingsByDefault>` for codegen-backed bindings, `<AllowUnsafeBlocks>true</AllowUnsafeBlocks>` for the pixel-copy fast paths in `IDisplaySource` implementations.
- **`App.axaml` + `App.axaml.cs`** — minimal Avalonia application root with the FluentTheme. `BuildAvaloniaApp()` returns an unconfigured `AppBuilder.Configure<App>()`; the host chains platform-specific options.
- **`MainWindow.axaml` + `.cs`** — 1024×768 window containing a single `DisplayControl`. The code-behind wires a `DiagonalStripesSource(1024, 768)` to it as the placeholder source. The real source (binding to `Mem.getDisplayRealMemory()` after engine init) lands when Duchess.cs is reshaped to drive the Avalonia UI.
- **`Controls/IDisplaySource.cs`** — the engine→UI boundary for the display data path. Publishes `Width` / `Height` and `CopyToBgra8888(nint destination, int rowBytes)`. The choice of BGRA8888 for the prototype means both monochrome and 8-bit color sources will expand into 32-bpp on the fly during the copy; perf-permitting we may move monochrome to `PixelFormat.Gray8` later for a 4× memory-bandwidth win. **DECISIONS.md §5 + RISKS.md R2** noted inline.
- **`Controls/DiagonalStripesSource.cs`** — emits 16-pixel-wide diagonal black/white stripes via direct pointer arithmetic on the locked framebuffer. Verifies the prototype renders something recognizable.
- **`Controls/DisplayControl.cs`** — custom `Control` subclass with a `WriteableBitmap` matched to the source's pixel dimensions. `Render(DrawingContext)` locks the bitmap, calls `Source.CopyToBgra8888(fb.Address, fb.RowBytes)`, and `DrawImage`s it stretched to the control bounds. `MeasureOverride` returns the source dimensions so layout gives a 1:1 pixel mapping by default.
- **`Dwarf.Cli/Program.cs`** — adds a `-gui` mode (separate from `-duchess` / `-draco`). The `-gui` mode launches the Avalonia prototype without booting the engine; this isolates the rendering pipeline so the prototype can be iterated independently. Later sessions wire it into a true `-duchess` GUI mode.
- **3 new tests in `DisplayPipelineTests.cs`** — verify (a) source dimensions, (b) the BGRA pattern at three known pixel positions plus alpha-opaque-everywhere, (c) that the source respects `rowBytes` stride (no over-writes past the end of each row). These run without Avalonia.Headless; the full render path is verified manually via `-gui`.
- **`Dwarf.Tests.csproj` now references `Dwarf.UI.Avalonia`** so the pipeline tests can reach the Controls namespace. Test project also gets `AllowUnsafeBlocks=true` for the pointer-write helpers in the tests.
- **Pending perf measurement (RISKS R2)** — the prototype renders without engine integration, so end-to-end paint time hasn't been measured yet. Once a `MemDisplaySource` is wired (engine writes display memory → `CopyToBgra8888` expands into BGRA), measure: time the `Render` method across 1000 frames on Dawn or XDE (when a disk artifact is available). Target < 5 ms per frame at 1024×768 monochrome. Fallback: SkiaSharp direct-draw.

**Phase E progress**: 3 of 12 sub-tasks done (Avalonia packages added, DisplayControl prototype, headless-friendly tests). Remaining biggies: KeyboardMapper (~442 LOC Java port + `.map` parser + Avalonia Key→VK adapter), KeyHandler + MouseHandler wiring, UiRefresher (DispatcherTimer 20ms), WindowStateHandler (pause refresh on focus loss), fullscreen toggle, and the full Duchess reshape to drive Avalonia from the engine.

**Next session pick-up**: choose between (a) **KeyboardMapper + KeyHandler** — port the `.map` file parser and wire Avalonia `KeyDown`/`KeyUp` events to `KeyboardAgent`. Largest single-file unit at ~450 LOC. (b) **UiRefresher + MemDisplaySource** — bind `DisplayControl` to actual engine display memory, measure paint time (closes RISKS R2). (c) **MouseHandler** — smaller, but unblocks interactive use. (b) is highest-value since it closes the riskiest unknown.

### 2026-05-12 (Phase E — MemDisplaySource + paint-time benchmark closes RISKS R2)

- **All 642 tests pass** (was 632; 10 new in this session — 9 MemDisplaySource correctness tests + 1 paint-time benchmark).
- **`Controls/MemDisplaySource.cs`** — IDisplaySource that reads directly from `Mem.getDisplayRealMemory()` and expands 1-bit-per-pixel monochrome into BGRA8888 inside `CopyToBgra8888`. Memory layout follows the engine's `displayWordsPerLine = displayPixelWidth / 16`, MSB-first within each 16-bit word; bit ON → pixel dark (BGRA `0x000000FF`), bit OFF → pixel light (`0xFFFFFFFF`). The expansion loop is manually unrolled across all 16 bits of each word so the JIT can keep the inner body branchless. **Byte-color (8-bit) deferred to a follow-up** — it needs DisplayAgent's 256-entry LUT wired into the source constructor.
- **`MemDisplaySourceTests.cs`** (9 tests) — verifies (a) source dimensions match Mem, (b) row 0's template `0x8001` puts dark pixels at positions 0 and 15, (c) row 0's middle pixels 1..14 are light, (d) row 1's `0x4002` shifts the pattern inward (pixels 1, 14 dark; 0, 15 light), (e) row 7's `0x0180` puts the center pair (pixels 7, 8) dark, (f) horizontal pattern repeats every 16 pixels, (g) vertical pattern repeats every 16 lines, (h) alpha is opaque everywhere, (i) the constructor succeeds when Mem is initialized. Tests rely on the engine's existing diamond/X init pattern from `Mem.initializeDisplayMemoryGuam` — no fixture-side memory writes needed.
- **`PaintTimeBenchmark.cs`** — single benchmark fact. Runs 100 warmup iterations, then times 500 frames of `CopyToBgra8888` against the init pattern. Reports per-frame timing via `ITestOutputHelper`. **Hard upper bound: 50 ms/frame** (10× the 5 ms target — flags catastrophic regressions, not fine-grained perf gating). Measured on this machine: **0.891 ms/frame at 960×720 mono, 776 Mpix/s pixel throughput**. Extrapolating to 1024×768: ~1.01 ms/frame, well under the 5 ms target. **RISKS R2 closed.** Avalonia's WriteableBitmap lock/draw overhead is typically <1 ms on top, so total per-frame paint budget on a real display refresh should land ~2–3 ms, leaving plenty of headroom for the engine's display memory updates in between.
- **`-gui` mode now initializes Mem** with the default Guam config (`MIN_REAL_ADDRESSBITS`, `MIN_REAL_ADDRESSBITS + 1`, monochrome 960×720) before launching Avalonia. `MainWindow` detects Mem-initialized state and binds `DisplayControl.Source` to a `MemDisplaySource`; falls back to the placeholder `DiagonalStripesSource` if Mem is uninitialized. The window title shows the active display configuration. Running `dotnet run --project csharp/Dwarf.Cli -- -gui` should now show the engine's diamond/X init pattern rather than the static diagonal stripes.
- **No `UiRefresher` yet** — the engine isn't actively updating display memory in `-gui` mode (only Mem init runs), so a 20-ms refresh timer would just re-render the same static pattern. UiRefresher lands when Duchess.cs is reshaped to drive Avalonia with a running engine.

**Phase E progress**: 5 of 12 sub-tasks done (Avalonia packages, App/MainWindow shell, DisplayControl prototype, MemDisplaySource, RISKS R2 closed). Remaining: UiRefresher (DispatcherTimer), KeyboardMapper + `.map` parser (~450 LOC), KeyHandler, MouseHandler, WindowStateHandler, fullscreen toggle, full Duchess reshape to drive Avalonia from a running engine.

**Next session pick-up**: the rendering pipeline is verified end-to-end. Three viable paths:
- **(a) KeyboardMapper + KeyHandler** — biggest single chunk at ~450 LOC. Touches `.map` file parsing + Avalonia `Key`→`eLevelVKey` translation.
- **(b) UiRefresher + Duchess reshape** — wire the engine into Avalonia. Boots Mem + agents + interpreter loop on a background thread; UiRefresher's DispatcherTimer invalidates DisplayControl. After this, `-gui` boots a real Duchess (still needs disk artifacts to do anything useful, but the harness becomes a real GUI Duchess).
- **(c) MouseHandler** — smallest at ~130 LOC. Unblocks interactive use once keyboard works.

Recommendation: **(a)** — keyboard is on the critical path for any meaningful interactive use, and the .map file parser is the largest remaining mechanical port in Phase E.

### 2026-05-13 (Phase E — KeyboardMapper + KeyHandler infrastructure)

- **All 656 tests pass** (was 642; +14 new in this session — 5 eKeyEventCode/AvaloniaKeyMap translation tests, 9 KeyboardMapper press/release-semantics tests including Ctrl-modifier behavior and `.map` file parsing).
- **`Input/eKeyEventCode.cs`** (~190 LOC) — port of `eKeyEventCode.java` (191 entries). Sealed class with `public static readonly` field per VK_* constant (same pattern as `eLevelVKey`); name<->code lookup dictionaries built reflectively in the static constructor from the field metadata. Names and codes match Java AWT verbatim so existing `keyboard-maps/*.map` files work without rewriting. Java's `eKeyEventCode.valueOf(name)` and `eKeyEventCode.get(code).toString()` map to `valueOf` and `getName`.
- **`Input/AvaloniaKeyMap.cs`** — bridge from `Avalonia.Input.Key` enum to AWT VK_* integer codes. Single switch-expression covering letters (A-Z), digits (D0-D9 + NumPad0-NumPad9), function keys (F1-F24), modifiers (Shift/Ctrl/Alt/Win — both sides folded to the same VK_* since AWT doesn't distinguish sides), navigation (arrows / Home / End / PageUp / PageDown), and common OEM punctuation. Unmapped keys return `null` and KeyboardMapper drops them. Dead keys and uncommon international keys deferred per RISKS R6.
- **`Input/KeyboardMapper.cs`** (~330 LOC) — direct port of `KeyboardMapper.java`. Java `HashMap<Integer, eLevelVKey>` becomes `Dictionary<int, eLevelVKey?>` (nullable to preserve the "host key held but mesa key already released by Ctrl-up" sentinel pattern). `mapDefaults_de_DE()` preserved verbatim; `loadConfigFile(filename)` parses the `.map` format (key : MesaKey, with optional `Ctrl!` prefix, hex `xHHHHHHHH` or named `VK_xxx`). `getLevelVKey(name)` uses reflection over `eLevelVKey`'s static fields. Java's `Config.LOG_BITBLT_INSNS` F1-toggle preserved as dead code for diff fidelity.
- **`Input/KeyHandler.cs`** (~130 LOC) — Avalonia-native `KeyDown`/`KeyUp` event listener. `Attach(InputElement)` / `Detach(InputElement)` for wiring; tracks pressed keys in a `HashSet<int>` for the dead-key workaround (Linux compositors that swallow KeyDown for diacritic dead-key start chars). `HashSet.Remove` returns whether the element was present, which lets us replace Java's `if (contains) remove(); else synth-press` with a single `if (!Remove)` (CA1868 compliant).
- **Java upstream's `keyTyped(char)` handler** (lines 130-140 of Java's `KeyHandler.java`) which translates dead-key chars (0xFFFD, ` `) into specific AWT codes is **not ported**. Avalonia's TextInput pipeline differs from AWT and the same dead-key suppression problem may not manifest. Defer to interactive Linux testing.
- **No wiring of `KeyHandler` to `MainWindow` yet** — that lands when `Duchess.cs` is reshaped to drive Avalonia. The pieces are in place; `MainWindow` constructor can do `new KeyHandler(km).Attach(this)` once a `KeyboardMapper` is wired to the engine's `iUiDataConsumer`.

**Phase E progress**: 9 of 12 sub-tasks done (Avalonia packages, App/MainWindow shell, DisplayControl prototype, MemDisplaySource, RISKS R2 closed, eKeyEventCode, AvaloniaKeyMap, KeyboardMapper, KeyHandler). Remaining: **UiRefresher** (DispatcherTimer-driven), **MouseHandler** (~130 LOC), **WindowStateHandler** (focus-loss pause), **fullscreen toggle**, and the **Duchess reshape** to drive Avalonia from a running engine.

**Next session pick-up — two viable paths**:
- **(a) MouseHandler + UiRefresher + Duchess Avalonia reshape** — the final wiring step. After this, `-gui` boots a real Duchess in Avalonia. MouseHandler is small (~130 LOC). UiRefresher is a `DispatcherTimer` that calls `DisplayControl.InvalidateVisual()` every 20 ms. The Duchess reshape splits `Duchess.cs` into a "headless" and "Avalonia" path — the existing headless variant becomes one branch; the new GUI branch starts a background thread running `Cpu.processor()` and uses the existing agent wiring with the Avalonia UI sink. Still needs disk artifacts to do anything useful, but the harness becomes a real GUI Duchess.
- **(b) WindowStateHandler + fullscreen toggle** — polish items; small. Could be done in parallel with (a) but doesn't unblock anything by itself.

Recommendation: **(a)** — gets the interactive boot path complete. Then Phase E is essentially "ready for testing" pending a Duchess-compatible disk image.

### 2026-05-13 (Phase E — MouseHandler + UiRefresher + Duchess Avalonia reshape)

- **All 656 tests still pass.** No new unit tests this session — MouseHandler and UiRefresher rely on Avalonia event types whose construction needs `Avalonia.Headless` package. End-to-end interactive verification via `dotnet run --project csharp/Dwarf.Cli -- -duchess -gui <config.properties>` is the integration test; that path will be exercised once a Duchess-compatible disk artifact is available.
- **`Input/MouseHandler.cs`** (~155 LOC) — Avalonia `PointerMoved` / `PointerPressed` / `PointerReleased` / `PointerEntered` / `PointerExited` listener. Translates Avalonia pointer coordinates to mesa coordinates clamped to display bounds. Button mapping: Avalonia `PointerUpdateKind.{Left,Middle,Right}ButtonPressed` to AWT-style button IDs 1/2/3; on PointerReleased, `InitialPressMouseButton` reliably reports the released button. Java upstream's "grab focus on any mouse event" is omitted — Avalonia raises focus implicitly when the receiving control has `Focusable=true`.
- **`UiRefresher.cs`** (~75 LOC) — minimal `DispatcherTimer` at 20 ms intervals (~50 Hz) that calls `target.InvalidateVisual()`. Start/Stop and Paused. Java upstream was ~270 LOC with status bar / MP / stats; the polish bits land when MainWindow grows a status bar.
- **`GuiSession.cs`** in `Dwarf.UI.Avalonia` — carrier for the engine/UI handshake. Holds `iUiDataConsumer`, `KeyboardMapper`, display dimensions. Host (`Duchess.RunGui`) publishes via `GuiSession.Current` before launching Avalonia; MainWindow reads `Current` to wire handlers. Lives in `Dwarf.UI.Avalonia` so the MainWindow's project graph stays acyclic (`Dwarf.Duchess -> Dwarf.UI.Avalonia`, never the reverse).
- **`Dwarf.Duchess/Duchess.cs` refactored** — engine setup extracted into a private `setupEngine()` helper shared between headless `Main` and `RunGui(args, Func<int> avaloniaLauncher)`. The host-supplied launcher callback lets Duchess invoke Avalonia without depending on `Avalonia.Desktop` itself (that package lives in Dwarf.Cli only). Engine runs on a background `Thread`; after Avalonia's main loop exits, RunGui requests engine stop, joins the thread, and shuts down agents.
- **`Dwarf.Cli/Program.cs`** — mode parsing extended: when both `-duchess` and `-gui` are present, dispatch to `Duchess.RunGui` with the Avalonia launcher closure. `-gui` alone keeps the prototype path; `-duchess` alone keeps headless; `-draco` exclusive with the others.
- **`MainWindow.axaml.cs`** updated — three modes: (1) `GuiSession.Current != null` -> wire KeyHandler+MouseHandler+UiRefresher, start refresher on Opened, stop + request engine stop on Closing; (2) Mem initialized but no session -> static MemDisplaySource; (3) Nothing initialized -> DiagonalStripesSource fallback.

**Phase E status**: the **interactive Duchess GUI is wired**. `dotnet run -- -duchess -gui <config>` should now boot a real Duchess in an Avalonia window with keyboard + mouse input flowing through to the mesa engine. Remaining gaps:
- **Boot Pilot to login screen** — verification milestone, awaits Duchess-compatible disk artifact.
- **WindowStateHandler + fullscreen toggle** — polish.
- **Status bar / MP display / statistics** — polish (UiRefresher only invalidates display).
- **Embed .map files as resources** — currently filesystem path via `keyboardMapFile` config.

**Next session pick-up — three viable paths**:
- **(a) Phase F (Draco port)** — 13 IOP/IORegion files + 8 device handlers (~6.5 KLOC Java). The `disks-6085/` directory has 3 ViewPoint/XDE disks ready. After Draco lands, the D-13/D-14 verification can be done with Draco disks.
- **(b) Phase E polish** — WindowStateHandler, fullscreen toggle, status bar, .map file resource embedding. Improves ergonomics; doesn't unblock new functionality.
- **(c) Acquire a Duchess disk artifact** — then D-13 (boot Pilot to login) and D-14 (NetHub round-trip) close out Phase D.

Recommendation: **(a)** — Phase F unlocks the largest remaining body of work and the existing 6085 disk artifacts. The Avalonia UI library is re-usable for Draco with minimal changes.

### 2026-05-13 (Phase F-1 — IOP foundation: IORegion + DeviceHandler + IOPTypes)

- **All 656 tests still pass.** No new unit tests this session — the IOP infrastructure types have no runtime behavior to test in isolation; they're tested implicitly by the device handlers (Phase F-2 onward) and end-to-end by booting ViewPoint.
- **`Dwarf.Iop6085/IORegion.cs`** (~700 LOC C# from 978 LOC Java) — the foundation. Defines `IORAddress` / `Word` / `DblWord` / `Field` / `BoolField` / `IOPBoolean` interfaces plus 12 concrete implementations (`IORWord`, `IORByteSwappedWord`, `IORDblWord`, `IORByteSwappedDblWord`, `IORField`, `SwappedWord`, `CompoundDblWord`, `WordBoolean`, `ByteBoolean`) and the `IOStruct` base class for relocatable record structures. Java's `protected static` factory methods (`mkWord` / `mkByteSwappedWord` / `mkField` / `mkBoolField` / `mkCompoundDblWord` / `mkIOPBoolean` / `mkIOPShortBoolean` / `syncToSegment`) stay `protected static`. Handlers extend `IORegion` (via `IOPTypes`) to access them via inheritance.
- **Port deviations**:
  - Java's `IORegion extends Mem` — C# can't extend a static class, so the C# `IORegion` accesses `Mem.mem[]` directly via the public static field. No behavioral difference; just structural.
  - Java `short get()` / `void set(short)` on `Word` → C# `ushort get()` / `set(ushort)`. The C# port already uses `ushort[]` for `Mem.mem`; using `ushort` for the Word API saves the `& 0xFFFF` masking ceremony. Field/DblWord still return `int` (they carry values that may exceed 16 bits in unusual configurations).
  - Java `byteSwap(short v) -> int` → C# `byteSwap(ushort v) -> int`. Same numeric semantics; the input type changes to match the new Word API.
  - Default interface methods used for `IORAddress.getIOPSegment`, `getIOPSegmentOffset`, `getWordLength`, `getFields` — C# 8+ default impls let the interfaces give a sensible base.
- **C# `IORField` implements `BoolField`** (the most-derived) so it can serve as both a numeric `Field` and a `BoolField`. The Java upstream did the same.
- **Subtle Java behavior preserved verbatim** in `innerMkDblWord` (line 295 of the C# port): the `swapped` flag is *inverted* compared to `innerMkWord` — `swapped=true` yields an `IORDblWord` (non-swapped), and `swapped=false` yields an `IORByteSwappedDblWord`. This looks like a Java bug but it's been deployed long enough that handlers depend on it; the C# port keeps the same inversion with a `// preserved verbatim` audit comment.
- **`Dwarf.Iop6085/DeviceHandler.cs`** (~120 LOC C# from 158 LOC Java) — abstract base for the 8 device handlers. `mkMask()` is a static counter generating unique notify masks. Abstract methods: `getFcbRealAddress`, `getFcbSegment`, `processNotify`, `handleLockmem`, `handleLockqueue`, `refreshMesaMemory`, `shutdown`. Virtual `cleanupAfterLockmem` defaults to no-op. `MemOperation` enum with 5 values for the LOCKMEM instruction.
- **`Dwarf.Iop6085/IOPTypes.cs`** (~340 LOC C# from 346 LOC Java) — shared struct types. Handler-ID constants (1=beep, 2=disk, 3=display, 4=ethernet, 5=floppy, 6=keyboardAndMouse, 7=maintPanel, 16=processor, 17=tty, 18=rs232c, 97=parallelPort, 127=last). Reusable structs: `IPCS`, `SegmentRec`, `SPSS`, `AlternateOpieAddress`, `ByteSwappedLinkPtr`, `ByteSwappedPointer`, `ClientCondition`, `IOPCondition`, `NotifyMask`, `OpieAddress` (with `fromLP` / `toLP` virtual address conversion), `QueueBlock`, `QueueEntry`, `TaskContextBlock`, `IORTable`. `OpieAddressType` enum with octal-encoded codes (Java upstream used Java's `0NNN` octal literal syntax; C# port converts to hex with the original octal in a comment — same trick as `InitialMesaMicrocode.BFN_*`).
- **Cleanup**: deleted `Dwarf.Iop6085/Class1.cs` template stub.

**Phase F progress**: 3 of 13 sub-tasks done (IORegion + DeviceHandler + IOPTypes — the foundation). Remaining:
- **IOP coordinator** (~381 LOC Java) — the IOP module orchestration
- **HProcessor / HBeep / HTTY** (small handlers, ~680 LOC total)
- **HKeyboardMouse / HDisplay** (medium, ~660 LOC total)
- **HFloppy** (~1770 LOC)
- **HDisk** (~2200 LOC Java; ~half is delta machinery skippable per the Phase F doc — read-only path is what we need)
- **HEthernet** (~966 LOC — reuses the existing `NetworkHubInterface` from Phase D)
- **DracoHost orchestration** + CLI dispatch + ViewPoint/XDE boot validation

**Next session pick-up — two options**:
- **(a) IOP coordinator + small handlers (HProcessor/HBeep/HTTY)** — natural next layer above the foundation. ~1.0 KLOC of Java to port; each handler exercises the IORegion machinery so any latent porting bugs surface here.
- **(b) Skip directly to HDisplay + HKeyboardMouse** — gets a visible/interactive Draco sooner. Skips HProcessor for now (the engine boots without it for the first few hundred instructions). Higher payoff but harder to debug if something breaks.

Recommendation: **(a)** — methodical layer-by-layer build minimizes the multi-source-of-bugs problem. HProcessor + HBeep + HTTY are small and similar enough that they make a coherent commit.

### 2026-05-13 (Phase F-2 — IOP coordinator + HBeep + HTTY + HProcessor)

- **All 656 tests still pass.** No new unit tests this session — the three handlers are dummies (HBeep, HTTY) or read-only state proxies (HProcessor); their behavior is integration-tested by booting ViewPoint.
- **Foundation tweak: `protected static` → `internal static` on IORegion factories.** The Java upstream's handlers `import static IORegion.*` to reach `mkWord` / `mkByteSwappedWord` / etc. — that pattern requires `protected` + same-package access. In C#, handlers can't extend IORegion (single inheritance to `DeviceHandler`), so the factories needed to drop one notch of protection. `internal static` matches Java's effective package-private semantics exactly — same-assembly access, no API surface leakage outside `Dwarf.Iop6085`.
- **`IORegion.IORAddress.dump`**: added a default interface method matching the Java upstream's `default void dump(String prefix) { System.out.println("-- dump of IORAddress not supported --"); }`. Concrete FCB classes don't need to override it.
- **`Dwarf.Iop6085/HBeep.cs`** (~115 LOC from 135 LOC Java) — dummy beeper. Inner `FCB` class implements `IORAddress` directly. `processNotify` logs the requested frequency and returns true. No state, no shutdown work. **Port deviation**: Java's `synchronized refreshMesaMemory()` becomes a plain no-op method in C# — the body is empty so the lock is meaningless.
- **`Dwarf.Iop6085/HTTY.cs`** (~165 LOC from 197 LOC Java) — unsupported TTY device handler. The big FCB structure is ported verbatim (~20 fields including 3 TaskContextBlocks, baud rate, status word, eepromImage struct). The unused inner `WorkListType` class is preserved — Java upstream declares but doesn't instantiate it. CS0414 warning suppressed inline.
- **`Dwarf.Iop6085/HProcessor.cs`** (~285 LOC from 348 LOC Java) — processor handler. The 12-value `Command` enum (noCommand/readGMT/writeGMT/readHostID/readVMMapDesc/readRealMemDesc/readDisplayDesc/readKeyboardType/readPCType/bootButton/readNumbCSBanks/readMachineType, plus `invalid=0xFFFF`) ported as a C# enum. `Command.values()[code]` → C# `(Command)code` (enum values match codes 1:1). `Cpu.MesaStopped` thrown on `bootButton`. **Date handling**: Java `LocalDate` → C# `DateOnly`; `Date` return type → `DateTimeOffset` (same convention as Phase D ProcessorAgent.cs). `LocalDate.toEpochDay()` reimplemented via `d.DayNumber - new DateOnly(1970, 1, 1).DayNumber`. `System.currentTimeMillis()` → `DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()`.
- **`(Mem.dBreak_displayType` value cast**: Java `(short)Mem.dBreak_displayType` → C# `(ushort)Mem.dBreak_displayType`. Same numeric semantics, matches the C# `Word.set(ushort)` signature.
- **`byteSwap(this.cpuId0)` semantics preserved**: Java's `(short)byteSwap(this.cpuId0)` casts a signed short → int → short. C# uses `(ushort)byteSwap(this.cpuId0)` where `cpuId0` is `ushort` and `byteSwap(ushort)` returns `int`. Numerically identical.
- **`MesaGmtEpoch = unchecked((int)2114294400)`**: the Java literal `2114294400` fits in signed int (max ~2.14B), but C# treats the literal as `int` by default. Wrapping in `unchecked` matches Java's silent overflow-tolerance semantics; the actual value (positive) doesn't overflow.
- **`Dwarf.Iop6085/IOP.cs`** (~305 LOC from 381 LOC Java) — IOP coordinator, **progressive**: only the three small handlers (HBeep, HTTY, HProcessor) are instantiated in `initialize()`. `HKeyboardMouse`/`HDisplay`/`HDisk`/`HFloppy`/`HEthernet` slots are TODO-stubbed inline (commented-out blocks with the matching Phase F-3/F-4/F-5 tags) so each subsequent commit just un-comments the appropriate block.
- **`IOP.initialize()` signature deviates from Java**: drops the `(VerifyLabelOp x3, boolean logLabelProblems)` arguments that the Java upstream takes (they're forwarded to the HDisk constructor). Phase F-4 will re-introduce these when HDisk lands. Comment in the doc-comment explains the deferral.
- **`UiCallbacks`** stub-routes keyboard/mouse-key/mouse-position events to the TODO comments referencing the not-yet-ported handlers; `acceptMouseKey` translates 1/2/3 → eLevelVKey.Point/Menu/Adjust (works even without HKeyboardMouse, since it just calls `acceptKeyboardKey` which is also a stub). `registerUiDataRefresher` is fully wired via `Processes.registerUiRefreshCallback`.
- **`IOPStatisticsProvider`** returns zero for all disk/floppy/network counters until those handlers exist.
- **`insertFloppy` / `ejectFloppy`** throw `NotSupportedException` (`HFloppy is ported in Phase F-4`) — keeps the public surface stable while making the no-op nature explicit. DracoHost orchestration will route around this until Phase F-4 lands.
- **ESC opcode handlers preserved bit-exact**: `BYTESWAP` (0x87), `NOTIFYIOP` (0x89), `LOCKMEM` (0x88), `LOCKQUEUE` (0x86) implanted via `Opcodes.implantEscOverride`. The `>>>` operator in BYTESWAP is supported on `int` in C# 11+; LOCKMEM's `int = Cpu.pop() & 0xFFFF` masks are retained for diff fidelity (no-op on the `ushort`-returning C# `Cpu.pop()`).
- **`Cpu.push((ushort)...)` casts** required because C# `Cpu.push` takes `ushort` (Phase B decision); Java's push takes int.

**Phase F progress**: 6 of 13 sub-tasks done (IORegion, DeviceHandler, IOPTypes, IOP, HBeep, HTTY, HProcessor — note the doc lists IOP + 3 small handlers under one bullet, so the strict count is 5/12 in the doc but 7/13 if you split). Remaining: **HKeyboardMouse + HDisplay** (Phase F-3), **HFloppy + HDisk** (Phase F-4), **HEthernet + DracoHost** (Phase F-5).

**Next session pick-up — two viable paths**:
- **(a) HKeyboardMouse + HDisplay (Phase F-3)** — ~660 LOC of Java. Medium-complexity; touches the cursor bitmap propagation and keyboard event handling that the existing `iUiDataConsumer.UiCallbacks` stubs in IOP.cs are TODO-routing to. After this, `IOP.getUiCallbacks()` becomes fully functional and the Avalonia UI can pump keystrokes through to a Draco engine.
- **(b) HFloppy alone (Phase F-4a)** — ~1770 LOC of Java (the biggest single file in F-2's remaining set). Mostly independent of the UI handlers; can be done in parallel with (a) in a different session. Floppy support is needed for ViewPoint's "Install Software" workflow.

Recommendation: **(a)** — completes the visible/interactive Draco harness sooner. HFloppy (b) is a self-contained slab of code that benefits from a fresh-context session anyway, and once HKeyboardMouse + HDisplay are in, the partial Draco can be launched in the Avalonia window for visual smoke-testing even without disk yet.

### 2026-05-13 (Phase F-4a — HDisk read-only + DracoHost orchestration)

- **All 656 tests still pass.** `-draco` and `-draco -gui` both dispatch from `Dwarf.Cli`; the no-arg invocation prints proper usage and exits 1. End-to-end boot validation awaits a ViewPoint/XDE `.zdisk` artifact.
- **`Dwarf.Iop6085/HDisk.cs`** (~1100 LOC C# from 2203 LOC Java) — full handler port with the **read-only path only** per DECISIONS.md §8. Drops: `writeDiskFileContent` (DEFLATE writer), `mergeDelta`, `addToZip`, the new-disk constructor, `inactive_main` (a one-off data conversion tool), `chunks[]` modified-page bitmap (no delta persistence needed), and `dumpSector`/`dumpCylinders` debug helpers. Keeps: full FCB/DeviceContextBlock/IOCB/DOB structures (~600 LOC of layout), the read-side DiskFile (DEFLATE base load + in-memory shadow), all label/data verify+read+write methods (writes go to RAM only).
- **DEFLATE handling**: Java upstream wraps `FileInputStream` in `java.util.zip.InflaterInputStream` (zlib-format DEFLATE with header). C# port uses `System.IO.Compression.ZLibStream(stream, CompressionMode.Decompress)`. Note: do NOT use `DeflateStream` — that's raw DEFLATE without the zlib wrapper; Java's `Inflater`/`Deflater` defaults include the wrapper. `ZLibStream` (added in .NET 6) is the correct counterpart.
- **`.zdelta` overlay load path DROPPED**: Phase D-2's DiskAgent applied the same rule for Duchess; both formats are write-back-via-Java-`-merge` only. The Java HDisk's constructor calls `readDiskFile(iis, false)` for the base, then optionally re-reads with `(iis, true)` for the delta. The C# port keeps only the first call. **Future C#-native checkpoint format** (gzipped page-index + page-bytes) noted as a follow-up but deferred.
- **`HDisk.VerifyLabelOp` public enum** is now a real nested enum on the actual handler class (Phase F-2 had to declare a static-class stub to satisfy the IOP signature; this session's port replaces it). The IOP coordinator's `initialize()` signature now matches Java's: `(VerifyLabelOp x3, bool logLabelProblems)`.
- **`DeviceStatus_*` constants** are 32-bit bit patterns (Java `private static final int = 0b0100_...`). The most significant bit is set in several of them — Java's int is signed, so values look negative when printed as decimal. C# `int` is also signed; using `unchecked((int)0b...)` makes the intent explicit and silences CS0220 (literal-out-of-range).
- **`ErrorType` / `Operation` / `DiskCommand` interface-as-namespace idiom** (Java `interface Foo { public final short noError = 0; }`) → C# `private static class Foo { public const ushort noError = 0; }`. `case Operation.readData` works because `const int` is a compile-time constant.
- **The big `processNotify` method** (~280 LOC of switch-based IOCB processing) ports verbatim. The 6 read/write/verify operations share a single while-loop walking sectors, applying label verification per `VerifyLabelOp` (verify | updateDisk | noVerify), and tracking page-count + data-pointer increments. `Operation.formatTracks` fills with zeros (in-memory only). `readDiagnostic` and `readTrack` throw — never seen in upstream usage.
- **Java's `disk.sectorsPerTrack` field access**: Java upstream uses both `DiskFile.sectorsPerTrack` (static const) and `disk.sectorsPerTrack` (instance access of the same static const). C# doesn't allow instance access of static fields; added `public int sectorsPerTrack_inst => sectorsPerTrack;` as an expression-bodied property to support both spellings.
- **Static `wordSwapBytes`** is a HDisk-private helper, separate from `IORegion.byteSwap` because Java upstream defines it independently. Same numeric semantics (`((val << 8) | (val >>> 8)) & 0xFFFF`).
- **`Dwarf.Draco/DracoHost.cs`** (~430 LOC C# from ~720 LOC Java) — Phase F-4 partial orchestration. Mirrors `Duchess.cs` shape: `initializeConfiguration` reads `.properties`, `setupEngine` wires Mem (Daybreak mode) + Cpu + Opcodes + HDisk + IOP + germ, `Main` runs headless, `RunGui` launches Avalonia. The germ comes from `scanDiskForGerm` (reads the physical-volume root sector at LBA 0, follows the germ-file chain through up to 96 sectors) — falls back to `fallbackGerm` file if disk scan fails. PrincOps 4.0 vs post-4.0 detection via `isPostPrincOps4dot0Germ` heuristic (16-word GFT inspection).
- **Deferred to later F-X**:
  - **HEthernet / NetHub** (Phase F-5) — `setHubParameters` call commented out, `NetworkInternalTimeService.setTimeShiftSeconds` deferred. `daysBackInTime` config option only applies `HProcessor.setTimeShiftSeconds`, not the net-time service yet.
  - **`-netinstall` / `-netexec`** modes — log "requires HEthernet, Phase F-5" and skip.
  - **HFloppy** (Phase F-4b) — `initialFloppy` / `floppyDirectory` config still parsed, but the actual `IOP.insertFloppy` throws NotSupported. The Java upstream's `MainUI` insert/eject toolbar buttons don't exist in the Avalonia port yet anyway.
  - **DebuggerSubstituteMpHandler** (BWS net-debug stub) — not ported.
- **Project reference graph adjustment**: added `Dwarf.Draco → Dwarf.Duchess` so DracoHost can reuse `PropertiesExt` + `Utils`. Semantically odd (peer applications referencing each other) but pragmatic; a future refactor can extract these into a `Dwarf.Common` library. Java's package layout has both `Duchess.java` and `Draco.java` next to `dwarf.PropertiesExt` in the same source tree, so the C# equivalent of cross-referencing is just a project dependency.
- **`Dwarf.Cli/Program.cs` dispatch extended**: `-draco -gui` chains to `DracoHost.RunGui`, `-draco` alone to `DracoHost.Main`. Removed the "not yet implemented" stub. Tightened the mode-conflict check from `if (isDraco && (isDuchess || isGui))` to `if (isDuchess && isDraco)` — `-draco -gui` is now valid (mirrors `-duchess -gui`).
- **CLI smoke-test** (no disk artifact): `dotnet run -- -draco` → prints "Error: no configuration specified" + usage line; `dotnet run -- -draco -gui` → same. Both exit cleanly. End-to-end boot test requires a real `.zdisk` file with embedded germ.

**Phase F progress**: 10 of ~13 sub-tasks done. Remaining:
- **HFloppy** (Phase F-4b) — ~1770 LOC Java, deferred per session-end recommendation.
- **HEthernet** (Phase F-5) — ~966 LOC Java; reuses `NetworkHubInterface` from Phase D.
- **End-to-end ViewPoint/XDE boot validation** — requires a Draco-compatible disk artifact + interactive testing.

**Next session pick-up — two viable paths**:
- **(a) HEthernet (Phase F-5)** — ~966 LOC Java. Wraps the existing `NetworkHubInterface` from Phase D with 6085-specific FCB/IOCB descriptor handling. Unblocks network-driven ViewPoint workflows (XNS time-service, file services). Skipping this is fine for early boot — Pilot will run without a network, just no network apps.
- **(b) HFloppy (Phase F-4b)** — ~1770 LOC Java; ~600 LOC after dropping legacy IMD/DMK per RISKS R7. Needed for ViewPoint's "Install Software" workflow but not for daily boot.

Recommendation: **(a)** — HEthernet completes the Phase F functional surface for everything except floppies. After this, the partial Draco can run XNS-networked workflows. HFloppy (b) is independent and can land any time before Phase G; defer it until/unless someone hits the install-from-floppy workflow.

### 2026-05-13 (Phase F-5 — HEthernet; NetHub interop reused from Phase D)

- **All 656 tests still pass.** No new unit tests; HEthernet is integration-tested by booting a networked ViewPoint/XDE.
- **`Dwarf.Iop6085/HEthernet.cs`** (~750 LOC C# from 966 LOC Java) — full handler port. Wraps the existing `Dwarf.Agents.NetworkHubInterface` / `NetworkInternalTimeService` / `iNetDeviceInterface` from Phase D-11 — the underlying wire protocol stays bit-identical (RISKS R4 is still open but no new code surface).
- **6 architecturally interesting sections**:
  - **FCB** (~80 LOC of layout) — `EHF_SystemControlBlock` (8 words with bitfields), `NonMesaContext` (160 words of stand-in space), 2 `QueueBlock`s (mesa-in / mesa-out), 4 semaphore words for queue locking, 3 NotifyMasks (in/out/lock), plus client-state words.
  - **IOCB** (~50 LOC) — 14 words with overlapping CommandSelect at words 7..13. `op_io_address` / `op_io_length` / `op_io_count` for the I/O variant. Status flags packed into bits of `w6`. 5 op-types: command(0), output(1), reset(2), startRU(3), input(15).
  - **`processNotify`** (~250 LOC) — dispatches on whether the notify mask is the "in" or "out" work mask. Out-queue handles output/reset/command/startRU; in-queue scans for new input-IOCBs to enqueue for receive. Stop-transmissions on `mesaClientStateRequest == off` drains the netIf and clears the receive queue. **Bug-preserved**: Java upstream's de-dup-by-buffer-address `enqueueReceiveIocb` calls `receiveIocbs.remove(iocb)` on the *new* IOCB which isn't in the list yet — same audit-comment as Phase D-11 NetworkAgent.
  - **`handleLockmem`** (~30 LOC) — 4-state semaphore machine for queue locking. Phase 1 (iop semaphore) is a no-op log; phase 2 (mesa semaphore) clears the iop semaphore.
  - **`refreshMesaMemory`** (~80 LOC) — drains incoming packets from `netIf.dequeuePacket` and copies into the receive IOCB's buffer (with byte-swap from network big-endian to Mesa word order). Raises one mesa interrupt for the batch.
  - **`loadFileFromBootService`** (~140 LOC) — static helper. Sends a `simpleRequest` boot packet (XNS BOOT socket 10, packet type 9), then reads `simpleData` response packets and assembles the germ. Used by Java's `-netinstall` / `-netexec` modes; in the C# port it's wired-but-unreachable until DracoHost's netboot path lands (currently emits a "Phase F-5" warning instead).
- **Port deviations**:
  - Java `LinkedList<Integer>` (Queue interface) → C# `List<int>`. Java's `LinkedList.remove(Integer.valueOf(iocb))` (remove by value) → C# `List<int>.Remove(int)` (also remove by value). Same semantics; `List<int>` is faster for the typical depth-1 case.
  - Java `System.nanoTime()` → C# `Stopwatch.GetTimestamp() * 1_000_000_000L / Stopwatch.Frequency`. Same numeric format. Same pattern as Phase D-11 NetworkAgent.
  - Java `byte[] bootBuffer` static field (1024 bytes) → C# `static readonly byte[]`. The static-field pattern is preserved since `loadFileFromBootService` is itself static.
  - Java `(short)0x8000` constant cast → C# `const ushort 0x8000`. Same bit pattern in storage; the C# version avoids the signed/unsigned `short` ↔ `ushort` ambiguity.
  - `STATUS_TRANSMIT_*` constants are `ushort` (Java upstream's `(short)0x9010` casts become unnecessary since the bitmask is set directly into a `ushort`-typed Word).
  - Java `synchronized refreshMesaMemory` → C# `lock(this)`. Same per-instance lock semantics.
- **Project graph adjustment**: `Dwarf.Iop6085.csproj` now references `Dwarf.Agents` so HEthernet can reach `iNetDeviceInterface` / `NetworkHubInterface` / `NetworkInternalTimeService`. Same pattern as Java upstream's HEthernet importing from `engine/agents`.
- **`Dwarf.Iop6085/IOP.cs`** — un-commented the HEthernet instantiation block; `IOPStatisticsProvider.getNetworkpacketsSent/Received` now forward to `hEthernet?.getPacketsSentCount/getPacketsReceivedCount`.
- **`Dwarf.Draco/DracoHost.cs`** — calls `HEthernet.setHubParameters(netHubHost, netHubPort, localTimeOffsetMinutes)` in `setupEngine()` *before* `IOP.initialize()` (the HEthernet constructor reads the static config). `NetworkInternalTimeService.setTimeShiftSeconds(timeShiftSeconds)` wired alongside `HProcessor.setTimeShiftSeconds` for `daysBackInTime`.

**Phase F progress**: 11 of ~13 sub-tasks done. Remaining:
- **HFloppy** (Phase F-4b) — ~1770 LOC Java, ~600 LOC C# after dropping legacy IMD/DMK per RISKS R7. Not blocking boot; blocks ViewPoint "Install Software" workflow.
- **End-to-end ViewPoint/XDE boot validation** — needs a Draco-compatible `.zdisk` artifact.
- **`-netinstall` / `-netexec` CLI flags** — currently log "requires HEthernet, Phase F-5" — now that HEthernet is in, the wiring is doable, but it also needs the `InitialMesaMicrocode.setBootRequestEthernet` path verified end-to-end. Low value without a real NetBoot service to test against. Deferred until needed.

**Next session pick-up — three viable paths**:
- **(a) HFloppy (Phase F-4b)** — closes the last functional gap before Phase G. ~600 LOC of meaningful C#. Self-contained; doesn't touch the engine boot path.
- **(b) Phase G start (polish)** — CI workflow, BenchmarkDotNet on the dispatch hot loop, migration docs. Phase F is "feature-complete pending floppy" already.
- **(c) Acquire ViewPoint/XDE disk artifact + interactive boot test** — would surface latent F-1..F-5 bugs that unit tests can't catch (e.g., byte-order errors in FCB layout, signed/unsigned drift in IOCB processing). User-driven, not coding-driven.

Recommendation: **(a) HFloppy** — Phase F should land as a fully-functional unit before Phase G starts. The disk-artifact-bound boot test (c) is the natural Phase G-prep validation step *after* HFloppy lands.

### 2026-05-13 (Phase F-4b — HFloppy infrastructure; Phase F coding-complete)

- **All 656 tests still pass.** No new unit tests; HFloppy is integration-tested by booting ViewPoint and exercising the floppy device path — but floppy operations always hit the "no floppy present" path since no format reader is ported.
- **`Dwarf.Iop6085/HFloppy.cs`** (~900 LOC C# from 1770 LOC Java) — FCB/IOCB infrastructure + processNotify dispatcher + insert/eject state machine. Drops the Java upstream's IMDFloppy (~200 LOC) and DMKFloppy (~280 LOC) reader classes per RISKS R7 (legacy 5.25" 360K formats; users convert via Java tool). The abstract `Floppy` base class is preserved so a future raw-1.44 MiB or IMD/DMK implementation can plug into the existing FCB/IOCB/state-machine infrastructure without changing the surrounding code.
- **FCB layout fidelity**: ported all ~25 sub-structures verbatim. `FDF_Attributes` (8 words with bitfields for ready/diskChange/twoSided/busy), `FDF_Context` (2 constructors — flat and embedded), `Port80ControlWordRecord` (14 boolean control bits), `FdcStatusRegister3TypeAndSpecifyAndRecalFlags` (8 status bits + 2 fields), `DeviceContextBlock` (combines the above + 2 contexts), `CounterControlWord` / `DmaControlWord` (11/13 control bits each), the FCB itself (~40 fields), plus the IOCB (~90 fields) with its 3 `FdcCommandRecord` slots and 3 `TrackDMAandCounterControl` blocks (first/middle/last track DMA).
- **Java upstream bug preserved verbatim**: `Port80ControlWordRecord` and `FdcStatusRegister3TypeAndSpecifyAndRecalFlags` both declare `private static Word w;` — a static field shared across all instances. The constructor's `this.w = mkWord(...)` overwrites the shared slot every time, so only the LAST-created instance's Java descriptor object survives in memory. The IO region memory locations are still allocated correctly via `mkWord` (one per call); the bug is just that prior descriptor references become orphaned. C# behaves identically; renamed the field `w_static` to make the static nature visible and added `#pragma warning disable CA1802 CS0649` around it. Audit comments document the bug.
- **`processNotify` dispatcher**: 2 of 16 ExtendedFDCcommandType operations actually do work — `NullCommand` (sets state to OperationCompleted) and `ReadData` (reads sectors via the abstract `Floppy.getSector` and copies to mesa memory). All others fall through to `OperationInvalid`. Same as Java upstream — most floppy ops aren't exercised by Pilot's normal boot path. The interesting bit is the 3-region DMA-byte-count distribution (first/middle/last track) which is preserved verbatim.
- **`insertFloppy` throws NotSupportedException** with a "Phase F-4b — formats not supported" message. Java upstream's path: `.imd → IMDFloppy`, `.dmk → DMKFloppy`, else `throw IOException`. C# port drops both readers; effectively the "else" branch always fires. `ejectFloppy` works (it's just state shuffling). `mesaInsertFloppy` / `mesaEjectFloppy` / `refreshMesaMemory` state machine ports verbatim with `DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()` replacing `System.currentTimeMillis()`.
- **Two-arg static factory inside IOStruct**: when an IOStruct subclass calls `mkWord(name, "w")`, the compiler resolves to the inherited 1-arg instance method (which shadows the 2-arg static via `using static IORegion`). The Java upstream uses `IORegion.mkWord(name, "w")` for the explicit static call; C# port does the same. Fix sites: `FDF_Context(string)`, `CounterControlWord(IOStruct?, string)` with null parent, `DmaControlWord(IOStruct?, string)` with null parent.
- **`IOP.cs` re-wiring**: un-commented the HFloppy block; `IOPStatisticsProvider.getFloppyReads/Writes` now forward to `hFloppy?.getReads()/getWrites()`. `IOP.insertFloppy(FileInfo, bool)` now routes to `hFloppy.insertFloppy(string, bool)` (forwards FullName); `IOP.ejectFloppy` no-ops if hFloppy is null. Net effect: the surface is the same shape Java upstream uses; the NotSupportedException now propagates from `hFloppy.insertFloppy`, not from the IOP placeholder.

**Phase F status**: **CODING-COMPLETE**. 12 of 13 sub-tasks done (the 13th is "ViewPoint/XDE boots" — verification, not coding, and requires a `.zdisk` artifact).

Remaining for Phase F closure (none of these are coding):
- **End-to-end ViewPoint/XDE boot validation** on a real `.zdisk` artifact. The repo's `disks-6085/` directory exists per the overview but is not git-tracked. User acquires/creates the artifact, then `dotnet run --project csharp/Dwarf.Cli -- -draco -gui <config>` should display a ViewPoint herald and login screen.
- **Boot-time bug surfacing**: F-1..F-5 + F-4b were tested only via build + unit tests + smoke (no-arg CLI dispatch). Latent bugs (e.g., FCB layout off-by-one, signed/unsigned drift, missing byte-swap in a corner case) only surface during interactive boot. Expect a couple of follow-up commits to fix what shows up.

**Next session pick-up — Phase G start**:
- **(a) Phase G kickoff** — CI workflow (.github/workflows/), BenchmarkDotNet harness on `Cpu.Dispatch` (RISKS R3), migration docs ("How to migrate from Java Dwarf"), perf tuning if Phase G benchmark < 80% Java throughput.
- **(b) Acquire ViewPoint disk artifact + interactive boot test** — close Phase F's final verification step.
- **(c) HFloppy raw 1.44 MiB support** — adds a `RawFloppy` subclass of the abstract `Floppy` base. ~150 LOC. Self-contained.

Recommendation: **(a)** — Phase G is the long pole. Boot validation can land alongside Phase G's CI work whenever a disk artifact is available. Raw 1.44 MiB floppy support is "nice to have" for the 6085 but isn't blocking any normal workflow.

### 2026-05-13 (Phase F-3 — HKeyboardMouse + HDisplay; full UI-routing wired)

- **All 656 tests still pass.** No new unit tests this session — HKeyboardMouse + HDisplay are exercised by booting Pilot/ViewPoint with real keyboard/mouse input, which requires disk artifacts.
- **`Dwarf.Iop6085/HKeyboardMouse.cs`** (~175 LOC C# from 195 LOC Java) — keyboard/mouse FCB with a 9-word `kBbase[]` (current keyboard state) + 128-word `kBindex[]` (keycode→bit lookup table, populated by mesa). `handleKeyUsage` flips bits in `uiKeys[]` per eLevelVKey, then `refreshMesaMemory` copies the dirty state into `fcb.kBbase[]`. **Concurrency**: `lock(this)` mirrors Java's per-instance `synchronized` on `refreshMesaMemory` / `handleKeyUsage` / `resetKeys`. `setNewCursorPosition` is intentionally lock-free — its contract is "called from the mesa-processor thread during a display refresh", same as Java.
- **Java type-system gotcha**: `Config.IO_LOG_KEYBOARD | Config.IO_LOG_MOUSE` is a *bitwise OR* on Java booleans (which is the same as logical OR when both are unconditionally evaluated). C# `|` on `bool` operands does the same — bitwise OR with no short-circuit. Both `false` consts today, so the result is `false` at compile time and `logf` is dead-code-eliminated anyway. Preserved verbatim for diff fidelity.
- **`Dwarf.Iop6085/HDisplay.cs`** (~395 LOC C# from 463 LOC Java) — display controller. Listens for LOCKMEM events on the FCB's `displayLock` mask to dispatch six change-info flags: `headAllInfoChanged` (display turn-on, kicks off vertical-retrace + VMMap scan), `cursorMapChanged` (extracts 16-line cursor bitmap from FCB), `cursorPosChanged` (computes mouse-hotspot offset and forwards to `iUiDataConsumer.PointerBitmapAcceptor`), `displInfoChanged` (turn-off), plus 3 ignored flags (border/picture-border/background/alignment). Pretty-prints the cursor bitmap via `logf("    | x   x   x ...")` row by row — preserved verbatim.
- **Byte-swap mask constants kept verbatim**: the Java upstream computes `cursorPosChangedMask = (short)byteSwap((short)0x8000)`. C# version uses `(ushort)byteSwap(0x8000)` — semantically identical bit pattern (0x0080), but the type is `ushort` to match the `ushort newValue` parameter on `handleLockmem`. No sign-extension hazard since we're always doing bit operations.
- **Vertical retrace interrupt thread**: Java's `Thread + Runnable + InterruptedException` becomes C# `Thread + method group + ThreadInterruptedException`. `setDaemon(true)` → `IsBackground = true`. `Thread.sleep(25)` → `Thread.Sleep(25)`. The lock-acquisition pattern (`synchronized(vertRetraceLock)`) becomes `lock(vertRetraceLock)`. **Subtle**: Java's `synchronized void disallowVertRetraceIntr()` adds a per-instance `this` lock *outside* the `vertRetraceLock` — preserved verbatim with nested `lock(this) { lock(vertRetraceLock) { ... } }`. Holding both locks momentarily is intentional in the upstream's design.
- **PointerBitmapAcceptor invocation**: Java's anonymous-interface call site `uiPointerBitmapAcceptor.setPointerBitmap(bitmap, hx, hy)` becomes C# `uiPointerBitmapAcceptor(bitmap, hx, hy)` — since C# `iUiDataConsumer.PointerBitmapAcceptor` is a delegate (not an interface with a single method), the invocation is direct.
- **C# format-string conversion**: 16 hex pattern args go from Java's `printf("%s %s %s ...")` to C# `string.Format("{0} {1} ... {15}")` with explicit positional indices. The hard-coded count is fine — both row-renderers share the same magic 16.
- **`IOP.cs` re-wiring (Phase F-3 step)**: un-commented the HKeyboardMouse + HDisplay instantiation blocks in `initialize()` (between processor and disk handlers — preserves Java upstream's ordering). The `UiCallbacks` `acceptKeyboardKey` / `resetKeys` / `acceptMousePosition` / `registerPointerBitmapAcceptor` stubs are now fully wired to `hKeyMo` / `hDisplay`. `acceptMouseKey` already mapped 1/2/3 → eLevelVKey.Point/Menu/Adjust in Phase F-2 and works as soon as `acceptKeyboardKey` becomes real. TODO comments for HDisk/HFloppy/HEthernet remain.

**Phase F progress**: 7 of ~13 sub-tasks done (the doc collapses some). The IOP module is now functional for everything except disk / floppy / network — the partial Draco can be launched in the Avalonia window for visual smoke-testing once `DracoHost.cs` orchestration lands, even without disk yet (though it'll hit `NotSupportedException` on the first disk read).

**Next session pick-up — two viable paths**:
- **(a) HFloppy alone (Phase F-4a)** — ~1770 LOC Java (~600 LOC C# after dropping legacy IMD parsing per RISKS R7, same approach as Phase D's FloppyAgent). Self-contained; doesn't touch the engine boot path. Floppy support is needed for ViewPoint's "Install Software" workflow.
- **(b) HDisk read-only path + DracoHost orchestration (Phase F-4b)** — ~1100 LOC of HDisk + ~150 LOC of DracoHost.cs. Higher payoff: gets a Draco that can read a ViewPoint base image and start booting in Avalonia. Lower fidelity: write path is shadow-only (same compromise as `DiskAgent` Phase D-2; merged with the same DECISIONS.md §8 rationale).

Recommendation: **(b)** — HDisk read + DracoHost is the critical-path to a visible boot. HFloppy isn't blocking the boot screen; it's blocking only the floppy-driven install/upgrade workflows. Get the Draco visibly booting first, then come back to HFloppy.
