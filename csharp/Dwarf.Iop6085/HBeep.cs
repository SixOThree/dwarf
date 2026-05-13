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

using Dwarf.Engine;
using static Dwarf.Iop6085.IORegion;

namespace Dwarf.Iop6085;

// IOP device handler for the beeper (dummy, no sounds provided for now).
public class HBeep : DeviceHandler
{
    private const string BeepFCB = "BeepFCB";

    private sealed class FCB : IORAddress
    {
        private readonly int startAddress;

        public readonly IOPTypes.TaskContextBlock beepTask;
        public readonly IOPTypes.IOPCondition beepCndt;
        public readonly IOPTypes.NotifyMask beepMask;
        public readonly Word frequency;

        public FCB()
        {
            this.startAddress = syncToSegment() + IOR_BASE;

            this.beepTask = new IOPTypes.TaskContextBlock(BeepFCB, "beepTask");
            this.beepCndt = new IOPTypes.IOPCondition(BeepFCB, "beepCndt");
            this.beepMask = new IOPTypes.NotifyMask(BeepFCB, "beepMask");
            this.frequency = mkByteSwappedWord(BeepFCB, "frequency");

            this.beepMask.byteMaskAndOffset.set(mkMask());
        }

        public string getName() => BeepFCB;

        public int getRealAddress() => this.startAddress;
    }

    /*
     * implementation of the iop6085 beep device handler
     */

    private readonly FCB fcb;

    public HBeep() : base(BeepFCB, Config.IO_LOG_DISPLAY)
    {
        this.fcb = new FCB();
    }

    public override int getFcbRealAddress() => this.fcb.getRealAddress();

    public override ushort getFcbSegment() => ((IORAddress)this.fcb).getIOPSegment();

    public override bool processNotify(ushort notifyMask)
    {
        // check if it's for us
        if (notifyMask != this.fcb.beepMask.byteMaskAndOffset.get())
        {
            return false;
        }

        // simulate beeping on / off
        this.logf("processNotify -> frequency = {0} (units??)\n", this.fcb.frequency.get() & 0xFFFF);
        // megaHertz: LONG CARDINAL <- 2764800;
        // fcb.frequency <- ByteSwap[Inline.LowHalf[Inline.LongDiv[megaHertz, MAX[frequency, 43]]]]

        // done
        return true;
    }

    public override void handleLockmem(ushort lockMask, int realAddress, MemOperation memOp, ushort oldValue, ushort newValue)
    {
        // not relevant
    }

    public override void handleLockqueue(int vAddr, int rAddr)
    {
        // not relevant for beep handler
    }

    public override void refreshMesaMemory()
    {
        // not relevant (Java upstream marked `synchronized`, but the body is a no-op)
    }

    public override void shutdown(System.Text.StringBuilder errMsgTarget)
    {
        // nothing to save or shutdown
    }
}
