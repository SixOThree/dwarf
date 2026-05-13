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

// IOP device handler for the display of a Daybreak/6085 machine.
public class HDisplay : DeviceHandler
{
    /*
     * state data of the ui components
     */

    // keyboard/mouse device handler for forwarding mouse movements
    // (handling mouse positions needs the hotspot position of the cursor
    // image, which is known here)
    private readonly HKeyboardMouse keyMoHandler;

    // mouse shape bits taken from the FCB until transferred to the UI
    private readonly ushort[] mesaCursor = new ushort[16];

    // the current mouse position as transmitted to the mesa engine
    private int mesaCurrX;
    private int mesaCurrY;

    // the hotspot position in the mouse bitmap
    private int mouseHotspotX = 0;
    private int mouseHotspotY = 0;

    // the mouse pointer bitmap if setting a new mouse pointer shape is pending
    private ushort[]? newCursorBitmap = null;

    // the mouse position coming from the ui (accessing these must be synchronized)
    private int uiCurrX = 0; // last position passed to the mesa machine
    private int uiCurrY = 0;
    private int uiNextX = 0; // new position from the ui to be passed to the mesa engine
    private int uiNextY = 0;
    private bool mouseMoved = false; // is a new mouse position waiting to be transferred to the mesa machine?

    /*
     * Function Control Block
     */

    private const string DisplayFCB = "DisplayFCB";

    private static readonly ushort cursorPosChangedMask = (ushort)byteSwap(0x8000);
    private static readonly ushort cursorMapChangedMask = (ushort)byteSwap(0x4000);
    private static readonly ushort borderPatChangedMask = (ushort)byteSwap(0x2000);
    private static readonly ushort backGrndChangedMask = (ushort)byteSwap(0x1000);
    private static readonly ushort displInfoChangedMask = (ushort)byteSwap(0x0800);
    private static readonly ushort pictureBorderPatChangedMask = (ushort)byteSwap(0x2800);
    private static readonly ushort alignmentChangedMask = (ushort)byteSwap(0x0400);
    private static readonly ushort allInfoChangedMask = (ushort)byteSwap(0xFC00);
    private static readonly ushort invInfoChangedMask = (ushort)byteSwap(0x03FF);

    private static readonly ushort headAllInfoChangedMask = (ushort)byteSwap(0xF800); // used by head for TurnOn (missing the flag 'alignmentChanged')

    private sealed class FCB : IORAddress
    {
        private readonly int startAddress;

        public readonly IOPTypes.TaskContextBlock displayTCB;
        public readonly Word displayLock;
        public readonly Word chngdInfo;
        public readonly IOPTypes.ClientCondition vertRetraceEvent;
        public readonly Word cursorXCoord;
        public readonly Word cursorYCoord;
        public readonly Word border;
        public readonly Field borderOddpairs;
        public readonly Field borderEvenpairs;
        public readonly Word[] cursorPattern = new Word[16];
        public readonly Word displCntl;
        public readonly Field displCntl_dataCursor;
        public readonly Field displCntl_picture;
        public readonly Field displCntl_mixRule;
        // begin CRTConfig crtConfig
        public readonly Word crtConfig_numberBitsPerLine;
        public readonly Word crtConfig_numberDisplayLines;
        public readonly Word crtConfig_configInfo;
        public readonly Word crtConfig_colorParams;
        public readonly Word crtConfig_xCoordOffset;
        public readonly Word crtConfig_yCoordOffset;
        public readonly Word crtConfig_pixelsRefresh;
        // end CRTConfig crtConfig
        // begin "Info in IO region that Mesa does not use"
        public readonly Word bitMapOrg;
        public readonly Word numberQuadWords;
        public readonly Word verticalCounts;
        public readonly Word horizontalcounts;
        public readonly Word displayIntrCnts;
        public readonly Word displayWdtCntsAndcursorUser;
        public readonly Word displayHWInitProc;
        public readonly Word cursorPositionProc;
        public readonly Word cursorPatternProc;
        public readonly Word borderPatternProc;
        public readonly Word backgndProc;
        public readonly Word commandProc;
        // end "Info in IO region that Mesa does not use"

