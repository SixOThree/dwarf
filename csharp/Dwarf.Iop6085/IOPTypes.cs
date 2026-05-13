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

namespace Dwarf.Iop6085;

// Common data structures allocated statically in the IO region (FCBs/DCBs)
// or dynamically (IOCBs), plus shared handler-ID constants.
//
// **Port note**: `IOPTypes` extends `IORegion` so the nested struct types
// have access to the protected static `mkWord` / `mkByteSwappedWord` /
// `mkField` factories. The struct types themselves extend `IORegion.IOStruct`.
public abstract class IOPTypes : IORegion
{
    /*
     * Handler IDs
     */
    public const int HandlerID_beep = 1;
    public const int HandlerID_disk = 2;
    public const int HandlerID_display = 3;
    public const int HandlerID_ethernet = 4;
    public const int HandlerID_floppy = 5;
    public const int HandlerID_keyboardAndMouse = 6;
    public const int HandlerID_maintPanel = 7;

    public const int HandlerID_processor = 16;

    public const int HandlerID_tty = 17;
    public const int HandlerID_rs232c = 18;

    public const int HandlerID_parallelPort = 97;

    public const int HandlerID_last = 127;

    /*
     * Technical Intel structures
     */

    public sealed class IPCS
    {
        public readonly Word ip;
        public readonly Word cs;

        public IPCS(string name, string locationName)
        {
            this.ip = mkByteSwappedWord(name, locationName + ".ip");
            this.cs = mkByteSwappedWord(name, locationName + ".cs");
        }
    }

    public sealed class SegmentRec
    {
        public readonly Word ioRegionSegment;
        public readonly Word stackSegment;

        public SegmentRec(string name, string locationName)
        {
            this.ioRegionSegment = mkByteSwappedWord(name, locationName + ".ioRegionSegment");
            this.stackSegment = mkByteSwappedWord(name, locationName + ".stackSegment");
        }
    }

    public sealed class SPSS
    {
        public readonly Word sp;
        public readonly Word ss;

        public SPSS(string name, string locationName)
        {
            this.sp = mkByteSwappedWord(name, locationName + ".sp");
            this.ss = mkByteSwappedWord(name, locationName + ".ss");
        }
    }

    /*
     * IOP structures
     */

    public sealed class AlternateOpieAddress
    {
        public readonly Word low;
        public readonly Word high;

        public AlternateOpieAddress(string name, string locationName)
        {
            this.low = mkByteSwappedWord(name, locationName + ".low");
            this.high = mkByteSwappedWord(name, locationName + ".high");
        }
    }

    public sealed class ByteSwappedLinkPtr : IOStruct
    {
        private readonly Word linkPtr;
        public readonly Field nonNilPtr;
        public readonly Field pointer;
        public readonly Field pointerByte;

        public ByteSwappedLinkPtr(string name, string locationName)
            : base((IOStruct?)null, name)
        {
            this.linkPtr = IORegion.mkByteSwappedWord(name, locationName);
            this.nonNilPtr = mkField("nonNilPtr", this.linkPtr, 0x8000);
            this.pointer = mkField("pointer", this.linkPtr, 0x7FFE);
            this.pointerByte = mkField("pointerByte", this.linkPtr, 0x0001);
        }

        public ByteSwappedLinkPtr(IOStruct embeddingParent, string locationName)
            : base(embeddingParent, locationName)
        {
            this.linkPtr = mkByteSwappedWord(locationName);
            this.nonNilPtr = mkField("nonNilPtr", this.linkPtr, 0x8000);
            this.pointer = mkField("pointer", this.linkPtr, 0x7FFE);
            this.pointerByte = mkField("pointerByte", this.linkPtr, 0x0001);

            this.endStruct();
        }
    }

    public sealed class ByteSwappedPointer : IOStruct
    {
        public readonly Word ptr;
        public readonly Field pointer;
        public readonly Field pointerByte;

        public ByteSwappedPointer(string name, string locationName)
            : base((IOStruct?)null, name)
        {
            this.ptr = IORegion.mkByteSwappedWord(name, locationName);
            this.pointer = mkField("pointer", this.ptr, 0xFFFE);
            this.pointerByte = mkField("pointerByte", this.ptr, 0x0001);
        }

        public ByteSwappedPointer(IOStruct embeddingParent, string locationName)
            : base(embeddingParent, locationName)
        {
            this.ptr = mkByteSwappedWord("ptr");
            this.pointer = mkField("pointer", this.ptr, 0xFFFE);
            this.pointerByte = mkField("pointerByte", this.ptr, 0x0001);

            this.endStruct();
        }
    }

