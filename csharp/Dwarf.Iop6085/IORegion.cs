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

// Infrastructure for locations in the Input/Output Region (IOR) and related
// structures of a 6085 machine.
//
// Two kinds of structures live in the IOR: fix-positioned structures
// allocated once at module-init time (FCBs/DCBs) and relocatable structures
// mapped to a variable location allocated by the face-client or the head for
// a single I/O operation (IOCBs, buffers).
//
// **Port note**: Java's `IORegion extends Mem` — the C# port doesn't inherit
// (`Mem` is a static class) but accesses `Mem.mem` directly via the public
// static field. The `protected static` factory methods stay `protected
// static` so subclasses (IOPTypes, the handlers) can call them via
// inheritance.
//
// **Byte order**: Mesa is big-endian; the 6085 IOP is little-endian (Intel
// 8088 family). Word locations come in two flavors — plain (Mesa-order)
// and "ByteSwapped" (IOP-order). The handlers pick the right kind per
// PrincOps documentation.
public abstract class IORegion
{
    private const int FIRST_IOR_PAGE = 32; // start page for the IO region
    private const int MAX_IOR_PAGES = 32;  // => 16 KByte ~ enough space??
    private const int SEGMENT_GRANULARITY_WORDS = 8; // words per segment unit ~ 16 bytes

    // first real (word) address in the IO region
    public const int IOR_BASE = FIRST_IOR_PAGE * PrincOpsDefs.WORDS_PER_PAGE;

    // real address-offset to IOR_BASE for the next free location in the IO region
    private static int currIorMesaAddressOffset = 0;

    // identified locations allocated in the IO region
    private static readonly IORAddress?[] iorAddresses = new IORAddress?[MAX_IOR_PAGES * PrincOpsDefs.WORDS_PER_PAGE];

    /*
     * Public interfaces
     */

    // dumpable IO region locations
    public interface IORDumpable
    {
        void dump(string prefix); // to stdout
    }

    // Location in IO region.
    public interface IORAddress : IORDumpable
    {
        string getName();
        int getRealAddress();

        // Default implementations — C# 8+ supports default interface methods.
        ushort getIOPSegment() => (ushort)((this.getRealAddress() / SEGMENT_GRANULARITY_WORDS) & 0xFFFF);
        ushort getIOPSegmentOffset() => (ushort)(this.getRealAddress() % SEGMENT_GRANULARITY_WORDS);
        int getWordLength() => 1;
        List<Field>? getFields() => null;

        // Default dump matches Java upstream — concrete IORAddress impls (FCB
        // classes inside handlers) don't need to override it.
        void IORDumpable.dump(string prefix) => Console.WriteLine("-- dump of IORAddress not supported --");
    }

    // 16-bit word location in IO region.
    public interface Word : IORAddress
    {
        ushort get();
        void set(ushort value);
        void addField(Field f);
    }

    // 32-bit value (double word) location in IO region.
    public interface DblWord : IORAddress
    {
        int get();
        void set(int value);
        new int getWordLength() => 2;
    }

    // Subfield of a 16-bit word location with numeric semantics.
    public interface Field : DblWord { }

    // Subfield of a 16-bit word location with boolean semantics.
    public interface BoolField : Field
    {
        bool @is() => (this.get() != 0);
        void set(bool b) => this.set(b ? 1 : 0);
    }

    // 16-bit IOP boolean (the whole word represents a boolean: 0xFFFF=true, 0=false).
    public interface IOPBoolean : IORAddress
    {
        bool get();
        void set(bool value);
    }

    /*
     * Static helpers
     */

    // Get the location element for a real address in the IO region.
    // Returns a dummy "OUTSIDE-IOR-REGION" or "UNDEFINED-IOR-REGION" location
    // when nothing is defined there.
    public static IORAddress resolveRealAddress(int address)
    {
        int a = address - IOR_BASE;
        if (a < 0 || a >= iorAddresses.Length)
        {
            return new IORBase("OUTSIDE-IOR-REGION", new IOLocation(a));
        }
        IORAddress? iorLocation = iorAddresses[a];
        if (iorLocation == null)
        {
            iorLocation = new IORBase("UNDEFINED-IOR-REGION", new IOLocation(a));
            iorAddresses[a] = iorLocation;
        }
        return iorLocation;
    }

