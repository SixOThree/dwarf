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
using Dwarf.Engine.Opcodes;

namespace Dwarf.Agents;

// Management class for all agents as central dispatch instance for all
// agent-related operations.
//
// The `Agents` static class initializes the agents to correctly set up the
// FCB area at virtual memory ioArea start (mapped to real memory address
// 0x00000000), accessed by the mesa engine to read agent state or enlink
// IOCBs for agent requests before executing the CALLAGENT instruction (which
// invokes `callAgent()`). Conversely the devices can be closed and buffered
// data saved by deinitializing the agents.
//
// The UI can access the necessary callbacks for transferring UI changes to
// the mesa engine by calling `getUiCallbacks()`. Further methods allow
// changing the current "inserted" diskette in the simulated floppy drive.
// (UI-callback wiring and insert/eject floppy are deferred until the
// corresponding agents — Keyboard/Mouse/Display/Floppy — are ported in their
// own Phase D sub-tasks.)
//
// **Phase D-1 scope**: only NullAgent + ReservedAgent slots are wired. Each
// other slot is documented inline as a TODO referencing the Phase D sub-task
// that will fill it in. The ESC overrides for CALLAGENT and MAPDISPLAY are
// installed; calling an unported agent via CALLAGENT will hit the "agent
// not available" error path, which is the correct behavior for a partial
// boot.
public static class Agents
{
    // all agents
    private static readonly Agent?[] agent = new Agent?[Enum.GetValues<AgentDevice>().Length];

    // specific agents directly accessible for published functionality
    private static DiskAgent? diskAgent;
    // TODO Phase D-4: private static FloppyAgent? floppyAgent;
    // TODO Phase D-9: private static NetworkAgent? networkAgent;
    // TODO Phase D-5: private static DisplayAgent? displayAgent;
    // TODO Phase D-7: private static MouseAgent? mouseAgent;
    // TODO Phase D-6: private static KeyboardAgent? keyboardAgent;

    // TODO Phase D-5..D-7: port UiCallbacks (Java's `iUiDataConsumer`
    // implementation) once Keyboard/Mouse/Display agents land. Skeleton:
    //   private sealed class UiCallbacks : iUiDataConsumer { ... }
    //   public static iUiDataConsumer getUiCallbacks() => new UiCallbacks();

    // TODO Phase D-5: getDisplayColorTable() once DisplayAgent is ported.
    //   public static int[] getDisplayColorTable() => displayAgent.getColorTable();

