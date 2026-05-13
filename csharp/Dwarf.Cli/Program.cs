/*
Copyright (c) 2020, Dr. Hans-Walter Latz (original Java implementation)
Copyright (c) 2026, Matthew Dugal (C# .NET 10 port)
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

using Avalonia;
using Dwarf.Engine;

// Main program for the Dwarf Mesa emulator family.
//
// Modes:
//   -duchess <config.properties>   headless Guam-machine harness (Phase D-12)
//   -draco <config.properties>      6085/Daybreak emulator (Phase F — not yet ported)
//   -gui                            Phase E prototype: Avalonia window with the
//                                   diagonal-stripes test pattern (no engine yet)
//
// Phase E in progress: the `-gui` mode will eventually swap in a
// MemDisplaySource (binding to the engine's display memory) and merge
// with `-duchess` once the orchestration is fully ported.

static int Usage()
{
    Console.WriteLine("Usage: Dwarf -duchess|-draco <machine-specific-args...>");
    Console.WriteLine("       Dwarf -gui                              (Phase E prototype window)");
    return 1;
}

if (args.Length < 1)
{
    return Usage();
}

bool isDuchess = false;
bool isDraco = false;
bool isGui = false;
var newArgs = new List<string>();
foreach (string arg in args)
{
    string lcArg = arg.ToLowerInvariant();
    if (lcArg == "-duchess")
    {
        isDuchess = true;
    }
    else if (lcArg == "-draco")
    {
        isDraco = true;
    }
    else if (lcArg == "-gui")
    {
        isGui = true;
    }
    else
    {
        newArgs.Add(arg);
    }
}

// exactly one mode must be selected
int modeCount = (isDuchess ? 1 : 0) + (isDraco ? 1 : 0) + (isGui ? 1 : 0);
if (modeCount != 1)
{
    return Usage();
}

if (isGui)
{
    // Phase E prototype: bind the Avalonia window to actual engine display
    // memory. Init Mem with the default Guam configuration (PrincOps min
    // real + virtual bits, 960×720 mono). The diamond/X test pattern from
    // Mem.initializeDisplayMemoryGuam shows up — confirms the rendering
    // pipeline reaches all the way into the engine's framebuffer.
    Mem.initializeMemoryGuam(
        PrincOpsDefs.MIN_REAL_ADDRESSBITS,
        PrincOpsDefs.MIN_REAL_ADDRESSBITS + 1,
        DisplayType.monochrome,
        960, 720);

    return Dwarf.UI.Avalonia.App.BuildAvaloniaApp()
        .UsePlatformDetect()
        .LogToTrace()
        .StartWithClassicDesktopLifetime(newArgs.ToArray());
}

if (isDuchess)
{
    return Dwarf.Duchess.Duchess.Main(newArgs.ToArray());
}

// isDraco
Console.Error.WriteLine("Dwarf -draco is not yet implemented in the C# port (planned for Phase F).");
Console.Error.WriteLine("Use the Java jar for the 6085/Daybreak emulator until then.");
return 1;
