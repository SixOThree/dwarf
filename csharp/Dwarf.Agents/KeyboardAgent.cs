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

// Agent for the keyboard of a Dwarf machine.
//
// The purpose of this agent is to transfer the keyboard events (already
// translated to eLevelVKey values) into the bits in the FCB representing
// each key.
//
// The UI thread calls handleKeyUsage / resetKeys; the engine thread calls
// refreshMesaMemory. Java used `synchronized` on the three methods; the
// C# port uses an explicit lock object so the same exclusion holds.
public class KeyboardAgent : Agent
{
    /*
     * KeyboardFCBType (7 words for keystates)
     */
    private const int FCB_SIZE = 7;

    /*
     * key states
     */
    private const ushort ALL_KEYS_UP = 0xFFFF;

    private readonly ushort[] uiKeys = new ushort[FCB_SIZE];
    private bool uiKeysChanged = false;

    // exclusion lock between UI thread (handleKeyUsage/resetKeys) and
    // engine thread (refreshMesaMemory). Java relied on `synchronized` on
    // the instance; C# uses an explicit lock object.
    private readonly object _lock = new();

    public KeyboardAgent(int fcbAddress)
        : base(AgentDevice.keyboardAgent, fcbAddress, FCB_SIZE)
    {
        this.enableLogging(Config.IO_LOG_KEYBOARD);

        for (int i = 0; i < FCB_SIZE; i++)
        {
            this.uiKeys[i] = ALL_KEYS_UP;
        }
    }

    public override void shutdown(System.Text.StringBuilder errMsgTarget)
    {
        // nothing to shutdown for this agent
    }

    public override void call()
    {
        logf("call() - irrelevant, why is this called???\n");
    }

    protected override void initializeFcb()
    {
        for (int i = 0; i < FCB_SIZE; i++)
        {
            this.setFcbWord(i, ALL_KEYS_UP);
        }
    }

    public void resetKeys()
    {
        lock (_lock)
        {
            for (int i = 0; i < FCB_SIZE; i++)
            {
                this.uiKeys[i] = ALL_KEYS_UP;
            }
            this.uiKeysChanged = true;
            this.logf("resetKeys()\n");
        }
        Processes.requestDataRefresh();
    }

    public void handleKeyUsage(eLevelVKey key, bool isPressed)
    {
        lock (_lock)
        {
            if (isPressed)
            {
                key.setPressed(this.uiKeys);
            }
            else
            {
                key.setReleased(this.uiKeys);
            }
            this.uiKeysChanged = true;
            this.logf("handleKeyUsage( key = {0}, isPressed = {1} )\n", key, isPressed ? "true" : "false");
        }
        Processes.requestDataRefresh();
    }

    public override void refreshMesaMemory()
    {
        lock (_lock)
        {
            if (this.uiKeysChanged)
            {
                this.logf("refreshMesaMemory() -> \n");
                for (int i = 0; i < FCB_SIZE; i++)
                {
                    ushort keySetting = this.uiKeys[i];
                    this.setFcbWord(i, keySetting);
                }
                this.uiKeysChanged = false;
            }
        }
    }
}