    // Initialize the agents and set up the FCB area of the mesa engine.
    public static void initialize()
    {
        // install Guam-specific instructions
        Opcodes.implantEscOverride(0x89, "zESC.CALLAGENT", escCALLAGENT);
        Opcodes.implantEscOverride(0x8A, "zESC.MAPDISPLAY", escMAPDISPLAY);

        // relevant (virtual) addresses in ioRegion:
        // - first COUNT[AgentDevice] LONG POINTERs to the FCB of each agent
        // - then the FCB areas of the devices
        int currFcbPtr = Mem.ioArea;
        int currFcbArea = roundUp(currFcbPtr + (Enum.GetValues<AgentDevice>().Length * 2));

        // nullAgent at index 0
        int idx = 0;
        Mem.writeDblWord(currFcbPtr, currFcbArea);
        agent[idx] = new NullAgent(currFcbArea);
        currFcbPtr += 2;
        currFcbArea = roundUp(currFcbArea + agent[idx]!.getFcbSize());

        // diskAgent at index 1
        idx++;
        Mem.writeDblWord(currFcbPtr, currFcbArea);
        diskAgent = new DiskAgent(currFcbArea);
        agent[idx] = diskAgent;
        currFcbPtr += 2;
        currFcbArea = roundUp(currFcbArea + agent[idx]!.getFcbSize());

        // floppyAgent at index 2 — TODO Phase D-4
        idx++;
        Mem.writeDblWord(currFcbPtr, 0);
        currFcbPtr += 2;

        // networkAgent at index 3 — TODO Phase D-9
        idx++;
        Mem.writeDblWord(currFcbPtr, 0);
        currFcbPtr += 2;

        // parallelAgent at index 4 — wired here for layout; nullified by the
        // post-init pass below
        idx++;
        Mem.writeDblWord(currFcbPtr, currFcbArea);
        agent[idx] = new ParallelAgent(currFcbArea);
        currFcbPtr += 2;
        currFcbArea = roundUp(currFcbArea + agent[idx]!.getFcbSize());

        // keyboardAgent at index 5 — TODO Phase D-6
        idx++;
        Mem.writeDblWord(currFcbPtr, 0);
        currFcbPtr += 2;

        // beepAgent at index 6
        idx++;
        Mem.writeDblWord(currFcbPtr, currFcbArea);
        agent[idx] = new BeepAgent(currFcbArea);
        currFcbPtr += 2;
        currFcbArea = roundUp(currFcbArea + agent[idx]!.getFcbSize());

        // mouseAgent at index 7 — TODO Phase D-7
        idx++;
        Mem.writeDblWord(currFcbPtr, 0);
        currFcbPtr += 2;

        // processorAgent at index 8
        idx++;
        Mem.writeDblWord(currFcbPtr, currFcbArea);
        agent[idx] = new ProcessorAgent(currFcbArea);
        currFcbPtr += 2;
        currFcbArea = roundUp(currFcbArea + agent[idx]!.getFcbSize());

        // streamAgent at index 9
        idx++;
        Mem.writeDblWord(currFcbPtr, currFcbArea);
        agent[idx] = new StreamAgent(currFcbArea);
        currFcbPtr += 2;
        currFcbArea = roundUp(currFcbArea + agent[idx]!.getFcbSize());

        // serialAgent at index 10 — wired here for layout; nullified by the
        // post-init pass below
        idx++;
        Mem.writeDblWord(currFcbPtr, currFcbArea);
        agent[idx] = new SerialAgent(currFcbArea);
        currFcbPtr += 2;
        currFcbArea = roundUp(currFcbArea + agent[idx]!.getFcbSize());

        // ttyAgent at index 11 — wired here for layout; nullified by the
        // post-init pass below
        idx++;
        Mem.writeDblWord(currFcbPtr, currFcbArea);
        agent[idx] = new TtyAgent(currFcbArea);
        currFcbPtr += 2;
        currFcbArea = roundUp(currFcbArea + agent[idx]!.getFcbSize());

        // displayAgent at index 12 — TODO Phase D-5
        idx++;
        Mem.writeDblWord(currFcbPtr, 0);
        currFcbPtr += 2;

        // reserved3Agent at index 13
        idx++;
        Mem.writeDblWord(currFcbPtr, currFcbArea);
        agent[idx] = new ReservedAgent(AgentDevice.reserved3Agent, currFcbArea);
        currFcbPtr += 2;
        currFcbArea = roundUp(currFcbArea + agent[idx]!.getFcbSize());

        // reserved2Agent at index 14
        idx++;
        Mem.writeDblWord(currFcbPtr, currFcbArea);
        agent[idx] = new ReservedAgent(AgentDevice.reserved2Agent, currFcbArea);
        currFcbPtr += 2;
        currFcbArea = roundUp(currFcbArea + agent[idx]!.getFcbSize());

        // reserved1Agent at index 15
        idx++;
        Mem.writeDblWord(currFcbPtr, currFcbArea);
        agent[idx] = new ReservedAgent(AgentDevice.reserved1Agent, currFcbArea);
        currFcbPtr += 2;
        currFcbArea = roundUp(currFcbArea + agent[idx]!.getFcbSize());

        // sanity check: verify that each agent is at the expected index position
        for (int i = 0; i < Enum.GetValues<AgentDevice>().Length; i++)
        {
            if (agent[i] != null)
            {
                if (agent[i]!.getAgentType().getIndex() != i)
                {
                    Cpu.ERROR("Agents.initialize :: wrong agent at index " + i + " -> " + agent[i]!.getAgentType());
                }
            }
        }

        // register memory updater for transferring data between agent and mesa memory
        Processes.setMesaMemoryUpdater(processPendingMesaMemoryUpdates);

        // register statistics provider (Phase D-2/D-4/D-9 will supply real counters)
        Processes.setStatisticsProvider(new AgentStatisticsProvider());

        // reset the FCB pointer for agents not present in Dwarf
        idx = AgentDevice.nullAgent.getIndex();
        agent[idx] = null;
        Mem.writeDblWord(Mem.ioArea + (2 * idx), 0);
        idx = AgentDevice.parallelAgent.getIndex();
        agent[idx] = null;
        Mem.writeDblWord(Mem.ioArea + (2 * idx), 0);
        idx = AgentDevice.serialAgent.getIndex();
        agent[idx] = null;
        Mem.writeDblWord(Mem.ioArea + (2 * idx), 0);
        idx = AgentDevice.ttyAgent.getIndex();
        agent[idx] = null;
        Mem.writeDblWord(Mem.ioArea + (2 * idx), 0);
        idx = AgentDevice.reserved3Agent.getIndex();
        agent[idx] = null;
        Mem.writeDblWord(Mem.ioArea + (2 * idx), 0);
        idx = AgentDevice.reserved2Agent.getIndex();
        agent[idx] = null;
        Mem.writeDblWord(Mem.ioArea + (2 * idx), 0);
        idx = AgentDevice.reserved1Agent.getIndex();
        agent[idx] = null;
        Mem.writeDblWord(Mem.ioArea + (2 * idx), 0);
    }

    // ensure that an FCB starts at a double word address
    private static int roundUp(int ptr)
    {
        if ((ptr & 1) != 0) { return ptr + 1; }
        return ptr;
    }

