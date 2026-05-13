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

// Implementation of PrincOps 4.0 chapter 8: Block Transfers.
// Also: BITBLT/BITBLTX/COLORBLT/TRAPZBLT machinery.
//
// The instruction implementations deviate from PrincOps by not pushing the
// state back on the stack after each unit processed. Instead:
//   - state is pushed if an interrupt is pending
//   - state is added to the saved state by the page fault via MesaAbort.
//
// Java upstream used `new OpImpl() { public void execute() {...} }` anonymous
// inner classes to work around a Java 8 compiler/spec issue around lambda
// capture of mutable locals across try/catch — comment "flaw in Java 8 spec
// or bug in Eclipse-Java8-Compiler?". C# lambdas handle this fine; we use
// lambdas throughout.
//
// Logging code under `Config.LOG_BITBLT_INSNS && Config.dynLogBitblts` is
// dead-code-eliminated (LOG_BITBLT_INSNS is `const false`). Stripped for
// readability; not behavior-relevant.
public static class Ch08_Block_Transfers
{
    // ==================================================================
    // 8.1 Word Boundary Block Transfers
    // ==================================================================

    public static readonly OpImpl OPC_xF3_BLT = () =>
    {
        int dest   = Cpu.pop();
        int count  = Cpu.pop();
        int source = Cpu.pop();

        try
        {
            while (true)
            {
                if (count == 0) { return; }
                Mem.writeMDSWord(dest, Mem.readMDSWord(source)); // may throw MesaAbort on page fault
                count--;
                source++;
                dest++;
                if (Processes.interruptPending() && count > 0)
                {
                    Cpu.push(source);
                    Cpu.push(count);
                    Cpu.push(dest);
                    Cpu.PC = Cpu.savedPC;
                    return;
                }
            }
        }
        catch (Cpu.MesaAbort e)
        {
            e.beginUpdateStack();
            Cpu.push(source);
            Cpu.push(count);
            Cpu.push(dest);
            throw e.updateStack();
        }
    };

    public static readonly OpImpl OPC_xF4_BLTL = () =>
    {
        int dest   = Cpu.popLong();
        int count  = Cpu.pop();
        int source = Cpu.popLong();

        try
        {
            while (true)
            {
                if (count == 0) { return; }
                Mem.writeWord(dest, Mem.readWord(source));
                count--;
                source++;
                dest++;
                if (Processes.interruptPending() && count > 0)
                {
                    Cpu.pushLong(source);
                    Cpu.push(count);
                    Cpu.pushLong(dest);
                    Cpu.PC = Cpu.savedPC;
                    return;
                }
            }
        }
        catch (Cpu.MesaAbort e)
        {
            e.beginUpdateStack();
            Cpu.pushLong(source);
            Cpu.push(count);
            Cpu.pushLong(dest);
            throw e.updateStack();
        }
    };

    // BLTLR — Block Transfer Long Reversed (corrected per PrincOps-Corrections)
    public static readonly OpImpl ESC_x27_BLTLR = () =>
    {
        int dest   = Cpu.popLong();
        int count  = Cpu.pop();
        int source = Cpu.popLong();

        try
        {
            while (true)
            {
                if (count == 0) { return; }
                Mem.writeWord(dest + count - 1, Mem.readWord(source + count - 1));
                count--;
                if (Processes.interruptPending() && count > 0)
                {
                    Cpu.pushLong(source);
                    Cpu.push(count);
                    Cpu.pushLong(dest);
                    Cpu.PC = Cpu.savedPC;
                    return;
                }
            }
        }
        catch (Cpu.MesaAbort e)
        {
            e.beginUpdateStack();
            Cpu.pushLong(source);
            Cpu.push(count);
            Cpu.pushLong(dest);
            throw e.updateStack();
        }
    };

    public static readonly OpImpl OPC_xF5_BLTC = () =>
    {
        int dest   = Cpu.pop();
        int count  = Cpu.pop();
        int source = Cpu.pop();

        try
        {
            while (true)
            {
                if (count == 0) { return; }
                Mem.writeMDSWord(dest, Mem.readCode(source));
                count--;
                source++;
                dest++;
                if (Processes.interruptPending() && count > 0)
                {
                    Cpu.push(source);
                    Cpu.push(count);
                    Cpu.push(dest);
                    Cpu.PC = Cpu.savedPC;
                    return;
                }
            }
        }
        catch (Cpu.MesaAbort e)
        {
            e.beginUpdateStack();
            Cpu.push(source);
            Cpu.push(count);
            Cpu.push(dest);
            throw e.updateStack();
        }
    };

    public static readonly OpImpl OPC_xF6_BLTCL = () =>
    {
        int dest   = Cpu.popLong();
        int count  = Cpu.pop();
        int source = Cpu.pop();

        try
        {
            while (true)
            {
                if (count == 0) { return; }
                Mem.writeWord(dest, Mem.readCode(source));
                count--;
                source++;
                dest++;
                if (Processes.interruptPending() && count > 0)
                {
                    Cpu.push(source);
                    Cpu.push(count);
                    Cpu.pushLong(dest);
                    Cpu.PC = Cpu.savedPC;
                    return;
                }
            }
        }
        catch (Cpu.MesaAbort e)
        {
            e.beginUpdateStack();
            Cpu.push(source);
            Cpu.push(count);
            Cpu.pushLong(dest);
            throw e.updateStack();
        }
    };

    public static readonly OpImpl ESC_x2A_CKSUM = () =>
    {
        int source = Cpu.popLong();
        int count  = Cpu.pop();
        int cksum  = Cpu.pop();
        try
        {
            while (true)
            {
                if (count == 0) { break; }
                cksum = checksum(cksum, Mem.readWord(source));
                count--;
                source++;
                if (Processes.interruptPending() && count > 0)
                {
                    Cpu.push(cksum);
                    Cpu.push(count);
                    Cpu.pushLong(source);
                    Cpu.PC = Cpu.savedPC;
                    return;
                }
            }
        }
        catch (Cpu.MesaAbort e)
        {
            e.beginUpdateStack();
            Cpu.push(cksum);
            Cpu.push(count);
            Cpu.pushLong(source);
            throw e.updateStack();
        }
        if (cksum == 0xFFFF) { cksum = 0; }
        Cpu.push(cksum & 0xFFFF);
    };

