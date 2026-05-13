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

// IOP device handler for the processor of a Daybreak/6085 machine for
// accessing (mostly reading) characteristics of the machine.
public class HProcessor : DeviceHandler
{
    private static long timeShiftMilliSeconds = 0;

    public static void setTimeShiftSeconds(long seconds)
    {
        timeShiftMilliSeconds = seconds * 1000;
    }

    private const string ProcessorFCB = "ProcessorFCB";

    private enum Command
    {
        noCommand = 0,
        readGMT = 1,
        writeGMT = 2,
        readHostID = 3,
        readVMMapDesc = 4,
        readRealMemDesc = 5,
        readDisplayDesc = 6,
        readKeyboardType = 7,
        readPCType = 8,
        bootButton = 9,
        readNumbCSBanks = 10,
        readMachineType = 11,

        invalid = 0xFFFF,
    }

    private static Command mapCommand(int code)
    {
        if (code < (int)Command.noCommand || code > (int)Command.readMachineType)
        {
            return Command.invalid;
        }
        // enum values 0..11 match command codes 1:1
        return (Command)code;
    }

    private sealed class FCB : IORAddress
    {
        private readonly int startAddress;

        public readonly IOPTypes.NotifyMask notifiersLockMask;
        public readonly Word upNotifyBits;
        public readonly DblWord downNotifyBits;
        public readonly IOPTypes.IOPCondition mesaClientCondition;
        public readonly Word mesaClientMask;
        public readonly Word timeOfDayIsValidAndCommand;
        public readonly Field timeOfDayIsValid;
        public readonly Field command;
        public readonly Word data0;
        public readonly Word data1;
        public readonly Word data2;
        public readonly IOPTypes.TaskContextBlock processorTCB;
        public readonly IOPTypes.TaskContextBlock clientTCB;

        public readonly DblWord byteSwappedGMT;

        public FCB()
        {
            this.startAddress = syncToSegment() + IOR_BASE;

            this.notifiersLockMask = new IOPTypes.NotifyMask(ProcessorFCB, "notifiersLockMask");
            this.upNotifyBits = mkWord(ProcessorFCB, "upNotifyBits");
            this.downNotifyBits = mkDblWord(ProcessorFCB, "downNotifyBits");
            this.mesaClientCondition = new IOPTypes.IOPCondition(ProcessorFCB, "mesaClientCondition");
            this.mesaClientMask = mkWord(ProcessorFCB, "mesaClientMask");
            this.timeOfDayIsValidAndCommand = mkWord(ProcessorFCB, "timeOfDayIsValidAndCommand");
            this.timeOfDayIsValid = mkField("timeOfDayIsValid", this.timeOfDayIsValidAndCommand, 0xFF00);
            this.command = mkField("command", this.timeOfDayIsValidAndCommand, 0x00FF);
            this.data0 = mkByteSwappedWord(ProcessorFCB, "data[0]");
            this.data1 = mkByteSwappedWord(ProcessorFCB, "data[1]");
            this.data2 = mkByteSwappedWord(ProcessorFCB, "data[2]");
            this.processorTCB = new IOPTypes.TaskContextBlock(ProcessorFCB, "processorTCB");
            this.clientTCB = new IOPTypes.TaskContextBlock(ProcessorFCB, "clientTCB");

            this.byteSwappedGMT = mkCompoundDblWord(this.data0, this.data1);

            this.mesaClientMask.set(mkMask());
        }

        public string getName() => ProcessorFCB;

        public int getRealAddress() => this.startAddress;
    }

    /*
     * implementation of the iop6085 interface for supporting the mesa-processor
     */

    private readonly FCB fcb;

    // machine ID fetched from configuration via class Cpu
    private readonly ushort cpuId0;
    private readonly ushort cpuId1;
    private readonly ushort cpuId2;

    // difference between (our) simulated GMT and (Pilot's) expected GMT
    // originally 0 i.e. local time, set to time difference to local time by command writeGMT
    private int gmtCorrection = 0;

    // work-around for XDE HeraldWindow having a blinking warning instead of the date
    // if the "current" time is not in the expected time frame (somewhere between the bootfile
    // build date and some (4 or 5) years later)
    // so the system date can be faked (at a 2nd level :-)) to a given date for the first n-thousand
    // instructions, so the HeraldWindow sees a specific date when it checks for a plausible boot time
    // and the "correct" date is returned after that number of instructions when "the rest of XDE" asks
    // for the time
    private static long xdeNoBlinkInsnLimit = 0;
    private static long xdeNoBlinkBaseMSecs = 0;
    private static long xdeNoBlinkDateMSecs = 0;

    public static void installXdeNoBlinkWorkAround(DateOnly noBlinkTargetDate, long insnsLimit)
    {
        xdeNoBlinkInsnLimit = insnsLimit;
        long nowMSecs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        xdeNoBlinkBaseMSecs = (nowMSecs / 86_400_000L) * 86_400_000L; // midnight of today
        xdeNoBlinkDateMSecs = toEpochDay(noBlinkTargetDate) * 86_400_000L; // date to be returned until insnsLimit instructions are reached
    }

    public HProcessor() : base("Processor", Config.IO_LOG_PROCESSOR)
    {
        this.fcb = new FCB();

        this.fcb.timeOfDayIsValid.set(1); // true; meaning: we can always deliver a valid time

        this.cpuId0 = (ushort)Cpu.getPIDword(1);
        this.cpuId1 = (ushort)Cpu.getPIDword(2);
        this.cpuId2 = (ushort)Cpu.getPIDword(3);
    }

    public override int getFcbRealAddress() => this.fcb.getRealAddress();

    public override ushort getFcbSegment() => ((IORAddress)this.fcb).getIOPSegment();