    /*
     * Agent specific instructions
     */

    // MAPDISPLAY - Map Display
    private static readonly OpImpl escMAPDISPLAY = () =>
    {
        int pageCountInEachBlock = Cpu.pop() & 0xFFFF;
        int totalPageCount = Cpu.pop() & 0xFFFF;
        int startingRealPage = Cpu.popLong();
        int startingVirtualPage = Cpu.popLong();

        // sanity checks
        if (startingRealPage != Mem.getDisplayRealPage()) { Cpu.ERROR("MAPDISPLAY :: startingRealPage != Mem.getDisplayRealPage()"); }
        if (totalPageCount != Mem.getDisplayPageSize()) { Cpu.ERROR("MAPDISPLAY :: totalPageCount != Mem.getDisplayPageSize()"); }

        // strange but this seems to be the right usage of the misleadingly named arguments
        // (works for both 1-bit and 8-bit deep displays)
        int firstVirtualPage = startingVirtualPage + pageCountInEachBlock - totalPageCount;

        Mem.mapDisplayMemory(firstVirtualPage);
    };

    // CALLAGENT - Call Device Agent
    private static readonly OpImpl escCALLAGENT = () =>
    {
        int agentIndex = Cpu.pop();

        if (agentIndex < 0 || agentIndex >= agent.Length)
        {
            Cpu.ERROR("CALLAGENT :: invalid agentIndex " + agentIndex);
            return; // we won't get here
        }
        if (agent[agentIndex] == null)
        {
            Cpu.ERROR("CALLAGENT :: agent not available at agentIndex " + agentIndex);
            return; // we won't get here
        }
        agent[agentIndex]!.call();
    };

    /*
     * support for external operations
     */

    // Transfer all cached data changes into mesa memory space.
    private static void processPendingMesaMemoryUpdates()
    {
        for (int i = 0; i < Enum.GetValues<AgentDevice>().Length; i++)
        {
            if (agent[i] != null)
            {
                agent[i]!.refreshMesaMemory();
            }
        }
    }

    // Request a write back of all buffered data on all agents and finalize
    // the agents.
    public static void shutdown(System.Text.StringBuilder errMsgTarget)
    {
        for (int i = 0; i < agent.Length; i++)
        {
            Agent? a = agent[i];
            if (a != null) { a.shutdown(errMsgTarget); }
        }
    }

    /*
     * access to statistical data
     *
     * Phase D-1: returns zeros across the board (no real counter sources yet).
     * Phase D-2/D-4/D-9: hook up the diskAgent/floppyAgent/networkAgent
     * counter accessors once those agents are ported.
     */

    private sealed class AgentStatisticsProvider : Processes.StatisticsProvider
    {
        public int getDiskReads() => diskAgent?.getReads() ?? 0;
        public int getDiskWrites() => diskAgent?.getWrites() ?? 0;
        public int getFloppyReads() => 0;          // TODO Phase D-4
        public int getFloppyWrites() => 0;         // TODO Phase D-4
        public int getNetworkpacketsSent() => 0;   // TODO Phase D-9
        public int getNetworkpacketsReceived() => 0; // TODO Phase D-9
    }

    /*
     * floppy operations — TODO Phase D-4: insertFloppy / ejectFloppy once
     * FloppyAgent is ported.
     */

    // for debugging purposes
    public static void dumpIoArea()
    {
        Console.WriteLine("Agent::InitializeAgent() :: begin");

        Console.WriteLine("  FCB addresses at IO-region start");
        for (int i = 0; i < agent.Length; i++)
        {
            int agFcbPtr = Mem.ioArea + (i * 2);
            Console.WriteLine($"     agent[{i:D2}] -> FCB at 0x{agFcbPtr:X8} : 0x{Mem.readWord(agFcbPtr):X4} 0x{Mem.readWord(agFcbPtr + 1):X4}");
        }

        for (int i = 0; i < agent.Length; i++)
        {
            Agent? a = agent[i];
            if (a == null)
            {
                Console.WriteLine($"Agent::InitializeAgent() :: agent[{i}] not present");
                continue;
            }
            int sz = a.getFcbSize();
            int ad = a.getFcbAddress();
            Console.WriteLine($"Agent::InitializeAgent() :: agent[{i}] .. FCBSize = {sz:D3} .. at 0x{ad:X8} .. name = '{a.getAgentType()}'");
            if (sz == 0) { continue; }
            Console.Write("   FCB content:");
            for (int x = 0; x < sz; x++)
            {
                if ((x % 8) == 0) { Console.Write("\n     "); }
                Console.Write($" 0x{Mem.readWord(ad + x):X4}");
            }
            Console.WriteLine();
        }

        Console.WriteLine("Agent::InitializeAgent() -----------------------\n");
    }
}