    private static int checksum(int cksum, ushort data)
    {
        int temp = (cksum + data) & 0xFFFF;
        if (cksum > temp) { temp += 1; }
        if (temp >= 0x8000)
        {
            temp = (temp * 2) + 1;
        }
        else
        {
            temp *= 2;
        }
        return temp & 0xFFFF;
    }

    // ==================================================================
    // 8.2 Block Comparisons
    // ==================================================================

    public static readonly OpImpl ESC_x28_BLEL = () =>
    {
        int ptr1  = Cpu.popLong();
        int count = Cpu.pop();
        int ptr2  = Cpu.popLong();
        try
        {
            while (true)
            {
                if (count == 0) { Cpu.push((ushort)1); return; }
                if (Mem.readWord(ptr1) != Mem.readWord(ptr2)) { Cpu.push((ushort)0); return; }
                count--;
                ptr1++;
                ptr2++;
                if (Processes.interruptPending())
                {
                    if (count == 0) { Cpu.push((ushort)1); return; }
                    Cpu.pushLong(ptr2);
                    Cpu.push(count);
                    Cpu.pushLong(ptr1);
                    Cpu.PC = Cpu.savedPC;
                    return;
                }
            }
        }
        catch (Cpu.MesaAbort e)
        {
            e.beginUpdateStack();
            Cpu.pushLong(ptr2);
            Cpu.push(count);
            Cpu.pushLong(ptr1);
            throw e.updateStack();
        }
    };

    public static readonly OpImpl ESC_x29_BLECL = () =>
    {
        int ptr    = Cpu.popLong();
        int count  = Cpu.pop();
        int offset = Cpu.pop();
        try
        {
            while (true)
            {
                if (count == 0) { Cpu.push((ushort)1); return; }
                if (Mem.readWord(ptr) != Mem.readCode(offset)) { Cpu.push((ushort)0); return; }
                count--;
                ptr++;
                offset++;
                if (Processes.interruptPending())
                {
                    if (count == 0) { Cpu.push((ushort)1); return; }
                    Cpu.push(offset);
                    Cpu.push(count);
                    Cpu.pushLong(ptr);
                    Cpu.PC = Cpu.savedPC;
                    return;
                }
            }
        }
        catch (Cpu.MesaAbort e)
        {
            e.beginUpdateStack();
            Cpu.push(offset);
            Cpu.push(count);
            Cpu.pushLong(ptr);
            throw e.updateStack();
        }
    };

    // ==================================================================
    // 8.3 Byte Boundary Block Transfers
    // ==================================================================

    public static readonly OpImpl ESC_x2D_BYTBLT = () =>
    {
        int sourceOffset = Cpu.pop();
        int sourceBase   = Cpu.popLong();
        int count        = Cpu.pop();
        int destOffset   = Cpu.pop();
        int destBase     = Cpu.popLong();

        sourceBase  += sourceOffset / 2;
        sourceOffset = sourceOffset % 2;
        destBase    += destOffset / 2;
        destOffset   = destOffset % 2;

        try
        {
            while (count != 0)
            {
                Mem.storeByte(destBase, destOffset, Mem.fetchByte(sourceBase, sourceOffset));
                count--;
                sourceOffset++;
                destOffset++;
                if (Processes.interruptPending() && count > 0)
                {
                    Cpu.pushLong(destBase);
                    Cpu.push(destOffset);
                    Cpu.push(count);
                    Cpu.pushLong(sourceBase);
                    Cpu.push(sourceOffset);
                    Cpu.PC = Cpu.savedPC;
                    return;
                }
            }
        }
        catch (Cpu.MesaAbort e)
        {
            e.beginUpdateStack();
            Cpu.pushLong(destBase);
            Cpu.push(destOffset);
            Cpu.push(count);
            Cpu.pushLong(sourceBase);
            Cpu.push(sourceOffset);
            throw e.updateStack();
        }
    };

    public static readonly OpImpl ESC_x2E_BYTBLTR = () =>
    {
        int sourceOffset = Cpu.pop();
        int sourceBase   = Cpu.popLong();
        int count        = Cpu.pop();
        int destOffset   = Cpu.pop();
        int destBase     = Cpu.popLong();

        sourceBase  += sourceOffset / 2;
        sourceOffset = sourceOffset % 2;
        destBase    += destOffset / 2;
        destOffset   = destOffset % 2;

        int srcPos = sourceOffset + count;
        int dstPos = destOffset + count;

        try
        {
            while (count != 0)
            {
                srcPos--;
                dstPos--;
                Mem.storeByte(destBase, dstPos, Mem.fetchByte(sourceBase, srcPos));
                count--;
                if (Processes.interruptPending() && count > 0)
                {
                    Cpu.pushLong(destBase);
                    Cpu.push(destOffset);
                    Cpu.push(count);
                    Cpu.pushLong(sourceBase);
                    Cpu.push(sourceOffset);
                    Cpu.PC = Cpu.savedPC;
                    return;
                }
            }
        }
        catch (Cpu.MesaAbort e)
        {
            e.beginUpdateStack();
            Cpu.pushLong(destBase);
            Cpu.push(destOffset);
            Cpu.push(count);
            Cpu.pushLong(sourceBase);
            Cpu.push(sourceOffset);
            throw e.updateStack();
        }
    };

    // ==================================================================
    // 8.4 Bit Boundary Block Transfers
    // ==================================================================

    private enum Direction { forward, backward }
    private enum PixelType { bit, display }
    private enum SrcFunc { fnull, fcomplement }
    private enum DstFunc { src, srcIfDstLE1, srcIf0, srcIfDstNot0, srcIfNot0, srcIfDst0, pixelXor, srcXorDst }

