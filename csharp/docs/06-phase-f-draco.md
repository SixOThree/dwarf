# Phase F — Draco port

**Status**: In progress — F-1 + F-2 + F-3 (HKeyboardMouse + HDisplay) landed 2026-05-13
**Estimated effort**: 3–4 weeks single-engineer FTE
**Predecessor**: Phase E (Duchess UI working)
**Successor**: Phase G (polish)

## Context recap

Duchess works end-to-end. Phase F ports the **Draco (Xerox 6085) hardware emulation** — the IOP (Input/Output Processor) and its eight device handlers. Unlike the Duchess agents which use the simpler Guam ABI, Draco emulates 6085 hardware through memory-mapped I/O regions (the IORegion infrastructure). Most of the UI from Phase E is shared with Draco; only the orchestration in `Dwarf.Draco` differs.

After Phase F, ViewPoint 2.0 / XDE 5.0 boots on emulated 6085 hardware.

## Goals

1. IOP/IORegion infrastructure ported (memory-mapped I/O descriptor system)
2. All 8 device handlers ported (HDisk, HFloppy, HEthernet, HDisplay, HKeyboardMouse, HBeep, HTTY, HProcessor)
3. `Dwarf.Cli -draco <config>` boots a 6085 OS image visibly
4. NetHub interop verified for Draco the same way as Duchess in Phase D

## Java files to read

### IOP infrastructure (port first)

| Java path | LOC | What it contains |
|---|---:|---|
| `engine/iop6085/IOP.java` | 381 | IOP coordination, dispatch to handlers |
| `engine/iop6085/IORegion.java` | 978 | Memory-mapped I/O descriptor + byte-swap helpers |
| `engine/iop6085/IOPTypes.java` | 346 | Type definitions for IOP structs |
| `engine/iop6085/DeviceHandler.java` | 158 | Base handler interface |

### Device handlers (port in roughly this order)

| Java path | LOC | What it contains |
|---|---:|---|
| `engine/iop6085/HProcessor.java` | 348 | Processor control, time |
| `engine/iop6085/HBeep.java` | 135 | Beep |
| `engine/iop6085/HTTY.java` | 197 | Serial TTY |
| `engine/iop6085/HKeyboardMouse.java` | 195 | Combined keyboard + mouse controller |
| `engine/iop6085/HDisplay.java` | 463 | Display controller |
| `engine/iop6085/HFloppy.java` | 1,770 | Floppy controller (large, has IMD-style parsing for 6085 floppies) |
| `engine/iop6085/HDisk.java` | 2,203 | Disk controller — delta loading/saving (read-only path needed in C#) |
| `engine/iop6085/HEthernet.java` | 966 | Ethernet controller, NetHub interface (Draco edition) |

## C# files to create

Mirror under `csharp/Dwarf.Iop6085/`:

- `IOP.cs`, `IORegion.cs`, `IOPTypes.cs`, `DeviceHandler.cs`
- `HProcessor.cs`, `HBeep.cs`, `HTTY.cs`, `HKeyboardMouse.cs`, `HDisplay.cs`, `HFloppy.cs`, `HDisk.cs`, `HEthernet.cs`

Plus orchestration in `Dwarf.Draco/`:

- `DracoHost.cs` — wires Engine + Iop6085 + UI (mirrors `Draco.java`)

## Implementation notes

### IORegion — memory-mapped I/O descriptors

The IOP communicates with Mesa via descriptor structs in shared memory (the "IO region"). `IORegion.java` provides:

- `IORAddress` — virtual address pointer to a descriptor
- `Field` — descriptor field accessor (byte / word / sub-word)
- `mkByteSwappedWord` — endianness helpers (Mesa BE ↔ IOP whatever)

Port these carefully. The byte-swap helpers are a real concern because the 6085 IOP's native order may differ from Mesa's. **Read every call site** to understand which direction the swap should go.

### HDisk — read canonical base only

Same approach as `DiskAgent` in Phase D: C# port reads only the canonical base disk (output of Java `-merge`). The legacy delta-loading code in `HDisk.java` (~2,200 LOC, ~half of which is delta handling) **does not need to be ported in full** — implement only the read path of a merged base; write goes to a new C#-native checkpoint format.

This saves substantial effort. Document clearly in `HDisk.cs` that the legacy delta format is intentionally not supported on the write path.

### HEthernet — uses the same NetHub protocol

The 6085 talks to the NetHub via slightly different framing than the Guam agent (it sits inside an IORegion descriptor structure). But the wire-protocol bytes that reach the NetHub server are the same. Reuse the `Dwarf.Net.NetworkHubInterface` from Phase D; wrap it with the 6085-specific descriptor handling in `HEthernet.cs`.

### Shared UI

`Dwarf.UI.Avalonia` is shared. The Draco orchestration in `Dwarf.Draco` wires the same `IDisplaySink`/`IKeyboardSource`/`IMouseSource` interfaces to the 6085 handlers (`HDisplay` writes to display memory, `HKeyboardMouse` consumes keyboard/mouse events). MainWindow can be the same; the `-draco` CLI flag dispatches to `DracoHost` instead of `DuchessHost`.

### CLI dispatch

In `Dwarf.Cli/Program.cs`:

```csharp
if (args.Contains("-duchess"))
    return await DuchessHost.RunAsync(args);
if (args.Contains("-draco"))
    return await DracoHost.RunAsync(args);
PrintUsage();
return 1;
```

## Verification

```powershell
# Build
dotnet build csharp/Dwarf.slnx --nologo

# Phase B regression
dotnet test csharp/Dwarf.slnx --filter "FullyQualifiedName~Ch0" --nologo --verbosity quiet
# Expect: 608 tests still pass

# End-to-end Draco smoke
dotnet run --project csharp/Dwarf.Cli -- -draco <draco-config.properties>
# Expect: window appears; ViewPoint 2.0 or XDE 5.0 boots visibly

# Duchess regression (Phase E still works)
dotnet run --project csharp/Dwarf.Cli -- -duchess <duchess-config.properties>
# Expect: Dawn/XDE/GlobalView still boots (no regression from Phase F changes)

# NetHub interop (Draco edition)
# Run Java NetHub server; connect Java Dwarf -draco and C# Dwarf -draco; compare pcap
```

## Sub-tasks

- [x] Port `IOPTypes.cs`, `DeviceHandler.cs`
- [x] Port `IORegion.cs` (978 LOC Java → ~700 LOC C# — careful endianness work; Java's `short get()` becomes C# `ushort get()` to match the engine's `ushort[]` memory backing)
- [x] Port `IOP.cs` (coordinator — partial: only the 3 small handlers wired; HKeyboardMouse/HDisplay/HDisk/HFloppy/HEthernet TODO-stubbed)
- [x] Port `HProcessor.cs`, `HBeep.cs`, `HTTY.cs` (small handlers first)
- [x] Port `HKeyboardMouse.cs`, `HDisplay.cs`
- [ ] Port `HFloppy.cs`
- [ ] Port `HDisk.cs` (read-only path only)
- [ ] Port `HEthernet.cs` (wraps shared `NetworkHubInterface`)
- [ ] Implement `DracoHost.cs` in `Dwarf.Draco/`
- [ ] Wire `Dwarf.Cli` `-draco` dispatch
- [ ] ViewPoint 2.0 / XDE 5.0 boots
- [ ] Duchess regression — `-duchess` still works
- [ ] Commit: `feat(iop6085): Phase F — Draco 6085 emulation; ViewPoint boots`

## Hand-off

When done: tick Phase F in PROGRESS.md, set active phase to G. Note which Draco OS image successfully booted.
