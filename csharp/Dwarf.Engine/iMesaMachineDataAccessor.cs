/*
Copyright (c) 2017, Dr. Hans-Walter Latz (original Java implementation)
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
OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY,
WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR
OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF
ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
*/

namespace Dwarf.Engine;

// UI callbacks that can be registered with the mesa engine, allowing the
// engine to provide data at a suitable moment in the instruction processing
// (when checking for interrupts or timeouts), so no concurrent changes occur
// while the callback is active.
//
// The lowercase 'i' prefix mirrors the Java name for traceability.
public interface iMesaMachineDataAccessor
{
    // Callback allowing the UI to transfer the display bits from the display
    // memory in the real memory space of the mesa engine for updating the
    // UI's pixelmap.
    //
    // Using `pageFlags` and `firstPage`, transferring the display memory can
    // be restricted to changed pages, as the engine resets the dirty flag on
    // the virtual display memory pages after the UI has accessed it.
    //
    // realMemory : the real memory used by the mesa engine
    // memOffset  : real address of the display memory in `realMemory`,
    //              starting at a memory page
    // memWords   : length in addressable words of the display memory
    // pageFlags  : the virtual page map used by the mesa engine
    // firstPage  : index of the virtual page for `memOffset` in `pageFlags`
    void accessRealMemory(
        ushort[] realMemory, int memOffset, int memWords,
        ushort[] pageFlags, int firstPage);

    // Callback informing the UI of a value change on the Maintenance Panel.
    void acceptMP(int mp);

    // Callback informing about statistical values from the mesa engine
    // accumulated since starting the mesa engine.
    void acceptStatistics(
        long counterInstructions,
        int counterDiskReads,
        int counterDiskWrites,
        int counterFloppyReads,
        int counterFloppyWrites,
        int counterNetworkPacketsReceived,
        int counterNetworkPacketsSent);
}
