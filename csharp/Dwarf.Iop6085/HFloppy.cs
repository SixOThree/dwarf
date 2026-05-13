/*
Copyright (c) 2019, 2020, Dr. Hans-Walter Latz (original Java implementation)
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

// IOP device handler for the floppy drive of a Daybreak/6085 machine.
//
// **Phase F-4b port note**: only the FCB / IOCB layout + processNotify dispatch
// + insert/eject state machine are ported. The Java upstream's IMDFloppy and
// DMKFloppy reader classes (~900 LOC) are dropped per RISKS R7 — IMD/DMK
// floppy formats are legacy, deferred indefinitely. `insertFloppy` throws
// `NotSupportedException` until a format-read path lands. The C# port supports
// no floppy formats at all — same effective state as the Java upstream did for
// non-IMD/non-DMK files. A future sub-task may add 1.44 MiB raw support.
public class HFloppy : DeviceHandler
{
    /*
     * Function Control Block
     */

    private const string FloppyFCB = "FloppyFCB";

    private sealed class FDF_Attributes
    {
        public readonly Word type;                          // Device.Type, --type of drive (defined in DeviceTypes)
        public readonly Word numberOfCylinders;             // CARDINAL, --Number of cylinders available
        public readonly Word numberOfHeadsAndSectors;
        public readonly Field numberOfHeads;                // --Number of read/write heads
        public readonly Field maxSectorsPerTrack;           // --Maximum number of sectors per track
        public readonly Word formatLength;                  // --Length in words of the format buffer
        public readonly Word flags;
        public readonly BoolField ready;                    // BOOLEAN, --Whether drive is ready (contains a diskette)
        public readonly BoolField diskChange;               // BOOLEAN, --Whether drive has gone not ready since last successful op
        public readonly BoolField twoSided;                 // BOOLEAN, --Whether diskette has data on both sides
        public readonly BoolField busy;                     // BOOLEAN, --Whether drive is busy (operation in progress)

        public FDF_Attributes(string name)
        {
            this.type = mkWord(name, "type");
            this.numberOfCylinders = mkWord(name, "numberOfCylinders");
            this.numberOfHeadsAndSectors = mkWord(name, "numberOfHeadsAndSectors");
            this.numberOfHeads = mkField("numberOfHeads", this.numberOfHeadsAndSectors, 0xFF00);
            this.maxSectorsPerTrack = mkField("maxSectorsPerTrack", this.numberOfHeadsAndSectors, 0x00FF);
            this.formatLength = mkWord(name, "formatLength");
            this.flags = mkWord(name, "flags");
            this.ready = mkBoolField("ready", this.flags, 0x8000);
            this.diskChange = mkBoolField("diskChange", this.flags, 0x4000);
            this.twoSided = mkBoolField("twoSided", this.flags, 0x2000);
            this.busy = mkBoolField("busy", this.flags, 0x1000);
        }
    }

    private sealed class FDF_Context : IOStruct
    {
        public readonly Word w;
        public readonly BoolField protect;
        public readonly BoolField isTroyFormat;
        public readonly BoolField isDoubleDensity;
        public readonly Field sectorLength; // SectorLength: TYPE = [0..1024)

        public FDF_Context(string name) : base(null, name)
        {
            // Use the namespace-qualified static factory; inside an IOStruct,
            // `mkWord(string)` is the inherited instance overload that shadows
            // the 2-arg static. Same trick as Java's `IORegion.mkWord(name, "w")`.
            this.w = IORegion.mkWord(name, "w");
            this.protect = mkBoolField("protect", this.w, 0x8000);
            this.isTroyFormat = mkBoolField("isTroyFormat", this.w, 0x4000);
            this.isDoubleDensity = mkBoolField("isDoubleDensity", this.w, 0x2000);
            this.sectorLength = mkField("sectorLength", this.w, 0x1FFF);
        }

        public FDF_Context(IOStruct embeddingParent, string locationName) : base(embeddingParent, locationName)
        {
            this.w = mkWord("word");
            this.protect = mkBoolField("protect", this.w, 0x8000);
            this.isTroyFormat = mkBoolField("isTroyFormat", this.w, 0x4000);
            this.isDoubleDensity = mkBoolField("isDoubleDensity", this.w, 0x2000);
            this.sectorLength = mkField("sectorLength", this.w, 0x1FFF);

            this.endStruct();
        }
    }

    // Java upstream bug preserved verbatim: `private static Word w;` declares a
    // static field that all instances share. The constructor's `this.w = mkWord(...)`
    // overwrites the shared slot. Only the LAST-allocated descriptor reference
    // survives. The actual memory locations are allocated correctly via `mkWord`
    // each time — the bug is just that the prior Word-descriptor java objects
    // become unreferenced. C# behaves identically; preserved for diff fidelity.
    private sealed class Port80ControlWordRecord
    {
#pragma warning disable CA1802 // mark fields as static (matches Java verbatim)
#pragma warning disable CS0649
        private static Word? w_static;
#pragma warning restore CS0649
#pragma warning restore CA1802
        public readonly BoolField enableMainMemory;
        public readonly BoolField enableTimerZero;
        public readonly BoolField fddMotorOn;
        public readonly BoolField fddInUse;
        public readonly BoolField allowTimerTC;
        public readonly BoolField fddLowSpeed;
        public readonly BoolField selectChAIntClk;
        public readonly BoolField enableDCEClk;
        public readonly BoolField driveSelect3;
        public readonly BoolField driveSelect2;
        public readonly BoolField driveSelect1;
        public readonly BoolField driveSelect0;
        public readonly BoolField select250KbDataRate;
        public readonly Field preCompensation;

        public Port80ControlWordRecord(string name)
        {
            w_static = mkWord(name, "w");
            this.enableMainMemory = mkBoolField("enableMainMemory", w_static, 0x8000);
            this.enableTimerZero = mkBoolField("enableTimerZero", w_static, 0x4000);
            this.fddMotorOn = mkBoolField("fddMotorOn", w_static, 0x2000);
            this.fddInUse = mkBoolField("fddInUse", w_static, 0x1000);
            this.allowTimerTC = mkBoolField("allowTimerTC", w_static, 0x0800);
            this.fddLowSpeed = mkBoolField("fddLowSpeed", w_static, 0x0400);
            this.selectChAIntClk = mkBoolField("selectChAIntClk", w_static, 0x0200);
            this.enableDCEClk = mkBoolField("enableDCEClk", w_static, 0x0100);
            this.driveSelect3 = mkBoolField("driveSelect3", w_static, 0x0080);
            this.driveSelect2 = mkBoolField("driveSelect2", w_static, 0x0040);
            this.driveSelect1 = mkBoolField("driveSelect1", w_static, 0x0020);
            this.driveSelect0 = mkBoolField("driveSelect0", w_static, 0x0010);
            this.select250KbDataRate = mkBoolField("select250KbDataRate", w_static, 0x0008);
            this.preCompensation = mkField("preCompensation", w_static, 0x0007);
        }
    }

    // Same Java upstream bug as Port80ControlWordRecord — `static Word w;` is
    // shared across all instances. Preserved verbatim.
    private sealed class FdcStatusRegister3TypeAndSpecifyAndRecalFlags
    {
#pragma warning disable CA1802
#pragma warning disable CS0649
        private static Word? w_static;
#pragma warning restore CS0649
#pragma warning restore CA1802
        public readonly BoolField fault;
        public readonly BoolField writeProtected;
        public readonly BoolField ready;
        public readonly BoolField track0;
        public readonly BoolField twoSided;
        public readonly BoolField theHeadAddress;
        public readonly Field theDriveNumber;
        public readonly Field specifyAndRecalFlags;

        public FdcStatusRegister3TypeAndSpecifyAndRecalFlags(string name)
        {
            w_static = mkWord(name, "w");
            this.fault = mkBoolField("fault", w_static, 0x8000);
            this.writeProtected = mkBoolField("writeProtected", w_static, 0x4000);
            this.ready = mkBoolField("ready", w_static, 0x2000);
            this.track0 = mkBoolField("track0", w_static, 0x1000);
            this.twoSided = mkBoolField("twoSided", w_static, 0x0800);
            this.theHeadAddress = mkBoolField("theHeadAddress", w_static, 0x0400);
            this.theDriveNumber = mkField("theDriveNumber", w_static, 0x0300);
            this.specifyAndRecalFlags = mkField("specifyAndRecalFlags", w_static, 0x00FF);
        }
    }

    private sealed class DeviceContextBlock
    {
        public readonly FDF_Attributes deviceAttributes; // -- only used by Head
        public readonly Word w5;
        public readonly Field dcbExtraByte1;
        public readonly IOPBoolean driveBusy;        // -- set by Handler, read by Head
        public readonly IOPBoolean diagnosticDiskChanged;
        public readonly IOPBoolean pilotDiskChanged;
        public readonly FDF_Context diagnosticContext;
        public readonly FDF_Context pilotContext;
        public readonly IOPBoolean doorOpen;         // -- set by Handler, read by Head
        public readonly FdcStatusRegister3TypeAndSpecifyAndRecalFlags statusRegister3;
        public readonly Port80ControlWordRecord port80ControlWord;
        public readonly Word w13;
        public readonly Field stepRateTimePlusHeadUnloadTime;
        public readonly Field headLoadTimePlusNotInDMAmode;

        public DeviceContextBlock(string name)
        {
            this.deviceAttributes = new FDF_Attributes(name + ".deviceAttributes");
            this.w5 = mkWord(name, "word5");
            this.dcbExtraByte1 = mkField("dcbExtraByte1", this.w5, 0xFF00);
            this.driveBusy = mkIOPShortBoolean("driveBusy", this.w5, false);
            this.diagnosticDiskChanged = mkIOPBoolean(name, "diagnosticDiskChanged");
            this.pilotDiskChanged = mkIOPBoolean(name, "pilotDiskChanged");
            this.diagnosticContext = new FDF_Context(name + ".diagnosticContext");
            this.pilotContext = new FDF_Context(name + ".pilotContext");
            this.doorOpen = mkIOPBoolean(name, "doorOpen");
            this.statusRegister3 = new FdcStatusRegister3TypeAndSpecifyAndRecalFlags("w11");
            this.port80ControlWord = new Port80ControlWordRecord(name + ".port80ControlWord");
            this.w13 = mkWord(name, "word13");
            this.stepRateTimePlusHeadUnloadTime = mkField("stepRateTimePlusHeadUnloadTime", this.w13, 0xFF00);
            this.headLoadTimePlusNotInDMAmode = mkField("headLoadTimePlusNotInDMAmode", this.w13, 0x00FF);
        }
    }

    private sealed class CounterControlWord : IOStruct
    {
        public readonly Word w;
        public readonly BoolField enable;
        public readonly BoolField changeEnable;
        public readonly BoolField counterInterruptWhenDone;
        public readonly BoolField registerInUse;
        public readonly Field notUsed;
        public readonly BoolField maximumCount;
        public readonly BoolField retrigger;
        public readonly BoolField prescaler;
        public readonly BoolField external;
        public readonly BoolField alternate;
        public readonly BoolField continuous;

        public CounterControlWord(string name) : this(null, name) { }

        public CounterControlWord(IOStruct? embeddingParent, string name) : base(embeddingParent, name)
        {
            this.w = (embeddingParent == null)
                ? IORegion.mkByteSwappedWord(name, "w")
                : mkByteSwappedWord("w");
            this.enable = mkBoolField("enable", this.w, 0x8000);
            this.changeEnable = mkBoolField("changeEnable", this.w, 0x4000);
            this.counterInterruptWhenDone = mkBoolField("counterInterruptWhenDone", this.w, 0x2000);
            this.registerInUse = mkBoolField("registerInUse", this.w, 0x1000);
            this.notUsed = mkField("notUsed", this.w, 0x0FC0);
            this.maximumCount = mkBoolField("maximumCount", this.w, 0x0020);
            this.retrigger = mkBoolField("retrigger", this.w, 0x0010);
            this.prescaler = mkBoolField("prescaler", this.w, 0x0008);
            this.external = mkBoolField("external", this.w, 0x0004);
            this.alternate = mkBoolField("alternate", this.w, 0x0002);
            this.continuous = mkBoolField("continous", this.w, 0x0001);

            this.endStruct();
        }
    }

    private sealed class DmaControlWord : IOStruct
    {
        public readonly Word w;
        public readonly BoolField isMemoryDestination;
        public readonly BoolField decrementDestination;
        public readonly BoolField incrementDestination;
        public readonly BoolField isMemorySource;
        public readonly BoolField decrementSource;
        public readonly BoolField incrementSource;
        public readonly BoolField stopWhenTransferCountIsZero;
        public readonly BoolField dmaInterruptWhenDone;
        public readonly Field synchronization;
        public readonly BoolField highChannelPriority;
        public readonly BoolField transmitDataRequest;
        public readonly BoolField changeStartChannel;
        public readonly BoolField startChannel;
        public readonly BoolField byteOrWordTransfer;

        public DmaControlWord(string name) : this(null, name) { }

        public DmaControlWord(IOStruct? embeddingParent, string name) : base(embeddingParent, name)
        {
            this.w = (embeddingParent == null)
                ? IORegion.mkByteSwappedWord(name, "word")
                : mkByteSwappedWord("word");
            this.isMemoryDestination = mkBoolField("isMemoryDestination", this.w, 0x8000);
            this.decrementDestination = mkBoolField("decrementDestination", this.w, 0x4000);
            this.incrementDestination = mkBoolField("incrementDestination", this.w, 0x2000);
            this.isMemorySource = mkBoolField("isMemorySource", this.w, 0x1000);
            this.decrementSource = mkBoolField("decrementSource", this.w, 0x0800);
            this.incrementSource = mkBoolField("incrementSource", this.w, 0x0400);
            this.stopWhenTransferCountIsZero = mkBoolField("stopWhenTransferCountIsZero", this.w, 0x0200);
            this.dmaInterruptWhenDone = mkBoolField("dmaInterruptWhenDone", this.w, 0x0100);
            this.synchronization = mkField("synchronization", this.w, 0x00C0);
            this.highChannelPriority = mkBoolField("highChannelPriority", this.w, 0x0020);
            this.transmitDataRequest = mkBoolField("transmitDataRequest", this.w, 0x0010);
            this.changeStartChannel = mkBoolField("changeStartChannel", this.w, 0x0004);
            this.startChannel = mkBoolField("startChannel", this.w, 0x0002);
            this.byteOrWordTransfer = mkBoolField("byteOrWordTransfer", this.w, 0x0001);

            this.endStruct();
        }
    }

    private sealed class FCB : IORAddress
    {
        private readonly int startAddress;

        public readonly IOPTypes.TaskContextBlock task;
        public readonly IOPTypes.TaskContextBlock dmaTask;
        public readonly Word w18;
        public readonly IOPBoolean stopHandler;
        public readonly IOPBoolean resetFDC;
        public readonly Word w19;
        public readonly IOPBoolean handlerIsStopped;
        public readonly IOPBoolean fdcHung;
        public readonly Word w20;
        public readonly IOPBoolean waitingForDMAInterrupt;
        public readonly IOPBoolean firstDMAInterrupt;
        public readonly Word w21;
        public readonly Field driveMotorControlCount;
        public readonly IOPBoolean timeoutOccurred;
        public readonly Word w22;
        public readonly Field badDMAInterruptCount;
        public readonly Field badFDCInterruptCount;
        public readonly Word w23;
        public readonly IOPBoolean tapeThisIOCB;
        public readonly Word w24;
        public readonly Field fillerByteForFormatting;
        public readonly IOPBoolean diagnosticsOn;
        public readonly Word encodedDeviceTypes; // EncodedDeviceType, read from EEPROM by handler
        public readonly IOPTypes.NotifyMask workMask;
        public readonly IOPTypes.IOPCondition workNotify;
        public readonly Word lockMask;
        public readonly IOPTypes.OpieAddress currentIOCB;
        public readonly IOPTypes.QueueBlock diagnosticQueue;
        public readonly IOPTypes.QueueBlock pilotQueue;
        public readonly IOPTypes.QueueBlock p80186Queue;
        public readonly DeviceContextBlock dcb0;
        public readonly DeviceContextBlock dcb1;
        public readonly DeviceContextBlock dcb2;
        public readonly DeviceContextBlock dcb3;
        public readonly DeviceContextBlock[] dcb;
        public readonly Word totalBytesToTransfer;
        public readonly CounterControlWord counterControlRegister;
        public readonly Word firstDMAtransferCount;
        public readonly DmaControlWord firstDmaControlWord;
        public readonly Word numberOfMiddleDMAtransfers;
        public readonly Word middleDMAtransferCount;
        public readonly DmaControlWord middleDmaControlWord;
        public readonly Word lastDMAtransferCount;
        public readonly DmaControlWord lastDmaControlWord;
        public readonly Word wx;
        public readonly Field currentTrack;
        public readonly Field extraByte1;
        public readonly Word queueSemaphore;

        public FCB()
        {
            this.startAddress = syncToSegment() + IOR_BASE;

            this.task = new IOPTypes.TaskContextBlock(FloppyFCB, "task");
            this.dmaTask = new IOPTypes.TaskContextBlock(FloppyFCB, "dmaTask");
            this.w18 = mkWord(FloppyFCB, "word18");
            this.stopHandler = mkIOPShortBoolean("stopHandler", this.w18, true);
            this.resetFDC = mkIOPShortBoolean("resetFDC", this.w18, false);
            this.w19 = mkWord(FloppyFCB, "word19");
            this.handlerIsStopped = mkIOPShortBoolean("handlerIsStopped", this.w19, true);
            this.fdcHung = mkIOPShortBoolean("fdcHung", this.w19, false);
            this.w20 = mkWord(FloppyFCB, "word20");
            this.waitingForDMAInterrupt = mkIOPShortBoolean("waitingForDMAInterrupt", this.w20, true);
            this.firstDMAInterrupt = mkIOPShortBoolean("firstDMAInterrupt", this.w20, false);
            this.w21 = mkWord(FloppyFCB, "word21");
            this.driveMotorControlCount = mkField("driveMotorControlCount", this.w21, 0xFF00);
            this.timeoutOccurred = mkIOPShortBoolean("timeoutOccurred", this.w21, false);
            this.w22 = mkWord(FloppyFCB, "word22");
            this.badDMAInterruptCount = mkField("badDMAInterruptCount", this.w22, 0xFF00);
            this.badFDCInterruptCount = mkField("badFDCInterruptCount", this.w22, 0x00FF);
            this.w23 = mkWord(FloppyFCB, "word23");
            this.tapeThisIOCB = mkIOPShortBoolean("tapeThisIOCB", this.w23, true);
            this.w24 = mkWord(FloppyFCB, "word24");
            this.fillerByteForFormatting = mkField("fillerByteForFormatting", this.w24, 0xFF00);
            this.diagnosticsOn = mkIOPShortBoolean("diagnosticsOn", this.w24, false);
            this.encodedDeviceTypes = mkWord(FloppyFCB, "encodedDeviceTypes");
            this.workMask = new IOPTypes.NotifyMask(FloppyFCB, "workMask");
            this.workNotify = new IOPTypes.IOPCondition(FloppyFCB, "workNotify");
            this.lockMask = mkWord(FloppyFCB, "lockMask");
            this.currentIOCB = new IOPTypes.OpieAddress(FloppyFCB, "currentIOCB");
            this.diagnosticQueue = new IOPTypes.QueueBlock(FloppyFCB, "diagnosticQueue");
            this.pilotQueue = new IOPTypes.QueueBlock(FloppyFCB, "pilotQueue");
            this.p80186Queue = new IOPTypes.QueueBlock(FloppyFCB, "80186Queue");
            this.dcb0 = new DeviceContextBlock(FloppyFCB + ".dcb[0]");
            this.dcb1 = new DeviceContextBlock(FloppyFCB + ".dcb[1]");
            this.dcb2 = new DeviceContextBlock(FloppyFCB + ".dcb[2]");
            this.dcb3 = new DeviceContextBlock(FloppyFCB + ".dcb[3]");
            this.dcb = new DeviceContextBlock[] { this.dcb0, this.dcb1, this.dcb2, this.dcb3 };
            this.totalBytesToTransfer = mkWord(FloppyFCB, "totalBytesToTransfer");
            this.counterControlRegister = new CounterControlWord(FloppyFCB + ".counterControlRegister");
            this.firstDMAtransferCount = mkWord(FloppyFCB, "firstDMAtransferCount");
            this.firstDmaControlWord = new DmaControlWord(FloppyFCB + ".firstDmaControlWord");
            this.numberOfMiddleDMAtransfers = mkWord(FloppyFCB, "numberOfMiddleDMAtransfers");
            this.middleDMAtransferCount = mkWord(FloppyFCB, "middleDMAtransferCount");
            this.middleDmaControlWord = new DmaControlWord(FloppyFCB + ".middletDmaControlWord");
            this.lastDMAtransferCount = mkWord(FloppyFCB, "lastDMAtransferCount");
            this.lastDmaControlWord = new DmaControlWord(FloppyFCB + ".lastDmaControlWord");
            this.wx = mkWord(FloppyFCB, "wx");
            this.currentTrack = mkField("currentTrack", this.wx, 0xFF00);
            this.extraByte1 = mkField("extraByte1", this.wx, 0x00FF);
            this.queueSemaphore = mkWord(FloppyFCB, "queueSemaphore");

            // initialize masks for communication with mesahead
            this.workMask.byteMaskAndOffset.set(mkMask());
            this.lockMask.set(mkMask());
        }

        public string getName() => FloppyFCB;

        public int getRealAddress() => this.startAddress;
    }

    /*
     * Input Output Control Block
     */

    private static class Function
    {
        public const int nop = 0;
        public const int readSector = 1;
        public const int writeSector = 2;
        public const int writeDeletedSector = 3;
        public const int readID = 4;
        public const int formatTrack = 5;
    }

    private sealed class DiskAddress : IOStruct
    {
        public readonly Word cylinder;
        public readonly Word headAndSector;
        public readonly Field head;
        public readonly Field sector;

        public DiskAddress(IOStruct embeddingParent, string name) : base(embeddingParent, name)
        {
            this.cylinder = mkWord("cylinder");
            this.headAndSector = mkWord("headAndSector");
            this.head = mkField("head", this.headAndSector, 0xFF00);
            this.sector = mkField("sector", this.headAndSector, 0x00FF);

            this.endStruct();
        }
    }

    private sealed class FDF_Operation : IOStruct
    {
        public readonly Word device;
        public readonly Word function;
        public readonly DiskAddress address;
        public readonly DblWord dataPtr;
        public readonly Word w6;
        public readonly BoolField incrementDataPointer;
        public readonly Field tries;
        public readonly Word count;

        public FDF_Operation(IOStruct embeddingParent, string name) : base(embeddingParent, name)
        {
            this.device = mkWord("device");
            this.function = mkWord("function");
            this.address = new DiskAddress(this, "address");
            this.dataPtr = mkDblWord("dataPtr");
            this.w6 = mkWord("word6");
            this.incrementDataPointer = mkBoolField("incrementDataPointer", this.w6, 0x8000);
            this.tries = mkField("tries", this.w6, 0x7FFF);
            this.count = mkWord("count");

            this.endStruct();
        }

        public override string ToString() =>
            string.Format(
                "Operation[ device: {0}, function: {1} , address: {2}/{3}/{4} . dataPtr: 0x{5:X6} , incrDataPointer: {6} , count = {7} ]",
                this.device.get(),
                getFunctionName(this.function.get()),
                this.address.cylinder.get(), this.address.head.get(), this.address.sector.get(),
                this.dataPtr.get(),
                this.incrementDataPointer.get() != 0,
                this.count.get());
    }

    private static class ExtendedFDCcommandType
    {
        public const int NullCommand = 0;
        public const int FormatTrack = 1;
        public const int ReadData = 2;
        public const int ReadDeletedData = 3;
        public const int ReadID = 4;
        public const int ReadTrack = 5;
        public const int Recalibrate = 6;
        public const int ScanEqual = 7;
        public const int ScanHighOrEqual = 8;
        public const int ScanLowOrEqual = 9;
        public const int Seek = 10;
        public const int SenseDriveStatus = 11;
        public const int SenseInterruptStatus = 12;
        public const int Specify = 13;
        public const int WriteData = 14;
        public const int WriteDeletedData = 15;
        public const int lastAndInvalid = 255;
    }

    private static string getFunctionName(int code) => code switch
    {
        Function.nop => "nop",
        Function.readSector => "readSector",
        Function.writeSector => "writeSector",
        Function.writeDeletedSector => "writeDeletedSector",
        Function.readID => "readID",
        Function.formatTrack => "formatTrack",
        _ => string.Format("invalid Function-code[{0}]", code),
    };

    private static string getExtendedFDCcommandTypeName(int code) => code switch
    {
        ExtendedFDCcommandType.NullCommand => "NullCommand",
        ExtendedFDCcommandType.FormatTrack => "FormatTrack",
        ExtendedFDCcommandType.ReadData => "ReadData",
        ExtendedFDCcommandType.ReadDeletedData => "ReadDeletedData",
        ExtendedFDCcommandType.ReadID => "ReadID",
        ExtendedFDCcommandType.ReadTrack => "ReadTrack",
        ExtendedFDCcommandType.Recalibrate => "Recalibrate",
        ExtendedFDCcommandType.ScanEqual => "ScanEqual",
        ExtendedFDCcommandType.ScanHighOrEqual => "ScanHighOrEqual",
        ExtendedFDCcommandType.Seek => "Seek",
        ExtendedFDCcommandType.SenseDriveStatus => "SenseDriveStatus",
        ExtendedFDCcommandType.SenseInterruptStatus => "SenseInterruptStatus",
        ExtendedFDCcommandType.Specify => "Specify",
        ExtendedFDCcommandType.WriteData => "WriteData",
        ExtendedFDCcommandType.WriteDeletedData => "WriteDeletedData",
        ExtendedFDCcommandType.lastAndInvalid => "lastAndInvalid",
        _ => string.Format("invalid ExtendedFDCcommandType-code[{0}]", code),
    };

    private static class FDF_Status
    {
        public const int inProgress = 0;
        public const int goodCompletion = 1;
        public const int diskChange = 2;
        public const int notReady = 3;
        public const int cylinderError = 4;
        public const int deletedData = 5;
        public const int recordNotFound = 6;
        public const int headerError = 7;
        public const int dataError = 8;
        public const int dataLost = 9;
        public const int writeFault = 10;
        public const int memoryError = 11;
        public const int invalidOperation = 12;
        public const int aborted = 13;
        public const int otherError = 14;
    }

    public static class OperationStateType
    {
        public const int OperationDoesNotExist = 0;
        public const int OperationInvalid = 1;
        public const int OperationBuilt = 2;
        public const int OperationWaiting = 3;
        public const int OperationInProgress = 4;
        public const int OperationAborted = 5;
        public const int OperationCompleted = 6;
        public const int OperationFailed = 7;
        public const int none = 255;
    }

    private sealed class TrackDMAandCounterControl : IOStruct
    {
        public readonly Word TotalBytesToTransfer;
        public readonly Word TotalBytesActuallyTransfered;
        public readonly CounterControlWord CounterControlRegister;
        public readonly Word FirstDMAtransferCount;
        public readonly DmaControlWord FirstDMAcontrolWord;
        public readonly Word NumberOfMiddleDMAtransfers;
        public readonly Word MiddleDMAtransferCount;
        public readonly DmaControlWord MiddleDMAcontrolWord;
        public readonly Word LastDMAtransferCount;
        public readonly DmaControlWord LastDMAcontrolWord;

        public TrackDMAandCounterControl(IOStruct embeddingParent, string name) : base(embeddingParent, name)
        {
            this.TotalBytesToTransfer = mkByteSwappedWord("TotalBytesToTransfer");
            this.TotalBytesActuallyTransfered = mkByteSwappedWord("TotalBytesActuallyTransfered");
            this.CounterControlRegister = new CounterControlWord(this, "CounterControlRegister");
            this.FirstDMAtransferCount = mkByteSwappedWord("FirstDMAtransferCount");
            this.FirstDMAcontrolWord = new DmaControlWord(this, "FirstDMAcontrolWord");
            this.NumberOfMiddleDMAtransfers = mkByteSwappedWord("NumberOfMiddleDMAtransfers");
            this.MiddleDMAtransferCount = mkByteSwappedWord("MiddleDMAtransferCount");
            this.MiddleDMAcontrolWord = new DmaControlWord(this, "MiddleDMAcontrolWord");
            this.LastDMAtransferCount = mkByteSwappedWord("LastDMAtransferCount");
            this.LastDMAcontrolWord = new DmaControlWord(this, "LastDMAcontrolWord");

            this.endStruct();
        }
    }

    private sealed class FdcCommandRecord : IOStruct
    {
        public readonly Word w0;
        public readonly Field fdcCode;
        public readonly Field DataTransferCode;
        public readonly Word w1;
        public readonly Field anExtraByte;
        public readonly IOPBoolean MustWaitForInterrupt;
        public readonly Word w2;
        public readonly Field NumberOfCommandBytes;
        public readonly Field NumberOfCommandBytesWritten;
        public readonly Word CommandBytes_0_1;
        public readonly Word CommandBytes_2_3;
        public readonly Word CommandBytes_4_5;
        public readonly Word CommandBytes_6_7;
        public readonly Word CommandBytes_8_9;
        public readonly Word w8;
        public readonly Field NumberOfResultBytes;
        public readonly Field NumberOfResultBytesRead;
        public readonly Word ResultBytes_0_1;
        public readonly Word ResultBytes_2_3;
        public readonly Word ResultBytes_4_5;
        public readonly Word ResultBytes_6_7;

        public FdcCommandRecord(IOStruct embeddingParent, string name) : base(embeddingParent, name)
        {
            this.w0 = mkWord("word0");
            this.fdcCode = mkField("fdcCode", this.w0, 0xFF00);
            this.DataTransferCode = mkField("DataTransferCode", this.w0, 0x00FF);
            this.w1 = mkWord("word1");
            this.anExtraByte = mkField("anExtraByte", this.w1, 0xFF00);
            this.MustWaitForInterrupt = mkIOPShortBoolean("MustWaitForInterrupt", this.w1, false);
            this.w2 = mkWord("word2");
            this.NumberOfCommandBytes = mkField("NumberOfCommandBytes", this.w2, 0xFF00);
            this.NumberOfCommandBytesWritten = mkField("NumberOfCommandBytesWritten", this.w2, 0x00FF);
            this.CommandBytes_0_1 = mkWord("CommandBytes_0+1");
            this.CommandBytes_2_3 = mkWord("CommandBytes_2+3");
            this.CommandBytes_4_5 = mkWord("CommandBytes_4+5");
            this.CommandBytes_6_7 = mkWord("CommandBytes_6+7");
            this.CommandBytes_8_9 = mkWord("CommandBytes_8+9");
            this.w8 = mkWord("word8");
            this.NumberOfResultBytes = mkField("NumberOfResultBytes", this.w8, 0xFF00);
            this.NumberOfResultBytesRead = mkField("NumberOfResultBytesRead", this.w8, 0x00FF);
            this.ResultBytes_0_1 = mkWord("ResultBytes_0+1");
            this.ResultBytes_2_3 = mkWord("ResultBytes_2+3");
            this.ResultBytes_4_5 = mkWord("ResultBytes_4+5");
            this.ResultBytes_6_7 = mkWord("ResultBytes_6+7");

            this.endStruct();
        }
    }

    private const string FloppyIOCB = "FloppyIOCB";

    private sealed class IOCB : IOStruct
    {
        public readonly FDF_Operation operation;

        public readonly Word w8;
        public readonly Field generalizedFDCOperation;
        public readonly Field savedStatus;
        public readonly FDF_Context theContext;
        public readonly Word w10;
        public readonly BoolField alternateSectors;
        public readonly BoolField multiTrackMode;
        public readonly BoolField skipDeletedSector;
        public readonly Field currentTryCount;

        public readonly Word w11;
        public readonly IOPBoolean operationIsQueued;
        public readonly Field operationState;
        public readonly IOPTypes.OpieAddress nextIOCB;
        public readonly IOPTypes.OpieAddress dataAddress;
        public readonly IOPTypes.ClientCondition actualClientCondition;

        public readonly Word w19;
        public readonly BoolField finalStateOfFDC_RequestForMaster;
        public readonly BoolField finalStateOfFDC_DataInputOutput;
        public readonly BoolField finalStateOfFDC_nonDMAmode;
        public readonly BoolField finalStateOfFDC_fdcBusy;
        public readonly BoolField finalStateOfFDC_DiskDrive3busy;
        public readonly BoolField finalStateOfFDC_DiskDrive2busy;
        public readonly BoolField finalStateOfFDC_DiskDrive1busy;
        public readonly BoolField finalStateOfFDC_DiskDrive0busy;
        public readonly IOPBoolean specifyFlag;
        public readonly Word w20;
        public readonly IOPBoolean PCEResetFDCFlag;
        public readonly Field PCEStartMotorFlags;
        public readonly Word w21;
        public readonly IOPBoolean ResetAndFlushFlag;
        public readonly IOPBoolean RecalFlag;
        public readonly Word w22;
        public readonly Field daDriveNumber;
        public readonly IOPBoolean FDCHung;
        public readonly TrackDMAandCounterControl firstTrack;
        public readonly Word FinalDMACount;
        public readonly Word w34;
        public readonly IOPBoolean incrementDataPointer;
        public readonly IOPBoolean TimeoutOccurred;
        public readonly Word numberOfFDCCommands;
        public readonly Word currentFDCCommand;
        public readonly FdcCommandRecord fdcCommands0;
        public readonly FdcCommandRecord fdcCommands1;
        public readonly FdcCommandRecord fdcCommands2;
        public readonly FdcCommandRecord[] fdcCommands;
        public readonly Word w76;
        public readonly Field tapeSelection;
        public readonly Field stream;
        public readonly Word numberOfMiddleTrackTransfers;
        public readonly TrackDMAandCounterControl middleTrack;
        public readonly TrackDMAandCounterControl lastTrack;

        public IOCB(int @base) : base(@base, FloppyIOCB)
        {
            this.operation = new FDF_Operation(this, "operation");

            this.w8 = mkWord("word8");
            this.generalizedFDCOperation = mkField("generalizedFDCOperation", this.w8, 0xFF00);
            this.savedStatus = mkField("savedStatus", this.w8, 0x00FF);
            this.theContext = new FDF_Context(this, "theContext");
            this.w10 = mkWord("word10");
            this.alternateSectors = mkBoolField("alternateSectors", this.w10, 0x8000);
            this.multiTrackMode = mkBoolField("multiTrackMode", this.w10, 0x4000);
            this.skipDeletedSector = mkBoolField("skipDeletedSector", this.w10, 0x2000);
            this.currentTryCount = mkField("currentTryCount", this.w10, 0x00FF);

            this.w11 = mkWord("word11");
            this.operationIsQueued = mkIOPShortBoolean("operationIsQueued", this.w11, true);
            this.operationState = mkField("operationState", this.w11, 0x00FF);
            this.nextIOCB = new IOPTypes.OpieAddress(this, "nextIOCB");
            this.dataAddress = new IOPTypes.OpieAddress(this, "dataAddress");
            this.actualClientCondition = new IOPTypes.ClientCondition(this, "actualClientCondition");

            this.w19 = mkWord("word19");
            this.finalStateOfFDC_RequestForMaster = mkBoolField("finalStateOfFDC.RequestForMaster", this.w19, 0x8000);
            this.finalStateOfFDC_DataInputOutput = mkBoolField("finalStateOfFDC.DataInputOutput", this.w19, 0x4000);
            this.finalStateOfFDC_nonDMAmode = mkBoolField("finalStateOfFDC.nonDMAmode", this.w19, 0x2000);
            this.finalStateOfFDC_fdcBusy = mkBoolField("finalStateOfFDC.fdcBusy", this.w19, 0x1000);
            this.finalStateOfFDC_DiskDrive3busy = mkBoolField("finalStateOfFDC.DiskDrive3busy", this.w19, 0x0800);
            this.finalStateOfFDC_DiskDrive2busy = mkBoolField("finalStateOfFDC.DiskDrive2busy", this.w19, 0x0400);
            this.finalStateOfFDC_DiskDrive1busy = mkBoolField("finalStateOfFDC.DiskDrive1busy", this.w19, 0x0200);
            this.finalStateOfFDC_DiskDrive0busy = mkBoolField("finalStateOfFDC.DiskDrive0busy", this.w19, 0x0100);
            this.specifyFlag = mkIOPShortBoolean("specifyFlag", this.w19, false);
            this.w20 = mkWord("word20");
            this.PCEResetFDCFlag = mkIOPShortBoolean("PCEResetFDCFlag", this.w20, true);
            this.PCEStartMotorFlags = mkField("PCEStartMotorFlags", this.w20, 0x00FF);
            this.w21 = mkWord("word21");
            this.ResetAndFlushFlag = mkIOPShortBoolean("ResetAndFlushFlag", this.w21, true);
            this.RecalFlag = mkIOPShortBoolean("RecalFlag", this.w21, false);
            this.w22 = mkWord("word22");
            this.daDriveNumber = mkField("DaDriveNumber", this.w22, 0xFF00);
            this.FDCHung = mkIOPShortBoolean("FDCHung", this.w22, false);
            this.firstTrack = new TrackDMAandCounterControl(this, "firstTrack");
            this.FinalDMACount = mkByteSwappedWord("FinalDMACount");
            this.w34 = mkWord("word34");
            this.incrementDataPointer = mkIOPShortBoolean("IncrementDataPointer", this.w34, true);
            this.TimeoutOccurred = mkIOPShortBoolean("TimeoutOccurred", this.w34, false);
            this.numberOfFDCCommands = mkByteSwappedWord("NumberOfFDCCommands");
            this.currentFDCCommand = mkByteSwappedWord("CurrentFDCCommand");
            this.fdcCommands0 = new FdcCommandRecord(this, "fdcCommands[0]");
            this.fdcCommands1 = new FdcCommandRecord(this, "fdcCommands[1]");
            this.fdcCommands2 = new FdcCommandRecord(this, "fdcCommands[2]");
            this.fdcCommands = new FdcCommandRecord[] { this.fdcCommands0, this.fdcCommands1, this.fdcCommands2 };
            this.w76 = mkWord("word76");
            this.tapeSelection = mkField("tapeSelection", this.w76, 0xFF00);
            this.stream = mkField("stream", this.w76, 0x00FF);
            this.numberOfMiddleTrackTransfers = mkByteSwappedWord("numberOfMiddleTrackTransfers");
            this.middleTrack = new TrackDMAandCounterControl(this, "middleTrack");
            this.lastTrack = new TrackDMAandCounterControl(this, "lastTrack");
        }
    }

    /*
     * statistical data
     */

    private int reads = 0;
    private int writes = 0;

    public int getReads() => this.reads;
    public int getWrites() => this.writes;

    /*
     * implementation
     */

    private readonly FCB fcb;
    private readonly IOCB workIocb = new IOCB(0);

    public HFloppy() : base(FloppyFCB, Config.IO_LOG_FLOPPY)
    {
        this.fcb = new FCB();

        // initialize FCB: we have 1x Shugart SA-455 as drive 0 and no more drives
        this.fcb.encodedDeviceTypes.set(0x4000); // 1x sa455DiskDrive(4), 3x NoDiskDrive(0)

        this.fcb.dcb[0].deviceAttributes.type.set(19); // sa455: FIRST[Floppy]+2 with FIRST[Floppy]=17
        this.fcb.dcb[0].deviceAttributes.numberOfCylinders.set(40);
        this.fcb.dcb[0].deviceAttributes.maxSectorsPerTrack.set(16); // 10 sectors with 512 bytes, 16 sectors with 256
        this.fcb.dcb[0].deviceAttributes.formatLength.set(12288);
        this.fcb.dcb[0].deviceAttributes.ready.set(false);

        this.fcb.dcb[1].deviceAttributes.type.set(0); // Device.nullType
        this.fcb.dcb[2].deviceAttributes.type.set(0);
        this.fcb.dcb[3].deviceAttributes.type.set(0);

        // set state to "no floppy inserted"
        this.mesaEjectFloppy();
    }

    public override int getFcbRealAddress() => this.fcb.getRealAddress();

    public override ushort getFcbSegment() => ((IORAddress)this.fcb).getIOPSegment();

    public override bool processNotify(ushort notifyMask)
    {
        if (notifyMask != this.fcb.workMask.byteMaskAndOffset.get())
        {
            // not for us, let an other handler take care of this
            return false;
        }

        this.slogf("\n");
        this.logf("IOP::HFloppy.processNotify() - begin\n");

        // process the stopHandler-flag
        if (this.fcb.stopHandler.get())
        {
            this.logf("IOP::HFloppy.processNotify() -> stopHandler\n");
            this.fcb.handlerIsStopped.set(true);
            this.logf("IOP::HFloppy.processNotify() - done\n\n");
            return true;
        }
        else if (this.fcb.handlerIsStopped.get())
        {
            this.logf("IOP::HFloppy.processNotify() -> handler restarted\n");

            // update status in DCB
            bool havingFloppy = (this.currFloppy != null);
            this.fcb.dcb[0].deviceAttributes.ready.set(havingFloppy);
            this.fcb.dcb[0].deviceAttributes.diskChange.set(havingFloppy);
            this.fcb.dcb[0].deviceAttributes.twoSided.set(havingFloppy);
            this.logf("===> floppy status: {0}\n", havingFloppy);
        }
        this.fcb.handlerIsStopped.set(false);

        // process the iocb handling requests
        int iopIocbPtr = this.fcb.pilotQueue.queueHead.toLP();
        this.logf("IOP::HFloppy.processNotify()  -> fcb.pilotQueue.queueHead.toLP() = 0x{0:X6}\n", iopIocbPtr);
        while (iopIocbPtr != 0)
        {
            this.workIocb.rebaseToVirtualAddress(iopIocbPtr);

            this.dumpWorkIocb("IOP::HFloppy.processNotify()  -> IOCB data:");

            // get operation data
            int genOp = this.workIocb.generalizedFDCOperation.get();
            int dataPtr = this.workIocb.dataAddress.toLP();
            bool incrDataPtr = this.workIocb.incrementDataPointer.get();
            ushort intrMask = this.workIocb.actualClientCondition.maskValue.get();

            // get and work with the last fdc-operation as the only relevant one
            int fdcCommandIndex = Math.Max(0, Math.Min(this.workIocb.numberOfFDCCommands.get() - 1, this.workIocb.fdcCommands.Length - 1));
            FdcCommandRecord fdcCommand = this.workIocb.fdcCommands[fdcCommandIndex];
            int op = fdcCommand.fdcCode.get();
            int driveNo = fdcCommand.CommandBytes_0_1.get() & 0xFF;
            int cyl = (fdcCommand.CommandBytes_2_3.get() >> 8) & 0xFF;
            int head = fdcCommand.CommandBytes_2_3.get() & 0xFF;
            int sect = (fdcCommand.CommandBytes_4_5.get() >> 8) & 0xFF;
            int sectorLengthEncoded = fdcCommand.CommandBytes_4_5.get() & 0xFF;
            int sectorWordLength = (sectorLengthEncoded == 0) ? 64 : (sectorLengthEncoded == 1) ? 128 : 256;
            int sectorsPerTrack = (fdcCommand.CommandBytes_6_7.get() >> 8) & 0xFF;

            this.slogf(
                "++ iocb.fdcCommands[{0}]: op = {1}, driveNo = {2}, cyl/head/sect: {3}/{4}/{5}, sectLength: code {6} => {7} words, sectors/track: {8}\n",
                fdcCommandIndex, op, driveNo, cyl, head, sect, sectorLengthEncoded, sectorWordLength, sectorsPerTrack);

            // tell which fdcCommand was processed last
            this.workIocb.currentFDCCommand.set((ushort)(fdcCommandIndex + 1));
            fdcCommand.NumberOfCommandBytesWritten.set(fdcCommand.NumberOfCommandBytes.get());

            // interpret the dma-specs a bit to get the number of sectors to transfer
            int firstTrackTransferBytes = this.workIocb.firstTrack.TotalBytesToTransfer.get() & 0xFFFF;
            int middleTrackTransferBytes = this.workIocb.middleTrack.TotalBytesToTransfer.get() & 0xFFFF;
            int lastTrackTransferBytes = this.workIocb.lastTrack.TotalBytesToTransfer.get() & 0xFFFF;
            int totalTransferBytes = firstTrackTransferBytes + middleTrackTransferBytes + lastTrackTransferBytes;
            int totalTransferSectors = totalTransferBytes / (sectorWordLength * 2);
            this.slogf("++ iocb-dma-summary: totalTransterBytes = {0} ( {1} , {2} , {3} ) => {4} sectors\n",
                totalTransferBytes,
                this.workIocb.firstTrack.TotalBytesToTransfer.get(), this.workIocb.middleTrack.TotalBytesToTransfer.get(), this.workIocb.lastTrack.TotalBytesToTransfer.get(),
                totalTransferSectors);

            // plausibility-checks
            if (genOp != op)
            {
                this.slogf("#### warning: iocb.generalizedOperation({0}) != fdcCommand.op({1})\n", genOp, op);
            }
            if (driveNo != this.workIocb.operation.device.get())
            {
                this.slogf("#### warning: fdcCommand.driveNo({0}) != iocb.operation.device({1})\n", driveNo, this.workIocb.operation.device.get());
            }
            if (driveNo != this.workIocb.daDriveNumber.get())
            {
                this.slogf("#### warning: fdcCommand.driveNo({0}) != iocb.daDriveNumber({1})\n", driveNo, this.workIocb.daDriveNumber.get());
            }
            if (totalTransferSectors != this.workIocb.operation.count.get())
            {
                this.slogf("#### warning: fdcCommand->sectorCount({0}) != iocb.operation.count({1})\n", totalTransferSectors, this.workIocb.operation.count.get());
            }

            if (this.workIocb.daDriveNumber.get() == 0) // only drive=0 supported
            {
                switch (genOp)
                {
                    case ExtendedFDCcommandType.NullCommand:
                        // nothing to do, setting the operationState is apparently enough
                        this.workIocb.operationState.set(OperationStateType.OperationCompleted);
                        break;

                    case ExtendedFDCcommandType.ReadData:
                    {
                        // transfer data
                        int targetPtr = dataPtr;
                        int remainingSects = totalTransferSectors;
                        int bytesTransferred = 0;
                        int nextCyl = cyl;
                        int nextHead = head;
                        int nextSect = sect;
                        while (remainingSects > 0)
                        {
                            // get the sector
                            ushort[]? sector = this.currFloppy?.getSector(nextCyl, nextHead, nextSect);
                            if (sector == null)
                            {
                                break; // abort data transfer if sector not found
                            }

                            // transfer sector content
                            for (int i = 0; i < Math.Min(sector.Length, sectorWordLength); i++)
                            {
                                Mem.writeWord(targetPtr + i, sector[i]);
                                bytesTransferred += 2;
                            }
                            targetPtr += sectorWordLength;

                            // move to next sector
                            nextCyl = this.currFloppy!.getNextCyl();
                            nextHead = this.currFloppy.getNextHead();
                            nextSect = this.currFloppy.getNextSect();

                            remainingSects--;
                        }

                        this.reads++;

                        // distribute byte transfers over the dma-specs
                        int byteCount = Math.Min(firstTrackTransferBytes, bytesTransferred);
                        this.workIocb.firstTrack.TotalBytesActuallyTransfered.set((ushort)byteCount);
                        bytesTransferred -= byteCount;
                        byteCount = Math.Min(middleTrackTransferBytes, bytesTransferred);
                        this.workIocb.middleTrack.TotalBytesActuallyTransfered.set((ushort)byteCount);
                        bytesTransferred -= byteCount;
                        byteCount = Math.Min(lastTrackTransferBytes, bytesTransferred);
                        this.workIocb.lastTrack.TotalBytesActuallyTransfered.set((ushort)byteCount);

                        // hack: adjust iocb.FinalDMACount
                        int finalDmaCount = 0x00010000 - (this.workIocb.firstTrack.TotalBytesActuallyTransfered.get() & 0xFFFF);
                        this.workIocb.FinalDMACount.set((ushort)(finalDmaCount & 0xFFFF));

                        // produce result bytes of the fdcCommand
                        fdcCommand.ResultBytes_0_1.set(0);
                        fdcCommand.ResultBytes_2_3.set((ushort)(nextCyl & 0x00FF));
                        fdcCommand.ResultBytes_4_5.set((ushort)((nextHead << 8) | (nextSect & 0x00FF)));
                        fdcCommand.NumberOfResultBytesRead.set(fdcCommand.NumberOfResultBytes.get());

                        // tell the operation was successful
                        this.workIocb.operationState.set(OperationStateType.OperationCompleted);
                        break;
                    }

                    default:
                        this.workIocb.operationState.set(OperationStateType.OperationInvalid);
                        break;
                }
            }
            else
            {
                this.workIocb.operationState.set(OperationStateType.OperationInvalid);
            }

            // set general data
            this.workIocb.currentTryCount.set(1);
            this.workIocb.FDCHung.set(false);

            // show resulting iocb state
            this.slogf("\n");
            this.dumpWorkIocb("IOP::HFloppy.processNotify()  -> IOCB after processing:");
            this.slogf("\n");

            // raise interrupt to inform about outcome
            this.logf("IOP::HFloppy.processNotify() -> IOCB at 0x{0:X6} processed, raising interrupt 0x{1:X4}\n", iopIocbPtr, intrMask);
            Processes.requestMesaInterrupt(intrMask);

            // forward to next enqueued IOCB
            iopIocbPtr = this.workIocb.nextIOCB.toLP();
            this.logf("IOP::HFloppy.processNotify()  -> iocb.nextIOCB.toLP() = 0x{0:X6}\n", iopIocbPtr);
        }

        this.logf("IOP::HFloppy.processNotify() - done\n\n");
        return true;
    }

    private void dumpWorkIocb(string intro)
    {
        if (!this.logging) { return; }
        this.logf(intro + "\n");
        this.slogf("    - operation: {0}\n", this.workIocb.operation.ToString());
        this.slogf("    - generalizedFDCOperation........: {0}\n", getExtendedFDCcommandTypeName(this.workIocb.generalizedFDCOperation.get()));
        this.slogf("    - dataAddress....................: 0x{0:X6}\n", this.workIocb.dataAddress.toLP());
        this.slogf("    - actualClientCondition.maskValue: 0x{0:X4}\n", this.workIocb.actualClientCondition.maskValue.get());
        this.slogf("    - daDriveNumber..................: {0}\n", this.workIocb.daDriveNumber.get());
        this.slogf("    - incrementDataPointer...........: {0}\n", this.workIocb.incrementDataPointer.get());
        this.slogf("    - numberOfFDCCommands............: {0}\n", this.workIocb.numberOfFDCCommands.get());
        this.slogf("    - currentFDCCommand..............: {0}\n", this.workIocb.currentFDCCommand.get());
        this.dumpFdcCommandRecord("fdcCommands[0]", this.workIocb.fdcCommands0);
        this.dumpFdcCommandRecord("fdcCommands[1]", this.workIocb.fdcCommands1);
        this.dumpFdcCommandRecord("fdcCommands[2]", this.workIocb.fdcCommands2);
        this.dumpTrackDMAandCounterControl("firstTrack", this.workIocb.firstTrack);
        this.dumpTrackDMAandCounterControl("middleTrack", this.workIocb.middleTrack);
        this.dumpTrackDMAandCounterControl("lastTrack", this.workIocb.lastTrack);
    }

    private void dumpTrackDMAandCounterControl(string name, TrackDMAandCounterControl trackCtl)
    {
        this.slogf("    - {0} ::\n", name);
        this.slogf("        -- TotalBytesToTransfer--------: {0}\n", trackCtl.TotalBytesToTransfer.get() & 0xFFFF);
        this.slogf("        -- TotalBytesActuallyTransfered: {0}\n", trackCtl.TotalBytesActuallyTransfered.get() & 0xFFFF);
        this.slogf("        -- CounterControlRegister------: 0x{0:X4}\n", trackCtl.CounterControlRegister.w.get() & 0xFFFF);
        this.slogf("        -- FirstDMAtransferCount-------: {0}\n", trackCtl.FirstDMAtransferCount.get() & 0xFFFF);
        this.slogf("        -- FirstDMAcontrolWord---------: 0x{0:X4}\n", trackCtl.FirstDMAcontrolWord.w.get() & 0xFFFF);
        this.slogf("        -- NumberOfMiddleDMAtransfers--: {0}\n", trackCtl.NumberOfMiddleDMAtransfers.get() & 0xFFFF);
        this.slogf("        -- MiddleDMAtransferCount------: {0}\n", trackCtl.MiddleDMAtransferCount.get() & 0xFFFF);
        this.slogf("        -- MiddleDMAcontrolWord--------: 0x{0:X4}\n", trackCtl.MiddleDMAcontrolWord.w.get() & 0xFFFF);
        this.slogf("        -- LastDMAtransferCount--------: {0}\n", trackCtl.LastDMAtransferCount.get() & 0xFFFF);
        this.slogf("        -- LastDMAcontrolWord----------: 0x{0:X4}\n", trackCtl.LastDMAcontrolWord.w.get() & 0xFFFF);
    }

    private void dumpFdcCommandRecord(string name, FdcCommandRecord cmd)
    {
        this.slogf("    - {0} ::\n", name);
        this.slogf("        -- fdcCode : {0}\n", getExtendedFDCcommandTypeName(cmd.fdcCode.get()));
        this.slogf("        -- dataTransferCode: {0}\n", cmd.DataTransferCode.get());
        this.slogf("        -- anExtryByte: {0:X2}\n", cmd.anExtraByte.get() & 0xFF);
        this.slogf("        -- mustWaitForInterrupt: {0}\n", cmd.MustWaitForInterrupt.get());
        this.slogf("        -- numberOfCommandBytes: {0}\n", cmd.NumberOfCommandBytes.get() & 0xFF);
        this.slogf("        -- numberOfCommandBytesWritten: {0}\n", cmd.NumberOfCommandBytesWritten.get() & 0xFF);
        this.slogf("        -- command bytes: 0x {0:X4} {1:X4} {2:X4} {3:X4} {4:X4}\n",
            cmd.CommandBytes_0_1.get() & 0xFFFF, cmd.CommandBytes_2_3.get() & 0xFFFF, cmd.CommandBytes_4_5.get() & 0xFFFF, cmd.CommandBytes_6_7.get() & 0xFFFF, cmd.CommandBytes_8_9.get() & 0xFFFF);
        this.slogf("        -- numberOfResultBytes: {0}\n", cmd.NumberOfResultBytes.get() & 0xFF);
        this.slogf("        -- numberOfResultBytesWritten: {0}\n", cmd.NumberOfResultBytesRead.get() & 0xFF);
        this.slogf("        -- result bytes: 0x {0:X4} {1:X4} {2:X4} {3:X4}\n",
            cmd.ResultBytes_0_1.get() & 0xFFFF, cmd.ResultBytes_2_3.get() & 0xFFFF, cmd.ResultBytes_4_5.get() & 0xFFFF, cmd.ResultBytes_6_7.get() & 0xFFFF);
    }

    public override void handleLockmem(ushort lockMask, int realAddress, MemOperation memOp, ushort oldValue, ushort newValue)
    {
        // check if it is for us
        if (lockMask != this.fcb.lockMask.get()) { return; }

        this.logf("IOP::HFloppy.handleLockmem(rAddr = 0x{0:X6} , memOp = {1} , oldValue = 0x{2:X4} , newValue = 0x{3:X4}\n",
                realAddress, memOp.ToString(), oldValue, newValue);

        // process the synchronized memory operation
        int ra_queueSemaphore = this.fcb.queueSemaphore.getRealAddress();
        int ra_pilotQueue_queueNext = this.fcb.pilotQueue.queueNext.a15ToA0.ptr.getRealAddress();
        if (realAddress == ra_queueSemaphore)
        {
            this.logf("IOP::HFloppy.handleLockmem() -> fcb.queueSemaphore accessed by head\n");
        }
        else if (realAddress == (ra_pilotQueue_queueNext + 1))
        {
            this.logf("IOP::HFloppy.handleLockmem() -> fcb.pilotQueue.queueNext upper word accessed by head\n");
        }
        else if (realAddress == ra_pilotQueue_queueNext)
        {
            this.logf("IOP::HFloppy.handleLockmem() -> fcb.pilotQueue.queueNext lower word accessed by head\n");
            this.fcb.queueSemaphore.set(0);
        }
        else
        {
            this.logf("IOP::HFloppy.handleLockmem() -> unrecognized access by head ... !!!\n");
        }
    }

    public override void handleLockqueue(int vAddr, int rAddr)
    {
        // not relevant for floppy handler
    }

    public override void shutdown(System.Text.StringBuilder errMsgTarget)
    {
        // save current floppy if present and modified
        if (this.currFloppy != null && this.currFloppy.isChanged())
        {
            this.currFloppy.save(errMsgTarget);
        }
        this.currFloppy = null;
    }

    /*
     * ***** floppy disk management
     */

    private Floppy? nextFloppy = null;
    private bool nextEjected = false;

    private Floppy? currFloppy = null;

    // **Phase F-4b**: insertFloppy currently throws — IMD/DMK format readers
    // are dropped per RISKS R7. A future sub-task may add 1.44 MiB raw support.
    public bool insertFloppy(string filePath, bool readonly_)
    {
        lock (this)
        {
            _ = readonly_;
            throw new NotSupportedException(
                $"HFloppy: floppy formats not supported in C# port (Phase F-4b). " +
                $"Tried to insert: {Path.GetFileName(filePath)}");
        }
    }

    public void ejectFloppy()
    {
        lock (this)
        {
            this.nextFloppy = null;
            this.nextEjected = true;
        }
    }

    private long noMesaFloppyInsertBefore = 0;

    private void mesaEjectFloppy()
    {
        this.currFloppy = null;
        this.nextEjected = false;
        // ensure that the mesa machine has a chance to see that no floppy was (temporarily) inserted
        this.noMesaFloppyInsertBefore = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + 500;

        DeviceContextBlock dcb = this.fcb.dcb[0];
        dcb.driveBusy.set(false);
        dcb.pilotDiskChanged.set(false);
        dcb.doorOpen.set(true);
        dcb.statusRegister3.fault.set(false);
        dcb.statusRegister3.writeProtected.set(true);
        dcb.statusRegister3.ready.set(false);
        dcb.statusRegister3.track0.set(true);
        dcb.statusRegister3.twoSided.set(true);
        dcb.statusRegister3.theHeadAddress.set(0);
    }

    private void mesaInsertFloppy()
    {
        if (this.noMesaFloppyInsertBefore > DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())
        {
            return;
        }
        if (this.nextFloppy == null || this.currFloppy != null)
        {
            return;
        }

        this.currFloppy = this.nextFloppy;
        this.nextFloppy = null;

        DeviceContextBlock dcb = this.fcb.dcb[0];
        dcb.driveBusy.set(false);
        dcb.pilotDiskChanged.set(true);
        dcb.doorOpen.set(false);
        dcb.statusRegister3.fault.set(false);
        dcb.statusRegister3.writeProtected.set(this.currFloppy.isReadonly());
        dcb.statusRegister3.ready.set(true);
        dcb.statusRegister3.track0.set(true);
        dcb.statusRegister3.twoSided.set(true);
        dcb.statusRegister3.theHeadAddress.set(0);
    }

    public override void refreshMesaMemory()
    {
        lock (this)
        {
            if (this.nextFloppy != null || this.nextEjected)
            {
                // save current floppy if present and modified
                if (this.currFloppy != null && this.currFloppy.isChanged())
                {
                    this.currFloppy.save(null);
                }

                // eject the old floppy
                if (this.nextEjected)
                {
                    this.mesaEjectFloppy();
                }

                // load the new floppy
                this.mesaInsertFloppy();
            }
        }
    }

    // Abstract base class for emulated floppy formats. The Java upstream had
    // IMDFloppy and DMKFloppy as concrete subclasses; both are dropped in the
    // C# port. Kept as an abstract hook so a future format implementation can
    // plug into the existing FCB/IOCB/state-machine infrastructure.
    private abstract class Floppy
    {
        protected readonly string filePath;

        // list of tracks, with track: list of sectors, with sector: array of ushort
        protected readonly List<List<ushort[]>> tracks = new();

#pragma warning disable CS0649 // never assigned — concrete IMDFloppy / DMKFloppy / Raw subclasses (deferred per RISKS R7) will set these
        // sector counts in 1st and other cylinders
        protected int cyl0Sectors;
        protected int dataSectors;

        // cylinders in floppy
        protected int cylinders;

        // sector length on tracks 0, 1 and others
        protected int track0WordsPerSector;
        protected int track1WordsPerSector;
        protected int dataWordsPerSector;
#pragma warning restore CS0649

        protected Floppy(string filePath)
        {
            this.filePath = filePath;
        }

        public abstract bool isReadonly();

        public abstract bool isChanged();

        public abstract bool save(System.Text.StringBuilder? errors);

        public int getCyl0Sectors() => this.cyl0Sectors;
        public int getDataSectors() => this.dataSectors;
        public int getCylinders() => this.cylinders;
        public int getTrack0WordsPerSector() => this.track0WordsPerSector;
        public int getTrack1WordsPerSector() => this.track1WordsPerSector;
        public int getDataWordsPerSector() => this.dataWordsPerSector;

        private int nextCyl = 0;
        private int nextHead = 0;
        private int nextSect = 0;

        /* cyl & head: 0-based, sect: 1-based (as in Pilot-DiskAddress) */
        public ushort[]? getSector(int cyl, int head, int sect)
        {
            if (cyl < 0 || cyl >= this.cylinders) { return null; }
            if (head < 0 || head >= 2) { return null; }
            if (sect < 1) { return null; }
            if (cyl == 0)
            {
                if (sect >= this.cyl0Sectors) { return null; }
                this.nextCyl = cyl;
                this.nextHead = head;
                this.nextSect = sect + 1;
            }
            else
            {
                if (sect > this.dataSectors) { return null; }
                this.nextCyl = cyl;
                this.nextHead = head;
                this.nextSect = sect + 1;
                if (this.nextSect > this.dataSectors)
                {
                    this.nextSect = 1;
                    if (this.nextHead == 0)
                    {
                        this.nextHead = 1;
                    }
                    else
                    {
                        this.nextHead = 0;
                        this.nextCyl++;
                    }
                }
            }
            return this.tracks[(cyl * 2) + head][sect - 1];
        }

        public int getNextCyl() => this.nextCyl;
        public int getNextHead() => this.nextHead;
        public int getNextSect() => this.nextSect;
    }
}