        public readonly Field crtConfig_pixels;
        public readonly Field crtConfig_refresh;

        public FCB()
        {
            this.startAddress = syncToSegment() + IOR_BASE;

            this.displayTCB = new IOPTypes.TaskContextBlock(DisplayFCB, "displayTCB");
            this.displayLock = mkWord(DisplayFCB, "displayLock");
            this.chngdInfo = mkWord(DisplayFCB, "chngdInfo");
            this.vertRetraceEvent = new IOPTypes.ClientCondition(DisplayFCB, "vertRetraceEvent");
            this.cursorXCoord = mkByteSwappedWord(DisplayFCB, "cursorXCoord");
            this.cursorYCoord = mkByteSwappedWord(DisplayFCB, "cursorYCoord");
            this.border = mkWord(DisplayFCB, "border");
            this.borderOddpairs = mkField("borderOddpairs", this.border, 0xFF00);
            this.borderEvenpairs = mkField("borderEvenpairs", this.border, 0x00FF);
            for (int i = 0; i < this.cursorPattern.Length; i++)
            {
                this.cursorPattern[i] = mkWord(DisplayFCB, "border[" + i + "]");
            }
            this.displCntl = mkWord(DisplayFCB, "displCntl");
            this.displCntl_dataCursor = mkField("dataCursor", this.displCntl, 0xF000);
            this.displCntl_picture = mkField("picture", this.displCntl, 0x0800);
            this.displCntl_mixRule = mkField("mixRule", this.displCntl, 0x00F00);

            this.crtConfig_numberBitsPerLine = mkByteSwappedWord(DisplayFCB, "crtConfig.numberBitsPerLine");
            this.crtConfig_numberDisplayLines = mkByteSwappedWord(DisplayFCB, "crtConfig.numberDisplayLines");
            this.crtConfig_configInfo = mkWord(DisplayFCB, "crtConfig.configInfo");
            this.crtConfig_colorParams = mkByteSwappedWord(DisplayFCB, "crtConfig.colorParams");
            this.crtConfig_xCoordOffset = mkByteSwappedWord(DisplayFCB, "crtConfig.xCoordOffset");
            this.crtConfig_yCoordOffset = mkByteSwappedWord(DisplayFCB, "crtConfig.yCoordOffset");
            this.crtConfig_pixelsRefresh = mkWord(DisplayFCB, "crtConfig.pixelsRefresh");
            this.crtConfig_pixels = mkField("pixels", this.crtConfig_pixelsRefresh, 0xFF00);
            this.crtConfig_refresh = mkField("refresh", this.crtConfig_pixelsRefresh, 0x00FF);

            this.bitMapOrg = mkWord(DisplayFCB, "bitMapOrg");
            this.numberQuadWords = mkWord(DisplayFCB, "numberQuadWords");
            this.verticalCounts = mkWord(DisplayFCB, "verticalCounts");
            this.horizontalcounts = mkWord(DisplayFCB, "horizontalcounts");
            this.displayIntrCnts = mkWord(DisplayFCB, "displayIntrCnts");
            this.displayWdtCntsAndcursorUser = mkWord(DisplayFCB, "displayWdtCntsAndcursorUser");
            this.displayHWInitProc = mkWord(DisplayFCB, "displayHWInitProc");
            this.cursorPositionProc = mkWord(DisplayFCB, "cursorPositionProc");
            this.cursorPatternProc = mkWord(DisplayFCB, "cursorPatternProc");
            this.borderPatternProc = mkWord(DisplayFCB, "borderPatternProc");
            this.backgndProc = mkWord(DisplayFCB, "backgndProc");
            this.commandProc = mkWord(DisplayFCB, "commandProc");

            this.displayLock.set(mkMask());
        }

        public string getName() => DisplayFCB;