    public static void dumpIORegionStructure(int iorVMBaseAddress)
    {
        Console.Write("## Begin IORegion\n");
        for (int a = 0; a < currIorMesaAddressOffset; a++)
        {
            IORAddress? iorLocation = iorAddresses[a];
            if (iorLocation != null)
            {
                int expAddr = IOR_BASE + a;
                int locAddr = iorLocation.getRealAddress();
                if (expAddr != locAddr)
                {
                    Console.Write($"## expAddr[0x{expAddr:X6}] != locAddress[0x{locAddr:X6}] [ virtual: 0x{iorVMBaseAddress + a:X6} ] => 0x{Mem.mem[locAddr]:X4} -- {iorLocation.getName()}\n");
                }
                else
                {
                    Console.Write($"real: 0x{locAddr:X6} [ virtual: 0x{iorVMBaseAddress + a:X6} ] => 0x{Mem.mem[locAddr]:X4} -- {iorLocation.getName()}\n");
                }
            }
        }
        Console.Write("## End IORegion\n");
    }

    // Reserve the next word location for the given handler and location names.
    internal static Word mkWord(string handlerName, string locationName) =>
        innerMkWord(handlerName, locationName, false);

    // Reserve the next byte-swapped word location.
    internal static Word mkByteSwappedWord(string handlerName, string locationName) =>
        innerMkWord(handlerName, locationName, true);

    // Wrap a word with a byte-swap accessor.
    internal static Word mkSwapped(Word w) => new SwappedWord(w);

    // Reserve the next double-word location.
    internal static DblWord mkDblWord(string handlerName, string locationName) =>
        innerMkDblWord(handlerName, locationName, false);

    // Reserve the next byte-swapped double-word location.
    internal static DblWord mkByteSwappedDblWord(string handlerName, string locationName) =>
        innerMkDblWord(handlerName, locationName, true);

    // Define a numeric subfield inside a word location defined by the bit mask.
    internal static Field mkField(string fieldName, Word w, int bits) =>
        new IORField(fieldName, w, bits, isBool: false);

    // Define a boolean subfield inside a word location defined by the bit mask.
    internal static BoolField mkBoolField(string fieldName, Word w, int bits) =>
        new IORField(fieldName, w, bits, isBool: true);

    // Build a double-word from two consecutive single words.
    internal static DblWord mkCompoundDblWord(Word w0, Word w1) =>
        new CompoundDblWord(w0, w1);

    // Reserve the next location for a 16-bit boolean.
    internal static IOPBoolean mkIOPBoolean(string handlerName, string locationName)
    {
        if (currIorMesaAddressOffset >= iorAddresses.Length)
        {
            throw new ArgumentException("No more space in IO-Region");
        }
        iIOLocation location = new IOLocation(currIorMesaAddressOffset);
        IOPBoolean b = new WordBoolean(handlerName + ":" + locationName, location);
        iorAddresses[currIorMesaAddressOffset] = b;
        currIorMesaAddressOffset += 1;
        return b;
    }

    // Define a boolean subfield on the upper or lower byte of a word location.
    internal static IOPBoolean mkIOPShortBoolean(string fieldName, Word w, bool hiByte) =>
        new ByteBoolean(fieldName, w, hiByte);

    // Move the address of the next free location to the next segment
    // boundary (16 bytes or 8 words). Must be called when starting a new
    // independently-addressable data structure (FCB, DCB, ...).
    internal static int syncToSegment()
    {
        int segmentLimitOffset = currIorMesaAddressOffset % SEGMENT_GRANULARITY_WORDS;
        if (segmentLimitOffset != 0)
        {
            currIorMesaAddressOffset += SEGMENT_GRANULARITY_WORDS - segmentLimitOffset;
        }
        return currIorMesaAddressOffset;
    }

    // Swap the bytes of a word.
    public static int byteSwap(ushort v) =>
        ((v >> 8) & 0xFF) | ((v & 0xFF) << 8);

    private static void dmp(string pattern, params object[] args) =>
        Console.Write(string.Format(pattern, args));

    private static IOStruct? lastIoStruct = null;

    public static IORAddress? resolveLastKnownStructure(int realAddr) =>
        lastIoStruct?.resolveRealAddress(realAddr);

    /*
     * IOStruct — base class for record structures and substructures
     */

