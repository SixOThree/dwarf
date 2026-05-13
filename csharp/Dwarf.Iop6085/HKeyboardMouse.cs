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

// IOP device handler for the keyboard and mouse of a Daybreak/6085 machine.
public class HKeyboardMouse : DeviceHandler
{
    /*
     * key states
     */
    private const int KEYBITS_WORDS = 9;

    private const ushort ALL_KEYS_UP = 0xFFFF;

    private readonly ushort[] uiKeys = new ushort[KEYBITS_WORDS];
    private bool uiKeysChanged = false;

    /*
     * Function Control Block
     */

    private const string KeyMoFCB = "KeyboardMouseFCB";

    private sealed class FCB : IORAddress
    {
        private readonly int startAddress;

        public readonly IOPTypes.TaskContextBlock keyBoardAndMouseTask;
        public readonly Word hexValue_convertKeyCodeToBit;
        public readonly Word frameErrorCnt;
        public readonly Word overRunErrorCnt;
        public readonly Word parityErrorCnt;
        public readonly Word spuriousIntCnt;
        public readonly Word watchDogCnt;
        public readonly Word badInterruptCnt;
        public readonly Word mouseX;
        public readonly Word mouseY;
        public readonly Word[] kBbase = new Word[KEYBITS_WORDS];
        public readonly Word[] kBindex = new Word[128];

        public FCB()
        {
            this.startAddress = syncToSegment() + IOR_BASE;

            this.keyBoardAndMouseTask = new IOPTypes.TaskContextBlock(KeyMoFCB, "displayTCB");
            this.hexValue_convertKeyCodeToBit = mkWord(KeyMoFCB, "hexValue+convertKeyCodeToBit");
            this.frameErrorCnt = mkByteSwappedWord(KeyMoFCB, "frameErrorCnt");
            this.overRunErrorCnt = mkByteSwappedWord(KeyMoFCB, "overRunErrorCnt");
            this.parityErrorCnt = mkByteSwappedWord(KeyMoFCB, "parityErrorCnt");
            this.spuriousIntCnt = mkByteSwappedWord(KeyMoFCB, "spuriousIntCnt");
            this.watchDogCnt = mkByteSwappedWord(KeyMoFCB, "watchDogCnt");
            this.badInterruptCnt = mkByteSwappedWord(KeyMoFCB, "badInterruptCnt");
            this.mouseX = mkWord(KeyMoFCB, "mouseX");
            this.mouseY = mkWord(KeyMoFCB, "mouseY");
            for (int i = 0; i < this.kBbase.Length; i++)
            {
                this.kBbase[i] = mkWord(KeyMoFCB, "kBbase[" + i + "]");
            }
            for (int i = 0; i < this.kBindex.Length; i++)
            {
                this.kBindex[i] = mkWord(KeyMoFCB, "kBindex[" + i + "]");
            }
        }

        public string getName() => KeyMoFCB;

        public int getRealAddress() => this.startAddress;
    }

    /*
     * implementation of the iop6085 keyboard and mouse interface
     */

    private readonly FCB fcb;

    // Java composed the two log flags with `|` (bitwise OR on booleans = logical OR).
    // C# does the same when both operands are bool. Const-fold yields false today.
    public HKeyboardMouse() : base(KeyMoFCB, Config.IO_LOG_KEYBOARD | Config.IO_LOG_MOUSE)
    {
        this.fcb = new FCB();

        for (int i = 0; i < KEYBITS_WORDS; i++)
        {
            this.fcb.kBbase[i].set(ALL_KEYS_UP);
            this.uiKeys[i] = ALL_KEYS_UP;
        }
        this.uiKeysChanged = false;
    }

    public override int getFcbRealAddress() => this.fcb.getRealAddress();

    public override ushort getFcbSegment() => ((IORAddress)this.fcb).getIOPSegment();

    public override bool processNotify(ushort notifyMask)
    {
        // no active notifications by client
        return false;
    }

    public override void handleLockmem(ushort lockMask, int realAddress, MemOperation memOp, ushort oldValue, ushort newValue)
    {
        // no synchronization necessary with client (access to fcb fields serialized through refreshMesaMemory(), see below)
    }

    public override void handleLockqueue(int vAddr, int rAddr)
    {
        // not relevant for keyboard/mouse handler
    }

    public override void shutdown(System.Text.StringBuilder errMsgTarget)
    {
        // nothing to save or shutdown
    }

    public override void refreshMesaMemory()
    {
        lock (this)
        {
            // transfer keyboard states from UI area to mesa memory
            if (this.uiKeysChanged)
            {
                for (int i = 0; i < KEYBITS_WORDS; i++)
                {
                    this.fcb.kBbase[i].set(this.uiKeys[i]);
                }
                this.uiKeysChanged = false;
            }
        }
    }

    public void handleKeyUsage(eLevelVKey key, bool isPressed)
    {
        lock (this)
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
            Processes.requestDataRefresh();
        }
    }

    public void resetKeys()
    {
        lock (this)
        {
            for (int i = 0; i < KEYBITS_WORDS; i++)
            {
                this.uiKeys[i] = ALL_KEYS_UP;
            }
            this.uiKeysChanged = true;
            Processes.requestDataRefresh();
        }
    }

    // must be called by the display device during a refreshMesaMemory() method, i.e. from the mesa processor thread!
    public void setNewCursorPosition(ushort x, ushort y)
    {
        this.fcb.mouseX.set(x);
        this.fcb.mouseY.set(y);
    }
}
