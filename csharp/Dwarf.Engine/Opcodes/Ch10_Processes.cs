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

namespace Dwarf.Engine.Opcodes;

// Implementation of PrincOps 4.0 chapter 10: Processes (scheduling).
//
// Phase B note: no unit tests for this chapter. Implementations compile via
// `Processes.*` stubs that throw `NotImplementedException("Phase C")`.
// Phase C wires up the real PSB/queue machinery.
public static class Ch10_Processes
{
    // ==================================================================
    // 10.2.1 Monitor Entry
    // ==================================================================

    public static readonly OpImpl OPC_xF1_ME = () =>
    {
        int m = Cpu.popLong();
        Cpu.checkEmptyStack();

        ushort mon = Mem.readWord(m);
        if (!Processes.isMonitorLocked(mon))
        {
            mon = Processes.setMonitorLocked(mon);
            Mem.writeWord(m, mon);
            Cpu.push(PrincOpsDefs.TRUE);
        }
        else
        {
            Processes.enterFailed(m);
        }
    };

    // ==================================================================
    // 10.2.2 Monitor Exit
    // ==================================================================

    public static readonly OpImpl OPC_xF2_MX = () =>
    {
        int m = Cpu.popLong();
        Cpu.checkEmptyStack();

        if (Processes.exit(m))
        {
            Processes.reschedule(false);
        }
    };

    // ==================================================================
    // 10.2.3 Monitor Wait
    // ==================================================================

    public static readonly OpImpl ESC_x02_MW = () =>
    {
        int t = Cpu.pop();
        int c = Cpu.popLong();
        int m = Cpu.popLong();
        Cpu.checkEmptyStack();

        Processes.cleanupCondition(c);

        bool requeue = Processes.exit(m);
        ushort flags = Processes.fetchPSB_flags(Cpu.PSB);
        ushort cond = Mem.readWord(c);

        if (!Processes.isPsbFlagsAbort(flags) || !Processes.isConditionAbortable(cond))
        {
            if (Processes.isConditionWakeup(cond))
            {
                cond = Processes.unsetConditionWakeup(cond);
                Mem.writeWord(c, cond);
            }
            else
            {
                Processes.storePSB_timeout(Cpu.PSB, (ushort)((t == 0) ? 0 : Math.Max(1, (Cpu.PTC + t) & 0xFFFF)));
                flags = Processes.setPsbFlagsWaiting(flags);
                Processes.storePSB_flags(Cpu.PSB, flags);
                Processes.requeue(Processes.PDA_LP_header_ready, c, Cpu.PSB);
                requeue = true;
            }
        }
        if (requeue)
        {
            Processes.reschedule(false);
        }
    };

    // ==================================================================
    // 10.2.4 Monitor Reentry
    // ==================================================================

    public static readonly OpImpl ESC_x03_MR = () =>
    {
        int c = Cpu.popLong();
        int m = Cpu.popLong();
        Cpu.checkEmptyStack();

        ushort mon = Mem.readWord(m);
        if (!Processes.isMonitorLocked(mon))
        {
            Processes.cleanupCondition(c);
            ushort flags = Processes.fetchPSB_flags(Cpu.PSB);
            flags = Processes.setPsbFlags_cleanup(flags, Processes.PsbNull);
            Processes.storePSB_flags(Cpu.PSB, flags);
            if (Processes.isPsbFlagsAbort(flags))
            {
                ushort cond = Mem.readWord(c);
                if (Processes.isConditionAbortable(cond)) { Cpu.processTrap(); }
            }
            mon = Processes.setMonitorLocked(mon);
            Mem.writeWord(m, mon);
            Cpu.push(PrincOpsDefs.TRUE);
        }
        else
        {
            Processes.enterFailed(m);
        }
    };

    // ==================================================================
    // 10.2.5 Notify and Broadcast
    // ==================================================================

    public static readonly OpImpl ESC_x04_NC = () =>
    {
        int c = Cpu.popLong();
        Cpu.checkEmptyStack();

        Processes.cleanupCondition(c);
        ushort cond = Mem.readWord(c);
        if (Processes.getCondition_tail(cond) != Processes.PsbNull)
        {
            Processes.wakeHead(c);
            Processes.reschedule(false);
        }
    };

    public static readonly OpImpl ESC_x05_BC = () =>
    {
        bool requeue = false;
        int c = Cpu.popLong();
        Cpu.checkEmptyStack();

        Processes.cleanupCondition(c);
        ushort cond = Mem.readWord(c);
        while (Processes.getCondition_tail(cond) != Processes.PsbNull)
        {
            Processes.wakeHead(c);
            requeue = true;
            cond = Mem.readWord(c);
        }
        if (requeue)
        {
            Processes.reschedule(false);
        }
    };

    // ==================================================================
    // 10.2.6 Requeue
    // ==================================================================

    public static readonly OpImpl ESC_x06_REQ = () =>
    {
        int psbHandle = Cpu.pop();
        int dstQue = Cpu.popLong();
        int srcQue = Cpu.popLong();
        Cpu.checkEmptyStack();
        Processes.requeue(srcQue, dstQue, Processes.psbIndex(psbHandle));
        Processes.reschedule(false);
    };

    // ==================================================================
    // 10.2.7 Set Process Priority
    // ==================================================================

    public static readonly OpImpl ESC_x0F_SPP = () =>
    {
        int priority = Cpu.pop();
        Cpu.checkEmptyStack();

        ushort link = Processes.fetchPSB_link(Cpu.PSB);
        link = Processes.setPsbLink_priority(link, priority);
        Processes.storePSB_link(Cpu.PSB, link);
        Processes.requeue(Processes.PDA_LP_header_ready, Processes.PDA_LP_header_ready, Cpu.PSB);
        Processes.reschedule(false);
    };

    // ==================================================================
    // 10.4.4.3 Disabling Interrupts
    // ==================================================================

    public static readonly OpImpl ESC_x10_DI = () =>
    {
        if (Cpu.WDC == PrincOpsDefs.WdcMax)
        {
            Cpu.interruptError();
        }
        Processes.disableInterrupts();
    };

    public static readonly OpImpl ESC_x11_EI = () =>
    {
        if (Cpu.WDC == 0)
        {
            Cpu.interruptError();
        }
        Processes.enableInterrupts();
    };

    // ==================================================================
    // Registration
    // ==================================================================

    public static void RegisterAll()
    {
        Opcodes.Install(0xF1, "ME", OPC_xF1_ME);
        Opcodes.Install(0xF2, "MX", OPC_xF2_MX);
        Opcodes.InstallEsc(0x02, "MW",  ESC_x02_MW);
        Opcodes.InstallEsc(0x03, "MR",  ESC_x03_MR);
        Opcodes.InstallEsc(0x04, "NC",  ESC_x04_NC);
        Opcodes.InstallEsc(0x05, "BC",  ESC_x05_BC);
        Opcodes.InstallEsc(0x06, "REQ", ESC_x06_REQ);
        Opcodes.InstallEsc(0x0F, "SPP", ESC_x0F_SPP);
        Opcodes.InstallEsc(0x10, "DI",  ESC_x10_DI);
        Opcodes.InstallEsc(0x11, "EI",  ESC_x11_EI);
    }
}