    // Base class for IO record structures and substructures embedded in
    // records. Instances describe a contiguous memory area in the IORegion,
    // either at a fixed or relocatable position.
    //
    // The `mk*`-methods mirror the static top-level factories but allocate
    // inside the relocatable IO record rather than at the next free
    // absolute position.
    public abstract class IOStruct : IORDumpable
    {
        private readonly IOStruct? embeddingParent;
        private readonly iIOLocation baseLocation;
        private readonly string structName;
        private int currOffset = 0;

        private readonly List<IORDumpable> children = new();
        private int realFirstAddr = -1;
        private int realLastAddr = -1;

        // Constructor for top-level records.
        protected IOStruct(int @base, string name)
        {
            this.baseLocation = new IOBaseLocation(@base);
            this.structName = name;
            this.embeddingParent = null;
        }

        // Constructor for embedded substructures.
        protected IOStruct(IOStruct? embeddingParent, string name)
        {
            this.baseLocation = (embeddingParent == null)
                ? new IOLocation(currIorMesaAddressOffset)
                : embeddingParent.getBaseLocation(this);
            this.structName = (embeddingParent == null)
                ? name
                : embeddingParent.structName + "." + name;
            this.embeddingParent = embeddingParent;
        }

        public void dump(string prefix)
        {
            dmp("{0}>> {1} [ at real address: 0x{2:X6} ]\n", prefix, this.structName, this.getRealAddress());
            string childPrefix = prefix + "   ";
            foreach (IORDumpable child in this.children)
            {
                child.dump(childPrefix);
            }
            dmp("{0}<< {1}\n", prefix, this.structName);
        }

        private iIOLocation getBaseLocation(IORDumpable child)
        {
            this.children.Add(child);
            return new IOLocation(this.baseLocation, this.currOffset);
        }

        protected internal int getRealAddress() => this.baseLocation.realAddress();

        // Close sub-structure — **must** be called at end of the structure
        // definition so the offsets of items following the substructure in
        // the parent have correct offsets.
        protected void endStruct()
        {
            this.embeddingParent?.addStructLength(this.currOffset);
        }

        private void addStructLength(int structLength)
        {
            this.currOffset += structLength;
        }

        public void rebaseToVirtualAddress(int newBase)
        {
            if (this.baseLocation is IOBaseLocation)
            {
                this.rebaseToRealAddress(Mem.getRealAddress(newBase, false)); // TODO: better writable: true ??
            }
            else
            {
                Console.WriteLine("** Warning: NOT relocating absolute positioned IOStruct");
            }
        }

        public void rebaseToRealAddress(int newBase)
        {
            if (this.baseLocation is IOBaseLocation iobl)
            {
                iobl.rebase(newBase);
                if (this.structName.StartsWith("Floppy", StringComparison.Ordinal))
                {
                    lastIoStruct = this;
                }
                this.realFirstAddr = newBase;
                this.realLastAddr = newBase + currOffset - 1;
            }
            else
            {
                lastIoStruct = null;
                Console.WriteLine("** Warning: NOT relocating absolute positioned IOStruct");
            }
        }

        internal IORAddress? resolveRealAddress(int addr) => resolveRealAddress(addr, true);

        private IORAddress? resolveRealAddress(int addr, bool checkRange)
        {
            if (checkRange && (addr < this.realFirstAddr || addr > this.realLastAddr)) { return null; }
            foreach (IORDumpable c in this.children)
            {
                if (c is IORAddress iora)
                {
                    int ioraAddr = iora.getRealAddress();
                    if (ioraAddr >= addr && ioraAddr < (ioraAddr + iora.getWordLength()))
                    {
                        return iora;
                    }
                }
                else if (c is IOStruct ios)
                {
                    IORAddress? sub = ios.resolveRealAddress(addr, false);
                    if (sub != null) { return sub; }
                }
            }
            return null;
        }

        protected Word mkWord(string memberName)
        {
            Word w = new IORWord(
                this.structName + "." + memberName,
                new IOLocation(this.baseLocation, this.currOffset));
            this.children.Add(w);
            this.currOffset++;
            return w;
        }

        protected Word mkByteSwappedWord(string memberName)
        {
            Word w = new IORByteSwappedWord(
                this.structName + "." + memberName,
                new IOLocation(this.baseLocation, this.currOffset));
            this.children.Add(w);
            this.currOffset++;
            return w;
        }

        protected DblWord mkDblWord(string memberName)
        {
            DblWord w = new IORDblWord(
                this.structName + "." + memberName,
                new IOLocation(this.baseLocation, this.currOffset));
            this.children.Add(w);
            this.currOffset += 2;
            return w;
        }