        public int getRealAddress() => this.startAddress;
    }

    /*
     * implementation of the iop6085 display interface
     */

    private readonly FCB fcb;

    public HDisplay(HKeyboardMouse keyMoHandler) : base(DisplayFCB, Config.IO_LOG_DISPLAY)
    {
        this.keyMoHandler = keyMoHandler;

        // allocate FCB data
        this.fcb = new FCB();

        // fill display configuration values (handler -> head)
        this.fcb.crtConfig_numberBitsPerLine.set((ushort)Mem.displayPixelWidth);
        this.fcb.crtConfig_numberDisplayLines.set((ushort)Mem.displayPixelHeight);
        this.fcb.crtConfig_configInfo.set(0);  // Daybreak (b7=0), interlaced (b6=0)
        this.fcb.crtConfig_colorParams.set(0); // non-color (b15=0)
        this.fcb.crtConfig_pixels.set(80);     // 80 pixels/inch
        this.fcb.crtConfig_refresh.set(60);    // 60 refreshes/second

        // run the vertical retrace notifier
        this.startVertRetraceNotifier();
    }

    public override int getFcbRealAddress() => this.fcb.getRealAddress();

    public override ushort getFcbSegment() => ((IORAddress)this.fcb).getIOPSegment();

    public override bool processNotify(ushort notifyMask)
    {
        // not relevant, the display device is triggered by LOCKMEM only
        return false;
    }

