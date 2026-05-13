# Phase E — Avalonia UI for Duchess

**Status**: In progress — scaffolding + DisplayControl prototype landed 2026-05-12
**Estimated effort**: 2–3 weeks single-engineer FTE
**Predecessor**: Phase D (Duchess agents; headless boot works)
**Successor**: Phase F (Draco port)

⚠ **Riskiest UX phase** — prototype the display rendering *first*.

## Context recap

The engine boots and the agents work headlessly. Phase E gives Duchess a real Avalonia 11 UI: window, menu bar, toolbar, display rendering, keyboard/mouse plumbing, fullscreen toggle, focus-aware refresh. After Phase E, a user can run `dotnet run --project csharp/Dwarf.Cli -- -duchess config.properties` and interact with Dawn / XDE / GlobalView through the C# UI.

## Goals

1. Avalonia 11 application hosts a `MainWindow` for Duchess
2. `DisplayControl` renders the Mesa display memory into a `WriteableBitmap` at 50 Hz
3. Keyboard input flows from Avalonia `KeyDown`/`KeyUp` events → `KeyboardMapper` → `KeyboardAgent`
4. Mouse input flows from Avalonia `PointerMoved` / `PointerPressed` / `PointerReleased` → `MouseAgent`
5. Fullscreen toggle works
6. Window-state changes (minimize/restore, focus loss) pause/resume the refresh appropriately
7. Pilot/GlobalView boots end-to-end with usable keyboard and mouse

## Java files to read

| Java path | LOC | What it contains |
|---|---:|---|
| `dwarf/DisplayPane.java` | 265 | Abstract base: `BufferedImage`, cursor caching |
| `dwarf/DisplayMonochromePane.java` | 86 | Concrete monochrome (`TYPE_BYTE_GRAY`) |
| `dwarf/Display8BitColorPane.java` | 96 | Concrete 8-bit color (`TYPE_INT_RGB`) |
| `dwarf/MainUI.java` | 349 | JFrame, toolbar, floppy buttons, fullscreen toggle |
| `dwarf/UiRefresher.java` | 274 | Swing Timer (20 ms) → repaint; engine→UI bridge |
| `dwarf/KeyHandler.java` | 142 | `KeyListener` with Linux dead-key workaround |
| `dwarf/KeyboardMapper.java` | 442 | `.map` file parser + AWT KeyEvent → mesa eLevelVKey mapping |
| `dwarf/MouseHandler.java` | 124 | Mouse listeners forwarding to engine |
| `dwarf/eKeyEventCode.java` | 246 | AWT VK_* → human-name mapping |
| `dwarf/PropertiesExt.java` | 113 | Config persistence |
| `dwarf/WindowStateListener.java` | 118 | Pause refresh on focus loss |
| `dwarf/TestUiDataConsumer.java` | 291 | Mock UI consumer (already partly done in Phase D as headless harness) |
| `dwarf/DebuggerSubstituteMpHandler.java` | 180 | Debugger handler (low priority) |
| `engine/eLevelVKey.java` | n/a | Mesa side of the keyboard mapping |
| `engine/iUiDataConsumer.java` | n/a | UI ↔ Engine boundary interface |

Also: `keyboard-maps/kbd_linux_de_DE.map` (sample `.map` format — docs are inline at top of file)

## C# files to create

