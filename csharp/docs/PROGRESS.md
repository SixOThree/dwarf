# Dwarf C# Port — Progress

**Current phase**: A (Foundation)
**Started**: 2026-05-12
**Last session**: 2026-05-12

## Phase status

- [x] **Phase 0**: Scaffolding + per-phase docs
- [ ] **Phase A**: Foundation             ← active
- [ ] **Phase B**: Opcodes + tests
- [ ] **Phase C**: Engine completeness
- [ ] **Phase D**: Duchess agents
- [ ] **Phase E**: Avalonia UI for Duchess
- [ ] **Phase F**: Draco port
- [ ] **Phase G**: Polish

## Phase A sub-tasks (active)

See `01-phase-a-foundation.md` for details.

- [ ] Port `PrincOpsDefs.java` → `Dwarf.Engine/PrincOpsDefs.cs`     ← next
- [ ] Port `PilotDefs.java` → `Dwarf.Engine/PilotDefs.cs`
- [ ] Port `Config.java` → `Dwarf.Engine/Config.cs`
- [ ] Port `Mem.java` skeleton → `Dwarf.Engine/Mem.cs` (allocation, page map, accessors only — no opcode logic)
- [ ] Port `Cpu.java` skeleton → `Dwarf.Engine/Cpu.cs` (registers, traps, stack — no dispatch yet)
- [ ] Port `AbstractInstructionTest.java` → `Dwarf.Tests/AbstractInstructionTest.cs`
- [ ] Verify: solution builds, xUnit discovers tests, all expected to fail predictably

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
