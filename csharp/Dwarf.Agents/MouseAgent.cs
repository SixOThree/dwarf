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

// Agent for the mouse pointer of a Dwarf machine.
//
// This agent is bidirectional: the current mouse position in the Dwarf
// window is transmitted from the UI to the mesa engine on one side; a new
// mouse pointer bitmap and hotspot defined by the running Pilot-based OS
// are registered to the UI on the other side.
//
// For the mouse shape, a new bitmap given to the display agent by the OS
// is passed from the display agent to the mouse agent — the new shape is
// only complete when the hotspot is known, which is determined when the
// mouse agent next receives a position. Receiving that position triggers
// the callback to the UI with the new mouse pointer shape.
public class MouseAgent : Agent
{
    // the current mouse position as transmitted to the mesa engine
    private int mesaCurrX;
    private int mesaCurrY;

    // the hotspot in the mouse bitmap
    private int mouseHotspotX = 0;
    private int mouseHotspotY = 0;

    // the mouse pointer bitmap if setting a new mouse pointer shape is pending
    private ushort[]? newCursorBitmap = null;

    // the mouse position coming from the UI (accessing these must be locked)
    private int uiCurrX = 0; // last position passed to the mesa machine
    private int uiCurrY = 0;
    private int uiNextX = 0; // new position from the UI to be passed to the mesa engine
    private int uiNextY = 0;
    private bool mouseMoved = false; // is passing the mouse position pending?

    // exclusion lock for UI thread (recordMouseMoved) vs engine thread
    // (refreshMesaMemory). Java's `synchronized` on these methods becomes
    // an explicit C# lock.
    private readonly object _lock = new();

    /*
     * MouseFCBType
     */
    private const int fcb_w_currentMousePosition_X = 0; // current position: set by the "real" mouse attached
    private const int fcb_w_currentMousePosition_Y = 1;
    private const int fcb_w_cursorOffset_X = 2; // delta as specified by the mesa client program
    private const int fcb_w_cursorOffset_Y = 3;
    private const int fcb_w_newValue_X = 4; // new position as specified by the mesa client program
    private const int fcb_w_newValue_Y = 5;
    private const int fcb_w_command = 6;
    private const int FCB_SIZE = 7;

    /*
     * MouseCommandType
     */
    private const ushort Command_nop = 0;
    private const ushort Command_setPosition = 1;
    private const ushort Command_setCursorPosition = 2;

    /*
     * mesa machine interface
     */

    public MouseAgent(int fcbAddress)
        : base(AgentDevice.mouseAgent, fcbAddress, FCB_SIZE)
    {
        this.enableLogging(Config.IO_LOG_MOUSE);

        this.uiCurrX = this.mesaCurrX + this.mouseHotspotX;
        this.uiCurrY = this.mesaCurrY + this.mouseHotspotY;
        this.logf("CTOR  => uiCurrX = {0} , uiCurrY = {1}\n", this.uiCurrX, this.uiCurrY);
    }

    public override void shutdown(System.Text.StringBuilder errMsgTarget)
    {
        // nothing to shutdown for this agent
    }