    public sealed class ClientCondition : IOStruct
    {
        public readonly Word handlerIDAndConditionRelMaskPtr;
        public readonly Field handlerID;
        public readonly Field conditionRelMaskPtr_maskWordOffset;
        public readonly Field conditionRelMaskPtr_maskPtrByte;
        public readonly ByteSwappedLinkPtr conditionPtr;
        public readonly Word maskValue;

        public ClientCondition(string name, string locationName)
            : base((IOStruct?)null, name)
        {
            this.handlerIDAndConditionRelMaskPtr = IORegion.mkWord(name, locationName + ".handlerIDAndConditionRelMaskPtr");
            this.handlerID = mkField("handlerID", this.handlerIDAndConditionRelMaskPtr, 0xFF00);
            this.conditionRelMaskPtr_maskWordOffset = mkField("conditionRelMaskPtr.maskWordOffset", this.handlerIDAndConditionRelMaskPtr, 0x00FE);
            this.conditionRelMaskPtr_maskPtrByte = mkField("conditionRelMaskPtr.maskPtrByte", this.handlerIDAndConditionRelMaskPtr, 0x0001);
            this.conditionPtr = new ByteSwappedLinkPtr(name, locationName + ".conditionPtr");
            this.maskValue = IORegion.mkWord(name, locationName + ".maskValue");
        }

        public ClientCondition(IOStruct embeddingParent, string locationName)
            : base(embeddingParent, locationName)
        {
            this.handlerIDAndConditionRelMaskPtr = mkWord("handlerIDAndConditionRelMaskPtr");
            this.handlerID = mkField("handlerID", this.handlerIDAndConditionRelMaskPtr, 0xFF00);
            this.conditionRelMaskPtr_maskWordOffset = mkField("conditionRelMaskPtr.maskWordOffset", this.handlerIDAndConditionRelMaskPtr, 0x00FE);
            this.conditionRelMaskPtr_maskPtrByte = mkField("conditionRelMaskPtr.maskPtrByte", this.handlerIDAndConditionRelMaskPtr, 0x0001);
            this.conditionPtr = new ByteSwappedLinkPtr(this, "conditionPtr");
            this.maskValue = mkWord("maskValue");

            this.endStruct();
        }
    }

    public sealed class IOPCondition
    {
        public readonly ByteSwappedLinkPtr tcbLinkPtr;

        public IOPCondition(string name, string locationName)
        {
            this.tcbLinkPtr = new ByteSwappedLinkPtr(name, locationName + ".tcbLinkPtr");
        }
    }

    public sealed class NotifyMask
    {
        public readonly Word byteMaskAndOffset;
        public readonly Field byteMask;
        public readonly Field byteOffset;

        public NotifyMask(string name, string locationName)
        {
            this.byteMaskAndOffset = mkWord(name, locationName + ".byteMaskAndOffset");
            this.byteMask = mkField("byteMask", this.byteMaskAndOffset, 0xFF00);
            this.byteOffset = mkField("byteOffset", this.byteMaskAndOffset, 0x00FF);
        }
    }

    public enum OpieAddressType
    {
        nil = 0,
        extendedBus = 0x10,             // 020 octal
        extendedBusPage = 0x30,         // 060 octal
        iopLogical = 0x50,              // 0120 octal
        iopIORegionRelative = 0x51,     // 0121 octal
        pcLogical = 0x90,               // 0220 octal
        virtualWord = 0xE0,             // 0340 octal
        virtualFirst64KRelative = 0xE1, // 0341 octal
        virtualPage = 0xF0,             // 0360 octal
        last = 0xFF,                    // 0377 octal
    }

    public sealed class OpieAddress : IOStruct
    {
        public readonly ByteSwappedPointer a15ToA0;
        private readonly Word a23ToA16AndType;
        public readonly Field a23ToA16;
        public readonly Field type;

        public OpieAddress(string name, string locationName)
            : base((IOStruct?)null, name)
        {
            this.a15ToA0 = new ByteSwappedPointer(name, locationName + ".a15ToA0");
            this.a23ToA16AndType = IORegion.mkWord(name, locationName + ".a23ToA16AndType");
            this.a23ToA16 = mkField("a23ToA16", this.a23ToA16AndType, 0xFF00);
            this.type = mkField("type", this.a23ToA16AndType, 0x00FF);
        }

        public OpieAddress(IOStruct embeddingParent, string locationName)
            : base(embeddingParent, locationName)
        {
            this.a15ToA0 = new ByteSwappedPointer(this, "a15ToA0");
            this.a23ToA16AndType = mkWord("a23ToA16AndType");
            this.a23ToA16 = mkField("a23ToA16", this.a23ToA16AndType, 0xFF00);
            this.type = mkField("type", this.a23ToA16AndType, 0x00FF);

            this.endStruct();
        }