        protected DblWord mkByteSwappedDblWord(string memberName)
        {
            DblWord w = new IORByteSwappedDblWord(
                this.structName + "." + memberName,
                new IOLocation(this.baseLocation, this.currOffset));
            this.children.Add(w);
            this.currOffset += 2;
            return w;
        }

        protected Field mkField(string fieldName, Word w, int bits)
        {
            Field f = new IORField(fieldName, w, bits, isBool: false);
            this.children.Add(f);
            return f;
        }

        protected BoolField mkBoolField(string fieldName, Word w, int bits)
        {
            BoolField f = new IORField(fieldName, w, bits, isBool: true);
            this.children.Add(f);
            return f;
        }

        protected DblWord mkCompoundDblWord(Word w0, Word w1)
        {
            DblWord dw = new CompoundDblWord(w0, w1);
            this.children.Add(dw);
            return dw;
        }

        protected IOPBoolean mkIOPBoolean(string memberName)
        {
            IOPBoolean b = new WordBoolean(
                this.structName + "." + memberName,
                new IOLocation(this.baseLocation, this.currOffset));
            this.children.Add(b);
            this.currOffset++;
            return b;
        }

        protected IOPBoolean mkIOPShortBoolean(string fieldName, Word w, bool hiByte)
        {
            IOPBoolean b = new ByteBoolean(fieldName, w, hiByte);
            this.children.Add(b);
            return b;
        }
    }

    /*
     * internal / implementation items
     */

    private static Word innerMkWord(string handlerName, string locationName, bool swapped)
    {
        if (currIorMesaAddressOffset >= iorAddresses.Length)
        {
            throw new ArgumentException("No more space in IO-Region");
        }
        iIOLocation location = new IOLocation(currIorMesaAddressOffset);
        Word a = swapped
            ? new IORByteSwappedWord(handlerName + ":" + locationName, location)
            : new IORWord(handlerName + ":" + locationName, location);
        iorAddresses[currIorMesaAddressOffset] = a;
        currIorMesaAddressOffset += 1;
        return a;
    }

    private static DblWord innerMkDblWord(string handlerName, string locationName, bool swapped)
    {
        if ((currIorMesaAddressOffset + 1) >= iorAddresses.Length)
        {
            throw new ArgumentException("No more space in IO-Region");
        }
        iIOLocation location = new IOLocation(currIorMesaAddressOffset);
        // Java upstream apparently inverts the `swapped` flag here vs in
        // `innerMkWord` — the unswapped factory call yields an
        // `IORByteSwappedDblWord` and vice versa. Preserved verbatim for
        // diff fidelity.
        DblWord a = swapped
            ? new IORDblWord(handlerName + ":" + locationName + "[low]", location)
            : new IORByteSwappedDblWord(handlerName + ":" + locationName + "[low]", location);
        IORBase h = new IORBase(handlerName + ":" + locationName + "[high]", new IOLocation(currIorMesaAddressOffset + 1));
        iorAddresses[currIorMesaAddressOffset] = a;
        iorAddresses[currIorMesaAddressOffset + 1] = h;
        currIorMesaAddressOffset += 2;
        return a;
    }

    internal interface iIOLocation
    {
        int realAddress();
    }

    private sealed class IORBaseLocation : iIOLocation
    {
        public int realAddress() => IOR_BASE;
    }

    private sealed class IOLocation : iIOLocation
    {
        private readonly iIOLocation @base;
        private readonly int offset;

        public IOLocation(int offset)
        {
            this.@base = new IORBaseLocation();
            this.offset = offset;
        }

        public IOLocation(iIOLocation @base, int offset)
        {
            this.@base = @base;
            this.offset = offset;
        }

        public int realAddress() => this.@base.realAddress() + this.offset;
    }

    private sealed class IOBaseLocation : iIOLocation
    {
        private int baseLocation;
        public IOBaseLocation(int @base) { this.baseLocation = @base; }
        public void rebase(int newBase) { this.baseLocation = newBase; }
        public int realAddress() => this.baseLocation;
    }

    private class IORBase : IORAddress
    {
        protected readonly string name;
        protected readonly iIOLocation location;

        public IORBase(string name, iIOLocation location)
        {
            this.name = name;
            this.location = location;
        }

