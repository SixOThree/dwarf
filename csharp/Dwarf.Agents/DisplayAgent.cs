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

// Agent for the display of a Dwarf machine.
//
// Besides providing basic information about the display (size, color depth),
// this agent provides a set of operations; only one is effectively implemented
// — set the mouse pointer shape. Since setting is a 2-step operation (set the
// bits, then move the pointer to adjust for the new hotspot), the bitmap is
// forwarded to the mouseAgent, which will call back the UI when the mouse
// position arrives.
public class DisplayAgent : Agent
{
    private readonly MouseAgent mouseAgent;

    /*
     * DisplayFCBType
     */
    private const int fcb_w_command = 0;
    private const int fcb_w_status = 1;
    private const int fcb_dbl_displayMemoryAddress = 2; // ! page, not address — and: real page (not virtual page)
    private const int fcb_w_color_u0 = 4; // nibbles: red, green
    private const int fcb_w_color_u1 = 5; // nibbles: blue, reserved
    private const int fcb_w_colormapping0 = 6;
    private const int fcb_w_colormapping1 = 7;
    private const int fcb_w_destRectanle_x = 8;
    private const int fcb_w_destRectanle_y = 9;
    private const int fcb_w_destRectanle_width = 10;
    private const int fcb_w_destRectanle_height = 11;
    private const int fcb_w_sourceOrigin_x = 12;
    private const int fcb_w_sourceOrigin_y = 13;
    private const int fcb_w16_cursorPattern = 14;
    private const int fcb_w4_pattern = 30;
    private const int fcb_w_patternFillMode = 34;
    private const int fcb_w_complemented = 35;
    private const int fcb_w_colorIndex = 36;
    private const int fcb_w_displayType = 37; // DisplayType ~ Mem.DisplayType.getBitDepth()
    private const int fcb_w_displayWidth = 38;
    private const int fcb_w_displayHeight = 39;
    private const int FCB_SIZE = 40;

    // DisplayIOFaceGuam
    private const ushort Command_nop                  = 0;
    private const ushort Command_setCLTEntry          = 1;
    private const ushort Command_getCLTEntry          = 2;
    private const ushort Command_setBackground        = 3;
    private const ushort Command_setCursorPattern     = 4;
    private const ushort Command_updateRectangle      = 5;
    private const ushort Command_copyRectangle        = 6;
    private const ushort Command_patternFillRectangle = 7;

    // DisplayStatus
    private const ushort Status_success                = 0;
    private const ushort Status_generalFailure         = 1;
    private const ushort Status_invalidCLTIndex        = 2;
    private const ushort Status_readOnlyCLT            = 3;
    private const ushort Status_invalidDestRectangle   = 4;
    private const ushort Status_invalidSourceRectangle = 5;

    // PatternFillMode
    private const int Pfm_copy = 0;
    private const int Pfm_and  = 1;
    private const int Pfm_or   = 2;
    private const int Pfm_xor  = 3;

    // Java upstream had an `isColorDisplay` field initialized here but never
    // read anywhere; C# flags assigned-but-unread private fields (CS0414),
    // so the field is dropped. `Mem.getDisplayType() == DisplayType.monochrome`
    // can be used at any future read site.
    private readonly int[] colorTable;

    public DisplayAgent(int fcbAddress, MouseAgent mouseAgent)
        : base(AgentDevice.displayAgent, fcbAddress, FCB_SIZE)
    {
        this.mouseAgent = mouseAgent;

        DisplayType displayType = Mem.getDisplayType();
        if (displayType == DisplayType.monochrome)
        {
            this.colorTable = new int[2];
            this.colorTable[0] = 0x00000000; // black
            this.colorTable[1] = 0x00FFFFFF; // white
        }
        else if (displayType == DisplayType.byteColor)
        {
            this.colorTable = new int[256];
            this.colorTable[0] = 0x00000000; // black
            // Java upstream has a typo here (overwrites this.colorTable[1] in
            // a loop bounded by `i`). Preserved verbatim — in practice all
            // 255 non-zero entries get overwritten by setCLTEntry before
            // they're observed, and the FCB area is zero-initialized in C#
            // anyway. See PROGRESS.md for the audit note.
            for (int i = 1; i < colorTable.Length; i++)
            {
                this.colorTable[1] = 0x00FFFFFF; // white
            }
        }
        else
        {
            throw new ArgumentException("Unsupported 'displayType' = " + displayType);
        }

        this.enableLogging(Config.IO_LOG_DISPLAY);
    }

    public int[] getColorTable() => this.colorTable;