    private static readonly DstFunc[] DSTFUNC_MAP_COLORBLT =
    {
        DstFunc.src, DstFunc.srcIfDstLE1, DstFunc.srcIf0, DstFunc.srcIfDstNot0,
        DstFunc.srcIfNot0, DstFunc.srcIfDst0, DstFunc.pixelXor, DstFunc.srcXorDst,
    };
    private static readonly DstFunc[] DSTFUNC_MAP_BITBLT =
    {
        DstFunc.src, DstFunc.srcIfDstNot0, DstFunc.srcIfDst0, DstFunc.srcXorDst,
    };

    private interface PixelSource
    {
        int getCurrPixel();
        void moveToNextPixel();
        void moveToNextLine();
        void loadLineCache();
    }

    private interface PixelSink : PixelSource
    {
        void setCurrPixel(int value);
        void flush();
    }

    private delegate int PixelCombiner(int left, int right);

    private static PixelCombiner getCombiner(SrcFunc srcFunc, DstFunc dstFunc)
    {
        PixelCombiner dstOp = dstFunc switch
        {
            DstFunc.src           => (s, d) => s,
            DstFunc.srcIfDstLE1   => (s, d) => (d > 1) ? d : s,
            DstFunc.srcIf0        => (s, d) => (s == 0) ? 0 : d,
            DstFunc.srcIfDstNot0  => (s, d) => (d == 0) ? 0 : s,
            DstFunc.srcIfNot0     => (s, d) => (s == 0) ? d : s,
            DstFunc.srcIfDst0     => (s, d) => (d == 0) ? s : d,
            DstFunc.pixelXor      => (s, d) => ((s < 1 && d < 1) || (s > 0 && d > 0)) ? 0 : 1,
            DstFunc.srcXorDst     => (s, d) => s ^ d,
            _                     => (s, d) => s,
        };

        if (srcFunc == SrcFunc.fnull)
        {
            return dstOp;
        }
        return (s, d) => (s == 0) ? dstOp(1, d) : dstOp(0, d);
    }

    // Pixel source/sink for true bitmaps in memory with line caching.
    private class PixmapForwardPixelSink : PixelSink
    {
        protected readonly int bitsPerPixel;
        private readonly int pixelMask;
        private readonly int pixelsPerLine;
        private readonly int bitsPerLine;
        private readonly int pixelTransferWidth;
        protected readonly int wordsPerLine;
        private readonly bool isBackward;
        private readonly int[] lineCache;

        private int lpLineStart;
        private int pixelOffset;
        private int currPixWordOffs;

        private int pixWord;
        private int pixWordOffs;
        private int pixShift;

        public PixmapForwardPixelSink(
            int lpLineStart, int pixelOffset, int pixelsPerLine,
            int transferWidth, int bitsPerPixel, bool backward)
        {
            if (bitsPerPixel != 1 && bitsPerPixel != 4 && bitsPerPixel != 8)
            {
                throw new ArgumentException("bitsPerPixel not one of 1,4,8");
            }

            int pixelsPerWord = PrincOpsDefs.WORD_BITS / bitsPerPixel;

            this.bitsPerPixel = bitsPerPixel;
            int tmpMask = 1;
            for (int i = 1; i < bitsPerPixel; i++) { tmpMask = (tmpMask << 1) | 1; }
            this.pixelMask = tmpMask;
            this.pixelsPerLine = Math.Abs(pixelsPerLine);
            this.bitsPerLine = this.pixelsPerLine * this.bitsPerPixel;
            this.pixelTransferWidth = transferWidth;
            this.wordsPerLine
                = (((Math.Abs(pixelOffset) + Math.Abs(transferWidth)) * bitsPerPixel) + PrincOpsDefs.WORD_BITS - 1) / PrincOpsDefs.WORD_BITS
                + (((Math.Abs(transferWidth) % 16) != 0) ? 1 : 0);
            this.lineCache = new int[this.wordsPerLine];
            this.isBackward = backward;

            int bitsOffset = pixelOffset * this.bitsPerPixel;
            this.lpLineStart = lpLineStart + (bitsOffset / PrincOpsDefs.WORD_BITS);
            this.pixelOffset = pixelOffset - ((this.lpLineStart - lpLineStart) * pixelsPerWord);
            this.pixShift = PrincOpsDefs.WORD_BITS - (bitsOffset % PrincOpsDefs.WORD_BITS) - this.bitsPerPixel;
        }

        public void moveToNextLine()
        {
            int oldBitsOffset = this.pixelOffset * this.bitsPerPixel;

            if (this.isBackward)
            {
                int nextBitsOffset = oldBitsOffset - this.bitsPerLine;
                int wordsToSubtract = Math.Abs((nextBitsOffset - PrincOpsDefs.WORD_BITS + 1) / PrincOpsDefs.WORD_BITS);
                int newBitsOffset = nextBitsOffset + (wordsToSubtract * PrincOpsDefs.WORD_BITS);

                this.lpLineStart -= wordsToSubtract;
                this.pixelOffset = newBitsOffset / this.bitsPerPixel;
                this.pixShift = PrincOpsDefs.WORD_BITS - newBitsOffset - this.bitsPerPixel;
            }
            else
            {
                int nextBitsOffset = oldBitsOffset + this.bitsPerLine;
                int wordsToAdd = nextBitsOffset / PrincOpsDefs.WORD_BITS;
                int newBitsOffset = nextBitsOffset % PrincOpsDefs.WORD_BITS;

                this.lpLineStart += wordsToAdd;
                this.pixelOffset = newBitsOffset / this.bitsPerPixel;
                this.pixShift = PrincOpsDefs.WORD_BITS - newBitsOffset - this.bitsPerPixel;
            }
        }

