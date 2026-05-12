# Load-bearing technical decisions

These eight decisions shape the port. Diverge from them only with deliberate reason — and update this doc when you do.

---

## 1. Opcode dispatch — explicit registration, no reflection

**Java**: `engine/Opcodes.java:181-183, 270-380` uses `Class.getDeclaredFields()` reflection over a fixed list of 8 chapter classes (line 154-163) to discover `OpImpl` static fields, parses the field name (`OPC_xA2_REC` / `OPCo_x5D_RGILP_pair` / etc.) to learn the opcode number and logging arglogspec, and fills `OpImpl[256] opcTable` / `escTable`. Dispatch is then:

```java
public static void dispatch(int opcode) { opcTable[opcode].execute(); }
```

**C#**: drop reflection. Each chapter class's static constructor explicitly calls `Opcodes.Install(0xA2, "REC", OPC_xA2_REC)`. Same `OpImpl[]` (= `Action[]`) dispatch table, same `opcTable[opcode]()` semantics. Benefits:

- Zero startup reflection cost
- Compile-time detection of duplicate opcode numbers (via `Install` throwing)
- AOT-friendly if we ever pursue NativeAOT
- Eliminates the "load-bearing field-naming convention" landmine

The `OPCo_` / `OPCn_` variants (PrincOps pre-/post-4.0 global-frame architectures) become separate `InstallOld(...)` / `InstallNew(...)` calls dispatched by `InitializeInstructionsPrincOps40()` vs `InitializeInstructionsPrincOpsPost40()`.

**Defer source generator until manual registration becomes painful.** Mechanical registration calls are easier to read and debug than generator output.

---

## 2. Memory model — `ushort[]`, no `unsafe`

**Java**: `engine/Mem.java:46-53, 599-630`:
- `short[] mem` — linear word store
- `int[] pageMap` — virtual page → real page base address (pre-shifted)
- `short[] pageFlags` — vacant/protected/referenced/dirty
- `getRealAddress(longPointer, forWrite)` performs bounds + page-fault + protection checks

**C#**: `ushort[] _mem`, `int[] _pageMap`, `ushort[] _pageFlags`. Native array indexing is JIT-eliminated in hot loops. **Do not use `unsafe` pointers** — they don't measurably help and they break NativeAOT.

Use `Span<ushort>` for block-transfer slice operations (the Ch08 chapter is large and benefits from `MemoryExtensions.Copy`, `Fill`, etc.). For 32-bit doubleword reads/writes, define explicit helpers (`ReadInt32(int lp)`, `WriteInt32(int lp, int v)`) that compose two `ushort` accesses.

---

## 3. Primitive widening — exploit unsigned types

Mesa is 16-bit. Java code masks with `& 0xFFFF` everywhere because `short` is signed in Java and there's no `ushort`. C# has `ushort`, `uint`, `ulong` natively — use them and the masks disappear.

**Java sign-extension idioms** in `engine/opcodes/Ch05_Stack_Instructions.java:41-86`:

```java
// Java
short s = (short)((b & 0x0080) != 0 ? (b | 0xFFFFFF00) : (b & 0x0000007F));
```

**C# equivalent**:

```csharp
short s = (short)(sbyte)b;   // direct sign-extension via signed cast
```

**This translates 1:1 but requires audit during Phase B** — silent sign-extension drift would corrupt the 608 unit tests in subtle ways that are very hard to debug later.

---

## 4. Threading — `synchronized` / `wait` / `notify` → `lock` / `Channel<T>`

The engine is single-threaded; only `engine/agents/NetworkHubInterface.java:64-101, 267-449` uses real threads (two background loops, packet queues with `synchronized` + `wait()` / `notifyAll()`).

**C#**: replace with `TcpClient` + two `async` Tasks reading/writing on a `System.Threading.Channels.Channel<byte[]>`. No manual thread management. Static state in `Cpu` and `Mem` carries fine to `static class` in C# — no concurrent mutation problems because the engine remains single-threaded.

---

## 5. UI rendering — `BufferedImage` → `WriteableBitmap`

**Java**: `dwarf/DisplayPane.java:67-114`. `BufferedImage(TYPE_BYTE_GRAY)` for monochrome, `TYPE_INT_RGB` for 8-bit color. Engine writes directly into the backing `DataBufferByte` / `DataBufferInt` array. `UiRefresher` is a `javax.swing.Timer(20ms)` that triggers `repaint()` on the EDT.

**Avalonia**: `WriteableBitmap(width, height, dpi, PixelFormat.Gray8)` (mono) or `PixelFormat.Bgra8888` (color). Engine writes bytes between `bitmap.Lock()` and `bitmap.Unlock()` calls. UI refresh is a `DispatcherTimer { Interval = TimeSpan.FromMilliseconds(20) }` that calls `InvalidateVisual()` on the display control.

**Prototype this early in Phase E.** Per-pixel throughput at 1024×768 @ 50 Hz is the single biggest performance unknown in the port. Fallback if needed: drop down to SkiaSharp direct draw via an Avalonia custom render control.

---

## 6. Endianness — encapsulated inside `Mem`, no global concern

Mesa is big-endian. Java is big-endian by default; .NET is little-endian-native. **Critically**, the current code already encapsulates endianness inside `Mem`'s byte↔word accessor methods. There are no `DataInputStream` or `ByteBuffer.order(...)` calls leaking across the engine.

**C#** port keeps the encapsulation: `Mem.GetByte(int lp)` and `Mem.PutByte(int lp, byte v)` implement explicit `(word >> 8) & 0xFF` / `word & 0xFF` math. No `BinaryPrimitives.ReverseEndianness` calls needed at every read site.

The migration tool (Java `-merge`) handles disk-format endianness on the Java side, so the C# port reads a canonical merged base image that's already in the right shape.

---

## 7. NetHub wire protocol — preserved bit-exact

`engine/agents/NetworkHubInterface.java:44-450` speaks a length-framed binary protocol to an external NetHub server (for emulated Ethernet). Constants: `MAX_NET_PACKET_LEN=766`, `MIN_NET_PACKET_LEN=14`, `RETRY_INTERVAL=2000`.

**C#** must preserve the wire format byte-for-byte to interop with existing NetHub deployments. Test against a running Java NetHub server during Phase D — capture pcaps from the Java client and replay against the C# implementation.

---

## 8. Disk format — not ported

Per user decision: the existing Java `-merge` utility stays alive as the one-shot migration path. C# port reads only canonical (post-merge) base disks. The `agents/DiskAgent.loadDelta` / `saveDelta` logic (DEFLATE-compressed delta + ZIP archival of old deltas) is **not** ported to C#.

C# may implement its own simpler checkpoint format for in-emulator-session disk persistence. This format is owned by the C# port and not interoperable with Java Dwarf.

Floppy formats:
- 1.44 MiB raw images: trivially read/written in C# (`FileStream` + bytes)
- IMD / DMK legacy formats: read-only, optional — port deferred unless users need it