        public string getName() => this.name;
        public int getRealAddress() => this.location.realAddress();

        public virtual void dump(string prefix)
        {
            dmp("{0}[ at real address: 0x{1:X6} ] : NO_VALUE : {2} (IORBase)\n", prefix, this.getRealAddress(), this.name);
        }
    }

    private class IORWord : IORBase, Word
    {
        private List<Field>? fields = null;

        public IORWord(string name, iIOLocation location) : base(name, location) { }

        public virtual ushort get() => Mem.mem[this.location.realAddress()];
        public virtual void set(ushort value) => Mem.mem[this.location.realAddress()] = value;

        public override void dump(string prefix)
        {
            dmp("{0}[ at real address: 0x{1:X6} ] : 0x{2:X4} : {3} (IORWord)\n", prefix, this.getRealAddress(), this.get(), this.name);
        }

        public void addField(Field f)
        {
            this.fields ??= new List<Field>();
            this.fields.Add(f);
        }

        public List<Field>? getFields() => this.fields;
    }

    private sealed class IORByteSwappedWord : IORWord
    {
        public IORByteSwappedWord(string name, iIOLocation location) : base(name, location) { }

        public override ushort get() => (ushort)byteSwap(Mem.mem[this.location.realAddress()]);
        public override void set(ushort value) => Mem.mem[this.location.realAddress()] = (ushort)byteSwap(value);

        public override void dump(string prefix)
        {
            dmp("{0}[ at real address: 0x{1:X6} ] : 0x{2:X4} : {3} (IORByteSwappedWord)\n", prefix, this.getRealAddress(), this.get(), this.name);
        }
    }

    private class IORDblWord : IORBase, DblWord
    {
        public IORDblWord(string name, iIOLocation location) : base(name, location) { }

        public virtual int get()
        {
            ushort low = Mem.mem[this.location.realAddress()];
            ushort high = Mem.mem[this.location.realAddress() + 1];
            return (high << 16) | (low & 0x0000FFFF);
        }

        public virtual void set(int value)
        {
            Mem.mem[this.location.realAddress()] = (ushort)(value & 0xFFFF);
            Mem.mem[this.location.realAddress() + 1] = (ushort)((uint)value >> 16);
        }

        public override void dump(string prefix)
        {
            dmp("{0}[ at real address: 0x{1:X6} ] : 0x{2:X8} : {3} (IORDblWord)\n", prefix, this.getRealAddress(), this.get(), this.name);
        }
    }

    private sealed class IORByteSwappedDblWord : IORDblWord
    {
        public IORByteSwappedDblWord(string name, iIOLocation location) : base(name, location) { }

        public override int get()
        {
            int low = byteSwap(Mem.mem[this.location.realAddress()]);
            int high = byteSwap(Mem.mem[this.location.realAddress() + 1]);
            return (high << 16) | (low & 0x0000FFFF);
        }

        public override void set(int value)
        {
            Mem.mem[this.location.realAddress()] = (ushort)byteSwap((ushort)(value & 0xFFFF));
            Mem.mem[this.location.realAddress() + 1] = (ushort)byteSwap((ushort)((uint)value >> 16));
        }

        public override void dump(string prefix)
        {
            dmp("{0}[ at real address: 0x{1:X6} ] : 0x{2:X8} : {3} (IORByteSwappedDblWord)\n", prefix, this.getRealAddress(), this.get(), this.name);
        }
    }

    private sealed class IORField : BoolField
    {
        private readonly Word @base;
        private readonly int shiftBy;
        private readonly int bits;
        private readonly int mask;
        private readonly bool isBool;
        private readonly string name;

        public IORField(string fieldName, Word w, int bits, bool isBool)
        {
            int tmpShift = 0;
            int tmpBits = bits & 0xFFFF;
            for (int i = 0; i < 16; i++)
            {
                if ((tmpBits & 0x0001) != 0) { break; }
                tmpShift++;
                tmpBits >>>= 1;
            }

            this.@base = w;
            this.shiftBy = tmpShift;
            this.bits = bits & 0xFFFF;
            this.mask = this.bits ^ 0xFFFF;
            this.name = $"{w.getName()}[bits:0x{bits:X4}]:{fieldName}";
            this.isBool = isBool;

            w.addField(this);
        }

        public string getName() => this.name;
        public int getRealAddress() => this.@base.getRealAddress();

