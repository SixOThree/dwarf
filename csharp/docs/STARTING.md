# Resuming the Dwarf C# Port

This is a multi-session port. Each Claude Code session may be days or weeks apart. Follow this protocol when resuming work.

## User incantation

```
Continue the Dwarf C# port. Read csharp/docs/PROGRESS.md to find the
current phase, then csharp/docs/STARTING.md for the resume protocol.
```

## Resume protocol (for the assistant)

Execute these steps in order before touching code:

### 1. Read `PROGRESS.md`

Identify the active phase and the next sub-task. The "← next" marker points to the exact work item.

### 2. Read `00-overview.md` (skim, ~2 min)

Refresh on: what Dwarf is, scope decisions, project layout, reference graph.

### 3. Read the active phase doc

Open `0N-phase-...-md` for the active phase. This doc is self-contained — Java files to read, C# files to create, verification commands, sub-task list. **Always re-read this even if you've worked on the phase before.**

### 4. Read `DECISIONS.md` *if relevant*

Only needed when your work touches one of the 8 load-bearing technical decisions (opcode dispatch, memory model, sign-extension, threading, UI rendering, endianness, NetHub protocol, disk format). If unsure, skim DECISIONS.md anyway.

### 5. Read the specific Java file(s)

The phase doc names the Java files to transliterate. Read them at the offset/line ranges that the doc specifies. **Do not modify them — `src/` is read-only canon.**

### 6. Build the solution before changing anything

```powershell
dotnet build csharp/Dwarf.slnx --nologo
```

Confirms the workspace is in a known-good state. If the build is red, **fix the build first** — do not start new work on top of a broken tree.

### 7. Run the existing tests

```powershell
dotnet test csharp/Dwarf.slnx --nologo --verbosity quiet
```

Establishes the test baseline. Note pass/fail counts before adding new work.

### 8. Resume work from the "← next" sub-task

Implement the next sub-task per the phase doc. Use TDD for ported opcodes (port the test first, watch it fail, port the impl, watch it pass).

### 9. Update `PROGRESS.md` when done

When you complete a sub-task:
- Tick the box (`- [ ]` → `- [x]`)
- Move the "← next" marker to the following sub-task
- Update the `Last session` date
- Commit the doc change alongside the code

When you complete a phase:
- Tick the phase in the top-level checklist
- Update `Current phase` to the next phase
- Re-run all verification commands for the closing phase (see `VERIFICATION.md`)
- Commit `chore(docs): close Phase X, start Phase Y`

## Verification before claiming "done"

Per `superpowers:verification-before-completion`: **evidence before assertions**. Before marking a sub-task complete in PROGRESS.md, run the relevant verification command and confirm the output. Never claim "tests pass" without running them.

## Branch hygiene

All port work is on the `csharp-port` branch:

```powershell
git branch --show-current   # should print: csharp-port
```

If you find yourself on `master`, stop and `git checkout csharp-port` before doing anything else. `master` is reserved for syncing from `upstream/master`.

## When in doubt, ask the user

If `PROGRESS.md` is ambiguous, or two sub-tasks could plausibly be "next", ask the user which to do first. Don't guess — the cost of a wrong guess across a 20-week project compounds.

## Things NOT to do

- ❌ Do not modify anything under `../src/` (Java tree) — it is transliteration canon, frozen.
- ❌ Do not change the project reference graph without updating `00-overview.md` and `DECISIONS.md`.
- ❌ Do not rename packages / namespaces — they mirror the Java layout for traceability.
- ❌ Do not skip writing BSD-3 headers in new `.cs` files — see `csharp/BSD3-HEADER.txt`.
- ❌ Do not merge `csharp-port` into `master` until Phase G is complete.
