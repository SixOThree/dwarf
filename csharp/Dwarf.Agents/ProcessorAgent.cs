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
OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ANY THEORY OF LIABILITY,
WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR
OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF
ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
*/

using Dwarf.Engine;

namespace Dwarf.Agents;

// Agent providing access to machine characteristics and the clock of a
// Dwarf machine.
public class ProcessorAgent : Agent
{
    /*
     * ProcessorFCBType
     */
    private const int fcb_w_processorID0 = 0;
    private const int fcb_w_processorID1 = 1;
    private const int fcb_w_processorID2 = 2;
    private const int fcb_w_microsecondsPerHundredPulses = 3;
    private const int fcb_w_millisecondsPerTick = 4;
    private const int fcb_w_alignmentFiller = 5;
    private const int fcb_dbl_realMemoryPageCount = 6;
    private const int fcb_dbl_virtualMemoryPageCount = 8;
    private const int fcb_dbl_gmt = 10;
    private const int fcb_w_command = 12;
    private const int fcb_w_status = 13; // ProcessorStatus
    private const int FCB_SIZE = 14;

    // ProcessorCommand: TYPE = MACHINE DEPENDENT {noop(0), readGMT(1), writeGMT(2)};
    private const ushort Command_noop     = 0;
    private const ushort Command_readGMT  = 1;
    private const ushort Command_writeGMT = 2;

    // ProcessorStatus: TYPE = MACHINE DEPENDENT {
    //   inProgress(0), success(1), failure(2)};
    private const ushort Status_inProgress = 0;
    private const ushort Status_success    = 1;
    private const ushort Status_failure    = 2;

    // difference between (our) simulated GMT and (Pilot's) expected GMT
    private int gmtCorrection = 0;

    // Work-around for XDE HeraldWindow having a blinking warning instead of
    // the date if the "current" time is not in the expected time frame
    // (somewhere between the bootfile build date and some (4 or 5) years
    // later). The system date can be faked at a 2nd level to a given date
    // for the first n-thousand instructions, so the HeraldWindow sees a
    // specific date when it checks for a plausible boot time; the "correct"
    // date is returned after that number of instructions when "the rest of
    // XDE" asks for the time.
    private static long xdeNoBlinkInsnLimit = 0;
    private static long xdeNoBlinkBaseMSecs = 0;
    private static long xdeNoBlinkDateMSecs = 0;

    public static void installXdeNoBlinkWorkAround(DateOnly noBlinkTargetDate, long insnsLimit)
    {
        xdeNoBlinkInsnLimit = insnsLimit;
        long nowMSecs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        xdeNoBlinkBaseMSecs = (nowMSecs / 86_400_000L) * 86_400_000L; // midnight of today
        // date to be returned until insnsLimit instructions are reached
        xdeNoBlinkDateMSecs = noBlinkTargetDate.DayNumber * 86_400_000L
                            - new DateOnly(1970, 1, 1).DayNumber * 86_400_000L;
    }

    public ProcessorAgent(int fcbAddress)
        : base(AgentDevice.processorAgent, fcbAddress, FCB_SIZE)
    {
    }

    public override void shutdown(System.Text.StringBuilder errMsgTarget)
    {
        // nothing to shutdown for this agent
    }

    public override void refreshMesaMemory()
    {
        // nothing to transfer to mesa memory for this agent
    }

    public override void call()
    {
        ushort cmd = this.getFcbWord(fcb_w_command);
        switch (cmd)
        {
            case Command_noop:
                this.setFcbWord(fcb_w_status, Status_success);
                break;

            case Command_readGMT:
                if (Cpu.insns > xdeNoBlinkInsnLimit)
                {
                    this.setFcbDblWord(fcb_dbl_gmt, getRawPilotTime() + gmtCorrection);
                }
                else
                {
                    long nowMSecs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                    this.setFcbDblWord(fcb_dbl_gmt, getRawPilotTime(xdeNoBlinkDateMSecs + (nowMSecs - xdeNoBlinkBaseMSecs)));
                }
                this.setFcbWord(fcb_w_status, Status_success);
                break;

            case Command_writeGMT:
                gmtCorrection = this.getFcbDblWord(fcb_dbl_gmt) - getRawPilotTime();
                this.setFcbWord(fcb_w_status, Status_success);
                break;

            default:
                this.setFcbWord(fcb_w_status, Status_failure);
                break;
        }
    }

    protected override void initializeFcb()
    {
        this.setFcbWord(fcb_w_processorID0, Cpu.getPIDword(1));
        this.setFcbWord(fcb_w_processorID1, Cpu.getPIDword(2));
        this.setFcbWord(fcb_w_processorID2, Cpu.getPIDword(3));
        this.setFcbWord(fcb_w_microsecondsPerHundredPulses, Cpu.MicrosecondsPerPulse * 100);
        this.setFcbWord(fcb_w_millisecondsPerTick, (Cpu.MicrosecondsPerPulse * Cpu.TimeOutInterval) / 1000);
        this.setFcbWord(fcb_w_alignmentFiller, (ushort)0);
        this.setFcbDblWord(fcb_dbl_realMemoryPageCount, Mem.getRealPagesSize());
        this.setFcbDblWord(fcb_dbl_virtualMemoryPageCount, Mem.getVirtualPagesSize());
        this.setFcbDblWord(fcb_dbl_gmt, getRawPilotTime() + gmtCorrection);
        this.setFcbWord(fcb_w_command, Command_noop);
        this.setFcbWord(fcb_w_status, Status_success);
    }

    // Java Time base  ::  1970-01-01 00:00:00
    // Pilot Time base ::  1968-01-01 00:00:00
    // => difference is 1 year + 1 leap-year => 731 days.
    private const int UnixToPilotSecondsDiff = 731 * 86400; // seconds

    // this is some unexplainable Xerox constant, but we have to use it...
    private const int MesaGmtEpoch = 2114294400;

    // get seconds since 1968-01-01 00:00:00 for a given Unix milliseconds timestamp
    private static int getRawPilotTime(long msecs)
    {
        long currUnixTimeInSeconds = msecs / 1000;
        return (int)((currUnixTimeInSeconds + UnixToPilotSecondsDiff + MesaGmtEpoch) & 0x00000000FFFFFFFFL);
    }

    // get seconds since 1968-01-01 00:00:00 for "now"
    private static int getRawPilotTime()
    {
        return getRawPilotTime(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
    }

    // Get the corresponding .NET DateTimeOffset for a given mesa-time.
    public static DateTimeOffset getJavaTime(int mesaTime)
    {
        long unixMillis = (mesaTime - UnixToPilotSecondsDiff - MesaGmtEpoch) * 1000L;
        return DateTimeOffset.FromUnixTimeMilliseconds(unixMillis);
    }
}
