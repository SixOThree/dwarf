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

using Dwarf.Engine;

namespace Dwarf.Agents;

// Abstract base class for all agents defining the common public interface
// and providing the common functionality.
//
// A derived class usually defines a set of constants for the structure of the
// FCB (fcb_*), commands (Command_*), status codes (Status_*) etc., as needed
// for communicating with Pilot.
//
// Important fine point: when an agent asynchronously receives data from the
// device it represents (e.g. from the UI or from the network), that data must
// be buffered and may not be written directly to the mesa engine's memory
// space, as the necessary synchronizations would excessively slow down the
// mesa engine. An agent can freely access mesa memory when executing the
// call() method (servicing a CALLAGENT instruction) and when executing
// refreshMesaMemory(), which is called at more or less regular intervals by
// the mesa engine for exactly the purpose of synchronizing mesa memory with
// the external data changes accumulated so far.
public abstract class Agent
{
    // the agent type (needed for logging only)
    protected readonly AgentDevice agentType;

    // the own FCB address (virtual memory long pointer)
    protected readonly int fcbAddress;

    // the size of the FCB (in words)
    protected readonly int fcbSize;

    // should this agent log the own actions?
    protected bool logging = false;

    // Enable or disable logging for this agent.
    public void enableLogging(bool enabled)
    {
        this.logging = enabled;
    }

    // Base constructor with minimal required parameters.
    //
    // agentType  : the own type for the agent (for logging)
    // fcbAddress : the base address of the own FCB
    // fcbSize    : the size of the own FCB
    protected Agent(AgentDevice agentType, int fcbAddress, int fcbSize)
    {
        this.agentType = agentType;
        this.fcbAddress = fcbAddress;
        this.fcbSize = fcbSize;

        this.logf("ctor - fcbAddress = 0x{0:X8} , fcbSize = {1} , initializing FCB\n", fcbAddress, fcbSize);
        this.initializeFcb();
    }

    public AgentDevice getAgentType() => this.agentType;
    public int getFcbAddress() => this.fcbAddress;
    public int getFcbSize() => this.fcbSize;

    // Logging for this agent if enabled, issuing a line prefix identifying
    // the agent type.
    protected void logf(string template, params object[] args)
    {
        if (!this.logging) { return; }
        Console.Write("Agent " + agentType + ": " + string.Format(template, args));
    }

    // Logging for this agent if enabled, without a line prefix.
    protected void slogf(string template, params object[] args)
    {
        if (!this.logging) { return; }
        Console.Write(string.Format(template, args));
    }

    /*
     * general public interface of an Agent
     */

    // Fill the agent's FCB with the initial data for the agent configuration.
    protected abstract void initializeFcb();

    // Execute the agent operation(s) as instructed in the FCB of the agent.
    public abstract void call();

    // Shutdown the agent, possibly saving back all buffered data to the
    // external media hosting the agent's content.
    public abstract void shutdown(System.Text.StringBuilder errMsgTarget);

    // Copy all buffered new external data into mesa memory space.
    public abstract void refreshMesaMemory();

    /*
     * common internal functionality provided to agents
     */

    // Read a word from the agent's FCB.
    protected ushort getFcbWord(int offset)
    {
        if (offset < 0 || offset >= this.fcbSize)
        {
            Cpu.ERROR("Agent.getFcbWord :: offset out of range: " + offset);
        }
        return Mem.readWord(this.fcbAddress + offset);
    }

    // Read a double-word from the agent's FCB.
    protected int getFcbDblWord(int offset)
    {
        if (offset < 0 || (offset + 1) >= this.fcbSize)
        {
            Cpu.ERROR("Agent.getFcbWord :: offset out of range: " + offset);
        }
        return Mem.readDblWord(this.fcbAddress + offset);
    }

    // Write a word into the agent's FCB.
    protected void setFcbWord(int offset, ushort word)
    {
        if (offset < 0 || offset >= this.fcbSize)
        {
            Cpu.ERROR("Agent.getFcbWord :: offset out of range: " + offset);
        }
        Mem.writeWord(this.fcbAddress + offset, word);
    }

    // Write a word (given as int) into the agent's FCB. Truncates to 16 bits.
    protected void setFcbWord(int offset, int word)
    {
        if (offset < 0 || offset >= this.fcbSize)
        {
            Cpu.ERROR("Agent.getFcbWord :: offset out of range: " + offset);
        }
        Mem.writeWord(this.fcbAddress + offset, (ushort)(word & 0xFFFF));
    }

    // Write a double-word into the agent's FCB.
    protected void setFcbDblWord(int offset, int dblWord)
    {
        if (offset < 0 || (offset + 1) >= this.fcbSize)
        {
            Cpu.ERROR("Agent.getFcbWord :: offset out of range: " + offset);
        }
        Mem.writeDblWord(this.fcbAddress + offset, dblWord);
    }
}
