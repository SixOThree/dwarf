# Migrating from Java Dwarf to C# Dwarf

This guide is for existing Java Dwarf users moving to the C# .NET 10 port. **As of v2.1.0-csharp, migration is reversible**: both emulators write the same `.dsk` / `.zdisk` base format on `-merge`, so you can switch between them as long as you run `-merge` first to consolidate the active overlay. The overlay formats differ (`.zdelta` for Java, `.cscheck` for C#), but neither emulator needs the other's overlay to operate.

## The short version

```powershell
# 1. Run the Java -merge step ONE LAST TIME against each disk:
java -jar dwarf.jar -merge <your-duchess-config.properties>
java -jar dwarf.jar -draco -merge <your-draco-config.properties>

# 2. Build and run C# Dwarf:
dotnet build csharp/Dwarf.slnx --configuration Release
dotnet run --project csharp/Dwarf.Cli --configuration Release -- -duchess <your-duchess-config.properties>
dotnet run --project csharp/Dwarf.Cli --configuration Release -- -draco <your-draco-config.properties>
```

That's the whole migration. Read on for the why and the caveats.

## Why the merge step is required

The Java emulator never writes back to your base disk file. Instead, on clean shutdown it writes a separate **delta file** (`.zdelta` for 6085, plain `<name>` delta for Guam) capturing pages that changed since the base. On next boot it reads `base + delta`. Rotating deltas accumulate as `<name>-YYYY.MM.DD_HH.mm.ss.SSS` backups governed by `oldDeltasToKeep`.

The C# port **does not read Java's `.zdelta` files** (per `csharp/docs/DECISIONS.md` §8). It reads only the canonical base disk format (which is shared between Java and C#) and writes its own separate `.cscheck` overlay on shutdown. If you switch without merging first, C# Dwarf sees the Java base disk in its pre-`.zdelta` state — you lose whatever changes lived in your last Java `.zdelta`.

Running `java -jar dwarf.jar -merge` folds the `.zdelta` into the base, archives the old base + deltas into a timestamped `.zip` next to your disk, and gives you a fully up-to-date base file that C# Dwarf reads directly.

## Per-emulator notes

### Duchess (Guam workstation: Dawn, XDE, GlobalView)

```powershell
# Merge first:
java -jar dwarf.jar -merge duchess-config.properties

# Then C# Dwarf, headless or with the Avalonia UI:
dotnet run --project csharp/Dwarf.Cli --configuration Release -- -duchess duchess-config.properties
dotnet run --project csharp/Dwarf.Cli --configuration Release -- -duchess -gui duchess-config.properties
```

The C# port's `Duchess.Main` reads a `.properties` file with the same keys the Java upstream uses (`boot`, `germ`, `switches`, `processorId`, `displayWidth`, `displayHeight`, `addressBitsVirtual`, `addressBitsReal`, `netHubHost`, `netHubPort`, `keyboardMapFile`, etc.). Existing configs work unmodified.

**Disk persistence** (as of v2.1.0-csharp): C# Dwarf writes session changes to a `.cscheck` overlay alongside your `.dsk` on clean shutdown (close the Avalonia window or Ctrl-C). On next boot the overlay is applied on top of the base. Rotation keeps the last `oldDeltasToKeep` overlays as timestamped `.cscheck-<timestamp>` backups. To consolidate the overlay back into the base, run C#'s `-merge`:

```powershell
dotnet run --project csharp/Dwarf.Cli --configuration Release -- -duchess -merge <config.properties>
```

This writes a fresh base `.dsk`, archives the prior base + overlays into a timestamped `.zip` alongside the disk, and clears the dirty flags. After `-merge`, the on-disk state is identical to what Java Dwarf would produce after its own `-merge` from the same starting state.

**What's not supported (yet)** in the C# port:
- Legacy IMD/DMK floppy formats — 1.44 MiB raw `.img` floppies are supported on the Guam path.

### Draco (Xerox 6085 / Daybreak: ViewPoint, XDE 5.0)

```powershell
# Merge first:
java -jar dwarf.jar -draco -merge draco-config.properties

# Then C# Dwarf:
dotnet run --project csharp/Dwarf.Cli --configuration Release -- -draco draco-config.properties
dotnet run --project csharp/Dwarf.Cli --configuration Release -- -draco -gui draco-config.properties
```

The C# port's `DracoHost.Main` reads the same `.properties` config the Java upstream uses (`boot`, `fallbackGerm`, `largeScreen`, `switches`, `labelOpOnRead`/`Write`/`Verify`, `netHubHost`, `netHubPort`, `xdeNoBlinkWorkAround`, etc.).

**Disk persistence** (as of v2.1.0-csharp): same `.cscheck` flow as Duchess. Run `-draco -merge <config>` to consolidate:

```powershell
dotnet run --project csharp/Dwarf.Cli --configuration Release -- -draco -merge <config.properties>
```

