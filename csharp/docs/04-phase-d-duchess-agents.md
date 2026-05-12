# Phase D — Duchess agents

**Status**: Not started
**Estimated effort**: 3–4 weeks single-engineer FTE
**Predecessor**: Phase C (engine boots germ)
**Successor**: Phase E (Avalonia UI for Duchess)

## Context recap

The engine works. Phase D adds the **Duchess (Guam) device subsystem** — the agents that let a Mesa OS like Pilot/GlobalView talk to disk, display, keyboard, mouse, floppy, and network. Each agent is a Java class implementing the Guam agent ABI: read/write commands, FCB (Function Control Block) structure in mesa memory.

After Phase D, Pilot/GlobalView can boot in C# Dwarf, but the only "display" is a headless framebuffer dump (UI comes in Phase E).

## Goals

1. All 22 agent files ported to `Dwarf.Agents` project
2. Disk agent reads a canonical base disk image (output of Java `-merge`) and supports in-memory + simple new-format checkpoint
3. Network agent + `NetworkHubInterface` talk to a NetHub server over TCP (byte-identical wire protocol)
4. A headless harness boots Pilot/GlobalView and runs successfully to login screen (no UI yet)

## Java files to read

### Core agent infrastructure (port first)

| Java path | LOC | What it contains |
|---|---:|---|
| `engine/agents/Agent.java` | 232 | Base class: FCB address, `call()` entry, `refreshMesaMemory()` |
| `engine/agents/AgentDevice.java` | 65 | Device-info struct |
| `engine/agents/Agents.java` | 490 | Agent registry, dispatch |
| `engine/agents/iNetDeviceInterface.java` | 82 | Network agent abstraction |
| `engine/agents/NullAgent.java` | 63 | No-op agent for empty slots |
| `engine/agents/ReservedAgent.java` | 63 | Reserved-slot stub |

### Storage agents

| Java path | LOC | What it contains |
|---|---:|---|
| `engine/agents/DiskAgent.java` | 973 | Disk read/write, base image load, **delta load is read-only in C#** (uses Java `-merge` output) |
| `engine/agents/DiskState.java` | 48 | Disk state struct |
| `engine/agents/FloppyAgent.java` | 1,531 | Floppy IMD/DMK parsing (legacy, optional) + raw 1.44 MiB read/write |

### I/O agents

| Java path | LOC | What it contains |
|---|---:|---|
| `engine/agents/DisplayAgent.java` | 375 | Display buffer plumbing — writes to Mem display region; UI reads it |
| `engine/agents/KeyboardAgent.java` | 135 | Key event queue |
| `engine/agents/MouseAgent.java` | 231 | Mouse position + button state |
| `engine/agents/BeepAgent.java` | 67 | Beep events |
| `engine/agents/TtyAgent.java` | 64 | Serial TTY (stub) |
| `engine/agents/SerialAgent.java` | 64 | Serial (stub) |
| `engine/agents/StreamAgent.java` | 164 | Stream abstraction |
| `engine/agents/ParallelAgent.java` | 66 | Parallel port (stub) |
| `engine/agents/ProcessorAgent.java` | 178 | Processor info / time-of-day |

### Network agents

| Java path | LOC | What it contains |
|---|---:|---|
| `engine/agents/NetworkAgent.java` | 583 | Ethernet packet queue, IOCB handling |
| `engine/agents/NetworkHubInterface.java` | 469 | TCP client speaking NetHub wire protocol; two threads + sync queues |
| `engine/agents/NetworkInternalTimeService.java` | 251 | Time service fallback when NetHub unreachable |

## C# files to create

Mirror the Java layout under `csharp/Dwarf.Agents/`. Use the same filenames (drop `.java`, add `.cs`):

- `Agent.cs`, `AgentDevice.cs`, `Agents.cs`, `INetDeviceInterface.cs`, `NullAgent.cs`, `ReservedAgent.cs`
- `DiskAgent.cs`, `DiskState.cs`, `FloppyAgent.cs`
- `DisplayAgent.cs`, `KeyboardAgent.cs`, `MouseAgent.cs`, `BeepAgent.cs`, `TtyAgent.cs`, `SerialAgent.cs`, `StreamAgent.cs`, `ParallelAgent.cs`, `ProcessorAgent.cs`
- `NetworkAgent.cs`, `NetworkHubInterface.cs`, `NetworkInternalTimeService.cs`

Plus a UI-boundary abstraction layer (no Avalonia refs!):

- `csharp/Dwarf.Agents/Ui/IDisplaySink.cs` — interface UI implements
- `csharp/Dwarf.Agents/Ui/IKeyboardSource.cs` — interface UI calls when key pressed
- `csharp/Dwarf.Agents/Ui/IMouseSource.cs` — interface UI calls when mouse moves

These let `DisplayAgent`/`KeyboardAgent`/`MouseAgent` work in a UI-free harness (Phase D) and bind to Avalonia later (Phase E).

## Implementation notes