        public void loadLineCache()
        {
            int wordsToCache = (((this.pixelOffset + this.pixelTransferWidth) * this.bitsPerPixel) + PrincOpsDefs.WORD_BITS - 1) / PrincOpsDefs.WORD_BITS;
            if (wordsToCache > wordsPerLine)
            {
                Console.Error.WriteLine($"## wordsToCache({wordsToCache}) > wordsPerLine({wordsPerLine}) <<== pixelOffset({pixelOffset}) , pixelsPerLine({pixelsPerLine})");
            }
            for (int i = 0; i < wordsToCache; i++)
            {
                this.lineCache[i] = Mem.readWord(this.lpLineStart + i);
            }
            this.pixWordOffs = 0;
            this.currPixWordOffs = 0;
            this.pixWord = this.lineCache[this.currPixWordOffs];
        }

        public void moveToNextPixel()
        {
            this.pixShift -= this.bitsPerPixel;
            if (this.pixShift < 0)
            {
                this.pixWordOffs++;
                this.pixShift = PrincOpsDefs.WORD_BITS - this.bitsPerPixel;
            }
        }

        public int getCurrPixel()
        {
            if (this.currPixWordOffs != this.pixWordOffs)
            {
                this.flush();
                this.pixWord = this.lineCache[this.pixWordOffs];
                this.currPixWordOffs = this.pixWordOffs;
            }
            return (this.pixWord >> this.pixShift) & this.pixelMask;
        }

        public void setCurrPixel(int newValue)
        {
            if (this.currPixWordOffs != this.pixWordOffs)
            {
                this.flush();
                this.pixWord = this.lineCache[this.pixWordOffs];
                this.currPixWordOffs = this.pixWordOffs;
            }
            this.pixWord
                = (this.pixWord & ~(this.pixelMask << this.pixShift))
                | ((newValue & this.pixelMask) << this.pixShift);
        }

        public virtual void flush()
        {
            Mem.writeWord(this.lpLineStart + this.currPixWordOffs, (ushort)(this.pixWord & 0xFFFF));
        }
    }

    // Readonly variant — overrides flush() to prevent memory writes when used
    // as a pure pixel source.
    private sealed class PixmapForwardPixelSource : PixmapForwardPixelSink
    {
        public PixmapForwardPixelSource(
            int lpLineStart, int pixelOffset, int pixelsPerLine,
            int transferWidth, int bitsPerPixel, bool backward)
            : base(lpLineStart, pixelOffset, pixelsPerLine, transferWidth, bitsPerPixel, backward) { }

        public override void flush() { /* no-op */ }
    }

    // Pixel source for a packed monochrome pattern (one bit per pixel).
    private sealed class MonochromePackedPatternPixelSource : PixelSource
    {
        private readonly int wordsWidth;
        private readonly int pixelsHeight;
        private readonly int xOffset;
        private readonly int lpPatternStart;

        private int yOffset;
        private ushort[]? patternWords;

        private int currWordInLine;
        private int currWordOffset;
        private int currWord;
        private int currBitMask;

        public MonochromePackedPatternPixelSource(
            int argSrcWord, int argSrcBit, int yOffset, int widthMinusOne, int heightMinusOne)
        {
            this.wordsWidth = widthMinusOne + 1;
            this.pixelsHeight = heightMinusOne + 1;
            this.xOffset = argSrcBit;
            this.lpPatternStart = argSrcWord - (yOffset * this.wordsWidth);
            this.yOffset = yOffset;
        }

        public void loadLineCache()
        {
            if (patternWords == null)
            {
                ushort[] words = new ushort[this.wordsWidth * this.pixelsHeight];
                for (int i = 0; i < words.Length; i++)
                {
                    words[i] = Mem.readWord(this.lpPatternStart + i);
                }
                this.patternWords = words;
                this.yOffset--;
                this.moveToNextLine();
            }
        }

        public void moveToNextLine()
        {
            this.yOffset++;
            if (this.yOffset >= this.pixelsHeight) { this.yOffset = 0; }
            this.currWordInLine = 0;
            this.currWordOffset = this.yOffset * this.wordsWidth;
            this.currWord = this.patternWords![this.currWordOffset];
            this.currBitMask = 0x8000 >>> this.xOffset;
        }

        public int getCurrPixel() => ((this.currWord & this.currBitMask) == 0) ? 0 : 1;

        public void moveToNextPixel()
        {
            this.currBitMask >>>= 1;
            if (this.currBitMask == 0)
            {
                this.currWordInLine++;
                if (this.currWordInLine >= this.wordsWidth)
                {
                    this.currWordInLine = 0;
                    this.currWordOffset = this.yOffset * this.wordsWidth;
                }
                else
                {
                    this.currWordOffset++;
                }
                this.currWord = this.patternWords![this.currWordOffset];
                this.currBitMask = 0x8000;
            }
        }
    }

    // Pixel source for an unpacked pattern (one word per pixel).
    private sealed class UnpackedPatternPixelSource : PixelSource
    {
        private readonly int pixelsWidth;
        private readonly int pixelsHeight;
        private readonly int xOffset;
        private readonly int lpPatternStart;
        private readonly bool isMonochrome;

        private int yOffset;
        private int[]? patternPixels;
        private int currX;
        private int baseIdx;

        public UnpackedPatternPixelSource(
            int argSrcWord, int argSrcBit, int yOffset, int widthMinusOne,
            int heightMinusOne, bool monochrome)
        {
            this.pixelsWidth = widthMinusOne + 1;
            this.pixelsHeight = heightMinusOne + 1;
            this.xOffset = argSrcBit % this.pixelsWidth;
            this.lpPatternStart = argSrcWord - (yOffset * this.pixelsWidth);
            this.yOffset = yOffset % this.pixelsHeight;
            this.isMonochrome = monochrome;
        }

        public void loadLineCache()
        {
            if (patternPixels == null)
            {
                int[] pixels = new int[this.pixelsWidth * this.pixelsHeight];
                for (int i = 0; i < pixels.Length; i++)
                {
                    ushort pixelValue = Mem.readWord(this.lpPatternStart + i);
                    if (this.isMonochrome)
                    {
                        pixels[i] = (pixelValue == 0) ? 0 : 1;
                    }
                    else
                    {
                        pixels[i] = pixelValue;
                    }
                }
                this.patternPixels = pixels;
                this.yOffset--;
                this.moveToNextLine();
            }
        }

