# Migrating from Java Dwarf to C# Dwarf

This guide is for existing Java Dwarf users moving to the C# .NET 10 port. The migration is **one-way**: once you switch to C# Dwarf, the disk image format diverges and you cannot easily switch back without re-merging.

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

The C# port **reads only the canonical base disk** (per `csharp/docs/DECISIONS.md` §8). It will not read your `.zdelta` files. If you switch without merging first, C# Dwarf sees a stale disk — and any save it does will be lost on shutdown (writes go to an in-memory shadow, no checkpoint format is persisted yet).

Running `java -jar dwarf.jar -merge` folds the delta into the base, archives the old base + deltas into a timestamped `.zip` next to your disk, and gives you a new base file that C# Dwarf can read directly.

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

**What's not supported (yet)** in the C# port:
- Writing back disk changes to a delta file. Your session state lives in RAM and is lost when you close the emulator. A C#-native checkpoint format is on the roadmap.
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

**What's not supported (yet)** in the C# port:
- Writing back disk changes. Same as Duchess: in-memory shadow only.
- Floppy formats: neither IMD nor DMK nor raw 1.44 MiB. The FCB/IOCB infrastructure is wired, but `insertFloppy` always throws `NotSupportedException`. A future sub-task may add raw 1.44 MiB.
- `-netinstall` / `-netexec` netboot modes. The CLI flag is currently rejected; a future sub-task will wire it through `InitialMesaMicrocode.setBootRequestEthernet`.
- BWS net-debug stub (`DebuggerSubstituteMpHandler`). Set `stopOnNetDebug=true` works in Java only.

## Disk format compatibility

| Format | Java reads | Java writes | C# reads | C# writes |
|---|---|---|---|---|
| Guam base disk | yes | no | yes | no |
| Guam `.delta` overlay | yes | yes | **no** | no |
| 6085 `.zdisk` | yes | yes | yes | no |
| 6085 `.zdelta` overlay | yes | yes | **no** | no |
| Future C#-native checkpoint | n/a | n/a | yes | yes |

The C#-native checkpoint format will be a simpler page-index + page-bytes layout (gzipped). When it lands, the same one-way migration story applies — Java will not read the C# checkpoint.

## What happens if you skip the merge step?

- **Best case**: C# Dwarf boots with the contents of the base disk and your last session's deltas are silently ignored. The OS is in whatever state it was when the base was last merged.
- **Worst case**: the OS sees a base disk that's missing pages it expects (because Pilot's free-page bitmap on the base diverged from what's actually on the base, since the delta was supposed to fill in those pages). Pilot may halt or behave unpredictably.

Always merge first. Always.

## Rolling back to Java

If you decide to go back to Java after running C# Dwarf:

1. C# Dwarf does not persist anything. There's nothing to roll back.
2. Your original Java base + the archived `.zip` from the merge step still exist next to your disk. You can `unzip` the archive to recover the pre-merge base + delta files and continue with Java as if nothing happened.
3. If you ran the merge step, the Java base file is the merged version. Your Java session continues from that state, which is exactly what you want.

## NetHub interop

The C# port speaks the same NetHub wire protocol as the Java upstream (bit-identical per `csharp/docs/DECISIONS.md` §7). You can:

- Run a NetHub server (Java or any other implementation) and connect both Java Dwarf and C# Dwarf to it simultaneously — they'll see each other as ordinary XNS hosts.
- Migrate XNS services (DNS, Time, Auth, Filing) without re-configuration. The MAC address you've been using in `processorId` stays the same in the C# config.

## Reporting issues

If C# Dwarf misbehaves after a clean migration:

- File an issue at `https://github.com/SixOThree/dwarf` (the C# port lives on the `csharp-port` branch).
- Include your `.properties` config (redact MAC if sensitive), the OS/Mesa-image you're running, and the C# Dwarf console output.
- If your Java Dwarf runs the same disk correctly: that's strong evidence of a porting bug. Note that in the issue.