### Disk migration path

[DECISIONS.md §8](DECISIONS.md): C# port reads only canonical base disks. **Do not port the Java delta encoder.** Implementation:

1. C# `DiskAgent` opens the base file (read-only) on init, loads into RAM
2. Disk writes go to an in-memory shadow
3. On shutdown, write a simple C#-native delta file (format: page index + page bytes, optionally gzipped). On next boot, load base + apply C# delta.
4. Document for users: to migrate, run `java -jar dwarf.jar -merge <config>` once with Java Dwarf before switching

The Java code's `loadDelta` *can* be referenced for the format intent, but the C# port writes a new, simpler format. Don't try to be wire-compatible with Java deltas.

### NetHub wire protocol

[DECISIONS.md §7](DECISIONS.md): preserve byte-for-byte. The Java code is the authoritative protocol spec — read `NetworkHubInterface.java` carefully and document the wire format inline in the C# port. Test against a running Java NetHub server:

```powershell
# Terminal 1 — start Java NetHub
java -jar nethub.jar

# Terminal 2 — Java Dwarf as reference client
java -jar dwarf.jar -duchess config.properties

# Terminal 3 — C# Dwarf as test client
dotnet run --project csharp/Dwarf.Cli -- -duchess config.properties
```

Capture pcaps from both clients with Wireshark; the byte streams must be identical for equivalent operations.

### Threading

[DECISIONS.md §4](DECISIONS.md): replace Java threads + synchronized queues with `TcpClient` + two `async` Tasks + `Channel<byte[]>`. The agent layer itself is called synchronously from the engine thread; only the NetHub TCP client uses async. Bridge async ↔ sync at the agent boundary.

### Headless harness

Build a `Dwarf.Headless` console executable (can be `Dwarf.Cli` for now) that:

1. Parses `-duchess config.properties` (port `PropertiesExt.java` minimally — just enough to read the relevant keys)
2. Wires Engine + Agents
3. Implements `IDisplaySink` / `IKeyboardSource` / `IMouseSource` as headless stubs (display dumps to file periodically; keyboard reads stdin; mouse simulated)
4. Runs the engine loop indefinitely
5. Logs status to stdout

Once this can boot to a Pilot login prompt (visible in the dumped framebuffer), Phase D is complete.

## Verification

```powershell
# Build
dotnet build csharp/Dwarf.slnx --nologo
# Expect: green

# Phase B regression
dotnet test csharp/Dwarf.slnx --filter "FullyQualifiedName~Ch0" --nologo --verbosity quiet
# Expect: 608 tests passed

# Smoke test with disk
$config = "C:\path\to\your\config.properties"
dotnet run --project csharp/Dwarf.Cli -- -duchess $config --frames-out boot.framedump
# Watch boot.framedump grow as Pilot loads; after some seconds, dump should show
# pixel data resembling a Pilot login screen.

# NetHub round-trip (requires a running Java NetHub server)
# Configure two emulator instances with the same netHubHost; ping between them
```

## Sub-tasks

- [x] Port agent infrastructure: `Agent`, `AgentDevice`, `Agents`, `iNetDeviceInterface`, `NullAgent`, `ReservedAgent` — `Agents.cs` is *progressive*, only NullAgent/ReservedAgent/the six D-8 stubs are wired; the disk/floppy/network/keyboard/mouse/display slots are TODO comments that the corresponding sub-task fills in
- [ ] Port `DiskAgent` (base-image-only read path; C#-native checkpoint write path)
- [x] Port `DiskState`
- [ ] Port `FloppyAgent` (raw 1.44 MiB only; IMD/DMK deferred per RISKS R7)
- [ ] Port `DisplayAgent` + define `IDisplaySink` boundary
- [ ] Port `KeyboardAgent` + define `IKeyboardSource` boundary
- [ ] Port `MouseAgent` + define `IMouseSource` boundary
- [x] Port `BeepAgent`, `TtyAgent`, `SerialAgent`, `StreamAgent`, `ParallelAgent`, `ProcessorAgent` (small) — all six FCBs allocated; ProcessorAgent provides clock + machine info, others are no-op stubs as in Java
- [ ] Port `NetworkAgent`
- [ ] Port `NetworkHubInterface` (TcpClient + 2 async tasks + Channel<byte[]>)
- [ ] Port `NetworkInternalTimeService`
- [ ] Wire headless harness in `Dwarf.Cli` (`-duchess` flag dispatch, framebuffer dump output)
- [ ] Boot Pilot/GlobalView to login screen (visible in framedump)
- [ ] NetHub round-trip verified byte-identical to Java
- [ ] Commit: `feat(agents): Phase D — Duchess agents; Pilot boots headless; NetHub interop`

## Hand-off

When done: tick Phase D in PROGRESS.md, set active phase to E, commit doc changes.
Note in PROGRESS.md: which OS image successfully boots headless (Dawn? XDE? GlobalView?). This is useful information for Phase E debugging.