        public int getCurrPixel() => this.patternPixels![this.baseIdx + this.currX];

        public void moveToNextPixel()
        {
            this.currX++;
            if (this.currX >= this.pixelsWidth) { this.currX = 0; }
        }

        public void moveToNextLine()
        {
            this.yOffset++;
            if (this.yOffset >= this.pixelsHeight) { this.yOffset = 0; }
            this.currX = this.xOffset;
            this.baseIdx = this.yOffset * this.pixelsWidth;
        }
    }

    // Pixel source where every pixel has the same value.
    private sealed class UnipixelPatternSource : PixelSource
    {
        private readonly int pixel;
        public UnipixelPatternSource(int pixel) { this.pixel = pixel; }
        public int getCurrPixel() => this.pixel;
        public void moveToNextPixel() { }
        public void moveToNextLine() { }
        public void loadLineCache() { }
    }

    // Common BITBLT/BITBLTX/COLORBLT parameter holder + execution engine.
    private sealed class BitBltArgs
    {
        private static int lastPendingBitBltId;
        private readonly int id;

        public BitBltArgs() { this.id = ++lastPendingBitBltId; }
        public int getId() => this.id;

        private int dstWord;
        private int dstPixel;
        private short dstPpl;

        private int srcWord;
        private int srcPixel;

        private short srcPpl;
        private int patReserved;
        private bool patUnpacked;
        private int patYOffset;
        private int patWidthMinusOne;
        private int patHeightMinusOne;

        private int width;
        private int height;

        private Direction direction = Direction.forward;
        private PixelType srcType = PixelType.bit;
        private PixelType dstType = PixelType.bit;
        private bool pattern;
        private SrcFunc srcFunc = SrcFunc.fnull;
        private DstFunc dstFunc = DstFunc.src;
        private short flgReserved;

        private readonly ushort[] colorMapping = { 0, 1 };

        private PixelCombiner? combiner;
        private PixelSource? pixelSource;
        private PixelSink? pixelSink;
        private int remainingLines;

        // COLORBLT: load 13 words from *pointer
        public BitBltArgs loadFromColorBltArgs(ushort pointer, string? logMsg)
        {
            this.dstWord = Mem.readMDSDblWord(pointer);
            this.dstPixel = Mem.readMDSWord(pointer, 2);

            this.dstPpl = (short)Mem.readMDSWord(pointer, 3);

            this.srcWord = Mem.readMDSDblWord(pointer, 4);
            this.srcPixel = Mem.readMDSWord(pointer, 6);

            this.srcPpl = (short)Mem.readMDSWord(pointer, 7);
            this.patReserved = (this.srcPpl & 0xFFFF) >>> 12;
            this.patUnpacked = (this.patReserved != 0);
            this.patYOffset = (this.srcPpl >> 8) & 0x000F;
            this.patWidthMinusOne = (this.srcPpl >> 4) & 0x000F;
            this.patHeightMinusOne = this.srcPpl & 0x000F;

            this.width = Mem.readMDSWord(pointer, 8);
            this.height = Mem.readMDSWord(pointer, 9);

            ushort tmp = Mem.readMDSWord(pointer, 10);
            this.direction = ((tmp & 0x8000) == 0) ? Direction.forward : Direction.backward;
            this.srcType = ((tmp & 0x4000) == 0) ? PixelType.bit : PixelType.display;
            this.dstType = ((tmp & 0x2000) == 0) ? PixelType.bit : PixelType.display;
            this.pattern = (tmp & 0x1000) != 0;
            this.srcFunc = ((tmp & 0x0800) == 0) ? SrcFunc.fnull : SrcFunc.fcomplement;
            this.dstFunc = DSTFUNC_MAP_COLORBLT[((tmp & 0x0700) >>> 8)];
            this.flgReserved = (short)(tmp & 0x00FF);

            this.colorMapping[0] = Mem.readMDSWord(pointer, 11);
            this.colorMapping[1] = Mem.readMDSWord(pointer, 12);

            this.setupWorkers();
            return this;
        }

        // BITBLT: load 12 words from *pointer
        public BitBltArgs loadFromBitBltArgs(ushort pointer)
        {
            this.dstWord = Mem.readMDSDblWord(pointer);
            this.dstPixel = Mem.readMDSWord(pointer, 2);

            this.dstPpl = (short)Mem.readMDSWord(pointer, 3);

            this.srcWord = Mem.readMDSDblWord(pointer, 4);
            this.srcPixel = Mem.readMDSWord(pointer, 6);

            this.srcPpl = (short)Mem.readMDSWord(pointer, 7);
            this.patReserved = 1;
            this.patUnpacked = false;
            this.patYOffset = (this.srcPpl >> 8) & 0x000F;
            this.patWidthMinusOne = (this.srcPpl >> 4) & 0x000F;
            this.patHeightMinusOne = this.srcPpl & 0x000F;

            this.width = Mem.readMDSWord(pointer, 8);
            this.height = Mem.readMDSWord(pointer, 9);

            ushort tmp = Mem.readMDSWord(pointer, 10);
            this.direction = ((tmp & 0x8000) == 0) ? Direction.forward : Direction.backward;

            int displayStart = Mem.getDisplayVirtualPage() * PrincOpsDefs.WORDS_PER_PAGE;
            int displayEnd = displayStart + (Mem.getDisplayPageSize() * PrincOpsDefs.WORDS_PER_PAGE);
            this.srcType = (displayStart <= this.srcWord && this.srcWord < displayEnd) ? PixelType.display : PixelType.bit;
            this.dstType = (displayStart <= this.dstWord && this.dstWord < displayEnd) ? PixelType.display : PixelType.bit;

            this.pattern = (tmp & 0x1000) != 0;
            this.srcFunc = ((tmp & 0x0800) == 0) ? SrcFunc.fnull : SrcFunc.fcomplement;
            this.dstFunc = DSTFUNC_MAP_BITBLT[((tmp & 0x0600) >>> 9)];
            this.flgReserved = (short)(tmp & 0x01FF);

            this.colorMapping[0] = 0;
            this.colorMapping[1] = 1;

            // Bug in VP 2.x: PrincOps 4.0 says backward direction requires negative
            // srcPpl/dstPpl, but VP 2.x sets them positive. Workaround: treat as forward.
            if (this.direction == Direction.backward && this.dstPpl > 0 && this.srcPpl > 0)
            {
                this.direction = Direction.forward;
            }

            this.setupWorkers();
            return this;
        }

