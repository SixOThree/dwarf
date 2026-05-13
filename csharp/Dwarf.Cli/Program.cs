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
//   -duchess <config.properties>            headless Guam-machine harness
//   -duchess -gui <config.properties>       Avalonia GUI for Guam machine
//   -draco <config.properties>              headless 6085/Daybreak harness
//   -draco -gui <config.properties>         Avalonia GUI for Draco
//   -gui                                    standalone Avalonia prototype window
//                                           (no engine; test pattern only)

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

// At least one mode must be selected; -gui can stand alone (prototype
// window without engine) or combine with -duchess (full Avalonia Duchess)
// or -draco (full Avalonia Draco). -duchess and -draco are mutually exclusive.
if (!isDuchess && !isDraco && !isGui) { return Usage(); }
if (isDuchess && isDraco) { return Usage(); }

if (isDuchess && isGui)
{
    // Duchess in Avalonia: hand engine setup to Duchess.RunGui, which
    // calls back to launch the Avalonia application after the engine
    // thread is running.
    return Dwarf.Duchess.Duchess.RunGui(newArgs.ToArray(), () =>
        Dwarf.UI.Avalonia.App.BuildAvaloniaApp()
            .UsePlatformDetect()
            .LogToTrace()
            .StartWithClassicDesktopLifetime(newArgs.ToArray()));
}

if (isDraco && isGui)
{
    return Dwarf.Draco.DracoHost.RunGui(newArgs.ToArray(), () =>
        Dwarf.UI.Avalonia.App.BuildAvaloniaApp()
            .UsePlatformDetect()
            .LogToTrace()
            .StartWithClassicDesktopLifetime(newArgs.ToArray()));
}

if (isGui)
{
    // Phase E prototype: bind the Avalonia window to actual engine display
    // memory but with no engine running. Init Mem with the default Guam
    // configuration; the diamond/X test pattern from
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

// isDraco (headless)
return Dwarf.Draco.DracoHost.Main(newArgs.ToArray());
