/*
Copyright (c) 2017, Dr. Hans-Walter Latz (original Java implementation pattern)
Copyright (c) 2026, Matthew Dugal (C# .NET 10 port — headless variant)
All rights reserved.

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions are met:
    * Redistributions of source code must retain the above copyright
      notice, this list of conditions and the following disclaimer.
    * Redistributions in binary form must reproduce the above copyright
      notice, this list of conditions and the following disclaimer in the
      documentation and/or other materials provided with the distribution.
    * The names of the authors may not be used to endorse or promote products
      derived from this software without specific prior written permission.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS "AS IS" AND ANY EXPRESS
OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED WARRANTIES
OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE DISCLAIMED.
IN NO EVENT SHALL THE AUTHORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL,
SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO,
PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS;
OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ANY THEORY OF LIABILITY,
WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR
OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF
ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
*/

using Dwarf.Engine;

namespace Dwarf.Duchess;

// Headless implementation of `iMesaMachineDataAccessor` — the engine→UI
// callback used by `Processes.checkForTimeouts` to push display memory,
// MP-code changes, and engine statistics out to the UI. The Phase D-12
// harness has no UI, so this implementation:
//
//   - logs MP changes (one line per change) to stdout
//   - logs statistics periodically (throttled by the engine: 5 ticks per
//     second × statisticsThrottle = ~1 per second under default settings)
//   - optionally writes the raw display framebuffer to a file at a given
//     interval. The file is overwritten each time so it stays small. The
//     interval bounds how much frame data we generate (the engine drives
//     UI refresh every ~37 ms, so without throttling we'd write ~27 dumps
//     per second).
//
// Phase E will replace this with an Avalonia-backed implementation that
// drives a WriteableBitmap.
public sealed class HeadlessDisplaySink : iMesaMachineDataAccessor
{
    private readonly string? _framebufferOutPath;
    private readonly long _framebufferIntervalMs;

    private long _lastFramebufferDumpTick = 0;
    private long _lastStatsTick = 0;

    public HeadlessDisplaySink(string? framebufferOutPath, long framebufferIntervalMs)
    {
        _framebufferOutPath = framebufferOutPath;
        _framebufferIntervalMs = framebufferIntervalMs;
    }

    public void accessRealMemory(
        ushort[] realMemory, int memOffset, int memWords,
        ushort[] pageFlags, int firstPage)
    {
        if (_framebufferOutPath == null) { return; }

        long now = Environment.TickCount64;
        if (now - _lastFramebufferDumpTick < _framebufferIntervalMs) { return; }
        _lastFramebufferDumpTick = now;

        // Dump display memory as raw little-endian ushort words (matching
        // the C# port's in-memory representation). Phase E or a future
        // utility can render this to PNG; the raw form is enough for now.
        try
        {
            byte[] bytes = new byte[memWords * 2];
            Buffer.BlockCopy(realMemory, memOffset * 2, bytes, 0, memWords * 2);
            File.WriteAllBytes(_framebufferOutPath, bytes);
        }
        catch (IOException e)
        {
            Console.Error.WriteLine($"HeadlessDisplaySink: failed to dump framebuffer: {e.Message}");
        }
    }

    public void acceptMP(int mp)
    {
        Console.WriteLine($"[MP] code = {mp:D4}");
    }

    public void acceptStatistics(
        long counterInstructions,
        int counterDiskReads, int counterDiskWrites,
        int counterFloppyReads, int counterFloppyWrites,
        int counterNetworkPacketsReceived, int counterNetworkPacketsSent)
    {
        // Throttle to roughly one stats line per 2 seconds. The engine's
        // own statsThrottle is ~5 ticks per second; we damp further so the
        // console stays readable.
        long now = Environment.TickCount64;
        if (now - _lastStatsTick < 2000) { return; }
        _lastStatsTick = now;

        Console.WriteLine(
            $"[stats] insns={counterInstructions:N0} "
            + $"disk r/w={counterDiskReads}/{counterDiskWrites} "
            + $"floppy r/w={counterFloppyReads}/{counterFloppyWrites} "
            + $"net rx/tx={counterNetworkPacketsReceived}/{counterNetworkPacketsSent}");
    }
}