        // BITBLTX: read 11 words from the stack
        public BitBltArgs loadFromBitBltXArgs()
        {
            ushort tmp = Cpu.pop();
            this.height = Cpu.pop();
            this.width = Cpu.pop();
            this.srcPpl = (short)Cpu.pop();
            this.srcPixel = Cpu.pop();
            this.srcWord = Cpu.popLong();
            this.dstPpl = (short)Cpu.pop();
            this.dstPixel = Cpu.pop();
            this.dstWord = Cpu.popLong();

            this.patReserved = (this.srcPpl & 0xFFFF) >>> 12;
            this.patUnpacked = (this.patReserved != 0);
            this.patYOffset = (this.srcPpl >> 8) & 0x000F;
            this.patWidthMinusOne = (this.srcPpl >> 4) & 0x000F;
            this.patHeightMinusOne = this.srcPpl & 0x000F;

            this.direction = ((tmp & 0x8000) == 0) ? Direction.forward : Direction.backward;
            this.srcType = ((tmp & 0x4000) == 0) ? PixelType.bit : PixelType.display;
            this.dstType = ((tmp & 0x2000) == 0) ? PixelType.bit : PixelType.display;
            this.pattern = (tmp & 0x1000) != 0;
            this.srcFunc = ((tmp & 0x0800) == 0) ? SrcFunc.fnull : SrcFunc.fcomplement;
            this.dstFunc = DSTFUNC_MAP_COLORBLT[((tmp & 0x0700) >>> 8)];
            this.flgReserved = (short)(tmp & 0x00FF);

            this.setupWorkers();
            return this;
        }

        private void setupWorkers()
        {
            bool isBackward = (this.direction == Direction.backward);

            if (this.pattern)
            {
                bool onePixel = (this.patWidthMinusOne == 0 && this.patHeightMinusOne == 0);
                ushort w = Mem.readWord(this.srcWord);
                if (onePixel && this.patUnpacked)
                {
                    if (this.srcType == PixelType.bit || Mem.getDisplayType() == DisplayType.monochrome)
                    {
                        this.pixelSource = new UnipixelPatternSource((w == 0) ? 0 : 1);
                    }
                    else
                    {
                        this.pixelSource = new UnipixelPatternSource(w);
                    }
                }
                else if (onePixel && !this.patUnpacked && w == 0)
                {
                    this.pixelSource = new UnipixelPatternSource(0);
                }
                else if (onePixel && !this.patUnpacked && w == 0xFFFF)
                {
                    this.pixelSource = new UnipixelPatternSource(1);
                }
                else if (this.patUnpacked)
                {
                    this.pixelSource = new UnpackedPatternPixelSource(
                        this.srcWord, this.srcPixel, this.patYOffset,
                        this.patWidthMinusOne, this.patHeightMinusOne,
                        this.srcType == PixelType.bit || Mem.getDisplayType() == DisplayType.monochrome);
                }
                else
                {
                    this.pixelSource = new MonochromePackedPatternPixelSource(
                        this.srcWord, this.srcPixel, this.patYOffset,
                        this.patWidthMinusOne, this.patHeightMinusOne);
                }
            }
            else
            {
                this.pixelSource = new PixmapForwardPixelSource(
                    this.srcWord, this.srcPixel, this.srcPpl, this.width,
                    (this.srcType == PixelType.bit) ? 1 : Mem.getDisplayType().getBitDepth(),
                    isBackward);
            }

            this.pixelSink = new PixmapForwardPixelSink(
                this.dstWord, this.dstPixel, this.dstPpl, this.width,
                (this.dstType == PixelType.bit) ? 1 : Mem.getDisplayType().getBitDepth(),
                isBackward);

            this.combiner = getCombiner(this.srcFunc, this.dstFunc);
            this.remainingLines = this.height;
        }

        public bool isNoOp() => this.width == 0;

        public void execute()
        {
            bool mapSrcPixel = Mem.getDisplayType() != DisplayType.monochrome && this.srcType == PixelType.bit && this.srcFunc == SrcFunc.fnull;
            bool mapDstPixel = Mem.getDisplayType() != DisplayType.monochrome && this.dstType == PixelType.bit;

            while (this.remainingLines > 0)
            {
                this.pixelSource!.loadLineCache();
                this.pixelSink!.loadLineCache();

                for (int i = 0; i < this.width; i++)
                {
                    int srcPixel = mapSrcPixel
                        ? this.colorMapping[this.pixelSource.getCurrPixel()]
                        : this.pixelSource.getCurrPixel();
                    int oldDstPixel = mapDstPixel
                        ? this.colorMapping[this.pixelSink.getCurrPixel()]
                        : this.pixelSink.getCurrPixel();

                    int newDstPixel = this.combiner!(srcPixel, oldDstPixel);
                    this.pixelSink.setCurrPixel(newDstPixel);

                    this.pixelSource.moveToNextPixel();
                    this.pixelSink.moveToNextPixel();
                }
                this.pixelSink.flush();

                if (this.remainingLines-- > 1)
                {
                    this.pixelSource.moveToNextLine();
                    this.pixelSink.moveToNextLine();
                    if (Processes.interruptPending())
                    {
                        Cpu.PC = Cpu.savedPC;
                        return;
                    }
                }
            }

            // done: clear stack restart info and remove from pendingBitBlts
            Cpu.SP = 0;
            Cpu.savedSP = 0;
            pendingBitBlts.Remove(this.id);
        }
    }

