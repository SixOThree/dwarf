# Dwarf C# Port — Progress

**Current phase**: B (Opcodes + tests)
**Started**: 2026-05-12
**Last session**: 2026-05-12

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

- [ ] Implement `OpImpl` delegate + `Opcodes.cs` dispatch + Install API     ← next
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