    public override void call()
    {
        ushort command = this.getFcbWord(fcb_w_command);

        int currX = this.getFcbWord(fcb_w_currentMousePosition_X);
        int currY = this.getFcbWord(fcb_w_currentMousePosition_Y);
        int offsX = this.getFcbWord(fcb_w_cursorOffset_X);
        int offsY = this.getFcbWord(fcb_w_cursorOffset_Y);
        int newX = this.getFcbWord(fcb_w_newValue_X);
        int newY = this.getFcbWord(fcb_w_newValue_Y);

        int trgX = 0;
        int trgY = 0;

        switch (command)
        {
            case Command_nop:
                logf("call() - nop\n");
                break;

            case Command_setPosition:
                if (this.newCursorBitmap != null)
                {
                    // compute hotspot position and register the new mouse shape with the UI
                    logf("call() - setPosition with newCursorBitmap :: newX({0}) , newY({1}) ; mesaCurrX = {2} , mesaCurrY = {3}\n",
                        newX, newY, this.mesaCurrX, this.mesaCurrY);

                    int deltaHotspotX = this.mesaCurrX - newX;
                    int deltaHotspotY = this.mesaCurrY - newY;
                    this.mouseHotspotX = Math.Max(0, Math.Min(15, this.mouseHotspotX + deltaHotspotX));
                    this.mouseHotspotY = Math.Max(0, Math.Min(15, this.mouseHotspotY + deltaHotspotY));
                    logf("  => deltaHotspotX = {0} , deltaHotspotY = {1}  ==> newHotspotX = {2} , newHotspotY = {3}\n",
                        deltaHotspotX, deltaHotspotY, this.mouseHotspotX, this.mouseHotspotY);
                    if (this.uiPointerBitmapAcceptor != null)
                    {
                        uiPointerBitmapAcceptor(this.newCursorBitmap, this.mouseHotspotX, this.mouseHotspotY);
                    }
                    this.newCursorBitmap = null;

                    this.logf("  => uiCurrX = {0} , uiCurrY = {1}\n", this.uiCurrX, this.uiCurrY);
                    this.mesaCurrX = this.uiCurrX - this.mouseHotspotX;
                    this.mesaCurrY = this.uiCurrY - this.mouseHotspotY;
                    this.setFcbWord(fcb_w_currentMousePosition_X, this.mesaCurrX);
                    this.setFcbWord(fcb_w_currentMousePosition_Y, this.mesaCurrY);
                    this.logf("  => new mesaCurrX = {0} , mesaCurrY = {1}\n", this.mesaCurrX, this.mesaCurrY);
                }
                else
                {
                    // TODO: reposition the "real" mouse pointer?
                    trgX = newX + offsX;
                    trgY = newY + offsY;
                    logf("call() - setPosition :: newX({0}) , newY({1}) , offsX({2}) , offsY({3}) => targetX = {4} , targetY = {5}\n",
                        newX, newY, offsX, offsY, trgX, trgY);
                    // ?? this.setFcbWord(fcb_w_currentMousePosition_X, newX);
                    // ?? this.setFcbWord(fcb_w_currentMousePosition_Y, newY);
                }
                break;

            case Command_setCursorPosition:
                // TODO: reposition the "real" mouse pointer?
                trgX = currX + offsX;
                trgY = currY + offsY;
                logf("call() - setCursorPosition :: currX({0}) , currY({1} , offsX({2}) , offsY({3}) => targetX = {4} , targetY = {5}\n",
                    currX, currY, offsX, offsY, trgX, trgY);
                break;

            default:
                logf("call() - invalid command: {0}\n", command);
                break;
        }
    }

    protected override void initializeFcb()
    {
        this.uiCurrX = Mem.getDisplayPixelWidth() / 2;
        this.uiCurrY = Mem.getDisplayPixelHeight() / 2;
        this.mesaCurrX = this.uiCurrX - this.mouseHotspotX;
        this.mesaCurrY = this.uiCurrY - this.mouseHotspotY;

        this.setFcbWord(fcb_w_currentMousePosition_X, this.mesaCurrX);
        this.setFcbWord(fcb_w_currentMousePosition_Y, this.mesaCurrY);
        this.setFcbWord(fcb_w_cursorOffset_X, (ushort)0);
        this.setFcbWord(fcb_w_cursorOffset_Y, (ushort)0);
        this.setFcbWord(fcb_w_newValue_X, (ushort)0);
        this.setFcbWord(fcb_w_newValue_Y, (ushort)0);
        this.setFcbWord(fcb_w_command, Command_nop);
    }

    /*
     * UI interface
     */

    private iUiDataConsumer.PointerBitmapAcceptor? uiPointerBitmapAcceptor = null;

    public void setPointerBitmapAcceptor(iUiDataConsumer.PointerBitmapAcceptor acceptor)
    {
        this.uiPointerBitmapAcceptor = acceptor;
    }

    public void setPointerBitmap(ushort[] cursor)
    {
        this.newCursorBitmap = cursor;
    }

    public void recordMouseMoved(int toX, int toY)
    {
        lock (_lock)
        {
            this.uiNextX = toX;
            this.uiNextY = toY;
            this.mouseMoved = true;
            this.logf("recordMouseMoved( toX = {0}, toY = {1} )\n", this.uiNextX, this.uiNextY);
        }
        Processes.requestDataRefresh();
    }

    public override void refreshMesaMemory()
    {
        lock (_lock)
        {
            if (this.mouseMoved)
            {
                this.uiCurrX = this.uiNextX;
                this.uiCurrY = this.uiNextY;
                this.mesaCurrX = this.uiCurrX - this.mouseHotspotX;
                this.mesaCurrY = this.uiCurrY - this.mouseHotspotY;

                this.logf("refreshMesaMemory( uiX = {0} , uiY = {1} ) => mesaX = {2} , mesaY = {3}\n",
                    this.uiCurrX, this.uiCurrY, this.mesaCurrX, this.mesaCurrY);

                this.setFcbWord(fcb_w_currentMousePosition_X, this.mesaCurrX);
                this.setFcbWord(fcb_w_currentMousePosition_Y, this.mesaCurrY);
                this.mouseMoved = false;
            }
        }
    }
}