| C# path | Source / Purpose | Approx LOC |
|---|---|---:|
| `csharp/Dwarf.UI.Avalonia/App.axaml` + `App.axaml.cs` | Application root | (axaml + ~30 cs) |
| `csharp/Dwarf.UI.Avalonia/MainWindow.axaml` + `.cs` | Main window: menu, toolbar, display host, status bar | ~axaml + 300 cs |
| `csharp/Dwarf.UI.Avalonia/DisplayControl.cs` | Custom control with `WriteableBitmap`; binds to `IDisplaySource` | ~250 |
| `csharp/Dwarf.UI.Avalonia/MonochromeDisplay.cs` | Mono-specific pixel packing | ~80 |
| `csharp/Dwarf.UI.Avalonia/ColorDisplay.cs` | 8-bit color LUT pixel packing | ~100 |
| `csharp/Dwarf.UI.Avalonia/UiRefresher.cs` | `DispatcherTimer` 20 ms; calls `DisplayControl.InvalidateVisual()` | ~250 |
| `csharp/Dwarf.UI.Avalonia/KeyHandler.cs` | Wires Avalonia `KeyDown`/`KeyUp` to `KeyboardAgent` via `IKeyboardSink` | ~120 |
| `csharp/Dwarf.UI.Avalonia/KeyboardMapper.cs` | Ports `.map` parser; maps Avalonia `Key` → mesa `LevelVKey` | ~450 |
| `csharp/Dwarf.UI.Avalonia/MouseHandler.cs` | Wires Avalonia pointer events to `MouseAgent` via `IMouseSink` | ~130 |
| `csharp/Dwarf.UI.Avalonia/KeyCodeMap.cs` | Avalonia `Key` → name mapping (parallel to `eKeyEventCode.java`) | ~250 |
| `csharp/Dwarf.UI.Avalonia/PropertiesLoader.cs` | Loads `.properties` config | ~110 |
| `csharp/Dwarf.UI.Avalonia/WindowStateHandler.cs` | Pause refresh on focus loss / minimize | ~80 |
| `csharp/Dwarf.UI.Avalonia/FullscreenToggle.cs` | `WindowState.FullScreen` toggle, F11 keybinding | ~60 |
| `csharp/Dwarf.UI.Avalonia/keyboard-maps/` | Copy `.map` files from `keyboard-maps/` and embed | (resource) |

The `Dwarf.Duchess` project becomes the orchestrator that wires `Dwarf.Agents` to `Dwarf.UI.Avalonia` via the boundary interfaces from Phase D.

## Implementation notes

### Avalonia setup

Add NuGet packages to `Dwarf.UI.Avalonia.csproj`:

```xml
<ItemGroup>
  <PackageReference Include="Avalonia" Version="11.*" />
  <PackageReference Include="Avalonia.Desktop" Version="11.*" />
  <PackageReference Include="Avalonia.Themes.Fluent" Version="11.*" />
</ItemGroup>
```

If `Dwarf.UI.Avalonia` was created as a `classlib`, you may want to convert it (or `Dwarf.Cli`) to use `Microsoft.NET.Sdk` with `OutputType=WinExe` for the entry point. Easiest path: keep `Dwarf.UI.Avalonia` as a library, make `Dwarf.Cli` the Avalonia app entry point (it already references both Duchess and Draco).

### `DisplayControl` — the perf-critical bit

[DECISIONS.md §5](DECISIONS.md) + [RISKS.md R2](RISKS.md). **Prototype this FIRST in the phase.**

```csharp
public sealed class DisplayControl : Control
{
    private WriteableBitmap _bitmap;
    private IDisplaySource _source;

    public override void Render(DrawingContext ctx)
    {
        // 1. Engine writes have happened to the source's framebuffer
        // 2. Copy framebuffer into _bitmap's backbuffer:
        using (var fb = _bitmap.Lock())
        {
            _source.CopyTo(fb.Address, fb.Size, fb.RowBytes);
        }
        ctx.DrawImage(_bitmap, new Rect(Bounds.Size));
    }
}
```

Measure end-to-end paint time with a stopwatch:
- Boot Dawn or XDE
- Time `Render` across 1000 frames
- Target: < 5 ms per frame at 1024×768 monochrome

If too slow, fall back to a SkiaSharp-backed custom render. Avalonia 11 supports custom rendering via `IControlRenderer` plumbing.

### `WriteableBitmap` formats

- Monochrome: `PixelFormat.Gray8` if Avalonia 11 supports it on your target; otherwise `Bgra8888` with on-the-fly expansion. Mesa's monochrome framebuffer is **1 bit per pixel** packed — you'll need to expand to 8-bit-gray or 32-bit-BGRA during the lock/copy.
- 8-bit color: Mesa keeps a 256-entry LUT; expand 1-byte index → 32-bit BGRA pixel during copy.

### `KeyboardMapper` `.map` file

The format is documented inline at the top of `keyboard-maps/kbd_linux_de_DE.map`. It's a simple text format: `[Ctrl!]VK_KEY : MesaKeyName`. Port the parser; embed map files as resources (one per locale).

Avalonia `Key` enum is similar to AWT `VK_*` but not identical — write a small adapter in `KeyCodeMap.cs` that translates Avalonia → the same string names the `.map` file expects.

### Linux dead-key workaround

[RISKS.md R6](RISKS.md): Java had a workaround for Linux dead keys (synthetic press + 50ms delay). Avalonia's input pipeline may handle this natively. **Test first; add workaround only if needed.**

