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

// Agent for stream-attached "devices" or "coprocessors" — external programs
// and libraries on the "outside" of the mesa engine (the OS where the mesa
// emulator runs), providing services like local printing, file-system access,
// document conversion, copy&paste, drag&drop, and the like.
//
// (unsupported)
public class StreamAgent : Agent
{
    /*
     * CoProcessorFCBType
     */
    private const int fcb_lp_iocbHead = 0;
    private const int fcb_lp_iocbNext = 2;
    private const int fcb_w_headCommand = 4;
    private const int fcb_w_filler5 = 5;
    private const int fcb_w_headResult = 6;
    private const int fcb_w_filler7 = 7;
    private const int fcb_w_interruptSelector = 8;
    private const int fcb_w_stopAgent = 9;
    private const int fcb_w_agentStopped = 10;
    private const int fcb_w_streamWordSize = 11;
    private const int FCB_SIZE = 12;

    // CommandType: TYPE = MACHINE DEPENDENT
    //   {idle(0), accept(1), connect(2), delete(3), read(4), write(5)};
    private const int Command_idle    = 0;
    private const int Command_accept  = 1;
    private const int Command_connect = 2;
    private const int Command_delete  = 3;
    private const int Command_read    = 4;
    private const int Command_write   = 5;

    // ConnectionStateType: TYPE = MACHINE DEPENDENT
    //   {idle(0), accepting(1), connected(2), deleted(3)};
    private const int ConnState_idle      = 0;
    private const int ConnState_accepting = 1;
    private const int ConnState_connected = 2;
    private const int ConnState_deleted   = 3;

    // ResultType: TYPE = MACHINE DEPENDENT
    //   {completed(0), inProgress(1), error(2)};
    private const int Result_completed  = 0;
    private const int Result_inProgress = 1;
    private const int Result_error      = 2;

    public StreamAgent(int fcbAddress)
        : base(AgentDevice.streamAgent, fcbAddress, FCB_SIZE)
    {
    }

    public override void shutdown(System.Text.StringBuilder errMsgTarget)
    {
        // currently nothing to shutdown for this agent
    }

    public override void refreshMesaMemory()
    {
        // TODO: ?? nothing to transfer to mesa memory for this agent
    }

    public override void call()
    {
        bool stop = (this.getFcbWord(fcb_w_stopAgent) != PrincOpsDefs.FALSE);
        if (stop)
        {
            logf("call() - stopAgent = true\n");
            this.setFcbWord(fcb_w_agentStopped, PrincOpsDefs.TRUE);
            return;
        }
        this.setFcbWord(fcb_w_agentStopped, PrincOpsDefs.FALSE);

        int command = this.getFcbWord(fcb_w_headCommand);
        int interruptSelector = this.getFcbWord(fcb_w_interruptSelector);
        int iocbHead = this.getFcbWord(fcb_lp_iocbHead);
        int iocbNext = this.getFcbWord(fcb_lp_iocbNext);
        switch (command)
        {
            case Command_idle:
                logf("call() - headCommand = idle , iocbHead = 0x{0:X8} , iocbNext = 0x{1:X8} , intrSel = 0x{2:X4}\n",
                    iocbHead, iocbNext, interruptSelector);
                break;

            case Command_accept:
                logf("call() - headCommand = accept , iocbHead = 0x{0:X8} , iocbNext = 0x{1:X8} , intrSel = 0x{2:X4}\n",
                    iocbHead, iocbNext, interruptSelector);
                break;

            case Command_connect:
                logf("call() - headCommand = connect , iocbHead = 0x{0:X8} , iocbNext = 0x{1:X8} , intrSel = 0x{2:X4}\n",
                    iocbHead, iocbNext, interruptSelector);
                break;

            case Command_delete:
                logf("call() - headCommand = delete , iocbHead = 0x{0:X8} , iocbNext = 0x{1:X8} , intrSel = 0x{2:X4}\n",
                    iocbHead, iocbNext, interruptSelector);
                break;

            case Command_read:
                logf("call() - headCommand = read , iocbHead = 0x{0:X8} , iocbNext = 0x{1:X8} , intrSel = 0x{2:X4}\n",
                    iocbHead, iocbNext, interruptSelector);
                break;

            case Command_write:
                logf("call() - headCommand = write , iocbHead = 0x{0:X8} , iocbNext = 0x{1:X8} , intrSel = 0x{2:X4}\n",
                    iocbHead, iocbNext, interruptSelector);
                break;

            default:
                logf("call() - *invalid* headCommand = {0} , iocbHead = 0x{1:X8} , iocbNext = 0x{2:X8} , intrSel = 0x{3:X4}\n",
                    command, iocbHead, iocbNext, interruptSelector);
                break;
        }

        // TODO: implement
        this.setFcbWord(fcb_w_headResult, (ushort)Result_error);
    }

    protected override void initializeFcb()
    {
        this.setFcbDblWord(fcb_lp_iocbHead, 0);
        this.setFcbDblWord(fcb_lp_iocbNext, 0);
        this.setFcbWord(fcb_w_headCommand, (ushort)0);
        this.setFcbWord(fcb_w_filler5, (ushort)0);
        this.setFcbWord(fcb_w_headResult, (ushort)0);
        this.setFcbWord(fcb_w_filler7, (ushort)0);
        this.setFcbWord(fcb_w_interruptSelector, (ushort)0);
        this.setFcbWord(fcb_w_stopAgent, (ushort)0);
        this.setFcbWord(fcb_w_agentStopped, PrincOpsDefs.TRUE);
        this.setFcbWord(fcb_w_streamWordSize, (ushort)0);
    }
}
