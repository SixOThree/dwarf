# Dwarf C# Port — Overview

This is a multi-session port of the Dwarf Java 8 Mesa processor emulator to **C# .NET 10** with an **Avalonia 11** UI. Read this first when resuming work in a new session, then follow `STARTING.md`.

## What Dwarf is

Java emulator for the Xerox PARC Mesa processor architecture, with two front-ends sharing a common engine:

- **Duchess** — Guam-style software workstation (Dawn/XDE, GlobalView 2.1)
- **Draco** — Xerox 6085 / Daybreak hardware workstation (ViewPoint 2.0, XDE 5.0)

`DwarfMain` dispatches to one or the other based on `-duchess` / `-draco` flag.

## Scope of the port

| Decision | Choice |
|---|---|
| Goal | Modernize + improve (refactor internals, keep behavior) |
| UI framework | Avalonia 11 (cross-platform parity with Swing) |
| Front-ends | Both, Duchess first, then Draco |
| Disk binary compat | Migration tool only (Java `-merge` produces canonical base, C# reads that) |
| Solution location | `csharp/` subfolder inside this repo |
| Multi-session work | Yes — see `STARTING.md` for resume protocol |
| Git remote | `origin = SixOThree/dwarf` (fork); `upstream = devhawala/dwarf` (original) |
| Branching | All port work on `csharp-port` branch; merge to `master` after Phase G |

## Codebase inventory (Java source under `../src/`)

| Subsystem | Java path | Files | LOC |
|---|---|---:|---:|
| Engine core | `engine/` | 13 | 6,263 |
| Opcodes | `engine/opcodes/` | 9 | 5,170 |
| Duchess agents | `engine/agents/` | 22 | 6,194 |
| Draco IOP | `engine/iop6085/` | 13 | 8,138 |
| Swing UI | `dwarf/` | 14 | 2,729 |
| Entry points | `dmachine/` | 4 | 1,526 |
| Tests (JUnit 4) | `unittest/` | 8 | 7,897 — **608 test methods** |
| **Total** | | **85** | **38,081** |

Binary asset: `src/resources/base144.raw` (1.5 MiB floppy template). Keyboard maps in `keyboard-maps/*.map`. BSD-3 header on every `.java` file — preserve in ported `.cs` files.

## Repository layout

```
dwarf/                           (this repo)
├── src/                         (Java source — READ-ONLY; transliteration canon)
├── keyboard-maps/               (existing .map files)
├── disks-6085/                  (existing disk images)
├── ...
└── csharp/                      (entire C# port lives here)
    ├── Dwarf.slnx               (.NET 10 solution)
    ├── Directory.Build.props    (shared props: nullable, implicit usings, warnings-as-errors)
    ├── BSD3-HEADER.txt          (header template for new .cs files)
    ├── Dwarf.Engine/            (core: Cpu, Mem, Xfer, Processes, Config, PrincOpsDefs, PilotDefs + Opcodes/)
    ├── Dwarf.Agents/            (Duchess Guam agents)
    ├── Dwarf.Iop6085/           (Draco 6085 IOP)
    ├── Dwarf.Net/               (shared NetHub interface)
    ├── Dwarf.UI.Avalonia/       (shared Avalonia controls + interfaces)
    ├── Dwarf.Duchess/           (Duchess orchestration)
    ├── Dwarf.Draco/             (Draco orchestration)
    ├── Dwarf.Cli/               (single entry point — dispatches Duchess / Draco)
    ├── Dwarf.Tests/             (xUnit tests, ported from JUnit 4)
    └── docs/                    (multi-session port docs — this directory)
```

## Project reference graph

```
Dwarf.Agents     ──► Dwarf.Engine
Dwarf.Iop6085    ──► Dwarf.Engine
Dwarf.Net        ──► Dwarf.Engine
Dwarf.UI.Avalonia ─► Dwarf.Engine

Dwarf.Duchess    ──► Dwarf.Engine, Dwarf.Agents, Dwarf.Net, Dwarf.UI.Avalonia
Dwarf.Draco      ──► Dwarf.Engine, Dwarf.Iop6085, Dwarf.Net, Dwarf.UI.Avalonia
Dwarf.Cli        ──► Dwarf.Duchess, Dwarf.Draco
Dwarf.Tests      ──► Dwarf.Engine
```

The shared UI library does **not** reference Agents or Iop6085 directly — it exposes interfaces (`IDisplaySource`, `IKeyboardSink`, etc.) that the per-mode orchestration libraries implement and wire up. This mirrors the Java `iUiDataConsumer` interface.

## Phase summary

| Phase | Goal | Estimate |
|---|---|---|
| 0 | Scaffolding + docs | 1 day |
| A | Foundation: PrincOpsDefs, PilotDefs, Config, Mem/Cpu skeletons, test harness | 1–2 weeks |
| B | Opcodes + all 608 unit tests passing (**fidelity gate**) | 3–4 weeks |
| C | Engine completeness: Xfer, Processes, InitialMesaMicrocode, germ load | 1–2 weeks |
| D | Duchess agents (Disk, Display, Keyboard, Mouse, Floppy, Network, ...) | 3–4 weeks |
| E | Avalonia UI for Duchess — riskiest UX phase | 2–3 weeks |
| F | Draco port: IOP/IORegion + 8 device handlers | 3–4 weeks |
| G | Polish: docs, CI, perf tuning | 1–2 weeks |

**Total: 14–21 weeks single-engineer FTE; ±30% confidence window.**

Per-phase detail in `01-phase-a-foundation.md` through `07-phase-g-polish.md`.

## Where to find what

- **What's the current state of work?** → `PROGRESS.md`
- **How do I resume in a new session?** → `STARTING.md`
- **Why is X being done this way?** → `DECISIONS.md`
- **What could go wrong?** → `RISKS.md`
- **How do I verify each phase?** → `VERIFICATION.md` (or the per-phase doc)
- **What does Phase X look like?** → `0N-phase-...-md`