**What's not supported (yet)** in the C# port:
- Floppy formats: neither IMD nor DMK nor raw 1.44 MiB. The FCB/IOCB infrastructure is wired, but `insertFloppy` always throws `NotSupportedException`. A future sub-task may add raw 1.44 MiB.
- `-netinstall` / `-netexec` netboot modes. The CLI flag is currently rejected; a future sub-task will wire it through `InitialMesaMicrocode.setBootRequestEthernet`.
- BWS net-debug stub (`DebuggerSubstituteMpHandler`). Set `stopOnNetDebug=true` works in Java only.

## Disk format compatibility (v2.1.0-csharp)

| Format | Java reads | Java writes | C# reads | C# writes |
|---|---|---|---|---|
| Guam base disk (`.dsk`) | yes | yes (on -merge) | yes | yes (on -merge) |
| Guam `.delta` overlay | yes | yes | **no** | no |
| 6085 base disk (`.zdisk`) | yes | yes (on -merge) | yes | yes (on -merge) |
| 6085 `.zdelta` overlay | yes | yes | **no** | no |
| C# `.cscheck` overlay | **no** | no | yes | yes |

**Base disks are format-compatible in both directions.** Both emulators write the same `.dsk` and `.zdisk` formats on `-merge`. The asymmetry is only in the overlay formats — each emulator writes its own delta/checkpoint flavor that the other doesn't read.

So the round trip works:

```
Java Dwarf session  ─→  .dsk + .zdelta  ─→  (java -merge)  ─→  .dsk
                                                                 │
                          C# Dwarf reads  ─────────────────────────┘
                                  │
                                  ▼
                          .dsk + .cscheck  ─→  (C# -merge)  ─→  .dsk
                                                                  │
                          Java Dwarf reads  ─────────────────────────┘
```

You can switch between emulators freely, as long as you run `-merge` first to consolidate the active overlay back into the base before switching.

### .cscheck format spec

The C# checkpoint is a single GZipStream-compressed binary file, all little-endian:

```
Header (16 bytes):
  4 bytes  magic 'D' 'W' 'C' 'H'      (0x48435744 read as uint32 LE)
  2 bytes  version (uint16 LE)        = 1
  2 bytes  flags (uint16 LE)
           bit 0: 1 = Draco/.zdisk (266-word sectors, 10 label + 256 data)
                  0 = Duchess/.dsk (256-word pages, no label)
  4 bytes  sectorCount / pageCount    sanity check against the loaded base
  4 bytes  changedCount               number of dirty sector/page entries

Sector/page entries (× changedCount):
  4 bytes  linearIndex (uint32 LE)
  N × 2    word data (N = 266 for Draco, 256 for Duchess), little-endian
```

The format is deliberately distinct from Java's `.zdelta` (different magic, different compression wrapper, different endianness) so the two formats can never be confused.

## What happens if you skip the merge step?

- **Best case**: C# Dwarf boots with the contents of the base disk and your last Java session's `.zdelta` is silently ignored. The OS is in whatever state it was when the base was last merged on the Java side.
- **Worst case**: the OS sees a base disk that's missing pages it expects (because Pilot's free-page bitmap on the base diverged from what's actually on the base, since the delta was supposed to fill in those pages). Pilot may halt or behave unpredictably.

Always merge first on the Java side before switching to C#.

## Rolling back to Java

If you decide to go back to Java after running C# Dwarf:

1. **If you've made changes under C#**: run C#'s `-merge` first to fold the `.cscheck` into the base. Then open the base under Java — your changes are there.
2. **If you haven't made changes**: the base disk is unchanged from when you migrated; Java reads it normally.
3. **To recover the pre-migration state entirely**: the `.zip` archive next to your disk (created by Java's `-merge`) contains the original base + `.zdelta` files. Unzip and use those instead.

C#'s `-merge` produces the same `.dsk` / `.zdisk` format that Java's `-merge` does — they are format-compatible. The overlay formats (`.zdelta` vs `.cscheck`) are the only incompatible parts, and merge-then-switch always sidesteps the overlay step.

## NetHub interop

The C# port speaks the same NetHub wire protocol as the Java upstream (bit-identical per `csharp/docs/DECISIONS.md` §7). You can:

- Run a NetHub server (Java or any other implementation) and connect both Java Dwarf and C# Dwarf to it simultaneously — they'll see each other as ordinary XNS hosts.
- Migrate XNS services (DNS, Time, Auth, Filing) without re-configuration. The MAC address you've been using in `processorId` stays the same in the C# config.

## Reporting issues

If C# Dwarf misbehaves after a clean migration:

- File an issue at `https://github.com/SixOThree/dwarf` (the C# port lives on the `csharp-port` branch).
- Include your `.properties` config (redact MAC if sensitive), the OS/Mesa-image you're running, and the C# Dwarf console output.
- If your Java Dwarf runs the same disk correctly: that's strong evidence of a porting bug. Note that in the issue.