    public override bool processNotify(ushort notifyMask)
    {
        // check if it's for us
        if (notifyMask != 0 && notifyMask != this.fcb.mesaClientMask.get())
        {
            return false;
        }

        // interpret and handle the requested operation
        int cmdCode = this.fcb.command.get();
        Command cmd = mapCommand(cmdCode);
        switch (cmd)
        {
            case Command.noCommand:
                this.logf("no command given, why was executeFcbCommand() called ??\n");
                break;

            case Command.readGMT:
                this.logf("readGMT\n");
                if (Cpu.insns > xdeNoBlinkInsnLimit)
                {
                    this.fcb.byteSwappedGMT.set(getRawPilotTime() + this.gmtCorrection);
                }
                else
                {
                    long nowMSecs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                    this.fcb.byteSwappedGMT.set(getRawPilotTime(xdeNoBlinkDateMSecs + (nowMSecs - xdeNoBlinkBaseMSecs)));
                }
                break;

            case Command.writeGMT:
                this.gmtCorrection = this.fcb.byteSwappedGMT.get() - getRawPilotTime();
                this.logf("writeGMT -> new gmtCorrection: {0}\n", this.gmtCorrection);
                break;

            case Command.readHostID:
                this.logf("readHostID\n");
                // the host-id is expected unswapped, but .data0..2 automatically swap bytes, so swap the words once more time
                this.fcb.data0.set((ushort)byteSwap(this.cpuId0));
                this.fcb.data1.set((ushort)byteSwap(this.cpuId1));
                this.fcb.data2.set((ushort)byteSwap(this.cpuId2));
                break;

            case Command.readVMMapDesc:
                this.logf("readVMMapDesc\n");
                this.fcb.data0.set((ushort)Mem.dBreak_Real_firstMapPage);
                this.fcb.data1.set((ushort)Mem.dBreak_Real_countMapPages);
                break;

            case Command.readRealMemDesc:
                this.logf("readRealMemDesc\n");
                this.fcb.data0.set((ushort)Mem.dBreak_firstRealPageInVMM);
                this.fcb.data1.set((ushort)Mem.dBreak_lastRealPageInVMM);
                this.fcb.data2.set((ushort)Mem.dBreak_countRealPagesInVMM);
                break;

            case Command.readDisplayDesc:
                this.logf("readDisplayDesc\n");
                this.fcb.data0.set((ushort)Mem.dBreak_displayType);
                this.fcb.data1.set((ushort)Mem.dBreak_Real_firstDisplayBankPage);
                this.fcb.data2.set((ushort)Mem.dBreak_Real_countDisplayBankPages);
                break;

            case Command.readKeyboardType:
                this.logf("readKeyboardType\n");
                this.fcb.data0.set((ushort)2); // pretend "English Level V" keyboard
                break;

            case Command.readPCType:
                this.logf("readPCType\n");
                this.fcb.data0.set((ushort)0); // == false, no pc extension board present
                break;

            case Command.bootButton:
                this.logf("bootButton\n");
                throw new Cpu.MesaStopped("IOP6085::Processor ... bootButton");

            case Command.readNumbCSBanks:
                this.logf("readNumbCSBanks\n");
                this.fcb.data0.set((ushort)1); // one control-store (microcode) bank (4Kwords), whatever this may be used for
                break;

            case Command.readMachineType:
                this.logf("readMachineType\n");
                this.fcb.data0.set((ushort)3); // pretend we're a daybreak ...
                break;

            default:
                this.logf("invalid command-code: {0}\n", cmdCode);
                break;
        }

        this.fcb.command.set((int)Command.noCommand); // signal we're done with executing the fcb command
        return true;
    }

    public override void handleLockmem(ushort lockMask, int realAddress, MemOperation memOp, ushort oldValue, ushort newValue)
    {
        // nothing to do
    }

    public override void handleLockqueue(int vAddr, int rAddr)
    {
        // not relevant for processor handler
    }

    public override void refreshMesaMemory()
    {
        // nothing to do (Java upstream marked `synchronized`, but the body is a no-op)
    }

    public override void shutdown(System.Text.StringBuilder errMsgTarget)
    {
        // nothing to save or shutdown
    }

    /*
     * Unix <-> Mesa time mapping
     */

    // Java Time base  ::  1970-01-01 00:00:00
    // Pilot Time base ::  1968-01-01 00:00:00
    // => difference is 1 year + 1 leap-year => 731 days.
    private const int UnixToPilotSecondsDiff = 731 * 86400; // seconds

    // this is some unexplainable Xerox constant whatever for, but we have to use it...
    private const int MesaGmtEpoch = unchecked((int)2114294400);

    // get seconds since 1968-01-01 00:00:00 for a given Java milliseconds timestamp
    private static int getRawPilotTime(long msecs)
    {
        long currJavaTimeInSeconds = (msecs + timeShiftMilliSeconds) / 1000;
        return (int)((currJavaTimeInSeconds + UnixToPilotSecondsDiff + MesaGmtEpoch) & 0x00000000FFFFFFFFL);
    }

    // get seconds since 1968-01-01 00:00:00 for "now"
    private static int getRawPilotTime() =>
        getRawPilotTime(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

    // Get the corresponding Java-Date (here: `DateTimeOffset`) for a given mesa-time.
    public static DateTimeOffset getJavaTime(int mesaTime)
    {
        long javaMillis = (mesaTime - UnixToPilotSecondsDiff - MesaGmtEpoch) * 1000L;
        return DateTimeOffset.FromUnixTimeMilliseconds(javaMillis);
    }

    // Java's `LocalDate.toEpochDay()` — days since 1970-01-01.
    private static long toEpochDay(DateOnly d) =>
        d.DayNumber - new DateOnly(1970, 1, 1).DayNumber;
}
