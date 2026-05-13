/*
Copyright (c) 2019, Dr. Hans-Walter Latz (original Java implementation)
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

using System.IO;
using System.Text;
using Dwarf.Engine;
using Dwarf.Engine.Opcodes;

namespace Dwarf.Iop6085;

// Emulation of the Daybreak/6085 Input/Output Processor (IOP) board, setting
// up the device handlers for the supported / required devices and dispatching
// requests from the mesa-machine implementation or through machine-specific
// instructions to the respective handler(s).
//
// **Phase F-2 status**: only `HBeep`, `HTTY`, `HProcessor` are wired through —
// the remaining handlers (`HKeyboardMouse`, `HDisplay`, `HDisk`, `HFloppy`,
// `HEthernet`) come online in Phase F-3 / F-4 / F-5. Their slots in `iorTable`
// stay un-set, and the orchestration helpers `getUiCallbacks`, `insertFloppy`,
// `ejectFloppy` plus `IOPStatisticsProvider` return safe defaults when the
// corresponding handler is still null.
public static class IOP
{
    // the table of device handlers used by the mesa engine, holding the
    // FCBs (Function Control Blocks) of the handlers; each handler (device)
    // has a predefined index position.
    private static readonly IOPTypes.IORTable iorTable = new();

    // all handlers for devices that this IOP supports, as generic list and
    // then as type-specific instances
    private static readonly List<DeviceHandler> devHandlers = new();

    private static HBeep? hBeep;
    private static HDisplay? hDisplay;
    private static HKeyboardMouse? hKeyMo;
    private static HDisk? hDisk;
    private static HFloppy? hFloppy;
    private static HEthernet? hEthernet;
    private static HTTY? hTty;
    private static HProcessor? hProcessor;

    // setup the IOP and create all necessary device handlers
    public static void initialize(HDisk.VerifyLabelOp labelOpOnRead, HDisk.VerifyLabelOp labelOpOnWrite, HDisk.VerifyLabelOp labelOpOnVerify, bool logLabelProblems)
    {
        // install IOP6085-specific instructions
        Opcodes.implantEscOverride(0x87, "zESC.BYTESWAP", escBYTESWAP);
        Opcodes.implantEscOverride(0x89, "zESC.NOTIFYIOP", escNOTIFYIOP);
        Opcodes.implantEscOverride(0x88, "zESC.LOCKMEM", escLOCKMEM);
        Opcodes.implantEscOverride(0x86, "zESC.LOCKQUEUE", escLOCKQUEUE);

        // register memory updater for transferring data between agent and mesa memory
        Processes.setMesaMemoryUpdater(processPendingMesaMemoryUpdates);

        // register statistics provider
        Processes.setStatisticsProvider(new IOPStatisticsProvider());

        // and now the handlers...
        ushort fcbSegment;

        // device handler for beep
        hBeep = new HBeep();
        fcbSegment = hBeep.getFcbSegment();
        iorTable.segments[IOPTypes.HandlerID_beep].ioRegionSegment.set(fcbSegment);
        devHandlers.Add(hBeep);

        // device handler for keyboard & mouse
        hKeyMo = new HKeyboardMouse();
        fcbSegment = hKeyMo.getFcbSegment();
        iorTable.segments[IOPTypes.HandlerID_keyboardAndMouse].ioRegionSegment.set(fcbSegment);
        devHandlers.Add(hKeyMo);

        // device handler for display (takes HKeyboardMouse as ctor arg)
        hDisplay = new HDisplay(hKeyMo);
        fcbSegment = hDisplay.getFcbSegment();
        iorTable.segments[IOPTypes.HandlerID_display].ioRegionSegment.set(fcbSegment);
        devHandlers.Add(hDisplay);

        // device handler for processor
        hProcessor = new HProcessor();
        fcbSegment = hProcessor.getFcbSegment();
        iorTable.segments[IOPTypes.HandlerID_processor].ioRegionSegment.set(fcbSegment);
        devHandlers.Add(hProcessor);

        // device handler for hard disk(s)
        hDisk = new HDisk(labelOpOnRead, labelOpOnWrite, labelOpOnVerify, logLabelProblems);
        fcbSegment = hDisk.getFcbSegment();
        iorTable.segments[IOPTypes.HandlerID_disk].ioRegionSegment.set(fcbSegment);
        devHandlers.Add(hDisk);

        // device handler for floppy disk(s)
        hFloppy = new HFloppy();
        fcbSegment = hFloppy.getFcbSegment();
        iorTable.segments[IOPTypes.HandlerID_floppy].ioRegionSegment.set(fcbSegment);
        devHandlers.Add(hFloppy);

        // device handler for network
        hEthernet = new HEthernet();
        fcbSegment = hEthernet.getFcbSegment();
        iorTable.segments[IOPTypes.HandlerID_ethernet].ioRegionSegment.set(fcbSegment);
        devHandlers.Add(hEthernet);

        // device handler for tty
        hTty = new HTTY();
        fcbSegment = hTty.getFcbSegment();
        iorTable.segments[IOPTypes.HandlerID_tty].ioRegionSegment.set(fcbSegment);
        devHandlers.Add(hTty);
    }

    // Request a write back of all buffered data on all device handlers and
    // finalize the handlers.
    public static void shutdown(StringBuilder errMsgTarget)
    {
        foreach (DeviceHandler handler in devHandlers)
        {
            handler.shutdown(errMsgTarget);
        }
    }

    // Transfer all cached data changes by devices into mesa memory space.
    private static void processPendingMesaMemoryUpdates()
    {
        foreach (DeviceHandler handler in devHandlers)
        {
            handler.refreshMesaMemory();
        }
    }

    /*
     * implementation for special 6085 i/o related instructions
     */

    // BYTESWAP - byte swap in a word
    private static readonly OpImpl escBYTESWAP = () =>
    {
        int val = Cpu.pop() & 0xFFFF;
        int res = (val << 8) | (val >>> 8);
        Cpu.push((ushort)res);
    };

    // NOTIFYIOP - invocation of a specific device, identified by the mask in the FCB
    private static readonly OpImpl escNOTIFYIOP = () =>
    {
        ushort notifyMask = Cpu.pop();
        foreach (DeviceHandler handler in devHandlers)
        {
            if (handler.processNotify(notifyMask))
            {
                return;
            }
        }
    };

    // LOCKMEM - synchronized/interlocked access to a memory location in the IO-region
    private static readonly OpImpl escLOCKMEM = () =>
    {
        int mask = Cpu.pop() & 0xFFFF;
        int value = Cpu.pop() & 0xFFFF;
        int ioRegionOffset = Cpu.pop() & 0xFFFF;
        int operation = Cpu.pop() & 0xFFFF;

        int realLP = IORegion.IOR_BASE + ioRegionOffset;
        int oldValue = Mem.mem[realLP] & 0xFFFF;
        int newValue;
        DeviceHandler.MemOperation memOp;
        if (operation == (int)DeviceHandler.MemOperation.add)
        {
            newValue = oldValue + value;
            memOp = DeviceHandler.MemOperation.add;
        }
        else if (operation == (int)DeviceHandler.MemOperation.and)
        {
            newValue = oldValue & value;
            memOp = DeviceHandler.MemOperation.and;
        }
        else if (operation == (int)DeviceHandler.MemOperation.or)
        {
            newValue = oldValue | value;
            memOp = DeviceHandler.MemOperation.or;
        }
        else if (operation == (int)DeviceHandler.MemOperation.overwriteIfNil)
        {
            newValue = (oldValue != 0) ? oldValue : value;
            memOp = DeviceHandler.MemOperation.overwriteIfNil;
        }
        else if (operation == (int)DeviceHandler.MemOperation.xchg)
        {
            newValue = value;
            memOp = DeviceHandler.MemOperation.xchg;
        }
        else
        {
            throw new ArgumentException("operation is not a valid MemOperation");
        }

        foreach (DeviceHandler handler in devHandlers)
        {
            handler.handleLockmem((ushort)mask, realLP, memOp, (ushort)oldValue, (ushort)newValue);
        }

        Mem.mem[realLP] = (ushort)(newValue & 0xFFFF);

        foreach (DeviceHandler handler in devHandlers)
        {
            handler.cleanupAfterLockmem((ushort)mask, realLP);
        }

        Cpu.push((ushort)oldValue); // the instruction appears to return the oldValue
    };

    // LOCKQUEUE - lock a queue (until the queue's device is notified?)
    private static readonly OpImpl escLOCKQUEUE = () =>
    {
        int lpQueueVAddr = Cpu.popLong();
        foreach (DeviceHandler handler in devHandlers)
        {
            handler.handleLockqueue(lpQueueVAddr, Mem.getRealAddress(lpQueueVAddr, true));
        }
    };

    /*
     * interface between the UI implementation and the UI related devices
     */

    private sealed class UiCallbacks : iUiDataConsumer
    {
        public void acceptKeyboardKey(eLevelVKey key, bool isPressed)
        {
            if (hKeyMo == null) { return; }
            hKeyMo.handleKeyUsage(key, isPressed);
        }

        public void resetKeys()
        {
            if (hKeyMo == null) { return; }
            hKeyMo.resetKeys();
        }

        public void acceptMouseKey(int key, bool isPressed)
        {
            if (key == 1)
            {
                this.acceptKeyboardKey(eLevelVKey.Point, isPressed);
            }
            else if (key == 2)
            {
                this.acceptKeyboardKey(eLevelVKey.Menu, isPressed);
            }
            else if (key == 3)
            {
                this.acceptKeyboardKey(eLevelVKey.Adjust, isPressed);
            }
        }

        public void acceptMousePosition(int x, int y)
        {
            if (hDisplay == null) { return; }
            hDisplay.recordMouseMoved(x, y);
        }

        public void registerPointerBitmapAcceptor(iUiDataConsumer.PointerBitmapAcceptor acpt)
        {
            if (hDisplay == null) { return; }
            hDisplay.setPointerBitmapAcceptor(acpt);
        }

        public Func<int[]> registerUiDataRefresher(iMesaMachineDataAccessor refresher)
        {
            Processes.registerUiRefreshCallback(refresher);
            return null!;
        }
    }

    private static UiCallbacks? uiCallbacks = null;

    public static iUiDataConsumer getUiCallbacks()
    {
        uiCallbacks ??= new UiCallbacks();
        return uiCallbacks;
    }

    /*
     * access to statistical data for the I/O devices
     */

    private sealed class IOPStatisticsProvider : Processes.StatisticsProvider
    {
        public int getDiskReads() => hDisk?.getReads() ?? 0;
        public int getDiskWrites() => hDisk?.getWrites() ?? 0;
        public int getFloppyReads() => hFloppy?.getReads() ?? 0;
        public int getFloppyWrites() => hFloppy?.getWrites() ?? 0;
        public int getNetworkpacketsSent() => hEthernet?.getPacketsSentCount() ?? 0;
        public int getNetworkpacketsReceived() => hEthernet?.getPacketsReceivedCount() ?? 0;
    }

    /*
     * floppy handling operations — TODO Phase F-4 once HFloppy is ported
     */

    // Phase F-4b: HFloppy is wired, but its `insertFloppy` currently throws
    // `NotSupportedException` because no floppy format reader is ported (per
    // RISKS R7). This entry point exists so DracoHost / a future UI button can
    // call it; the exception propagates with a descriptive message.
    public static bool insertFloppy(FileInfo f, bool readonly_) =>
        hFloppy?.insertFloppy(f.FullName, readonly_)
        ?? throw new InvalidOperationException("IOP not initialized: insertFloppy called before initialize()");

    public static void ejectFloppy() => hFloppy?.ejectFloppy();
}