    private static readonly Dictionary<int, BitBltArgs> pendingBitBlts = new();

    private static void executeBitBlt(int initialStackDepth, Func<BitBltArgs> initializer)
    {
        if (Cpu.SP == initialStackDepth)
        {
            BitBltArgs bitBltOp = initializer();
            if (bitBltOp.isNoOp()) { return; }

            int id = bitBltOp.getId();
            pendingBitBlts[id] = bitBltOp;
            Cpu.pushLong(id);
            Cpu.savedSP = Cpu.SP;

            bitBltOp.execute();
        }
        else if (Cpu.SP == 2)
        {
            int id = Cpu.popLong();
            Cpu.SP = 2;

            if (pendingBitBlts.TryGetValue(id, out BitBltArgs? bitBltOp))
            {
                bitBltOp.execute();
            }
            else
            {
                Cpu.SP = 0;
                Cpu.savedSP = 0;
            }
        }
        else
        {
            Cpu.stackError();
        }
    }

    public static readonly OpImpl ESC_x2B_BITBLT = () =>
    {
        executeBitBlt(1, () => new BitBltArgs().loadFromBitBltArgs(Cpu.pop()));
    };

    public static readonly OpImpl ESC_xC2_BITBLTX = () =>
    {
        executeBitBlt(11, () => new BitBltArgs().loadFromBitBltXArgs());
    };

    public static readonly OpImpl ESC_xC0_COLORBLT = () =>
    {
        executeBitBlt(1, () => new BitBltArgs().loadFromColorBltArgs(Cpu.pop(), null));
    };

    // TXTBLT — delegated to software (Pilot) implementation.
    public static readonly OpImpl ESC_x2C_TXTBLT = () =>
    {
        Cpu.thrower.signalEscOpcodeTrap(0x2C);
    };

    // ==================================================================
    // 8.4.3.4 Trapezoid Block Transfer
    // ==================================================================

    private sealed class Interpolator
    {
        private int val;
        private readonly int delta;
        private int currInt;

        public Interpolator(int address)
        {
            this.val = Mem.readDblWord(address);
            this.delta = Mem.readDblWord(address + 2);
            this.currInt = this.val >> 16;
        }

        public int step()
        {
            this.val += this.delta;
            this.currInt = this.val >> 16;
            return this.currInt;
        }

        public int get() => this.currInt;
    }

    // Sink for true bitmaps with caching of complete pixel lines.
    private sealed class BitmapForwardPixelSink : PixelSink
    {
        private const int pixelMask = 1;
        private readonly int bitsPerLine;
        private readonly int pixelTransferWidth;
        private readonly int wordsPerLine;
        private readonly int[] lineCache;

        private int lpLineStart;
        private int pixelOffset;
        private int currPixWordOffs;

        private int pixWord;
        private int pixWordOffs;
        private int pixShift;

        public BitmapForwardPixelSink(int lpLineStart, int pixelOffset, int pixelsPerLine, int transferWidth)
        {
            this.bitsPerLine = Math.Abs(pixelsPerLine);
            this.pixelTransferWidth = transferWidth;
            this.wordsPerLine
                = ((Math.Abs(pixelOffset) + Math.Abs(transferWidth)) + PrincOpsDefs.WORD_BITS - 1) / PrincOpsDefs.WORD_BITS
                + (((Math.Abs(transferWidth) % 16) != 0) ? 1 : 0);
            this.lineCache = new int[this.wordsPerLine];

            int bitsOffset = pixelOffset;
            this.lpLineStart = lpLineStart + (bitsOffset / PrincOpsDefs.WORD_BITS);
            this.pixelOffset = pixelOffset - ((this.lpLineStart - lpLineStart) * PrincOpsDefs.WORD_BITS);
            this.pixShift = PrincOpsDefs.WORD_BITS - (bitsOffset % PrincOpsDefs.WORD_BITS) - 1;
        }

        public void moveToNextLine()
        {
            int oldBitsOffset = this.pixelOffset;
            int nextBitsOffset = oldBitsOffset + this.bitsPerLine;
            int wordsToAdd = nextBitsOffset / PrincOpsDefs.WORD_BITS;
            int newBitsOffset = nextBitsOffset % PrincOpsDefs.WORD_BITS;

            this.lpLineStart += wordsToAdd;
            this.pixelOffset = newBitsOffset;
            this.pixShift = PrincOpsDefs.WORD_BITS - newBitsOffset - 1;
        }

        public void loadLineCache()
        {
            int wordsToCache = ((this.pixelOffset + this.pixelTransferWidth) + PrincOpsDefs.WORD_BITS - 1) / PrincOpsDefs.WORD_BITS;
            for (int i = 0; i < wordsToCache; i++)
            {
                this.lineCache[i] = Mem.readWord(this.lpLineStart + i);
            }
            this.pixWordOffs = 0;
            this.currPixWordOffs = 0;
            this.pixWord = this.lineCache[this.currPixWordOffs];
        }

        public void moveToNextPixel()
        {
            this.pixShift -= 1;
            if (this.pixShift < 0)
            {
                this.pixWordOffs++;
                this.pixShift = PrincOpsDefs.WORD_BITS - 1;
            }
        }

        public int getCurrPixel()
        {
            if (this.currPixWordOffs != this.pixWordOffs)
            {
                this.flush();
                this.pixWord = this.lineCache[this.pixWordOffs];
                this.currPixWordOffs = this.pixWordOffs;
            }
            return (this.pixWord >> this.pixShift) & pixelMask;
        }

        public void setCurrPixel(int newValue)
        {
            if (this.currPixWordOffs != this.pixWordOffs)
            {
                this.flush();
                this.pixWord = this.lineCache[this.pixWordOffs];
                this.currPixWordOffs = this.pixWordOffs;
            }
            this.pixWord
                = (this.pixWord & ~(pixelMask << this.pixShift))
                | ((newValue & pixelMask) << this.pixShift);
        }

