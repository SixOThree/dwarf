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

// IOP device handler for the unsupported TTY device of a Daybreak/6085 machine.
public class HTTY : DeviceHandler
{
    /*
     * Function Control Block
     */

    private const string TTYFCB = "TTYFCB";

    // Unused in Java upstream too — declared for future expansion. Kept verbatim.
    private sealed class WorkListType
    {
#pragma warning disable CS0414 // The field is assigned but its value is never used
        private readonly IOPBoolean writeBaudRate;
#pragma warning restore CS0414

        public WorkListType(string name)
        {
            this.writeBaudRate = mkIOPBoolean(name, "writeBaudRate");
        }
    }

    private sealed class FCB : IORAddress
    {
        private readonly int startAddress;

        public readonly IOPTypes.TaskContextBlock txTcb;
        public readonly IOPTypes.TaskContextBlock specRxTcb;
        public readonly IOPTypes.TaskContextBlock rxTaskChBTcb;

        public readonly Word ttyLockMask;
        public readonly IOPTypes.NotifyMask ttyWorkMask;
        public readonly IOPTypes.ClientCondition ttyClientCondition;
        public readonly IOPTypes.IOPCondition ttyWorkCondition;

        public readonly Word txBuffer; // CHARACTER
        public readonly Word rxBuffer; // CHARACTER

        public readonly Word ttyWorkList; // MACHINE DEPENDENT with 16 BOOLEANs

        public readonly Word ttyBaudRate;
        public readonly Word wr1_wr3;
        public readonly Word wr4_wr5;

        public readonly Word iopSystemInputPort_rr0;
        public readonly Word rr1_rr2;

        public readonly Word ttyStatusWord;

        public readonly Word eepromImage_type; // MACHINE DEPENDENT {none(0), DCE(2), (LAST [CARDINAL])}
        public readonly Word eepromImage_attributes1;
        public readonly Word eepromImage_attributes2;
        public readonly Word eepromImage_attributes3;
        public readonly Word eepromImage_attributes4;

        public FCB()
        {
            this.startAddress = syncToSegment() + IOR_BASE;

            this.txTcb = new IOPTypes.TaskContextBlock(TTYFCB, "txTcb");
            this.specRxTcb = new IOPTypes.TaskContextBlock(TTYFCB, "specRxTcb");
            this.rxTaskChBTcb = new IOPTypes.TaskContextBlock(TTYFCB, "rxTaskChBTcb");

            this.ttyLockMask = mkWord(TTYFCB, "ttyLockMask");
            this.ttyWorkMask = new IOPTypes.NotifyMask(TTYFCB, "ttyWorkMask");
            this.ttyClientCondition = new IOPTypes.ClientCondition(TTYFCB, "ttyClientCondition");
            this.ttyWorkCondition = new IOPTypes.IOPCondition(TTYFCB, "ttyWorkCondition");

            this.txBuffer = mkWord(TTYFCB, "txBuffer");
            this.rxBuffer = mkWord(TTYFCB, "rxBuffer");

            this.ttyWorkList = mkWord(TTYFCB, "ttyWorkList");

            this.ttyBaudRate = mkByteSwappedWord(TTYFCB, "ttyBaudRate");
            this.wr1_wr3 = mkWord(TTYFCB, "wr1+wr3");
            this.wr4_wr5 = mkWord(TTYFCB, "wr4+wr5");

            this.iopSystemInputPort_rr0 = mkWord(TTYFCB, "iopSystemInputPort+rr0");
            this.rr1_rr2 = mkWord(TTYFCB, "rr1+rr2");

            this.ttyStatusWord = mkWord(TTYFCB, "ttyStatusWord");

            this.eepromImage_type = mkWord(TTYFCB, "eepromImage.type");
            this.eepromImage_attributes1 = mkWord(TTYFCB, "eepromImage_attributes[1]");
            this.eepromImage_attributes2 = mkWord(TTYFCB, "eepromImage_attributes[2]");
            this.eepromImage_attributes3 = mkWord(TTYFCB, "eepromImage_attributes[3]");
            this.eepromImage_attributes4 = mkWord(TTYFCB, "eepromImage_attributes[4]");

            // initialize notification mask
            this.ttyWorkMask.byteMaskAndOffset.set(mkMask());
        }

        public string getName() => TTYFCB;

        public int getRealAddress() => this.startAddress;
    }

    /*
     * implementation of the iop6085 tty interface
     */

    private readonly FCB fcb;

    public HTTY() : base(TTYFCB, Config.IO_LOG_TTY)
    {
        this.fcb = new FCB();
    }

    public override int getFcbRealAddress() => this.fcb.getRealAddress();

    public override ushort getFcbSegment() => ((IORAddress)this.fcb).getIOPSegment();

    public override bool processNotify(ushort notifyMask)
    {
        // check if it's for us
        if (notifyMask != this.fcb.ttyWorkMask.byteMaskAndOffset.get())
        {
            return false;
        }

        this.logf("IOP::HTTY.processNotify() - unimplemented yet ...\n");

        return true;
    }

    public override void handleLockmem(ushort lockMask, int realAddress, MemOperation memOp, ushort oldValue, ushort newValue)
    {
        // TTY device currently unsupported/unused
    }

    public override void handleLockqueue(int vAddr, int rAddr)
    {
        // TTY device currently unsupported/unused
    }

    public override void refreshMesaMemory()
    {
        // TTY device currently unsupported/unused
    }

    public override void shutdown(System.Text.StringBuilder errMsgTarget)
    {
        // TTY device currently unsupported/unused
    }
}
