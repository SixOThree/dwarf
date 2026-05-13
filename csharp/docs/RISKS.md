# Risk register

Update this doc as risks resolve (mark as mitigated, add notes from sessions where the risk materialized or didn't).

## High-likelihood, high-impact

### R1. Sign-extension drift in ported opcodes
**Status**: **Closed** (2026-05-12)
**Trigger phase**: B

Java's `short` is signed, so the engine masks `& 0xFFFF` everywhere it wants unsigned semantics. The audit during Phase B must catch every place where the C# port's choice of `short` / `ushort` / `int` differs from Java's effective intent.

**Resolution**: Phase B fidelity gate cleared with **618 tests passing** (608 ported + 10 dispatch/fixture smoke tests). Ch05 (193 tests, the most signed/unsigned-exposed chapter) passed first try, validating the `ushort` memory + `(short)Cpu.pop()` signed-arithmetic pattern. The full sweep covered every place Java did `& 0xFFFF` ceremony, `signExtendByte()`, or signed/unsigned arithmetic — see PROGRESS.md Phase B session entries for the per-chapter audit notes.

---

## Medium-likelihood, high-impact

### R2. Avalonia `WriteableBitmap` per-pixel throughput
**Status**: **Closed** (2026-05-12)
**Trigger phase**: E

`DisplayPane` writes the entire framebuffer to a `BufferedImage` backing array up to 50 times per second. Avalonia `WriteableBitmap` per-pixel throughput at 1024×768 @ 50 Hz is unproven in this port.

**Resolution**: `MemDisplaySource.CopyToBgra8888` measured at **0.891 ms/frame at 960×720 monochrome** (776 Mpix/s; extrapolated ~1.01 ms/frame at 1024×768). 5× under the 5 ms target with plenty of headroom for the Avalonia framework's lock/draw overhead. See `PaintTimeBenchmark.cs` for the measurement; PROGRESS.md 2026-05-12 entry for the analysis. No SkiaSharp fallback needed.

---

## Medium-likelihood, medium-impact

### R3. .NET JIT slower than HotSpot on `Action[]` dispatch
**Status**: **Closed** (2026-05-13)
**Trigger phase**: B (verification at end) — actually measured in Phase G-2

Mesa is interpreter-heavy. The `opcTable[opcode]()` virtual call through a delegate array is the absolute hot path. .NET's tiered JIT may or may not match HotSpot here.

**Resolution**: `Dwarf.Benchmarks.InterpreterLoopBenchmark` measures the 12-instruction loop from `MiscTests.test_SampleCode` via BenchmarkDotNet. Measured on the development workstation (.NET 10.0.6 RyuJIT AVX2 on Windows 11):

| Variant | ns/instruction | insns/sec | Allocations |
|---|---:|---:|---:|
| Pure dispatch (no interrupt checks) | 6.37 ns | 157 M | 0 |
| Full interpreter loop (throttled timeout) | 6.78 ns | 147 M | 0 |

Java baseline (MiscTests): ~36 M insns/sec on the same machine.

The C# port runs at **~4× Java throughput** with zero per-operation allocations. RyuJIT inlines the dispatch table lookup and the opcode bodies effectively; the housekeeping overhead (interrupt + throttled timeout checks) is only ~6.5%. None of the proposed mitigations (`AggressiveInlining`, source-generated switch, PGO toggle) are needed. RISKS R3 closed without action.

See `csharp/Dwarf.Benchmarks/InterpreterLoopBenchmark.cs` for the benchmark code, and PROGRESS.md 2026-05-13 entry for the analysis.

### R4. NetHub wire-protocol bugs
**Status**: Open
**Trigger phase**: D

The NetHub protocol is undocumented except by the Java source. Off-by-one in length framing or endian-confusion in headers would silently corrupt traffic.

**Mitigation**: During Phase D, run the Java Dwarf and C# Dwarf side-by-side against the same NetHub server; capture pcaps on both and diff. Any byte difference is a bug in C#.

### R5. Static-init ordering coupling between Cpu / Mem / Config
**Status**: **Closed** (2026-05-12)
**Trigger phase**: A

Java tolerates static-field init dependencies surprisingly well. C# is stricter, and the Java code has tight coupling between `Cpu`, `Mem`, `Config` static state.

**Resolution**: Phase A applied the planned mitigation — all initialization is via explicit methods called in documented order: `Mem.initializeMemoryGuam(...)` / `Mem.initializeMemoryDaybreak(...)` first, then `Cpu.resetRegisters()`, then `Opcodes.initializeInstructionsPrincOpsPost40()` (or `...PrincOps40()` for legacy), then `Xfer.switchToNewPrincOps()`. `Config` is `public const bool` for dead-code elimination, so no init ordering concern. The 656 tests all use this order via `AbstractInstructionTest.prepareCpuCommon`; `Duchess.setupEngine` and `DracoHost.setupEngine` mirror it.

---

## Low-likelihood, low-medium impact

### R6. Linux dead-key keyboard workaround
**Status**: Open — tentative not-needed; awaits interactive Linux testing
**Trigger phase**: E

`KeyHandler.java` has a workaround for Linux dead-key handling (synthetic press + 50ms delay). Whether this is needed under Avalonia (which has its own platform input pipeline) is unknown.

**Phase E note**: the port skipped the workaround. Avalonia's TextInput pipeline differs from AWT and the dead-key suppression problem may not manifest. The HashSet-based `pressedKeys` tracking + `Remove` short-circuit in `KeyHandler.cs` is in place if a synthetic press is ever needed. **Awaits interactive Linux testing** — close as Closed if dead keys work without the workaround on Linux/X11 or Wayland; re-open with a port of the synthetic-press shim if they don't.

### R7. Floppy IMD / DMK formats harder than expected
**Status**: **Permanently deferred** (2026-05-13)
**Trigger phase**: D / F-4b

Legacy floppy formats (IMD, DMK) are read-only in the Java code, parsed against geometry assumptions. Porting may reveal hidden assumptions.

**Resolution**: Per the original "defer" mitigation, neither Phase D-4 (Duchess `FloppyAgent`) nor Phase F-4b (Draco `HFloppy`) ports the IMD/DMK reader classes. Java upstream's `FloppyAgent.insertFloppy` / `HFloppy.insertFloppy` reject `.imd` and `.dmk` files with a `NotSupportedException` whose message points at the Java conversion tool. Phase D-4's `FloppyAgent` supports raw 1.44 MiB images (Guam path); Phase F-4b's `HFloppy` infrastructure is wired but the abstract `Floppy` base has no concrete subclass — both formats throw on `insertFloppy`. The `IMDFloppy` (~200 LOC) and `DMKFloppy` (~280 LOC) implementations from Java are not ported. A future contributor wanting 6085 floppy support can add a `RawFloppy` subclass of the abstract `Floppy` base (~150 LOC). MIGRATION.md (Phase G-1) documents this for end users.

### R8. Migration UX friction for existing Java Dwarf users
**Status**: **Closed** (2026-05-13)
**Trigger phase**: G

Users with active Java Dwarf disk deltas must run `-merge` once before using the C# port.

**Resolution**: Phase G-1 shipped `csharp/MIGRATION.md` (~120 LOC) documenting the one-time `java -jar dwarf.jar -merge` step for both Duchess and Draco. Covers the disk-format compatibility matrix (Java reads/writes vs C# reads/writes), what's not supported in C# yet (delta write-back, IMD/DMK floppies, netinstall/netexec, BWS net-debug stub), and rollback paths. The Java `-merge` archives the pre-merge base + deltas into a timestamped `.zip` so rollback is preserve-and-restore, not destructive. Root `readme.md` links to MIGRATION.md from the C# port section.

Wrapper script was not shipped — the manual `merge then run` flow is sufficient given the one-time nature. Java JAR shipping alongside C# binaries is a release-engineering choice deferred to the release tag step.

---

## Resolved / mitigated risks

Closed risks remain in-place in their original sections (above) with `**Closed**` status and a resolution paragraph — preserves the narrative across the port lifetime rather than fragmenting it.

**Current status (2026-05-13)**:
- **Closed**: R1 (sign-extension), R2 (Avalonia paint), R3 (.NET JIT dispatch), R5 (static-init), R8 (migration UX). 5 of 8.
- **Permanently deferred**: R7 (IMD/DMK floppies). 1 of 8.
- **Open**: R4 (NetHub wire-protocol — awaits Java/C# side-by-side pcap diff), R6 (Linux dead-key — awaits interactive Linux testing). 2 of 8.

Neither open risk blocks the Phase G merge or release. Both surface during real-world usage and can be addressed in follow-up PRs.
