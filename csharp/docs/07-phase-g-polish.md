# Phase G — Polish

**Status**: In progress — G-1 (CI workflow + MIGRATION.md + README C# port section) landed 2026-05-13
**Estimated effort**: 1–2 weeks single-engineer FTE
**Predecessor**: Phase F (Draco port)
**Successor**: (merge `csharp-port` → `master`, optionally archive Java tree)

## Context recap

Both Duchess and Draco run end-to-end with full UI. Phase G ties up loose ends: CI/CD, performance tuning, migration documentation, optional NativeAOT, README updates, and the merge back to `master`.

## Goals

1. GitHub Actions CI builds and tests on Windows, Linux, macOS for every PR
2. Performance baseline meets target: ≥80% of Java throughput on representative opcode workloads (relax target if reaching parity is too costly; document the gap)
3. Migration guide documents the Java `-merge` step for existing users
4. README updated to describe the C# port alongside (or replacing) the Java instructions
5. (Optional) NativeAOT compilation produces a small standalone binary
6. `csharp-port` branch is merged into `master`

## Tasks

### CI/CD

Create `.github/workflows/ci.yml`:

```yaml
name: CI
on: [push, pull_request]
jobs:
  build:
    strategy:
      matrix:
        os: [windows-latest, ubuntu-latest, macos-latest]
    runs-on: ${{ matrix.os }}
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with: { dotnet-version: '10.0.x' }
      - run: dotnet build csharp/Dwarf.slnx --nologo
      - run: dotnet test csharp/Dwarf.slnx --nologo --verbosity quiet
```

Optional: add a publish job that produces single-file binaries for each OS.

### Performance tuning

Run BenchmarkDotNet in Release mode:

```powershell
dotnet run -c Release --project csharp/Dwarf.Benchmarks
```

If hot opcodes are slow, options ([DECISIONS.md §1](DECISIONS.md), [RISKS.md R3](RISKS.md)):
- `[MethodImpl(MethodImplOptions.AggressiveInlining)]` on hot opcode methods
- Replace `OpImpl[]` delegate array with a generated `switch` statement (source generator over `[OpCode]` attributes)
- Enable PGO (on by default in .NET 8+; verify `<TieredPGO>true</TieredPGO>` in `Directory.Build.props` if needed)
- ReadyToRun for cold-start improvement: `<PublishReadyToRun>true</PublishReadyToRun>`

### Migration guide

Create `csharp/MIGRATION.md`:

```
# Migrating from Java Dwarf to C# Dwarf

If you have existing Java Dwarf disk images with active delta files,
run the Java -merge step ONCE before switching to C# Dwarf:

  java -jar dwarf.jar -merge <your-config.properties>

This folds your delta into the base image and archives old deltas.
The resulting base disk is what C# Dwarf reads.

After the merge, run C# Dwarf:

  dotnet run --project csharp/Dwarf.Cli -- -duchess <your-config.properties>

C# Dwarf maintains its own checkpoint format (not interoperable with
Java's delta format) — you cannot switch back-and-forth between
emulators without merging again.
```

### README update

Update root `readme.md` (existing Java README) to:
- Add a section "C# port" at the top with current status (Phase G complete; emulator works on Windows/Linux/macOS)
- Link to `csharp/docs/00-overview.md` for port details
- Keep the Java instructions for historical reference and the migration path

### (Optional) NativeAOT

If the team wants smaller binaries / faster startup:

```xml
<PropertyGroup>
  <PublishAot>true</PublishAot>
</PropertyGroup>
```

This requires:
- All reflection-using code removed (we already did this in Phase B — opcode registration is explicit)
- Trimming-safe code (no `Type.GetType(string)` etc.)
- Avalonia 11 supports AOT but check the version

Skip if not needed.

### Merge back to master

After all CI is green:

```powershell
git checkout master
git merge --no-ff csharp-port -m "feat: merge C# .NET 10 port"
git push origin master
```

Tag a release:

```powershell
git tag -a v2.0.0-csharp -m "C# .NET 10 port; both Duchess and Draco supported"
git push origin v2.0.0-csharp
```

## Verification

```powershell
# CI passes on all 3 OSes
gh run list --workflow=ci.yml --limit 1

# Full test suite
dotnet test csharp/Dwarf.slnx --nologo --verbosity quiet

# Performance regression
dotnet run -c Release --project csharp/Dwarf.Benchmarks
# Compare to Phase B baseline

# Both emulators run
dotnet run --project csharp/Dwarf.Cli -- -duchess <duchess-config>
dotnet run --project csharp/Dwarf.Cli -- -draco <draco-config>

# Migration guide step
java -jar dwarf.jar -merge <test-config>
dotnet run --project csharp/Dwarf.Cli -- -duchess <test-config>
# Expect: same OS image boots with same state in C# Dwarf
```

## Sub-tasks

- [x] Add `.github/workflows/ci.yml` for Win/Linux/macOS
- [ ] Verify CI passes on all platforms (awaits first push after this commit)
- [ ] Run BenchmarkDotNet; tune if below 80% Java throughput (deferred to Phase G-2)
- [x] Write `csharp/MIGRATION.md`
- [x] Update root `readme.md` with C# port section
- [ ] (Optional) Enable NativeAOT and verify single-file publish
- [ ] Final regression: both Duchess and Draco boot (awaits disk artifacts)
- [ ] Merge `csharp-port` → `master` (no-ff)
- [ ] Tag release
- [ ] Commit: `chore: Phase G — CI, perf tuning, migration guide`

## Hand-off

When done: 🎉 the port is shipped. Mark Phase G complete in PROGRESS.md. The repo's documentation now lives long after this initiative — anyone reading `csharp/docs/00-overview.md` can understand what was done and why.