        public void flush()
        {
            Mem.writeWord(this.lpLineStart + this.currPixWordOffs, (ushort)(this.pixWord & 0xFFFF));
        }
    }

    private static void runSubBITBLT(
        PixelSource pixelSource, PixelSink pixelSink, PixelCombiner combiner,
        int width, int remainingLines)
    {
        if (remainingLines <= 0 || pixelSource == null || pixelSink == null) { return; }

        while (remainingLines > 0)
        {
            pixelSource.loadLineCache();
            pixelSink.loadLineCache();

            for (int i = 0; i < width; i++)
            {
                int srcPixel = pixelSource.getCurrPixel();
                int oldDstPixel = pixelSink.getCurrPixel();
                int newDstPixel = combiner(srcPixel, oldDstPixel);
                pixelSink.setCurrPixel(newDstPixel);
                pixelSource.moveToNextPixel();
                pixelSink.moveToNextPixel();
            }
            pixelSink.flush();

            if (remainingLines-- > 1)
            {
                pixelSource.moveToNextLine();
                pixelSink.moveToNextLine();
            }
        }
    }

    public static readonly OpImpl ESC_xA4_TRAPZBLT = () =>
    {
        int tbPtr = Cpu.popLong();
        int dstWordPtr     = Mem.readDblWord(tbPtr + 0);
        // dstPixel (tbPtr + 2) — unused
        int dstBpl         = Mem.readWord(tbPtr + 3);
        int srcWordPtr     = Mem.readDblWord(tbPtr + 4);
        int srcPixel       = Mem.readWord(tbPtr + 6);
        int misc           = Mem.readWord(tbPtr + 7);
        Interpolator xMin  = new(tbPtr + 8);
        Interpolator xMax  = new(tbPtr + 12);
        int height         = Mem.readWord(tbPtr + 16);

        if (height == 0) { return; }

        SrcFunc srcFunc        = ((misc & 0x8000) == 0) ? SrcFunc.fnull : SrcFunc.fcomplement;
        DstFunc dstFunc        = DSTFUNC_MAP_BITBLT[((misc & 0x6000) >>> 13)];
        int     yOffset        = (misc & 0x0F00) >> 8;
        int     widthMinusOne  = (misc & 0x00F0) >> 4;
        int     heightMinusOne = misc & 0x000F;

        int wordsPerDstLine = dstBpl / 16;

        int currXMin = xMin.get();
        int currXMax = xMax.get();
        int currWidth = Math.Max(currXMax - currXMin, 1);
        int currHeight = 1;

        int dstBitAdjust = srcPixel + 16 - (currXMin % 16);
        int gray_BASE = srcWordPtr - yOffset;
        int gray_LENGTH = heightMinusOne + 1;

        int dstWord = currXMin / 16;
        int dstBit = currXMin % 16;

        PixelCombiner combiner = getCombiner(srcFunc, dstFunc);

        PixelSource pixelSource = new MonochromePackedPatternPixelSource(
            gray_BASE + yOffset, (dstBit + dstBitAdjust) % 16, yOffset, widthMinusOne, heightMinusOne);
        PixelSink pixelSink = new BitmapForwardPixelSink(dstWordPtr + dstWord, dstBit, dstBpl, currWidth);
        for (int i = 1; i < height; i++)
        {
            dstWordPtr += wordsPerDstLine;
            yOffset = (yOffset + 1) % gray_LENGTH;

            int newXMin = xMin.step();
            int newXMax = xMax.step();
            if (newXMin == currXMin && newXMax == currXMax)
            {
                currHeight++;
                continue;
            }

            runSubBITBLT(pixelSource, pixelSink, combiner, currWidth, currHeight);

            currXMin = newXMin;
            currXMax = newXMax;
            currWidth = Math.Max(currXMax - currXMin, 1);

            dstWord = currXMin / 16;
            dstBit = currXMin % 16;

            pixelSource = new MonochromePackedPatternPixelSource(
                gray_BASE + yOffset, (dstBit + dstBitAdjust) % 16, yOffset, widthMinusOne, heightMinusOne);
            pixelSink = new BitmapForwardPixelSink(dstWordPtr + dstWord, dstBit, dstBpl, currWidth);
            currHeight = 1;
        }
        runSubBITBLT(pixelSource, pixelSink, combiner, currWidth, currHeight);
    };

    // ==================================================================
    // Registration
    // ==================================================================

    public static void RegisterAll()
    {
        // Word boundary block transfers
        Opcodes.Install(0xF3, "BLT",   OPC_xF3_BLT);
        Opcodes.Install(0xF4, "BLTL",  OPC_xF4_BLTL);
        Opcodes.Install(0xF5, "BLTC",  OPC_xF5_BLTC);
        Opcodes.Install(0xF6, "BLTCL", OPC_xF6_BLTCL);
        Opcodes.InstallEsc(0x27, "BLTLR", ESC_x27_BLTLR);
        Opcodes.InstallEsc(0x2A, "CKSUM", ESC_x2A_CKSUM);

        // Block comparisons
        Opcodes.InstallEsc(0x28, "BLEL",  ESC_x28_BLEL);
        Opcodes.InstallEsc(0x29, "BLECL", ESC_x29_BLECL);

        // Byte boundary block transfers
        Opcodes.InstallEsc(0x2D, "BYTBLT",  ESC_x2D_BYTBLT);
        Opcodes.InstallEsc(0x2E, "BYTBLTR", ESC_x2E_BYTBLTR);

        // Bit boundary block transfers
        Opcodes.InstallEsc(0x2B, "BITBLT",  ESC_x2B_BITBLT);
        Opcodes.InstallEsc(0xC2, "BITBLTX", ESC_xC2_BITBLTX);
        Opcodes.InstallEsc(0xC0, "COLORBLT", ESC_xC0_COLORBLT);
        Opcodes.InstallEsc(0x2C, "TXTBLT",  ESC_x2C_TXTBLT);
        Opcodes.InstallEsc(0xA4, "TRAPZBLT", ESC_xA4_TRAPZBLT);
    }
}