        public void fromLP(int lp)
        {
            this.a15ToA0.ptr.set((ushort)(lp & 0xFFFF));
            if ((lp & unchecked((int)0xFFFF0000)) == 0)
            {
                this.a23ToA16.set(0);
                this.type.set((int)OpieAddressType.virtualFirst64KRelative);
            }
            else
            {
                this.a23ToA16.set((ushort)(((uint)lp & 0xFFFF0000u) >> 16));
                this.type.set((int)OpieAddressType.virtualWord);
            }
        }

        public int toLP()
        {
            int lp = this.a15ToA0.ptr.get() & 0xFFFF;
            if (this.type.get() == (int)OpieAddressType.virtualFirst64KRelative)
            {
                return lp;
            }
            if (this.type.get() == (int)OpieAddressType.virtualWord)
            {
                lp |= this.a23ToA16.get() << 16;
                return lp;
            }
            if (this.type.get() == (int)OpieAddressType.virtualPage)
            {
                return (this.a15ToA0.ptr.get() & 0xFFFF) * PrincOpsDefs.WORDS_PER_PAGE;
            }
            return 0;
        }
    }

    public sealed class QueueBlock
    {
        public readonly OpieAddress queueHead;
        public readonly OpieAddress queueTail;
        public readonly OpieAddress queueNext;

        public QueueBlock(string name, string locationName)
        {
            this.queueHead = new OpieAddress(name, locationName + ".queueHead");
            this.queueTail = new OpieAddress(name, locationName + ".queueTail");
            this.queueNext = new OpieAddress(name, locationName + ".queueNext");
        }
    }

    public sealed class QueueEntry
    {
        private readonly Word queueTypeAndNextHandlerID;
        public readonly Field queueType;
        public readonly Field nextHandlerID;
        public readonly ByteSwappedLinkPtr nextTCBLinkPtr;

        public QueueEntry(string name, string locationName)
        {
            this.queueTypeAndNextHandlerID = mkWord(name, locationName + ".queueTypeAndNextHandlerID");
            this.queueType = mkField("queueType", this.queueTypeAndNextHandlerID, 0xFF00);
            this.nextHandlerID = mkField("nextHandlerID", this.queueTypeAndNextHandlerID, 0x00FF);
            this.nextTCBLinkPtr = new ByteSwappedLinkPtr(name, locationName + ".nextTCBLinkPtr");
        }
    }

    public sealed class TaskContextBlock
    {
        public readonly QueueEntry taskQueue;
        public readonly ByteSwappedPointer taskCondition;
        public readonly ByteSwappedPointer taskICPtr;
        public readonly Word taskSP;
        public readonly SPSS returnSPSS;
        private readonly Word prevAndPresentStateAndTaskHandlerID;
        public readonly Field prevState;
        public readonly Field presentState;
        public readonly Field taskHandlerID;
        public readonly Word timerValue;

        public TaskContextBlock(string name, string locationName)
        {
            this.taskQueue = new QueueEntry(name, locationName + ".taskQueue");
            this.taskCondition = new ByteSwappedPointer(name, locationName + ".taskCondition");
            this.taskICPtr = new ByteSwappedPointer(name, locationName + ".taskICPtr");
            this.returnSPSS = new SPSS(name, locationName + ".returnSPSS");
            this.taskSP = mkByteSwappedWord(name, locationName + ".taskSP");
            this.prevAndPresentStateAndTaskHandlerID = mkWord(name, locationName + ".prevAndPresentStateAndTaskHandlerID");
            this.prevState = mkField("prevState", this.prevAndPresentStateAndTaskHandlerID, 0xF000);
            this.presentState = mkField("presentState", this.prevAndPresentStateAndTaskHandlerID, 0x0F00);
            this.taskHandlerID = mkField("taskHandlerID", this.prevAndPresentStateAndTaskHandlerID, 0x00FF);
            this.timerValue = mkByteSwappedWord(name, locationName + ".timerValue");
        }
    }

    public sealed class IORTable
    {
        public readonly Word mesaHasLock;
        public readonly Word iopRequestsLock;
        public readonly SegmentRec[] segments = new SegmentRec[HandlerID_last + 1];

        public IORTable()
        {
            const string name = "IORTable";
            this.mesaHasLock = mkWord(name, "mesaHasLock");
            this.iopRequestsLock = mkWord(name, "iopRequestsLock");
            for (int i = 0; i < segments.Length; i++)
            {
                this.segments[i] = new SegmentRec(name, "segments[" + i + "]");
            }
        }
    }
}