    public override void call()
    {
        ushort command = this.getFcbWord(fcb_w_command);
        switch (command)
        {
            case Command_nop:
                logf("call() - nop\n");
                this.setFcbWord(fcb_w_status, Status_success);
                break;

            case Command_setCLTEntry:
                this.setCLTEntry();
                break;

            case Command_getCLTEntry:
                this.getCLTEntry();
                break;

            case Command_setBackground:
                this.setBackground();
                break;

            case Command_setCursorPattern:
                this.setCursorPattern();
                break;

            case Command_updateRectangle:
                this.updateRectangle();
                break;

            case Command_copyRectangle:
                this.copyRectangle();
                break;

            case Command_patternFillRectangle:
                this.patternFillRectangle();
                break;

            default:
                // Java upstream had a printf with no argument substitution for {0};
                // preserved as a non-substituted literal for diff fidelity.
                logf("call() - unknown command {0}\n", command);
                this.setFcbWord(fcb_w_status, Status_generalFailure);
                break;
        }
    }

    private void setCLTEntry()
    {
        int colorIndex = this.getFcbWord(fcb_w_colorIndex) & 0xFFFF;
        int colorU0 = this.getFcbWord(fcb_w_color_u0) & 0xFFFF;
        int colorU1 = this.getFcbWord(fcb_w_color_u1) & 0xFFFF;

        if (colorIndex >= this.colorTable.Length)
        {
            logf("call() - setCLTEntry , colorIndex = {0} , u0 = 0x{1:X4} , u1 = 0x{2:X4} => status = Status_invalidCLTIndex   ++++++ invalid CLT index\n",
                colorIndex, colorU0, colorU1);
            this.setFcbWord(fcb_w_status, Status_invalidCLTIndex);
            return;
        }

        int red = colorU0 & 0x00FF;
        int green = (colorU0 >> 8) & 0x00FF;
        int blue = colorU1 & 0x00FF;

        int tableValue = (red << 16) | (green << 8) | blue;
        this.colorTable[colorIndex] = tableValue;
        this.setFcbWord(fcb_w_status, Status_success);

        logf("call() - setCLTEntry , colorIndex = {0} , (r,g,b) = 0x ( {1:X2} , {2:X2} , {3:X2} ) => status = Status_success\n",
            colorIndex, red, green, blue);
    }

    private void getCLTEntry()
    {
        int colorIndex = this.getFcbWord(fcb_w_colorIndex) & 0xFFFF;
        logf("call() - getCLTEntry , colorIndex = {0}\n", colorIndex);
        if (colorIndex > this.colorTable.Length)
        {
            this.setFcbWord(fcb_w_status, Status_invalidCLTIndex);
            return;
        }

        int tableValue = this.colorTable[colorIndex];
        int red = (tableValue >> 16) & 0x0000FF;
        int green = (tableValue >> 8) & 0x0000FF;
        int blue = tableValue & 0x0000FF;

        this.setFcbWord(fcb_w_color_u0, (green << 8) | red);
        this.setFcbWord(fcb_w_color_u1, blue);
        this.setFcbWord(fcb_w_status, Status_success);
    }

    private void setBackground()
    {
        bool inverse = (this.getFcbWord(fcb_w_complemented) != 0);
        logf("call() - setBackground , inverse = {0}\n", inverse ? "true" : "false");

        // TODO: implement somehow

        this.setFcbWord(fcb_w_status, Status_success);
    }