    public override void handleLockmem(ushort lockMask, int realAddress, MemOperation memOp, ushort oldValue, ushort newValue)
    {
        // check if it's for us
        if (lockMask != this.fcb.displayLock.get()) { return; }

        // check if it is a valid command
        ushort changedInfo = newValue; // this.fcb.chngdInfo.get();
        if ((changedInfo & invInfoChangedMask) != 0)
        {
            throw new ArgumentException(string.Format(
                "Unplausible chngdInfo for HDisplay.fcb: 0x{0:X4} (wrong byte-swap?)", changedInfo));
        }
        this.slogf("\n");
        this.logf("IOP::HDisplay.handleLockmem(rAddr = 0x{0:X6} , memOp = {1} , oldValue = 0x{2:X4} , newValue = 0x{3:X4}) -> changedInfo = 0x{4:X4}\n",
                realAddress, memOp.ToString(), oldValue, newValue, changedInfo & 0xFFFF);

        // check for change details
        if ((changedInfo & allInfoChangedMask) == 0)
        {
            return; // nothing changed...?
        }

        if (changedInfo == headAllInfoChangedMask)
        {
            // display was "turned on", do specific action for this special case
            this.logf(" => ***************** display turned ON *****************\n");
            this.allowVertRetraceIntr(this.fcb.vertRetraceEvent.maskValue.get());
            // ... scan VMMap for location of real display memory in VM
            bool found = Mem.locateRealDisplayMemoryInVMMap();
            this.logf("   => locateRealDisplayMemoryInVMMap() -> found = {0}\n", found.ToString().ToLowerInvariant());
            return;
        }

        if (changedInfo == cursorMapChangedMask)
        {
            this.logf(" => cursorMapChanged\n");
            this.logf("    +---------------------------------+\n");
            for (int i = 0; i < this.mesaCursor.Length; i++)
            {
                ushort line = this.fcb.cursorPattern[i].get();
                this.mesaCursor[i] = line;
                this.logf("    | {0} {1} {2} {3} {4} {5} {6} {7} {8} {9} {10} {11} {12} {13} {14} {15} |\n",
                    ((line & 0x8000) != 0) ? "x" : " ",
                    ((line & 0x4000) != 0) ? "x" : " ",
                    ((line & 0x2000) != 0) ? "x" : " ",
                    ((line & 0x1000) != 0) ? "x" : " ",
                    ((line & 0x0800) != 0) ? "x" : " ",
                    ((line & 0x0400) != 0) ? "x" : " ",
                    ((line & 0x0200) != 0) ? "x" : " ",
                    ((line & 0x0100) != 0) ? "x" : " ",
                    ((line & 0x0080) != 0) ? "x" : " ",
                    ((line & 0x0040) != 0) ? "x" : " ",
                    ((line & 0x0020) != 0) ? "x" : " ",
                    ((line & 0x0010) != 0) ? "x" : " ",
                    ((line & 0x0008) != 0) ? "x" : " ",
                    ((line & 0x0004) != 0) ? "x" : " ",
                    ((line & 0x0002) != 0) ? "x" : " ",
                    ((line & 0x0001) != 0) ? "x" : " "
                );
            }
            this.logf("    +---------------------------------+\n");

            // save the new shape until transferred to the UI
            this.newCursorBitmap = this.mesaCursor;
        }
        else if ((changedInfo & cursorMapChangedMask) != 0)
        {
            this.logf(" => cursorMapChanged-> ignored (as not single change)\n");
        }

        if (changedInfo == cursorPosChangedMask)
        {
            ushort newX = this.fcb.cursorXCoord.get();
            ushort newY = this.fcb.cursorYCoord.get();

            if (this.newCursorBitmap != null)
            {
                // compute hotspot position and register the new mouse shape with the ui
                this.logf(" => cursorPosChanged : setPosition with newCursorBitmap :: newX({0}) , newY({1}) ; mesaCurrX = {2} , mesaCurrY = {3} ; uiCurrX = {4} , uiCurrY = {5}\n",
                        newX, newY, this.mesaCurrX, this.mesaCurrY, this.uiCurrX, this.uiCurrY);

                int deltaHotspotX = this.mesaCurrX - newX;
                int deltaHotspotY = this.mesaCurrY - newY;
                this.mouseHotspotX = Math.Max(0, Math.Min(15, this.mouseHotspotX + deltaHotspotX));
                this.mouseHotspotY = Math.Max(0, Math.Min(15, this.mouseHotspotY + deltaHotspotY));
                this.logf("  => deltaHotspotX = {0} , deltaHotspotY = {1}  ==> newHotspotX = {2} , newHotspotY = {3}\n",
                        deltaHotspotX, deltaHotspotY, this.mouseHotspotX, this.mouseHotspotY);
                if (this.uiPointerBitmapAcceptor != null)
                {
                    uiPointerBitmapAcceptor(this.newCursorBitmap, this.mouseHotspotX, this.mouseHotspotY);
                    this.logf("  => new cursor registered in UI\n");
                }
                this.newCursorBitmap = null;

                this.logf("  => uiCurrX = {0} , uiCurrY = {1}\n", this.uiCurrX, this.uiCurrY);
                this.mesaCurrX = this.uiCurrX - this.mouseHotspotX;
                this.mesaCurrY = this.uiCurrY - this.mouseHotspotY;
                this.logf("  => new mesaCurrX = {0} , mesaCurrY = {1}\n", this.mesaCurrX, this.mesaCurrY);
            }
            else
            {
                this.logf(" => cursorPosChanged : new position [ {0} , {1} ]  ( mesaCurrX = {2} , mesaCurrY = {3} ; uiCurrX = {4} , uiCurrY = {5} )\n",
                        newX, newY, this.mesaCurrX, this.mesaCurrY, this.uiCurrX, this.uiCurrY);
                // TODO: reposition the "real" mouse pointer?
                // TODO: what to do with the new coordinates?
            }
        }
        else if ((changedInfo & cursorPosChangedMask) != 0)
        {
            this.logf(" => cursorPosChanged -> ignored (as not single change)\n");
        }

        if ((changedInfo & borderPatChangedMask) != 0)
        {
            this.logf(" => borderPatChangedMask (ignored)\n");
        }

        if ((changedInfo & backGrndChangedMask) != 0)
        {
            this.logf(" => backGrndChangedMask (ignored)\n");
        }

        if ((changedInfo & displInfoChangedMask) != 0)
        {
            // displayInfoChange *alone* <==> TurnOff
            this.logf(" => ***************** display turned OFF *****************\n");
            this.disallowVertRetraceIntr();
        }

        if ((changedInfo & pictureBorderPatChangedMask) != 0)
        {
            this.logf(" => pictureBorderPatChanged (ignored)\n");
        }

        if ((changedInfo & alignmentChangedMask) != 0)
        {
            this.logf(" => alignmentChanged (ignored)\n");
        }
    }

