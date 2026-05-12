# Risk register

Update this doc as risks resolve (mark as mitigated, add notes from sessions where the risk materialized or didn't).

## High-likelihood, high-impact

### R1. Sign-extension drift in ported opcodes
**Status**: Open
**Trigger phase**: B

Java's `short` is signed, so the engine masks `& 0xFFFF` everywhere it wants unsigned semantics. The audit during Phase B must catch every place where the C# port's choice of `short` / `ushort` / `int` differs from Java's effective intent.

**Mitigation**: All 608 unit tests in `unittest/Ch0*Test.java` must pass before any agent work starts. Treat the test suite as the contract. If a test passes for Java but fails for C#, the C# code has drifted — fix the C# side, never the test.

**Watch for**: any place Java does `(short)(value & 0xFFFF)`, `(int)(value & 0xFFFFFFFFL)`, `signExtendByte()`, `signExtendShort()`, or arithmetic between `int` and `short` operands.

---

## Medium-likelihood, high-impact

### R2. Avalonia `WriteableBitmap` per-pixel throughput
**Status**: Open
**Trigger phase**: E

`DisplayPane` writes the entire framebuffer to a `BufferedImage` backing array up to 50 times per second. Avalonia `WriteableBitmap` per-pixel throughput at 1024×768 @ 50 Hz is unproven in this port.

**Mitigation**: Prototype `DisplayControl` *early* in Phase E — before doing keyboard/mouse plumbing. Measure end-to-end paint time. If `WriteableBitmap` is too slow, fall back to a custom Avalonia control that draws via SkiaSharp's `Canvas.DrawBitmap` with a pinned byte buffer.

---

## Medium-likelihood, medium-impact

### R3. .NET JIT slower than HotSpot on `Action[]` dispatch
**Status**: Open
**Trigger phase**: B (verification at end)

Mesa is interpreter-heavy. The `opcTable[opcode]()` virtual call through a delegate array is the absolute hot path. .NET's tiered JIT may or may not match HotSpot here.

**Mitigation**: BenchmarkDotNet at the end of Phase B. Target: ≥80% of Java throughput on a representative opcode mix. If short, options:
- Mark hot opcode methods `[MethodImpl(MethodImplOptions.AggressiveInlining)]`
- Replace `Action[]` with a switch statement source-generated from `[OpCode]` attributes
- Profile-guided optimization (PGO) is on by default in .NET 8+; verify it's helping

### R4. NetHub wire-protocol bugs
**Status**: Open
**Trigger phase**: D

The NetHub protocol is undocumented except by the Java source. Off-by-one in length framing or endian-confusion in headers would silently corrupt traffic.

**Mitigation**: During Phase D, run the Java Dwarf and C# Dwarf side-by-side against the same NetHub server; capture pcaps on both and diff. Any byte difference is a bug in C#.

### R5. Static-init ordering coupling between Cpu / Mem / Config
**Status**: Open
**Trigger phase**: A

Java tolerates static-field init dependencies surprisingly well. C# is stricter, and the Java code has tight coupling between `Cpu`, `Mem`, `Config` static state.

**Mitigation**: In Phase A, replace static-field initialization with explicit `Initialize(...)` methods called in a documented order. Don't rely on C#'s `static` constructor ordering.

---

## Low-likelihood, low-medium impact

### R6. Linux dead-key keyboard workaround
**Status**: Open
**Trigger phase**: E

`KeyHandler.java` has a workaround for Linux dead-key handling (synthetic press + 50ms delay). Whether this is needed under Avalonia (which has its own platform input pipeline) is unknown.

**Mitigation**: Try the simple port first. Test on Linux during Phase E. If dead keys work without the workaround, remove it. If they don't, port the workaround.

### R7. Floppy IMD / DMK formats harder than expected
**Status**: Open (but deferred — see below)
**Trigger phase**: D (deferred)

Legacy floppy formats (IMD, DMK) are read-only in the Java code, parsed against geometry assumptions. Porting may reveal hidden assumptions.

**Mitigation**: **Defer.** 1.44 MiB raw images are the primary path. Users with IMD/DMK content can use the Java tool to convert. Re-evaluate at end of Phase D.

### R8. Migration UX friction for existing Java Dwarf users
**Status**: Open
**Trigger phase**: G

Users with active Java Dwarf disk deltas must run `-merge` once before using the C# port.

**Mitigation**: Document the migration path in Phase G. Ship the Java JAR alongside the C# build as an explicit migration utility. Consider a one-time wrapper script that runs `java -jar dwarf.jar -merge <config>` then launches the C# port.

---

## Resolved / mitigated risks

(Move risks here as they resolve, with notes on what happened.)
