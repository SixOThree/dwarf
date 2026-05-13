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

// Main program for the Dwarf Mesa emulator family. Dispatches to either
// the Guam machine emulation (Duchess) or the 6085/Daybreak emulation
// (Draco — Phase F, not yet ported), based on the first `-duchess` or
// `-draco` flag. Remaining args pass through to the dispatched program.
//
// Usage:
//   Dwarf -duchess <config.properties> [-v] [-merge] [-frames-out <path>] [-frames-interval-ms <ms>]
//   Dwarf -draco <config.properties>     (Phase F — not yet ported)

static int Usage()
{
    Console.WriteLine("Usage: Dwarf -duchess|-draco <machine-specific-args...>");
    return 1;
}

if (args.Length < 1)
{
    return Usage();
}

bool isDuchess = false;
bool isDraco = false;
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
    else
    {
        newArgs.Add(arg);
    }
}

if (isDuchess == isDraco)
{
    return Usage();
}

if (isDuchess)
{
    return Dwarf.Duchess.Duchess.Main(newArgs.ToArray());
}
else
{
    Console.Error.WriteLine("Dwarf -draco is not yet implemented in the C# port (planned for Phase F).");
    Console.Error.WriteLine("Use the Java jar for the 6085/Daybreak emulator until then.");
    return 1;
}