    public override void cleanupAfterLockmem(ushort lockMask, int realAddress)
    {
        if (lockMask != this.fcb.displayLock.get()) { return; }
        this.fcb.chngdInfo.set(0);
    }

    public override void handleLockqueue(int vAddr, int rAddr)
    {
        // not relevant for display handler
    }

    public override void shutdown(System.Text.StringBuilder errMsgTarget)
    {
        this.disallowVertRetraceIntr();
    }

    public override void refreshMesaMemory()
    {
        lock (this)
        {
            // transfer mouse position to mesa memory
            if (this.mouseMoved)
            {
                this.uiCurrX = this.uiNextX;
                this.uiCurrY = this.uiNextY;
                this.mesaCurrX = this.uiCurrX - this.mouseHotspotX;
                this.mesaCurrY = this.uiCurrY - this.mouseHotspotY;

                this.keyMoHandler.setNewCursorPosition((ushort)this.mesaCurrX, (ushort)this.mesaCurrY);
                this.mouseMoved = false;
            }
        }
    }

    public void recordMouseMoved(int toX, int toY)
    {
        lock (this)
        {
            this.uiNextX = toX;
            this.uiNextY = toY;
            this.mouseMoved = true;
            Processes.requestDataRefresh();
        }
    }

    /*
     * ui interface
     */

    private iUiDataConsumer.PointerBitmapAcceptor? uiPointerBitmapAcceptor = null;

    public void setPointerBitmapAcceptor(iUiDataConsumer.PointerBitmapAcceptor acceptor)
    {
        this.uiPointerBitmapAcceptor = acceptor;
    }

    /*
     * vertical retrace interrupts
     * (these interrupts are crucial as they trigger events in Pilot,
     * like checking for mouse movements and keyboard changes)
     * (this is probably the "stimulus" thread described somewhere in the programming manuals)
     */

    private bool doVertRetraceInterrupts = false;     // actively trigger interrupts?
    private ushort vertRetraceIntrMask = 0;           // interrupt mask to trigger
    private readonly object vertRetraceLock = new(); // synch-lock for accessing the above state

    private void allowVertRetraceIntr(ushort mask)
    {
        lock (this.vertRetraceLock)
        {
            this.doVertRetraceInterrupts = true;
            this.vertRetraceIntrMask = mask;
            this.logf(" => started vertical retrace interrupts with mask: 0x{0:X4}\n", this.vertRetraceIntrMask);
        }
    }

    private void disallowVertRetraceIntr()
    {
        lock (this)
        {
            lock (this.vertRetraceLock)
            {
                this.doVertRetraceInterrupts = false;
                this.vertRetraceIntrMask = 0;
            }
        }
    }

    private Thread? vertRetraceThread = null;

    private void verticalRetraceNotifier()
    {
        try
        {
            while (true)
            {
                Thread.Sleep(25); // 25 ms => 40 interrupts/second
                lock (this.vertRetraceLock)
                {
                    if (this.doVertRetraceInterrupts)
                    {
                        Processes.requestMesaInterrupt(this.vertRetraceIntrMask);
                    }
                }
            }
        }
        catch (ThreadInterruptedException)
        {
            // nothing to do: interrupted means no more retrace events
        }
    }

    private void startVertRetraceNotifier()
    {
        this.vertRetraceThread = new Thread(this.verticalRetraceNotifier)
        {
            IsBackground = true,
            Name = "HDisplay-VerticalRetraceNotifier",
        };
        this.vertRetraceThread.Start();
    }
}