        public int get()
        {
            int val = (this.@base.get() & this.bits) >>> this.shiftBy;
            return val;
        }

        public void set(int value)
        {
            int val = (value << this.shiftBy) & this.bits;
            int rest = this.@base.get() & this.mask;
            int newVal = val | rest;
            this.@base.set((ushort)(newVal & 0xFFFF));
        }

        public void dump(string prefix)
        {
            if (this.isBool)
            {
                dmp("{0}--[ at real address: 0x{1:X6} ] : {2} : {3} (BoolField)\n", prefix, this.getRealAddress(), ((BoolField)this).@is().ToString(), this.name);
            }
            else
            {
                dmp("{0}--[ at real address: 0x{1:X6} ] : 0x{2:X4} : {3} (IORField)\n", prefix, this.getRealAddress(), this.get(), this.name);
            }
        }
    }

    private sealed class SwappedWord : Word
    {
        private readonly Word @base;

        public SwappedWord(Word w) { this.@base = w; }

        public string getName() => this.@base.getName();
        public int getRealAddress() => this.@base.getRealAddress();

        public ushort get() => (ushort)byteSwap(this.@base.get());
        public void set(ushort value) => this.@base.set((ushort)byteSwap(value));

        public void dump(string prefix)
        {
            dmp("{0}--[ at real address: 0x{1:X6} ] : 0x{2:X4} : {3} (SwappedWord)\n", prefix, this.getRealAddress(), this.get(), this.getName());
        }

        public void addField(Field f) { /* no-op */ }
    }

    private sealed class CompoundDblWord : DblWord
    {
        private readonly string name;
        private readonly Word w0;
        private readonly Word w1;

        public CompoundDblWord(Word w0, Word w1)
        {
            this.name = w0.getName() + "[asDblWord]";
            this.w0 = w0;
            this.w1 = w1;
        }

        public string getName() => this.name;
        public int getRealAddress() => this.w0.getRealAddress();

        public int get()
        {
            int res = (this.w1.get() << 16) | (this.w0.get() & 0xFFFF);
            return res;
        }

        public void set(int value)
        {
            this.w0.set((ushort)(value & 0xFFFF));
            this.w1.set((ushort)(((uint)value >> 16) & 0xFFFF));
        }

        public void dump(string prefix)
        {
            dmp("{0}>>>>[ at real address: 0x{1:X6} ] : 0x{2:X8} : {3} (CompoundDblWord)\n", prefix, this.getRealAddress(), this.get(), this.getName());
        }
    }

    private sealed class WordBoolean : IORBase, IOPBoolean
    {
        public WordBoolean(string name, iIOLocation location) : base(name, location) { }

        public bool get() => (Mem.mem[this.location.realAddress()] != 0);
        public void set(bool value) => Mem.mem[this.location.realAddress()] = value ? (ushort)0xFFFF : (ushort)0;

        public override void dump(string prefix)
        {
            dmp("{0}[ at real address: 0x{1:X6} ] : {2} : {3} (WordBoolean)\n", prefix, this.getRealAddress(), this.get().ToString(), this.getName());
        }
    }

    private sealed class ByteBoolean : IOPBoolean
    {
        private readonly Word @base;
        private readonly bool hiByte;
        private readonly int getMask;
        private readonly int putMaskRest;
        private readonly string name;

        public ByteBoolean(string fieldName, Word w, bool hiByte)
        {
            this.@base = w;
            this.hiByte = hiByte;
            this.getMask = hiByte ? 0xFF00 : 0x00FF;
            this.putMaskRest = hiByte ? 0x00FF : 0xFF00;
            this.name = $"{w.getName()}-- [boolean,{(hiByte ? "upper" : "lower")}Byte]:{fieldName}";
        }

        public string getName() => this.name;
        public int getRealAddress() => this.@base.getRealAddress();

        public bool get()
        {
            int val = this.@base.get() & this.getMask;
            return val != 0;
        }

        public void set(bool value)
        {
            int bits = value ? this.getMask : 0;
            this.@base.set((ushort)((this.@base.get() & this.putMaskRest) | bits));
        }

        public void dump(string prefix)
        {
            dmp("{0}--[ at real address: 0x{1:X6} ] : {2} : {3} (ByteBoolean,{4})\n", prefix, this.getRealAddress(), this.get().ToString(), this.getName(), this.hiByte ? "hiByte" : "loByte");
        }
    }
}