### Threading

UI thread runs Avalonia. Engine thread runs Mesa interpreter loop. Communication:

- **Engine → UI**: engine thread writes to display memory (in `Mem._mem`); UI thread reads it during `Render` (no lock needed if writes are word-atomic — Mesa display is word-aligned, and 16-bit ushort writes are atomic on x64)
- **UI → Engine**: UI thread enqueues keyboard/mouse events into `KeyboardAgent`/`MouseAgent` thread-safe queues; engine thread dequeues on next agent poll

Use `System.Threading.Channels.Channel<T>` or `ConcurrentQueue<T>` for the input queues.

### Fullscreen

```csharp
window.WindowState = WindowState.FullScreen;   // toggle on F11
```

Track previous state to restore on toggle off.

## Verification

```powershell
# Build with new packages
dotnet build csharp/Dwarf.slnx --nologo

# Phase B regression
dotnet test csharp/Dwarf.slnx --filter "FullyQualifiedName~Ch0" --nologo --verbosity quiet
# Expect: 608 tests still pass

# End-to-end smoke
dotnet run --project csharp/Dwarf.Cli -- -duchess <config.properties>
# Expect: window appears; Dawn or XDE or GlobalView boots visibly;
# keyboard input echoes correctly; mouse moves cursor; F11 toggles fullscreen;
# closing the window cleanly shuts down the engine thread
```

Manual UI-checks (do these in a session — don't skip!):
- [ ] Resize window — display scales correctly
- [ ] Click and drag — mouse drag works
- [ ] Type a sentence — characters appear
- [ ] Special keys (Ctrl, Alt, Shift combos) — work as expected
- [ ] F11 → fullscreen → F11 → restore — window state preserved
- [ ] Alt-Tab away — refresh pauses; come back — refresh resumes
- [ ] Floppy mount/unmount buttons (if implemented in toolbar) — work

## Sub-tasks

- [x] Add Avalonia NuGet packages to `Dwarf.UI.Avalonia` + entry to `Dwarf.Cli` (Avalonia 11.3.15)
- [x] **Prototype `DisplayControl`** with `WriteableBitmap` — pixel-copy pipeline verified via 3 unit tests + 9 MemDisplaySource correctness tests + `-gui` visual smoke test. **Paint time measured: 0.891 ms/frame at 960×720 mono (extrapolated ~1.01 ms at 1024×768), 5× under the 5 ms target → RISKS R2 closed.** No SkiaSharp fallback needed.
- [x] Implement `App.axaml` + `MainWindow.axaml` shell (minimal — menu/toolbar/status-bar are deferred until needed)
- [x] Port `KeyboardMapper` + `.map` file parser — `eKeyEventCode` (191 AWT VK_* constants) + `AvaloniaKeyMap` (Avalonia.Key → AWT VK_* bridge) + `KeyboardMapper` (mappings + press/release with Ctrl-modifier semantics + `.map` file parsing) all landed. `.map` file embedding deferred — current path uses a filesystem path; resource-embedding can be added when the Avalonia Duchess reshape lands.
- [x] Port `KeyHandler` (Avalonia `KeyDown`/`KeyUp` → KeyboardMapper) — `Attach(InputElement)` / `Detach(InputElement)` wire to any Avalonia visual. Dead-key workaround (synthetic press + 50 ms `Task.Delay` release) ported; the Java `keyTyped` dead-key char handler (lines 130-140 Java upstream) deferred per RISKS R6 (test on Linux first).
- [ ] Port `MouseHandler` (Avalonia pointer events → MouseAgent)
- [ ] Implement `UiRefresher` (DispatcherTimer 20 ms)
- [ ] Implement `WindowStateHandler` (pause refresh on focus loss)
- [ ] Implement fullscreen toggle (F11)
- [ ] Wire `Dwarf.Duchess` orchestration: Engine + Agents + UI
- [ ] End-to-end smoke: Dawn or XDE boots, fully interactive
- [ ] Manual UI checks (see Verification)
- [ ] Commit: `feat(ui): Phase E — Avalonia UI for Duchess; full interactive boot`

## Hand-off

When done: tick Phase E in PROGRESS.md, set active phase to F. Note in PROGRESS.md:
- Measured paint time on representative workload
- Whether SkiaSharp fallback was needed
- Whether the Linux dead-key workaround was needed