    private void setCursorPattern()
    {
        logf("call() - setCursorPattern\n");
        logf("    +---------------------------------+\n");
        ushort[] cursor = new ushort[16];
        for (int i = 0; i < cursor.Length; i++)
        {
            ushort line = this.getFcbWord(fcb_w16_cursorPattern + i);
            cursor[i] = line;
            logf("    | {0} {1} {2} {3} {4} {5} {6} {7} {8} {9} {10} {11} {12} {13} {14} {15} |\n",
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
        logf("    +---------------------------------+\n");

        if (this.mouseAgent != null)
        {
            this.mouseAgent.setPointerBitmap(cursor);
        }

        this.setFcbWord(fcb_w_status, Status_success);
    }

    private void updateRectangle()
    {
        int x = this.getFcbWord(fcb_w_destRectanle_x);
        int y = this.getFcbWord(fcb_w_destRectanle_y);
        int w = this.getFcbWord(fcb_w_destRectanle_width);
        int h = this.getFcbWord(fcb_w_destRectanle_height);
        logf("call() - updateRectangle x = {0} , y = {1} , width = {2} , height = {3}\n", x, y, w, h);

        // TODO: implement more precise version?
        Mem.setDisplayMemoryDirty();
        logf("call() - updateRectangle => invalidated whole display memory\n");

        this.setFcbWord(fcb_w_status, Status_success);
    }

    private void copyRectangle()
    {
        int src_x = this.getFcbWord(fcb_w_sourceOrigin_x);
        int src_y = this.getFcbWord(fcb_w_sourceOrigin_y);
        int x = this.getFcbWord(fcb_w_destRectanle_x);
        int y = this.getFcbWord(fcb_w_destRectanle_y);
        int w = this.getFcbWord(fcb_w_destRectanle_width);
        int h = this.getFcbWord(fcb_w_destRectanle_height);
        logf("call() - copyRectangle src_x = {0} , src_y = {1} , x = {2} , y = {3} , width = {4} , height = {5}\n", src_x, src_y, x, y, w, h);

        // TODO: implement

        this.setFcbWord(fcb_w_status, Status_success);
    }

    private void patternFillRectangle()
    {
        int patternMode = this.getFcbWord(fcb_w_patternFillMode);
        string patternFillMode = patternMode switch
        {
            Pfm_copy => "copy(0)",
            Pfm_and  => "and(1)",
            Pfm_or   => "or(2)",
            Pfm_xor  => "xor(3)",
            _        => "invalid(" + patternMode + ")",
        };

        int x = this.getFcbWord(fcb_w_destRectanle_x);
        int y = this.getFcbWord(fcb_w_destRectanle_y);
        int w = this.getFcbWord(fcb_w_destRectanle_width);
        int h = this.getFcbWord(fcb_w_destRectanle_height);
        logf("call() - patternFillRectangle x = {0} , y = {1} , width = {2} , height = {3}, mode = \n", x, y, w, h, patternFillMode);

        logf("         pattern:\n");
        logf("           +---------------------------------+\n");
        for (int i = 0; i < 4; i++)
        {
            ushort line = this.getFcbWord(fcb_w4_pattern + i);
            logf("           | {0} {1} {2} {3} {4} {5} {6} {7} {8} {9} {10} {11} {12} {13} {14} {15} |\n",
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
        logf("           +---------------------------------+\n");

        // TODO: implement

        this.setFcbWord(fcb_w_status, Status_success);
    }

    public override void shutdown(System.Text.StringBuilder errMsgTarget)
    {
        // nothing to shutdown for this agent
    }

    public override void refreshMesaMemory()
    {
        // nothing to transfer to mesa memory for this agent
    }

    protected override void initializeFcb()
    {
        this.setFcbWord(fcb_w_command, Command_nop);
        this.setFcbWord(fcb_w_status, Status_success);
        this.setFcbDblWord(fcb_dbl_displayMemoryAddress, Mem.getDisplayRealPage());
        this.setFcbWord(fcb_w_color_u0, (ushort)0); // red, green
        this.setFcbWord(fcb_w_color_u1, (ushort)0); // blue, reserved
        this.setFcbWord(fcb_w_colormapping0, (ushort)0);
        this.setFcbWord(fcb_w_colormapping1, (ushort)1);
        this.setFcbWord(fcb_w_destRectanle_x, (ushort)0);
        this.setFcbWord(fcb_w_destRectanle_y, (ushort)0);
        this.setFcbWord(fcb_w_destRectanle_width, (ushort)0);
        this.setFcbWord(fcb_w_destRectanle_height, (ushort)0);
        this.setFcbWord(fcb_w_sourceOrigin_x, (ushort)0);
        this.setFcbWord(fcb_w_sourceOrigin_y, (ushort)0);
        // Java upstream bug preserved verbatim: writes 0 to fcb_w16_cursorPattern
        // 16 times (same slot) instead of 16 distinct slots. Harmless because the
        // FCB area is zero-initialized in the C# port (ushort[] backing memory).
        for (int i = 0; i < 16; i++) { this.setFcbWord(fcb_w16_cursorPattern, (ushort)0); }
        // Same bug pattern with fcb_w4_pattern. Preserved.
        for (int i = 0; i < 4; i++) { this.setFcbWord(fcb_w4_pattern, (ushort)0); }
        this.setFcbWord(fcb_w_patternFillMode, (ushort)Pfm_copy);
        this.setFcbWord(fcb_w_complemented, PrincOpsDefs.FALSE);
        this.setFcbWord(fcb_w_colorIndex, (ushort)0);
        this.setFcbWord(fcb_w_displayType, (ushort)Mem.getDisplayType().getType());
        this.setFcbWord(fcb_w_displayWidth, (ushort)Mem.getDisplayPixelWidth());
        this.setFcbWord(fcb_w_displayHeight, (ushort)Mem.getDisplayPixelHeight());
    }
}
