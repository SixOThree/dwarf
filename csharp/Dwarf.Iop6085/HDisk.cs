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

using System.IO.Compression;
using System.Text;
using Dwarf.Engine;
using static Dwarf.Iop6085.IORegion;

namespace Dwarf.Iop6085;

// IOP device handler for the emulated rigid disk of a Daybreak/6085 machine.
//
// Phase F-4 port notes (per DECISIONS.md §8):
//   - Reads canonical pre-merged .zdisk base files (DEFLATE-compressed; the C#
//     port uses `ZLibStream` from System.IO.Compression).
//   - The .zdelta overlay read path is dropped — Java's `-merge` is the only
//     supported way to fold deltas into the base, then the C# port reads that.
//
// Phase H notes (2026-05-13):
//   - C#-native checkpoint format (`.cscheck`) added. On boot, looks for
//     `<name>.zdisk.cscheck` next to the base and applies its sectors on
//     top of the in-memory state. On shutdown, `saveDisk` writes the
//     cumulative dirty sectors to a new `.cscheck`, rotating the prior
//     one to a timestamped backup.
//   - Format is GZipStream-compressed, little-endian, magic "DWCH", version 1.
//     Distinct from Java's `.zdelta` so the formats can't be confused.
//   - `mergeDisks` (-merge CLI mode) folds the checkpoint into the base —
//     produces a new .zdisk and archives the prior base + checkpoints.
public class HDisk : DeviceHandler
{
    /*
     * general constants
     */

    private const int MAX_DISKS = 2; // max. *one* disk??

    private static readonly int[] driveSelectMasks = { 2, 1 }; // must be MAX_DISK long

    private const ushort anyPilotDisk = 64; // Device.Type = [FIRST[Device.PilotDisk]]; -- a Pilot disk of unknown nature

    /*
     * Function Control Block and Device Context Block
     */

    private const string DiskFCB = "DiskFCB";

    // Java's "interface DiskCommand { public static final int ... }" idiom →
    // C# static class with const ints.
    private static class DiskCommand
    {
        public const int goToIdleLoop = 0;
        public const int xferDOBToController = 1;
        public const int executeDOB = 2;
        public const int xferDOBFromController = 3;
    }

    private static string getDiskCommandString(int cmd) => cmd switch
    {
        DiskCommand.goToIdleLoop => "goToIdleLoop",
        DiskCommand.xferDOBToController => "xferDOBToController",
        DiskCommand.executeDOB => "executeDOB",
        DiskCommand.xferDOBFromController => "xferDOBFromController",
        _ => "invalid(" + cmd + ")",
    };

    private sealed class DeviceContextBlock
    {
        public readonly Word mesaHead;        // IOCBPtr
        public readonly Word handlerMesaNext; // IOCBPtr
        public readonly Word mesaTail;        // IOCBPtr
        public readonly IOPBoolean blockMesaQueue;
        public readonly IOPTypes.OpieAddress iopHead;
        public readonly IOPTypes.OpieAddress handlerIOPNext;
        public readonly IOPTypes.OpieAddress iopTail;
        public readonly IOPBoolean blockIOPQueue;
        public readonly Word currentDriveMaskAndDiskCommand;
        public readonly Field currentDriveMask;
        public readonly Field diskCommand; // values from class DiskCommand
        public readonly IOPTypes.ClientCondition mesaClientCondition;
        public readonly IOPTypes.ClientCondition iopClientCondition;
        public readonly IOPTypes.OpieAddress currentIOCB;
        public readonly Word flagsMaskAndDiskState;
        public readonly BoolField recalibrate;
        public readonly BoolField driveExists;
        public readonly BoolField useEcc;
        public readonly Field selectMask;
        public readonly Field diskState;
        public readonly Field dcbOffset;
        public readonly Word devInfo_driveType;
        public readonly Word devInfo_sectorsPerTrackAndheadsPerCylinder;
        public readonly Field devInfo_sectorsPerTrack;
        public readonly Field devInfo_headsPerCylinder;
        public readonly Word devInfo_cylindersPerDrive;
        public readonly Word devInfo_reduceWriteCurrentCylinder;
        public readonly Word devInfo_precompensationCylinder;

        public DeviceContextBlock(string name)
        {
            this.mesaHead = mkWord(name, "mesaHead");
            this.handlerMesaNext = mkWord(name, "handlerMesaNext");
            this.mesaTail = mkWord(name, "mesaTail");
            this.blockMesaQueue = mkIOPBoolean(name, "blockMesaQueue");
            this.iopHead = new IOPTypes.OpieAddress(name, "iopHead");
            this.handlerIOPNext = new IOPTypes.OpieAddress(name, "handlerIOPNext");
            this.iopTail = new IOPTypes.OpieAddress(name, "iopTail");
            this.blockIOPQueue = mkIOPBoolean(name, "blockIOPQueue");
            this.currentDriveMaskAndDiskCommand = mkWord(name, "currentDriveMaskAndDiskCommand");
            this.currentDriveMask = mkField("currentDriveMask", this.currentDriveMaskAndDiskCommand, 0xFF00);
            this.diskCommand = mkField("diskCommand", this.currentDriveMaskAndDiskCommand, 0x00FF);
            this.mesaClientCondition = new IOPTypes.ClientCondition(name, "mesaClientCondition");
            this.iopClientCondition = new IOPTypes.ClientCondition(name, "iopClientCondition");
            this.currentIOCB = new IOPTypes.OpieAddress(name, "currentIOCB");
            this.flagsMaskAndDiskState = mkWord(name, "flagsMaskAndDiskState");
            this.recalibrate = mkBoolField("recalibrate", this.flagsMaskAndDiskState, 0x8000);
            this.driveExists = mkBoolField("driveExists", this.flagsMaskAndDiskState, 0x4000);
            this.useEcc = mkBoolField("useEcc", this.flagsMaskAndDiskState, 0x3000);
            this.selectMask = mkField("selectMask", this.flagsMaskAndDiskState, 0x0F00);
            this.diskState = mkField("diskState", this.flagsMaskAndDiskState, 0x00F0);
            this.dcbOffset = mkField("dcbOffset", this.flagsMaskAndDiskState, 0x000F);
            this.devInfo_driveType = mkWord(name, "devInfo.driveType");
            this.devInfo_sectorsPerTrackAndheadsPerCylinder = mkWord(name, "devInfo_sectorsPerTrackAndheadsPerCylinder");
            this.devInfo_sectorsPerTrack = mkField("devInfo.sectorsPerTrack", this.devInfo_sectorsPerTrackAndheadsPerCylinder, 0xFF00);
            this.devInfo_headsPerCylinder = mkField("devInfo.headsPerCylinder", this.devInfo_sectorsPerTrackAndheadsPerCylinder, 0x00FF);
            this.devInfo_cylindersPerDrive = mkByteSwappedWord(name, "devInfo.cylindersPerDrive");
            this.devInfo_reduceWriteCurrentCylinder = mkByteSwappedWord(name, "devInfo.reduceWriteCurrentCylinder");
            this.devInfo_precompensationCylinder = mkByteSwappedWord(name, "devInfo.precompensationCylinder");
        }
    }

    private sealed class FCB : IORAddress
    {
        private readonly int startAddress;

        public readonly IOPTypes.TaskContextBlock diskTask;
        public readonly IOPTypes.TaskContextBlock diskDMATask;
        public readonly IOPTypes.IOPCondition conditionDMAWork;
        public readonly IOPTypes.IOPCondition conditionDMADone;
        public readonly IOPTypes.IOPCondition conditionWork;
        public readonly IOPTypes.NotifyMask workMask;
        public readonly Word lockMask;
        public readonly IOPBoolean mesaCleanupRequest;
        public readonly IOPBoolean iopCleanupRequest;
        public readonly IOPBoolean handlerStoppedForMesa;
        public readonly IOPBoolean handlerStoppedForIOP;
        public readonly IOPBoolean handlerStoppedForMesaCleanup;
        public readonly IOPBoolean handlerStoppedForIOPCleanup;
        public readonly IOPBoolean startHandlerForMesa;
        public readonly IOPBoolean startHandlerForIOP;
        public readonly Word handlerStatecode;
        public readonly Word clientInfo_currentClientAndclientsToTest;
        public readonly Field clientInfo_currentClient;
        public readonly Field clientInfo_clientsToTest;
        public readonly Word clientInfo_numberOfPossibleClientsAndlastDriveMask;
        public readonly Field clientInfo_numberOfPossibleClients;
        public readonly Field clientInfo_lastDriveMask;
        public readonly IOPTypes.ByteSwappedPointer currentDrivePtr;
        public readonly Word controllerRegisters_word0;
        public readonly Word controllerRegisters_word1;
        public readonly Word driveInfoAndDMAStatus;
        public readonly Word unexpectedDiskInterruptCount;
        public readonly Word unexpectedDiskDMAInterruptCount;
        public readonly DeviceContextBlock dcb0;
        public readonly DeviceContextBlock dcb1;

        public readonly DeviceContextBlock[] dcb;

        public FCB()
        {
            this.startAddress = syncToSegment() + IOR_BASE;

            this.diskTask = new IOPTypes.TaskContextBlock(DiskFCB, "diskTask");
            this.diskDMATask = new IOPTypes.TaskContextBlock(DiskFCB, "diskDMATask");
            this.conditionDMAWork = new IOPTypes.IOPCondition(DiskFCB, "conditionDMAWork");
            this.conditionDMADone = new IOPTypes.IOPCondition(DiskFCB, "conditionDMADone");
            this.conditionWork = new IOPTypes.IOPCondition(DiskFCB, "conditionWork");
            this.workMask = new IOPTypes.NotifyMask(DiskFCB, "workMask");
            this.lockMask = mkWord(DiskFCB, "lockMask");
            this.mesaCleanupRequest = mkIOPBoolean(DiskFCB, "mesaCleanupRequest");
            this.iopCleanupRequest = mkIOPBoolean(DiskFCB, "iopCleanupRequest");
            this.handlerStoppedForMesa = mkIOPBoolean(DiskFCB, "handlerStoppedForMesa");
            this.handlerStoppedForIOP = mkIOPBoolean(DiskFCB, "handlerStoppedForIOP");
            this.handlerStoppedForMesaCleanup = mkIOPBoolean(DiskFCB, "handlerStoppedForMesaCleanup");
            this.handlerStoppedForIOPCleanup = mkIOPBoolean(DiskFCB, "handlerStoppedForIOPCleanup");
            this.startHandlerForMesa = mkIOPBoolean(DiskFCB, "startHandlerForMesa");
            this.startHandlerForIOP = mkIOPBoolean(DiskFCB, "startHandlerForIOP");
            this.handlerStatecode = mkWord(DiskFCB, "handlerState");
            this.clientInfo_currentClientAndclientsToTest = mkWord(DiskFCB, "clientInfo_currentClientAndclientsToTest");
            this.clientInfo_currentClient = mkField("clientInfo.currentClient", this.clientInfo_currentClientAndclientsToTest, 0xFF00);
            this.clientInfo_clientsToTest = mkField("clientInfo.clientsToTest", this.clientInfo_currentClientAndclientsToTest, 0x00FF);
            this.clientInfo_numberOfPossibleClientsAndlastDriveMask = mkWord(DiskFCB, "clientInfo_numberOfPossibleClientsAndlastDriveMask");
            this.clientInfo_numberOfPossibleClients = mkField("clientInfo.numberOfPossibleClients", this.clientInfo_numberOfPossibleClientsAndlastDriveMask, 0xFF00);
            this.clientInfo_lastDriveMask = mkField("clientInfo.lastDriveMask", this.clientInfo_numberOfPossibleClientsAndlastDriveMask, 0x00FF);
            this.currentDrivePtr = new IOPTypes.ByteSwappedPointer(DiskFCB, "currentDrivePtr");
            this.controllerRegisters_word0 = mkWord(DiskFCB, "controllerRegisters_word0");
            this.controllerRegisters_word1 = mkWord(DiskFCB, "controllerRegisters_word1");
            this.driveInfoAndDMAStatus = mkWord(DiskFCB, "driveInfoAndDMAStatus");
            this.unexpectedDiskInterruptCount = mkByteSwappedWord(DiskFCB, "unexpectedDiskInterruptCount");
            this.unexpectedDiskDMAInterruptCount = mkByteSwappedWord(DiskFCB, "unexpectedDiskDMAInterruptCount");
            this.dcb0 = new DeviceContextBlock(DiskFCB + ":dcb[0]");
            this.dcb1 = new DeviceContextBlock(DiskFCB + ":dcb[1]");
            this.dcb = new DeviceContextBlock[] { this.dcb0, this.dcb1 };

            this.workMask.byteMaskAndOffset.set(mkMask());
            this.lockMask.set(mkMask());
        }

        public string getName() => DiskFCB;

        public int getRealAddress() => this.startAddress;
    }

    private enum HandlerState
    {
        normalDiskHandlerState = 0,
        diskControllerNotIdling = 0x100,
        badDiskInterrupt = 0x200,
        badDiskDMAInterrupt = 0x300,
        DMAerror = 0x400,
        resettingDMATask = 0x500,
        resettingDiskTask = 0x600,
        resettingHandler = 0xFFFF,
    }

    /*
     * Input Output Control Block
     */

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

    private sealed class ByteSwappedDiskAddress : IOStruct
    {
        public readonly Word cylinder;
        public readonly Word sectorAndHead;
        public readonly Field head;
        public readonly Field sector;

        public ByteSwappedDiskAddress(IOStruct embeddingParent, string name) : base(embeddingParent, name)
        {
            this.cylinder = mkByteSwappedWord("cylinder");
            this.sectorAndHead = mkWord("headAndSector");
            this.sector = mkField("sector", this.sectorAndHead, 0xFF00);
            this.head = mkField("head", this.sectorAndHead, 0x00FF);

            this.endStruct();
        }
    }

    private sealed class CDF_Operation : IOStruct
    {
        public readonly DiskAddress clientHeader; // address of first sector of request
        public readonly DblWord labelPtr; // LONG POINTER TO Label, -- first label of request. MUST NOT BE NIL
        public readonly DblWord dataPtr; // LONG POINTER, -- first (page aligned) data address of operation
        public readonly Word opInfo;
        public readonly BoolField incrementDataPtr;
        public readonly BoolField enableTrackBuffer;
        public readonly Field command;
        public readonly Field tries;
        public readonly Word pageCount; // sectors remaining for this operation.
        public readonly Word deviceStatus_a;
        public readonly Word deviceStatus_b;
        public readonly DiskAddress diskHeader; // if command.header=Op[read], place the header here.
        public readonly Word device;

        public CDF_Operation(IOStruct embeddingParent, string name) : base(embeddingParent, name)
        {
            this.clientHeader = new DiskAddress(this, "clientHeader");
            this.labelPtr = mkDblWord("labelPtr");
            this.dataPtr = mkDblWord("dataPtr");
            this.opInfo = mkWord("opInfo");
            this.incrementDataPtr = mkBoolField("incrementDataPtr", this.opInfo, 0x8000);
            this.enableTrackBuffer = mkBoolField("enableTrackBuffer", this.opInfo, 0x4000);
            this.command = mkField("command", this.opInfo, 0x3F00);
            this.tries = mkField("tries", this.opInfo, 0x00FF);
            this.pageCount = mkWord("pageCount");
            this.deviceStatus_a = mkWord("deviceStatus.a");
            this.deviceStatus_b = mkWord("deviceStatus.b");
            this.diskHeader = new DiskAddress(this, "diskHeader");
            this.device = mkWord("device");

            this.endStruct();
        }

        public void setDeviceStatus(int status)
        {
            this.deviceStatus_a.set((ushort)(status >>> 16));
            this.deviceStatus_b.set((ushort)(status & 0xFFFF));
        }
    }

    /// <summary>OK :: complete + ready</summary>
    private const int DeviceStatus_OK = unchecked((int)0b0100_0001_0000_0000_0000_0000_0000_0000);

    /// <summary>Failed :: complete + errorDetected + ready + track00 + protocolViolation</summary>
    private const int DeviceStatus_FAILED_protocolViolation = unchecked((int)0b0110_0001_1001_0000_0000_0000_0000_0000);

    /// <summary>Failed :: complete + errorDetected + ready + track00 + sectorNotFound</summary>
    private const int DeviceStatus_FAILED_sectorNotFound = unchecked((int)0b0110_0001_0001_0000_1000_0000_0000_0000);

    /// <summary>Failed :: complete + errorDetected + ready + track00 + labelVerifyError</summary>
    private const int DeviceStatus_FAILED_labelVerifyError = unchecked((int)0b0110_0001_0001_0000_0000_1000_0000_0000);

    /// <summary>Failed :: complete + errorDetected + ready + track00 + dataVerifyError</summary>
    private const int DeviceStatus_FAILED_dataVerifyError = unchecked((int)0b0110_0001_0001_0000_0000_0000_1000_0000);

    /// <summary>Failed :: complete + errorDetected + ready + track00 + illegalCylinder</summary>
    private const int DeviceStatus_FAILED_illegalCylinder = unchecked((int)0b0110_0001_0011_0000_0000_0000_0000_0000);

    private static class ErrorType
    {
        public const ushort noError = 0;

        public const ushort fifoEmptyAtGetCommandBlock = 1;
        public const ushort fifoNotEmpty = 3;
        public const ushort fifoFull = 4;
        public const ushort fifoEmpty = 5;
        public const ushort fifoNotEmptyAtLoadCommandBlock = 7;
        public const ushort lastFIFOError = 0x000F;

        public const ushort firstHeaderError = 0x0010;
        public const ushort headerAddressMarkNotFound = 0x0011;
        public const ushort headerIDError = 0x0012;
        public const ushort headerVerifyError = 0x0013;
        public const ushort headerCRCorECCError = 0x0014;
        public const ushort lastHeaderError = 0x001F;

        public const ushort firstLabelError = 0x0020;
        public const ushort labelAddressMarkNotFound = 0x0021;
        public const ushort labelIDError = 0x0022;
        public const ushort labelVerifyError = 0x0023;
        public const ushort labelCRCorECCError = 0x0024;
        public const ushort labelCRCAndVerifyError = 0x0025;
        public const ushort lastLabelError = 0x002F;

        public const ushort firstDataError = 0x0030;
        public const ushort dataAddressMarkNotFound = 0x0031;
        public const ushort dataIDError = 0x0032;
        public const ushort dataVerifyError = 0x0033;
        public const ushort dataCRCorECCError = 0x0034;
        public const ushort dataCRCOrECCAndVerifyError = 0x0035;
        public const ushort lastDataError = 0x003F;

        public const ushort oldSectorNotFound = 0x0080;
        public const ushort sectorNotFound = 0x0081;
        public const ushort cylinderTooBig = 0x0082;
        public const ushort currentCylinderUnknown = 0x0084;

        public const ushort writeFault = 0x0085;

        public const ushort illegalOperation = 0x008A;
        public const ushort illegalDiagnosticOperation = 0x008B;
        public const ushort protocolViolation = 0x008C;

        public const ushort last = 0x00FF;
    }

    private static class Operation
    {
        public const int restore = 0;
        public const int formatTracks = 1;
        public const int readData = 2;
        public const int writeData = 3;
        public const int writeLabelAndData = 4;
        public const int readLabel = 5;
        public const int readLabelAndData = 6;
        public const int verifyData = 7;
        public const int readDiagnostic = 8;
        public const int readTrack = 16;
        public const int last = 0xFF;
    }

    private static string operationName(int op) => op switch
    {
        Operation.restore => "restore",
        Operation.formatTracks => "formatTracks",
        Operation.readData => "readData",
        Operation.writeData => "writeData",
        Operation.writeLabelAndData => "writeLabelAndData",
        Operation.readLabel => "readLabel",
        Operation.readLabelAndData => "readLabelAndData",
        Operation.verifyData => "verifyData",
        Operation.readDiagnostic => "readDiagnostic",
        Operation.readTrack => "readTrack",
        Operation.last => "last",
        _ => "invalid(" + op + ")",
    };

    private sealed class CDF_Label : IOStruct
    {
        public readonly Word fileID_0;
        public readonly Word fileID_1;
        public readonly Word fileID_2;
        public readonly Word fileID_3;
        public readonly Word fileID_4;
        public readonly Word[] fileID;
        public readonly Word filePageLo;
        public readonly Word filePageHiAndPageZeroAttributes;
        public readonly Field filePageHi;
        public readonly Field pageZeroAttributes;
        public readonly Word attributesInAllPages;
        public readonly Word dontCare0;
        public readonly Word dontCare1;

        public CDF_Label(IOStruct embeddingParent, string name) : base(embeddingParent, name)
        {
            this.fileID_0 = mkWord("fileID[0]");
            this.fileID_1 = mkWord("fileID[1]");
            this.fileID_2 = mkWord("fileID[2]");
            this.fileID_3 = mkWord("fileID[3]");
            this.fileID_4 = mkWord("fileID[4]");
            this.fileID = new Word[] { this.fileID_0, this.fileID_1, this.fileID_2, this.fileID_3, this.fileID_4 };
            this.filePageLo = mkWord("filePageLo");
            this.filePageHiAndPageZeroAttributes = mkWord("filePageHiAndPageZeroAttributes");
            this.filePageHi = mkField("filePageHi", this.filePageHiAndPageZeroAttributes, 0xFF00);
            this.pageZeroAttributes = mkField("pageZeroAttributes", this.filePageHiAndPageZeroAttributes, 0x00FF);
            this.attributesInAllPages = mkWord("attributesInAllPages");
            this.dontCare0 = mkWord("dontCare[0]");
            this.dontCare1 = mkWord("dontCare[1]");

            this.endStruct();
        }

        public override string ToString() =>
            string.Format(
                "Label(fileID[ {0:X4} {1:X4} {2:X4} {3:X4} {4:X4} ], filePage+page0attrs[ {5:X4} {6:X4} ], attrsInAllPages[ {7:X4} ], dontCare[ {8:X4} {9:X4} ])",
                this.fileID_0.get() & 0xFFFF, this.fileID_1.get() & 0xFFFF, this.fileID_2.get() & 0xFFFF, this.fileID_3.get() & 0xFFFF, this.fileID_4.get() & 0xFFFF,
                this.filePageLo.get() & 0xFFFF, this.filePageHiAndPageZeroAttributes.get() & 0xFFFF,
                this.attributesInAllPages.get() & 0xFFFF,
                this.dontCare0.get() & 0xFFFF, this.dontCare1.get() & 0xFFFF);

        public int getPageNo()
        {
            int lo = wordSwapBytes(this.filePageLo.get() & 0xFFFF);
            int pageNo = (this.filePageHi.get() << 16) | lo;
            return pageNo;
        }

        public void setPageNo(int pageNo)
        {
            this.filePageLo.set((ushort)wordSwapBytes(pageNo & 0xFFFF));
            this.filePageHi.set(pageNo >>> 16);
        }
    }

    private sealed class DOB : IOStruct
    {
        public readonly Word eccSyndrome_a;
        public readonly Word eccSyndrome_b;
        public readonly Word negativeSectorCount; // << INTEGER >>
        public readonly Word w3;
        public readonly Field sectorsPerTrack;
        public readonly Word w4;
        public readonly Field headsPerCylinder;
        public readonly Field currentVersion;
        public readonly Word cylindersPerDrive; // << CARDINAL >>
        public readonly Word w6;
        public readonly Field startingSectorOnTrack;
        public readonly Word reducedWriteCylinder; // << CARDINAL >>
        public readonly Word preCompensationCylinder; // << CARDINAL >>
        public readonly Word w9;
        public readonly Field writeEndCount;
        public readonly Word headerError;
        public readonly Word labelError;
        public readonly Word dataError;
        public readonly Word lastError;
        public readonly Word currentCylinder;
        public readonly Word w15;
        public readonly Field eccFlag;
        public readonly ByteSwappedDiskAddress header;
        public readonly Word sectorValid; // <<ARRAY OF BOOLEAN>>
        public readonly Word reserved2;
        public readonly Word driveAndControllerStatus;
        public readonly BoolField notReady;
        public readonly BoolField notSeekCompleted;
        public readonly Field zero;
        public readonly BoolField addressMarkOut;
        public readonly BoolField notStoredIndexMark;
        public readonly BoolField notTrack0;
        public readonly BoolField notWriteFault;
        public readonly BoolField lockDetected;
        public readonly BoolField readDataFound;
        public readonly BoolField notBDone;
        public readonly BoolField fifoEmptyAtRead;
        public readonly BoolField notSPMABit3;
        public readonly BoolField notSPMAMaxCount;
        public readonly BoolField fifoA1B1Same;
        public readonly BoolField fifoEmptySynchonized;
        public readonly BoolField fifoFullSynchonized;
        public readonly Word w21;
        public readonly Field operation;
        public readonly Word negativeFormatTrackCount;
        public readonly CDF_Label label;

        public DOB(IOStruct embeddingParent, string name) : base(embeddingParent, name)
        {
            this.eccSyndrome_a = mkWord("eccSyndrome.a");
            this.eccSyndrome_b = mkWord("eccSyndrome.b");
            this.negativeSectorCount = mkByteSwappedWord("negativeSectorCount");
            this.w3 = mkWord("word3");
            this.sectorsPerTrack = mkField("sectorsPerTrack", this.w3, 0xFF00);
            this.w4 = mkWord("word4");
            this.headsPerCylinder = mkField("headsPerCylinder", this.w4, 0xFF00);
            this.currentVersion = mkField("currentVersion", this.w4, 0x00FF);
            this.cylindersPerDrive = mkByteSwappedWord("cylindersPerDrive");
            this.w6 = mkWord("word6");
            this.startingSectorOnTrack = mkField("startingSectorOnTrack", this.w6, 0xFF00);
            this.reducedWriteCylinder = mkByteSwappedWord("reducedWriteCylinder");
            this.preCompensationCylinder = mkByteSwappedWord("preCompensationCylinder");
            this.w9 = mkWord("word9");
            this.writeEndCount = mkField("writeEndCount", this.w9, 0xFF00);
            this.headerError = mkByteSwappedWord("headerError"); // -> high-byte comes last -> matches ErrorType
            this.labelError = mkByteSwappedWord("labelError");
            this.dataError = mkByteSwappedWord("dataError");
            this.lastError = mkByteSwappedWord("lastError");
            this.currentCylinder = mkByteSwappedWord("currentCylinder");
            this.w15 = mkWord("word5");
            this.eccFlag = mkField("eccFlag", this.w15, 0xFF00);
            this.header = new ByteSwappedDiskAddress(this, "header");
            this.sectorValid = mkByteSwappedWord("sectorValid");
            this.reserved2 = mkWord("reserved2");
            this.driveAndControllerStatus = mkWord("driveAndControllerStatus");
            this.notReady = mkBoolField("notReady", this.driveAndControllerStatus, 0x8000);
            this.notSeekCompleted = mkBoolField("notSeekCompleted", this.driveAndControllerStatus, 0x4000);
            this.zero = mkField("zero", this.driveAndControllerStatus, 0x2000);
            this.addressMarkOut = mkBoolField("addressMarkOut", this.driveAndControllerStatus, 0x1000);
            this.notStoredIndexMark = mkBoolField("notStoredIndexMark", this.driveAndControllerStatus, 0x0800);
            this.notTrack0 = mkBoolField("notTrack0", this.driveAndControllerStatus, 0x0400);
            this.notWriteFault = mkBoolField("notWriteFault", this.driveAndControllerStatus, 0x0200);
            this.lockDetected = mkBoolField("lockDetected", this.driveAndControllerStatus, 0x0100);
            this.readDataFound = mkBoolField("readDataFound", this.driveAndControllerStatus, 0x0080);
            this.notBDone = mkBoolField("notBDone", this.driveAndControllerStatus, 0x0040);
            this.fifoEmptyAtRead = mkBoolField("fifoEmptyAtRead", this.driveAndControllerStatus, 0x0020);
            this.notSPMABit3 = mkBoolField("notSPMABit3", this.driveAndControllerStatus, 0x0010);
            this.notSPMAMaxCount = mkBoolField("notSPMAMaxCount", this.driveAndControllerStatus, 0x0008);
            this.fifoA1B1Same = mkBoolField("fifoA1B1Same", this.driveAndControllerStatus, 0x0004);
            this.fifoEmptySynchonized = mkBoolField("fifoEmptySynchonized", this.driveAndControllerStatus, 0x0002);
            this.fifoFullSynchonized = mkBoolField("fifoFullSynchonized", this.driveAndControllerStatus, 0x0001);
            this.w21 = mkWord("word21");
            this.operation = mkField("operation", this.w21, 0xFF00);
            this.negativeFormatTrackCount = mkByteSwappedWord("negativeFormatTrackCount");
            this.label = new CDF_Label(this, "label");

            this.endStruct();
        }
    }

    private const string DiskIOCB = "DiskIOCB";

    private sealed class IOCB : IOStruct
    {
        // << Operation. >>
        public readonly CDF_Operation op;

        // << Head only information. >>
        public readonly Word mesaNext; // IOCBPtr
        public readonly IOPTypes.OpieAddress iopNext;
        public readonly Word labelOpInfo;
        public readonly Field type;            // {normal, restore, labelFixup}, <<Set by InitIOCB for Poll>>
        public readonly Field labelFixupType;  // {none, readLabel, fixed, verifyErrorExpected}
        public readonly Field labelFixupTry;
        public readonly Word tryNo;            // "try" is reserved in Java...
        public readonly Word command;
        public readonly Word runLength;
        public readonly Word pageLocalization;
        public readonly Word preRestored;      // BOOLEAN
        private readonly Word[] filler = new Word[10];
        public readonly Word useBuffer;        // BOOLEAN
        public readonly Word bufferHit;        // BOOLEAN
        public readonly Word mapEntry;

        // << Handler information. >>
        public readonly IOPTypes.OpieAddress dataPtr;
        public readonly Word flags1;
        public readonly BoolField dataCommandTransfer;           // BOOLEAN
        public readonly BoolField dataCommandDirection_isToMesa; // {fromMesa,toMesa}
        public readonly BoolField incrementDataPtr;              // BOOLEAN
        public readonly BoolField complementDOB;                 // BOOLEAN
        public readonly Field etch_isEtch2;                      // {etch1(0), etch2(1)} <- etch1
        public readonly BoolField useLEDs;                       // BOOLEAN
        public readonly BoolField halt;                          // BOOLEAN
        public readonly BoolField diagnosticCommand;             // BOOLEAN
        public readonly Word pageCount;
        public readonly Word word47B;
        public readonly IOPBoolean stopHandlerOnCompletion;
        public readonly IOPBoolean onlyDOBFromController;
        public readonly Word word50B;
        public readonly IOPBoolean error;
        public readonly IOPBoolean diskOperationBlockError;
        public readonly Word word51B;
        public readonly Field controllerErrorType;
        public readonly Field dmaErrorType;
        public readonly Word word52B;
        public readonly IOPBoolean complete;
        public readonly IOPBoolean inProgress;
        public readonly Word word53B;
        public readonly Field dataTransferDirection;
        public readonly IOPBoolean dmaTimedOut;
        public readonly IOPTypes.OpieAddress nextIOCB;

        // << Handler & Controller information. >>

        // << Controller information. >>
        public readonly DOB dob;

        public IOCB(int @base) : base(@base, DiskIOCB)
        {
            this.op = new CDF_Operation(this, "op");

            this.mesaNext = mkWord("mesaNext");
            this.iopNext = new IOPTypes.OpieAddress(this, "iopNext");
            this.labelOpInfo = mkWord("labelOpInfo");
            this.type = mkField("type", this.labelOpInfo, 0xC000);
            this.labelFixupType = mkField("labelFixupType", this.labelOpInfo, 0x3000);
            this.labelFixupTry = mkField("labelFixupTry", this.labelOpInfo, 0x0FFF);
            this.tryNo = mkWord("try");
            this.command = mkWord("command");
            this.runLength = mkWord("runLength");
            this.pageLocalization = mkWord("pageLocalization");
            this.preRestored = mkWord("preRestored");
            for (int i = 0; i < this.filler.Length; i++) { this.filler[i] = mkWord("filler[" + i + "]"); }
            this.useBuffer = mkWord("useBuffer");
            this.bufferHit = mkWord("bufferHit");
            this.mapEntry = mkWord("mapEntry");

            this.dataPtr = new IOPTypes.OpieAddress(this, "dataPtr");
            this.flags1 = mkWord("flags1");
            this.dataCommandTransfer = mkBoolField("dataCommandTransfer", this.flags1, 0x8000);
            this.dataCommandDirection_isToMesa = mkBoolField("dataCommandDirection_toMesa", this.flags1, 0x0100);
            this.incrementDataPtr = mkBoolField("incrementDataPtr", this.flags1, 0x0080);
            this.complementDOB = mkBoolField("complementDOB", this.flags1, 0x0040);
            this.etch_isEtch2 = mkField("etch_isEtch2", this.flags1, 0x0020);
            this.useLEDs = mkBoolField("useLEDs", this.flags1, 0x0004);
            this.halt = mkBoolField("halt", this.flags1, 0x0002);
            this.diagnosticCommand = mkBoolField("diagnosticCommand", this.flags1, 0x0001);
            this.pageCount = mkByteSwappedWord("pageCount");
            this.word47B = mkWord("word47B");
            this.stopHandlerOnCompletion = mkIOPShortBoolean("stopHandlerOnCompletion", this.word47B, true);
            this.onlyDOBFromController = mkIOPShortBoolean("onlyDOBFromController", this.word47B, false);
            this.word50B = mkWord("word50B");
            this.error = mkIOPShortBoolean("error", this.word50B, true);
            this.diskOperationBlockError = mkIOPShortBoolean("diskOperationBlockError", this.word50B, false);
            this.word51B = mkWord("word51B");
            this.controllerErrorType = mkField("controllerErrorType", this.word51B, 0xFF00);
            this.dmaErrorType = mkField("dmaErrorType", this.word51B, 0x00FF);
            this.word52B = mkWord("word52B");
            this.complete = mkIOPShortBoolean("complete", this.word52B, true);
            this.inProgress = mkIOPShortBoolean("inProgress", this.word52B, false);
            this.word53B = mkWord("word53B");
            this.dataTransferDirection = mkField("dataTransferDirection", this.word53B, 0xFF00);
            this.dmaTimedOut = mkIOPShortBoolean("dmaTimedOut", this.word53B, false);
            this.nextIOCB = new IOPTypes.OpieAddress(this, "nextIOCB");

            this.dob = new DOB(this, "dob");
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
     * implementation of the iop6085 disk interface
     */

    // operation on the label
    public enum VerifyLabelOp
    {
        /// <summary>check disk label against value provided by Pilot (probably expected behaviour)</summary>
        verify,
        /// <summary>if the disk label does not match the value provided by Pilot, update the label on disk</summary>
        updateDisk,
        /// <summary>do not check labels when required by the disk operation (readData, writeData, verifyData)</summary>
        noVerify,
    }

    private readonly FCB fcb;

    private readonly IOCB workIocb = new IOCB(0); // will be rebased to access a specific IOCB

    private readonly VerifyLabelOp labelOpOnRead;
    private readonly VerifyLabelOp labelOpOnWrite;
    private readonly VerifyLabelOp labelOpOnVerify;
    private readonly bool logLabelProblems;

    public HDisk(VerifyLabelOp labelOpOnRead, VerifyLabelOp labelOpOnWrite, VerifyLabelOp labelOpOnVerify, bool logLabelProblems)
        : base(DiskFCB, Config.IO_LOG_DISK)
    {
        this.labelOpOnRead = labelOpOnRead;
        this.labelOpOnWrite = labelOpOnWrite;
        this.labelOpOnVerify = labelOpOnVerify;
        this.logLabelProblems = logLabelProblems;

        // allocate Function Context Block
        this.fcb = new FCB();

        // initialize the fcb and the dcb(s) according to the disk(s) registered up to now
        this.fcb.mesaCleanupRequest.set(false);
        this.fcb.iopCleanupRequest.set(false);
        this.fcb.handlerStoppedForMesa.set(false);
        this.fcb.handlerStoppedForIOP.set(false);
        this.fcb.handlerStoppedForMesaCleanup.set(false);
        this.fcb.handlerStoppedForIOPCleanup.set(false);
        this.fcb.startHandlerForMesa.set(false);
        this.fcb.startHandlerForIOP.set(false);
        this.fcb.handlerStatecode.set(0);
        for (int i = 0; i < MAX_DISKS; i++)
        {
            DeviceContextBlock dcb = this.fcb.dcb[i];
            if (i >= diskFiles.Count)
            {
                dcb.driveExists.set(false);
                dcb.diskState.set(0); // not disk at this index
            }
            else
            {
                DiskFile disk = diskFiles[i];
                dcb.driveExists.set(true);
                dcb.recalibrate.set(false);
                dcb.selectMask.set(driveSelectMasks[i]);
                dcb.useEcc.set(false);
                dcb.diskState.set(3); // ready
                dcb.dcbOffset.set(i * 26); // a DCB is 26 words long
                dcb.devInfo_driveType.set(anyPilotDisk);
                dcb.devInfo_sectorsPerTrack.set(DiskFile.sectorsPerTrack);
                dcb.devInfo_headsPerCylinder.set(disk.headCount);
                dcb.devInfo_cylindersPerDrive.set((ushort)disk.cylCount);
                dcb.devInfo_reduceWriteCurrentCylinder.set((ushort)disk.cylCount);
                dcb.devInfo_precompensationCylinder.set((ushort)disk.cylCount);
            }
        }
    }

    public override int getFcbRealAddress() => this.fcb.getRealAddress();

    public override ushort getFcbSegment() => ((IORAddress)this.fcb).getIOPSegment();

    private static bool dumpMap = false; // for debugging: dump the VM-map for the IO region

    private static int wordSwapBytes(int val)
    {
        val &= 0xFFFF;
        int res = (val << 8) | (val >>> 8);
        return res & 0xFFFF;
    }

    private static bool doAltLogs = false;

    private static void altlogf(string format, params object[] args)
    {
        if (doAltLogs) { Console.Write(string.Format(format, args)); }
    }

    public override bool processNotify(ushort notifyMask)
    {
        // check if it's for us
        if (notifyMask != this.fcb.workMask.byteMaskAndOffset.get())
        {
            return false;
        }

        this.slogf("\n");
        this.logf("IOP::HDisk.processNotify() - begin (insn: {0})\n", Cpu.insns);
        this.logf("IOP::HDisk.processNotify() : currentClient = {0} , clientsToTest = {1}\n",
                this.fcb.clientInfo_currentClient.get(),
                this.fcb.clientInfo_clientsToTest.get());
        this.logf("IOP::HDisk.processNotify() : handlerStoppedForMesaCleanup = {0} , mesaCleanupRequest = {1}\n",
                this.fcb.handlerStoppedForMesaCleanup.get(),
                this.fcb.mesaCleanupRequest.get());
        this.logf("IOP::HDisk.processNotify() : handlerStoppedForMesa = {0} , startHandlerForMesa = {1}\n",
                this.fcb.handlerStoppedForMesa.get(),
                this.fcb.startHandlerForMesa.get());

        if (dumpMap)
        {
            // Mem.dumpVmMap(0, 256) — not ported (debug aid only)
            dumpMap = false;
        }

        // check for cleanup request
        if (this.fcb.mesaCleanupRequest.get())
        {
            this.logf("IOP::HDisk.processNotify() -> mesaCleanupRequest\n");
            this.logf("IOP::HDisk.processNotify() - done\n\n");
            altlogf("IOP::HDisk.processNotify() -> mesaCleanupRequest\n");
            this.fcb.handlerStoppedForMesaCleanup.set(true);
            return true;
        }
        this.fcb.handlerStoppedForMesaCleanup.set(false);

        // handle (re)start handler request
        bool forceProtocolViolation = false;
        if (this.fcb.startHandlerForMesa.get())
        {
            this.fcb.handlerStoppedForMesa.set(false);
            this.fcb.startHandlerForMesa.set(false);
            this.logf("IOP::HDisk.processNotify() -> (re)started handler\n");
        }
        else if (this.fcb.handlerStoppedForMesa.get())
        {
            forceProtocolViolation = true;
            this.logf("IOP::HDisk.processNotify() -> invoked stopped handler -> force protocolViolation on all IOCBs\n");
        }
        this.fcb.handlerStatecode.set(0); // = normalDiskHandlerState
        this.fcb.unexpectedDiskInterruptCount.set(0);
        this.fcb.unexpectedDiskDMAInterruptCount.set(0);

        // process IOCBs for the disks
        for (int dcbNo = 0; dcbNo < MAX_DISKS; dcbNo++)
        {
            if (!this.fcb.dcb[dcbNo].driveExists.@is()) { continue; } // disk not present

            DiskFile? disk = (dcbNo < diskFiles.Count) ? diskFiles[dcbNo] : null;
            if (disk == null)
            {
                continue; // this disk is not present...
            }

            // process IOCBs registered for this drive
            int iocbPtr = this.fcb.dcb[dcbNo].mesaHead.get() & 0xFFFF;
            int iopIocbPtr = this.fcb.dcb[dcbNo].iopHead.toLP();
            bool allowInterrupt = false;

            this.logf("IOP::HDisk.processNotify() -> dcb {0} :: mesaHead: 0x{1:X8} , iopHead: 0x{2:X8} , recalibrate: {3,5} , diskCommand: {4}\n",
                    dcbNo, iocbPtr, iopIocbPtr, this.fcb.dcb[dcbNo].recalibrate.@is(), getDiskCommandString(this.fcb.dcb[dcbNo].diskCommand.get()));
            altlogf("\nIOP::HDisk.processNotify() -> dcb {0} :: mesaHead: 0x{1:X8} , iopHead: 0x{2:X8} , recalibrate: {3,5} , diskCommand: {4}\n",
                    dcbNo, iocbPtr, iopIocbPtr, this.fcb.dcb[dcbNo].recalibrate.@is(), getDiskCommandString(this.fcb.dcb[dcbNo].diskCommand.get()));

            while (iocbPtr != 0)
            {
                iocbPtr = wordSwapBytes(iocbPtr);
                this.logf("IOP::HDisk.processNotify() -> dcb {0}, processing IOCB 0x{1:X8}\n", dcbNo, iocbPtr);

                allowInterrupt = true; // as we have seen an IOCB

                this.workIocb.rebaseToVirtualAddress(iocbPtr);

                // prepare error state members
                bool failed = false;
                this.workIocb.dmaTimedOut.set(false);
                this.workIocb.dob.headerError.set(ErrorType.noError);
                this.workIocb.dob.labelError.set(ErrorType.noError);
                this.workIocb.dob.dataError.set(ErrorType.noError);
                this.workIocb.dob.lastError.set(ErrorType.noError);
                this.workIocb.dob.driveAndControllerStatus.set(0);
                this.workIocb.diskOperationBlockError.set(false);
                this.workIocb.op.setDeviceStatus(DeviceStatus_OK);

                // check for invoking the handler while stopped
                if (forceProtocolViolation)
                {
                    this.workIocb.dob.lastError.set(ErrorType.protocolViolation);
                    this.workIocb.diskOperationBlockError.set(true);
                    this.workIocb.op.setDeviceStatus(DeviceStatus_FAILED_protocolViolation);

                    this.workIocb.inProgress.set(false);
                    this.workIocb.complete.set(true);
                    this.workIocb.error.set(true);
                    this.workIocb.diskOperationBlockError.set(false);
                    this.logf("IOP::HDisk.processNotify() -> handler invoked while stopped, aborting IOCB with error: protocolViolation !!!\n");

                    iocbPtr = this.workIocb.mesaNext.get() & 0xFFFF;
                    iopIocbPtr = this.workIocb.nextIOCB.toLP();
                    this.logf("IOP::HDisk.processNotify() -> mesaNext: 0x{0:X8} , iopNext: 0x{1:X8}\n", iocbPtr, iopIocbPtr);
                    continue;
                }

                // get operation data
                int vDataPtr = this.workIocb.dataPtr.toLP();
                int pageCount = this.workIocb.pageCount.get();
                int negativeSectorCount = this.workIocb.dob.negativeSectorCount.get();
                bool incrementDataPtr = this.workIocb.incrementDataPtr.@is();
                int operation = this.workIocb.dob.operation.get();
                this.logf("IOP::HDisk.processNotify()    -> command = {0} , dataPtr = 0x{1:X8} , pageCount = {2} , negativeSectorCount = {3} , incrementDataPtr = {4}\n",
                        operationName(operation), vDataPtr, pageCount, negativeSectorCount, incrementDataPtr);

                // special case 'recalibrate'
                if (operation == Operation.restore)
                {
                    altlogf(">>>>> HDisk->restore (recalibrate) >>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>\n");
                    this.logf("IOP::HDisk.processNotify()    -> restore (recalibrate) => ignored (iocb complete, no error)\n");
                    this.workIocb.complete.set(true);
                    this.workIocb.error.set(false);
                    iocbPtr = this.workIocb.mesaNext.get() & 0xFFFF;
                    iopIocbPtr = this.workIocb.nextIOCB.toLP();
                    this.logf("IOP::HDisk.processNotify() -> mesaNext: 0x{0:X8} , iopNext: 0x{1:X8}\n", iocbPtr, iopIocbPtr);
                    continue;
                }

                // get disk coordinates to work with
                int cyl = this.workIocb.dob.header.cylinder.get() & 0xFFFF;
                int head = this.workIocb.dob.header.head.get();
                int sector = this.workIocb.dob.header.sector.get();
                bool useBuffer = this.workIocb.useBuffer.get() != 0;
                int absSectorIdx;
                int lastCylSectIdx;
                try
                {
                    absSectorIdx = disk.getLinearSector(cyl, head, sector);
                    lastCylSectIdx = disk.getLinearSector(cyl, disk.headCount - 1, disk.sectorsPerTrack_inst - 1);
                }
                catch (Exception)
                {
                    this.logf("IOP::HDisk.processNotify()    -> cylinder = {0} , head = {1} , sector = {2} , absSectorIdx = INVALID , useBuffer = {3}, ABORTING\n",
                            cyl, head, sector, useBuffer);

                    this.workIocb.dob.lastError.set(ErrorType.sectorNotFound);
                    this.workIocb.diskOperationBlockError.set(true);
                    this.workIocb.op.setDeviceStatus(DeviceStatus_FAILED_sectorNotFound);

                    this.workIocb.error.set(true);
                    this.workIocb.complete.set(true);

                    iocbPtr = this.workIocb.mesaNext.get() & 0xFFFF;
                    continue; // try with next IOCB
                }
                int lastAbsSectIdx = absSectorIdx;
                this.logf("IOP::HDisk.processNotify()    -> cylinder = {0} , head = {1} , sector = {2} , absSectorIdx = {3}, useBuffer = {4}\n",
                        cyl, head, sector, absSectorIdx, useBuffer);

                // disallow track-buffer usage
                if (useBuffer)
                {
                    this.logf("IOP::HDisk.processNotify() -> IOCB.useBuffer is not supported, aborting IOCB !!!\n");

                    this.workIocb.bufferHit.set(0);
                    this.workIocb.diskOperationBlockError.set(true);
                    this.workIocb.dob.lastError.set(ErrorType.illegalOperation);
                    this.workIocb.op.setDeviceStatus(DeviceStatus_FAILED_protocolViolation);
                    this.workIocb.error.set(true);
                    this.workIocb.complete.set(true);

                    iocbPtr = this.workIocb.mesaNext.get() & 0xFFFF;
                    continue; // try with next IOCB
                }

                // handle operation requested
                string opName = operationName(operation);
                altlogf(">> HDisk->{0}: absSectorIdx = {1} [ {2} / {3} / {4} ] , pageCount = {5} , incrDataPtr = {6} , vMem = [ 0x{7:X6} .. 0x{8:X6} )\n",
                        opName, absSectorIdx, cyl, head, sector, pageCount, incrementDataPtr, vDataPtr, vDataPtr + (pageCount * 256));
                switch (operation)
                {
                    // general disk i/o operations with the following functions on the sector-components:
                    case Operation.readData:            // header: verify, label: verify, data: read
                    case Operation.readLabel:           // header: verify, label: read,   data: -
                    case Operation.readLabelAndData:    // header: verify, label: read,   data: read
                    case Operation.writeData:           // header: verify, label: verify, data: write
                    case Operation.writeLabelAndData:   // header: verify, label: write,  data: write
                    case Operation.verifyData:          // header: verify, label: verify, data: verify
                    {
                        altlogf("   -- at start:  {0}\n", this.workIocb.dob.label.ToString());

                        // save possibly changed label data for subsequent restore
                        ushort lo = this.workIocb.dob.label.filePageLo.get();
                        ushort hi = this.workIocb.dob.label.filePageHiAndPageZeroAttributes.get();
                        bool restoreLabelPage = false;

                        int labelPageNoBase = this.workIocb.dob.label.getPageNo();
                        int currPageIdx = 0;

                        // process the sector components for the requested number of sectors
                        while (pageCount > 0)
                        {
                            // check for a (still) valid disk position
                            if (absSectorIdx >= disk.sectorCount)
                            {
                                this.workIocb.dob.lastError.set(ErrorType.sectorNotFound);
                                this.workIocb.diskOperationBlockError.set(true);
                                this.workIocb.op.setDeviceStatus(DeviceStatus_FAILED_sectorNotFound);

                                Console.Write("!! sector index out of range\n");

                                this.logf("IOP::HDisk.processNotify() -> {0}: sectorNotFound\n", operationName(operation));
                                failed = true;
                                break;
                            }

                            // set the label page number for label verify or write
                            this.workIocb.dob.label.setPageNo(labelPageNoBase + currPageIdx);

                            if (operation == Operation.readData)
                            {
                                this.reads++;
                                failed = this.doLabelVerification(disk, absSectorIdx, this.labelOpOnRead, "readData", cyl, head, sector);
                                disk.readSectorData(absSectorIdx, vDataPtr);
                            }
                            else if (operation == Operation.readLabel)
                            {
                                this.reads++;
                                disk.readSectorLabel(absSectorIdx, this.workIocb.dob.label);
                            }
                            else if (operation == Operation.readLabelAndData)
                            {
                                this.reads++;
                                disk.readSectorLabel(absSectorIdx, this.workIocb.dob.label);
                                disk.readSectorData(absSectorIdx, vDataPtr);
                            }
                            else if (operation == Operation.writeData)
                            {
                                this.writes++;
                                failed = this.doLabelVerification(disk, absSectorIdx, this.labelOpOnWrite, "writeData", cyl, head, sector);
                                disk.writeSectorData(absSectorIdx, vDataPtr);
                            }
                            else if (operation == Operation.writeLabelAndData)
                            {
                                this.writes++;
                                disk.writeSectorLabel(absSectorIdx, this.workIocb.dob.label);
                                disk.writeSectorData(absSectorIdx, vDataPtr);
                            }
                            else // this can only be: Operation.verifyData
                            {
                                this.reads++;
                                failed = this.doLabelVerification(disk, absSectorIdx, this.labelOpOnVerify, "verifyData", cyl, head, sector);

                                if (!disk.verifySectorData(absSectorIdx, vDataPtr))
                                {
                                    this.workIocb.dob.labelError.set(ErrorType.dataVerifyError);
                                    this.workIocb.dob.lastError.set(ErrorType.dataVerifyError);
                                    this.workIocb.diskOperationBlockError.set(true);

                                    this.logf("IOP::HDisk.processNotify() -> verifyData-error: dataVerifyError\n");
                                    failed = true;
                                }
                            }

                            // move to next sector
                            disk.linearToDiskAddress(absSectorIdx, this.workIocb.dob.header);
                            currPageIdx++;
                            absSectorIdx++;
                            pageCount--;
                            negativeSectorCount++;
                            if (incrementDataPtr)
                            {
                                vDataPtr += PrincOpsDefs.WORDS_PER_PAGE;
                                this.workIocb.dataPtr.fromLP(vDataPtr);
                            }
                        }

                        altlogf("   -- at end  :  {0}\n\n", this.workIocb.dob.label.ToString());

                        // restore possibly changed label data
                        if (restoreLabelPage)
                        {
                            this.workIocb.dob.label.filePageLo.set(lo);
                            this.workIocb.dob.label.filePageHiAndPageZeroAttributes.set(hi);
                        }

                        break;
                    }

                    // format operation, never seen so far...
                    case Operation.formatTracks:
                    {
                        this.logf("IOP::HDisk.processNotify() -> formatTracks: negativeFormatTrackCount = {0}\n",
                                this.workIocb.dob.negativeFormatTrackCount.get() & 0xFFFF);

                        if (cyl >= disk.cylCount)
                        {
                            this.workIocb.dob.lastError.set(ErrorType.cylinderTooBig);
                            this.workIocb.diskOperationBlockError.set(true);
                            this.workIocb.op.setDeviceStatus(DeviceStatus_FAILED_illegalCylinder);

                            this.logf("IOP::HDisk.processNotify() -> formatTracks-error: cylinderTooBig\n");
                            failed = true;
                            break;
                        }

                        // ?? trackCount: really tracks or whole cylinders ??
                        int remainingTracks = 0 - (this.workIocb.dob.negativeFormatTrackCount.get() & 0xFFFF);
                        int currCyl = cyl;
                        int currHead = 0;
                        int linearSector = disk.getLinearSector(currCyl, 0, 0);
                        ushort[] zeros = new ushort[256]; // should be allocated as all zeros, used for label and data in sector
                        while (remainingTracks > 0 && currCyl < disk.cylCount)
                        {
                            for (int sect = 0; sect < DiskFile.sectorsPerTrack; sect++)
                            {
                                disk.writeSectorLabelAndDataRaw(linearSector++, zeros, zeros);
                            }
                            remainingTracks--;
                            currHead++;
                            if (currHead >= disk.headCount)
                            {
                                currCyl++;
                                currHead = 0;
                            }
                        }

                        this.workIocb.dob.negativeFormatTrackCount.set((ushort)(0 - remainingTracks));
                        if (remainingTracks > 0)
                        {
                            this.workIocb.dob.lastError.set(ErrorType.cylinderTooBig);
                            this.workIocb.diskOperationBlockError.set(true);
                            this.workIocb.op.setDeviceStatus(DeviceStatus_FAILED_illegalCylinder);

                            this.logf("IOP::HDisk.processNotify() -> formatTracks-error: cylinderTooBig\n");
                            failed = true;
                        }

                        this.logf("IOP::HDisk.processNotify() -> formatTracks done\n");
                        break;
                    }

                    case Operation.readDiagnostic:
                        throw new ArgumentException("HDisk::HDisk.processNotify() -> Operation.readDiagnostic not supported");

                    case Operation.readTrack:
                        throw new ArgumentException("HDisk::HDisk.processNotify() -> Operation.readTrack not supported");

                    default:
                        throw new ArgumentException("HDisk::HDisk.processNotify() -> Operation ( invalid code = " + operation + " ) not supported");
                }

                // update status/counter fields in IOCB
                this.workIocb.complete.set(true);
                this.workIocb.error.set(failed);
                this.workIocb.pageCount.set((ushort)pageCount);
                this.workIocb.dob.negativeSectorCount.set((ushort)negativeSectorCount);
                this.workIocb.dob.currentCylinder.set((ushort)(((absSectorIdx > lastCylSectIdx) ? cyl + 1 : cyl) & 0xFFFF));
                this.workIocb.dob.sectorValid.set(0xFFFF); // all sectors are valid...
                this.workIocb.dob.notReady.set(false);
                this.workIocb.dob.notSeekCompleted.set(false);
                this.workIocb.dob.addressMarkOut.set(true);     // << Address Mark Detected >>
                this.workIocb.dob.notStoredIndexMark.set(false);
                this.workIocb.dob.notTrack0.set(cyl != 0 || head != 0);
                this.workIocb.dob.notWriteFault.set(true);
                this.workIocb.dob.lockDetected.set(false);
                this.workIocb.dob.readDataFound.set(true);
                this.workIocb.dob.notBDone.set(false);
                this.workIocb.dob.fifoEmptyAtRead.set(false);
                this.workIocb.dob.notSPMABit3.set(false);
                this.workIocb.dob.notSPMAMaxCount.set(false);
                this.workIocb.dob.fifoA1B1Same.set(false);
                this.workIocb.dob.fifoEmptySynchonized.set(false);
                this.workIocb.dob.fifoFullSynchonized.set(false);

                this.logf("IOP::HDisk.processNotify() -> completed IOCB, error = {0} [ pageCount = {1} , negativesectorCount = {2} , dataPtr = 0x{3:X6} ]\n",
                        failed, this.workIocb.pageCount.get(), this.workIocb.dob.negativeSectorCount.get(), this.workIocb.dataPtr.toLP());

                // possibly stop the handler
                if (failed || this.workIocb.stopHandlerOnCompletion.get())
                {
                    this.fcb.handlerStoppedForMesa.set(true);
                    this.logf("IOP::HDisk.processNotify() -> stopped (cause: {0})\n", failed ? "failed" : "workIocb.stopHandlerOnCompletion");
                }

                // done with this IOCB, get next IOCB in list
                iocbPtr = this.workIocb.mesaNext.get() & 0xFFFF;
                iopIocbPtr = this.workIocb.iopNext.toLP();
                this.logf("IOP::HDisk.processNotify() -> mesaNext: 0x{0:X8} , iopNext: 0x{1:X8}\n", iocbPtr, iopIocbPtr);
            }

            // raise interrupt to inform Pilot about the end of the i/o-operations for this FCB
            if (allowInterrupt)
            {
                ushort intrMask = this.fcb.dcb[dcbNo].mesaClientCondition.maskValue.get();
                this.logf("IOP::HDisk.processNotify() -> dcb {0} processed, raising interrupt 0x{1:X4}\n", dcbNo, intrMask);
                Processes.requestMesaInterrupt(intrMask);
            }
        }

        // done
        this.logf("IOP::HDisk.processNotify() - done\n\n");
        return true;
    }

    private bool /* label verify failed */ doLabelVerification(DiskFile disk, int absSectorIdx, VerifyLabelOp op, string diskOperation, int cyl, int head, int sector)
    {
        if (!disk.verifySectorLabel(absSectorIdx, this.workIocb.dob.label))
        {
            if (op == VerifyLabelOp.verify || absSectorIdx == 0)
            {
                this.workIocb.dob.labelError.set(ErrorType.labelVerifyError);
                this.workIocb.dob.lastError.set(ErrorType.labelVerifyError);
                this.workIocb.diskOperationBlockError.set(true);

                if (this.logLabelProblems)
                {
                    Console.Write(string.Format("!! {0}-error: labelVerifyError on absSectorIdx = {1} [ {2} / {3} / {4} ]\n", diskOperation, absSectorIdx, cyl, head, sector));
                    Console.Write(string.Format("   -- expected:  {0}\n", this.workIocb.dob.label.ToString()));
                    Console.Write(string.Format("   -- sect-lbl:  {0}\n", disk.getLabelString(absSectorIdx)));
                }
                this.logf("IOP::HDisk.processNotify() -> {0}-error: labelVerifyError\n", diskOperation);

                return true;
            }
            else if (op == VerifyLabelOp.updateDisk)
            {
                if (this.logLabelProblems)
                {
                    Console.Write(string.Format("!! {0}: updating label on absSectorIdx = {1} [ {2} / {3} / {4} ]\n", diskOperation, absSectorIdx, cyl, head, sector));
                    Console.Write(string.Format("   --  old(disk):  {0}\n", disk.getLabelString(absSectorIdx)));
                    Console.Write(string.Format("   -- new(Pilot):  {0}\n", this.workIocb.dob.label.ToString()));
                }
                disk.writeSectorLabel(absSectorIdx, this.workIocb.dob.label);
            }
        }

        return false;
    }

    public override void handleLockmem(ushort lockMask, int realAddress, MemOperation memOp, ushort oldValue, ushort newValue)
    {
        // disks do not synchronize access to fcb data (queue or the like) ?
        if (lockMask == this.fcb.lockMask.get())
        {
            this.logf("IOP::HDisk.handleLockmem() ... ??\n");
        }
    }

    public override void handleLockqueue(int vAddr, int rAddr)
    {
        // not relevant for disk handler
        this.logf("IOP::HDisk.handleLockqueue() ... ??\n");
    }

    public override void refreshMesaMemory()
    {
        // disk operations are synchronous, so nothing to transfer to/from mesa memory at synchronization points
    }

    /*
     * disk file management functions for the main program
     */

    private static readonly List<DiskFile> diskFiles = new();

    // add disk file
    public static bool addFile(string filePath, bool readonly_, int deltasToKeep, StringBuilder sb)
    {
        if (diskFiles.Count >= MAX_DISKS)
        {
            logWarning(sb, "IOP::HDisk.addFile :: only {0} disk currently supported, ignored disk file: {1}", MAX_DISKS, filePath);
            return false;
        }

        if (!File.Exists(filePath) && !readonly_)
        {
            Cpu.ERROR("IOP::HDisk.addFile :: file not found or not writable: " + filePath);
        }

        // create DiskFile and append to files
        try
        {
            if (Config.IO_LOG_DISK)
            {
                Cpu.logInfo("IOP::HDisk.addFile :: adding file '" + filePath + "'");
            }
            DiskFile diskfile = new DiskFile(filePath, readonly_, deltasToKeep);
            diskFiles.Add(diskfile);
            return true;
        }
        catch (DiskFileCorrupted)
        {
            logWarning(sb, "delta file corrupt, loaded disk possibly unusable");
            return false;
        }
    }

    // shutdown => save disk file(s) — Phase F-4: no-op (writes are in-memory only)
    public override void shutdown(StringBuilder errMsgTarget)
    {
        this.logf("shutdown\n");
        foreach (DiskFile f in diskFiles)
        {
            f.saveDisk(errMsgTarget);
        }
    }

    // merge disk base+delta files — Phase F-4: not supported (use Java -merge)
    public static void mergeDisks(TextWriter ps)
    {
        foreach (DiskFile df in diskFiles)
        {
            ps.WriteLine($"mergeDisks not supported in C# port. Run Java `dwarf.jar -merge` instead. (disk: {df.fileName})");
        }
    }

    public static int getAbsSectNo(int diskIdx, int cyl, int head, int sector)
    {
        if (diskIdx < 0 || diskIdx >= diskFiles.Count)
        {
            throw new ArgumentException("Invalid disk index given");
        }
        DiskFile df = diskFiles[diskIdx];
        return df.getLinearSector(cyl, head, sector);
    }

    public static void rawRead(int diskIdx, int absSector, ushort[] label, ushort[] data)
    {
        if (diskIdx < 0 || diskIdx >= diskFiles.Count)
        {
            throw new ArgumentException("Invalid disk index given");
        }
        DiskFile df = diskFiles[diskIdx];
        df.readSectorLabelAndDataRaw(absSector, label, data);
    }

    private static void logWarning(StringBuilder sb, string pattern, params object[] args)
    {
        string msg = (args.Length > 0) ? string.Format(pattern, args) : pattern;
        Cpu.logError(msg);
        sb.Append(msg).Append('\n');
    }

    /*
     * ***** in-memory representation of a 6085/Daybreak disk
     */

    public class DiskFileCorrupted : Exception
    {
        public DiskFileCorrupted() : base() { }
        public DiskFileCorrupted(string msg) : base(msg) { }
    }

    private sealed class DiskFile
    {
        // structure of an externally stored disk file:
        // - header: 6 words (#cylinder/#heads/16 <-> #totalSectorCount must match!):
        //     signature1 , #heads , #cylinder , #totalSectorCount(dbl-word) , signature2
        // - sectors in ascending order by cylinder,head,sector
        //     1 dbl-word linear sector-pos , 10 word header , 256 words data
        // (all compressed as a zlib stream, all (dbl-)words as big-endian)
        // (same format for full/delta files: a full-file has all sectors, a delta only the changed sectors)

        private const int signature1 = 0xDAAD;
        private const int signature2 = 0x5CC5;

        public const int wordsForSectorLabel = 10;
        public const int wordsPerSectorData = 256;
        public const int wordsPerSector = wordsForSectorLabel + wordsPerSectorData;

        public const int offsetLabel = 0;
        public const int offsetData = wordsForSectorLabel;

        public const int sectorsPerTrack = 16;
        public const int maxHeadCount = 16;
        public const int wordsPerTrack = sectorsPerTrack * wordsPerSector;

        public const int minCylCount = 40;

        public const string EXT_ZDISK = ".zdisk";
        public const string EXT_DELTA = ".zdelta";
        public const string EXT_TEMP_DELTA = ".temp_zdelta";

        // C#-native checkpoint format (Phase H). Lives alongside the .zdisk
        // base file as <name>.zdisk.cscheck. Cumulative — represents all
        // changes since the base. See DECISIONS.md §8 + csharp/MIGRATION.md
        // for the format spec.
        public const string EXT_CHECKPOINT = ".cscheck";
        public const string EXT_CHECKPOINT_TEMP = ".cscheck.tmp";

        // .cscheck format constants
        // Magic bytes: 'D' 'W' 'C' 'H' (Dwarf C# CHeckpoint). Read as a uint32 LE
        // from the file, the value is 0x48435744 (= "HCWD" reversed since LE).
        private const uint CHECKPOINT_MAGIC = 0x48435744;
        private const ushort CHECKPOINT_VERSION = 1;
        // Flag bit 0: 1 = Draco (266-word sectors with labels). 0 = Duchess (256-word sectors).
        private const ushort CHECKPOINT_FLAG_DRACO = 0x0001;

        // the file information for this emulated disk
        public readonly string fileName;
        private readonly string filePath;
        private readonly bool readonly_;
        private readonly int deltasToKeep;

        // constant disk geometry
        public readonly int cylCount;
        public readonly int headCount;
        public readonly int sectorsPerCyl;
        private readonly int wordsPerCylinder;
        public readonly int sectorCount;

        // instance field with the same value as the static `sectorsPerTrack` const
        // (Java upstream accesses `disk.sectorsPerTrack` — keep that access pattern available)
        public int sectorsPerTrack_inst => sectorsPerTrack;

        // disk content
        private readonly ushort[][] sectors; // for each sector: label + data

        // Per-sector dirty flags. Cumulative since the base was loaded —
        // includes both this-session writes and sectors restored from a
        // .cscheck overlay at boot. saveCheckpoint writes every sector
        // whose flag is true.
        private readonly bool[] sectorsChanged;

        private bool changed = false; // has the disk been changed at all? (any sectorsChanged[] bit set)

        // temp sector content buffer for persistence i/o
        private readonly byte[] sectorBuffer = new byte[wordsPerSector * 2];

        private void logf(string template, params object[] args)
        {
            if (Config.IO_LOG_DISK)
            {
                Console.Write(string.Format("DiskFile[" + this.fileName + "]: " + template, args));
            }
        }

        private void logf(StringBuilder sb, string template, params object[] args)
        {
            string msg = string.Format(template, args);
            if (sb.Length > 0) { sb.Append('\n'); }
            sb.Append(msg);
            this.logf(msg);
        }

        // open an existing disk
        //
        // Phase F-4 read-only behavior preserved: reads the canonical .zdisk
        // base (Java upstream's `.zdelta` overlay is intentionally not read).
        //
        // Phase H (2026-05-13): additionally looks for a C#-native `.cscheck`
        // overlay alongside the base. If present, applies it on top of the
        // base sectors and pre-marks those sectors as dirty so the next
        // saveCheckpoint preserves them.
        //
        // `deltasToKeep` is reused as the checkpoint rotation count (default
        // 5) — Java upstream's config key was `oldDeltasToKeep`.
        public DiskFile(string filePath, bool readonly_, int deltasToKeep)
        {
            this.filePath = filePath;
            this.fileName = Path.GetFileName(filePath);
            this.readonly_ = readonly_;
            this.deltasToKeep = deltasToKeep;

            try
            {
                // read full base file (.zdisk)
                using (FileStream fis = new(filePath, FileMode.Open, FileAccess.Read))
                using (ZLibStream iis = new(fis, CompressionMode.Decompress))
                {
                    int sig1 = readWord(iis);
                    int heads = readWord(iis);
                    int cyls = readWord(iis);
                    int sects = (readWord(iis) << 16) | readWord(iis);
                    int sig2 = readWord(iis);
                    int expectedSects = cyls * heads * sectorsPerTrack;
                    if (sig1 != signature1 || cyls < minCylCount || sects != expectedSects || sig2 != signature2)
                    {
                        throw new DiskFileCorrupted();
                    }

                    this.cylCount = cyls;
                    this.headCount = heads;
                    this.sectorsPerCyl = sectorsPerTrack * this.headCount;
                    this.wordsPerCylinder = wordsPerTrack * this.headCount;
                    this.sectorCount = sects;
                    this.sectors = new ushort[this.sectorCount][];
                    for (int i = 0; i < this.sectorCount; i++)
                    {
                        this.sectors[i] = new ushort[wordsPerSector];
                    }
                    this.sectorsChanged = new bool[this.sectorCount];

                    this.readDiskFile(iis, false);
                }

                // Phase H: apply C#-native .cscheck overlay if present
                this.tryApplyCheckpoint();
            }
            catch (IOException)
            {
                throw new DiskFileCorrupted();
            }
        }

        private void readRawSector(Stream i)
        {
            int pos = 0;
            int remaining = this.sectorBuffer.Length;
            while (remaining > 0)
            {
                int bytesRead = i.Read(this.sectorBuffer, pos, remaining);
                if (bytesRead == 0)
                {
                    throw new DiskFileCorrupted();
                }
                remaining -= bytesRead;
                pos += bytesRead;
            }
        }

        private void readDiskFile(Stream i, bool isDelta)
        {
            Console.Write(string.Format("reading {0} disk file: ", isDelta ? "delta" : "base"));
            int currSectNo = 0;
            int totalBytes = 12; // disk file prefix size
            while (true)
            {
                // get the absolute sector number
                int absSector;
                try
                {
                    absSector = (readWord(i) << 16) | readWord(i);
                }
                catch (DiskFileCorrupted)
                {
                    break;
                }
                if (absSector < 0 || absSector > this.sectorCount)
                {
                    throw new DiskFileCorrupted();
                }
                if (isDelta)
                {
                    this.changed = true;
                }

                // get the sector content (label + data)
                ushort[] rawSector = this.sectors[absSector];
                try
                {
                    this.readRawSector(i);
                    totalBytes += this.sectorBuffer.Length;
                }
                catch (IOException)
                {
                    throw new DiskFileCorrupted();
                }
                int b = 0;
                for (int w = 0; w < wordsPerSector; w++)
                {
                    int b1 = this.sectorBuffer[b++] & 0x00FF;
                    int b2 = this.sectorBuffer[b++] & 0x00FF;
                    rawSector[w] = (ushort)((b1 << 8) | b2);
                }
                currSectNo++;
            }
            Console.Write(string.Format("loaded {0} bytes for {1} sectors\n", totalBytes, currSectNo));
        }

        private static int readWord(Stream i)
        {
            try
            {
                int b1 = i.ReadByte();
                int b2 = i.ReadByte();
                if (b1 < 0 || b2 < 0)
                {
                    throw new DiskFileCorrupted();
                }
                return ((b1 << 8) | b2) & 0xFFFF;
            }
            catch (IOException)
            {
                throw new DiskFileCorrupted();
            }
        }

        // Save the current cumulative-since-base state to a `.cscheck`
        // overlay file next to the base disk. Mirrors Java upstream's
        // `saveDisk` in shape: rotate the existing checkpoint to a
        // timestamped backup, write a new one, delete archives beyond
        // `deltasToKeep`.
        //
        // Returns true if a checkpoint was written, false if there's
        // nothing to save (read-only disk, or no dirty sectors).
        // Messages explaining the outcome are appended to `errors`.
        //
        // Phase H (2026-05-13): replaces the Phase F-4 stub.
        public bool saveDisk(StringBuilder errors)
        {
            if (this.readonly_)
            {
                this.logf(errors, "disk is read only, no checkpoint written");
                return false;
            }
            if (!this.changed)
            {
                this.logf(errors, "disk is not changed, no checkpoint written");
                return false;
            }

            string checkpointPath = this.filePath + EXT_CHECKPOINT;
            string tempPath = this.filePath + EXT_CHECKPOINT_TEMP;

            // Write to a temp file first; atomically rename to the final
            // name only when the write succeeds.
            try
            {
                if (File.Exists(tempPath)) { File.Delete(tempPath); }
                int sectorsWritten = this.writeCheckpoint(tempPath);
                this.logf("wrote {0} dirty sectors to {1}\n", sectorsWritten, Path.GetFileName(tempPath));
            }
            catch (Exception e)
            {
                this.logf(errors, "failed to write checkpoint: {0}", e.Message);
                try { if (File.Exists(tempPath)) { File.Delete(tempPath); } } catch { /* swallow */ }
                return false;
            }

            // Rotate: archive an existing checkpoint with a timestamp suffix.
            // Format matches Java upstream's delta-rotation suffix so a future
            // user can `ls -t` the directory and see the recency.
            if (File.Exists(checkpointPath))
            {
                string ts = DateTime.Now.ToString("yyyy.MM.dd_HH.mm.ss.fff");
                string archivedPath = checkpointPath + "-" + ts;
                try
                {
                    File.Move(checkpointPath, archivedPath);
                }
                catch (Exception e)
                {
                    this.logf(errors, "failed to archive prior checkpoint: {0}", e.Message);
                    // Continue anyway — overwriting the existing checkpoint
                    // beats failing the entire save.
                    try { File.Delete(checkpointPath); } catch { /* swallow */ }
                }
            }
            try
            {
                File.Move(tempPath, checkpointPath);
            }
            catch (Exception e)
            {
                this.logf(errors, "failed to finalize checkpoint: {0}", e.Message);
                return false;
            }

            // Prune old archived checkpoints beyond `deltasToKeep`.
            this.pruneOldCheckpoints();

            return true;
        }

        // Java upstream had a `saveDisk` alias; keep saveCheckpoint as the
        // descriptive C#-side name for tests and future callers.
        public bool saveCheckpoint(StringBuilder errors) => this.saveDisk(errors);

        // Write the in-memory dirty sectors as a .cscheck file.
        // Returns the number of sectors written. Caller handles atomic rename.
        private int writeCheckpoint(string path)
        {
            int sectorsWritten = 0;
            using (FileStream fos = new(path, FileMode.Create, FileAccess.Write))
            using (GZipStream gz = new(fos, CompressionLevel.Optimal))
            {
                // Header (16 bytes, little-endian)
                writeUInt32LE(gz, CHECKPOINT_MAGIC);
                writeUInt16LE(gz, CHECKPOINT_VERSION);
                writeUInt16LE(gz, CHECKPOINT_FLAG_DRACO);
                writeUInt32LE(gz, (uint)this.sectorCount);
                // Count dirty sectors so the header can carry the exact count
                int dirtyCount = 0;
                for (int i = 0; i < this.sectorCount; i++)
                {
                    if (this.sectorsChanged[i]) { dirtyCount++; }
                }
                writeUInt32LE(gz, (uint)dirtyCount);

                // Sector entries
                for (int i = 0; i < this.sectorCount; i++)
                {
                    if (!this.sectorsChanged[i]) { continue; }
                    writeUInt32LE(gz, (uint)i);
                    ushort[] rawSector = this.sectors[i];
                    for (int w = 0; w < wordsPerSector; w++)
                    {
                        writeUInt16LE(gz, rawSector[w]);
                    }
                    sectorsWritten++;
                }
                gz.Flush();
            }
            return sectorsWritten;
        }

        // If a .cscheck file exists alongside the base, apply its sectors
        // on top of the in-memory state. Marks those sectors as dirty so a
        // subsequent saveCheckpoint preserves cumulative state.
        private void tryApplyCheckpoint()
        {
            string checkpointPath = this.filePath + EXT_CHECKPOINT;
            if (!File.Exists(checkpointPath)) { return; }

            int sectorsLoaded;
            try
            {
                using FileStream fis = new(checkpointPath, FileMode.Open, FileAccess.Read);
                using GZipStream gz = new(fis, CompressionMode.Decompress);

                uint magic = readUInt32LE(gz);
                if (magic != CHECKPOINT_MAGIC)
                {
                    throw new DiskFileCorrupted($".cscheck magic mismatch: expected 0x{CHECKPOINT_MAGIC:X8}, got 0x{magic:X8}");
                }
                ushort version = readUInt16LE(gz);
                if (version != CHECKPOINT_VERSION)
                {
                    throw new DiskFileCorrupted($".cscheck version unsupported: {version} (this build expects {CHECKPOINT_VERSION})");
                }
                ushort flags = readUInt16LE(gz);
                if ((flags & CHECKPOINT_FLAG_DRACO) == 0)
                {
                    throw new DiskFileCorrupted(".cscheck flags indicate Duchess/raw format but loaded into Draco/zdisk DiskFile");
                }
                uint headerSectorCount = readUInt32LE(gz);
                if (headerSectorCount != (uint)this.sectorCount)
                {
                    throw new DiskFileCorrupted($".cscheck sectorCount mismatch: header={headerSectorCount}, base={this.sectorCount}");
                }
                uint changedCount = readUInt32LE(gz);

                sectorsLoaded = 0;
                for (uint s = 0; s < changedCount; s++)
                {
                    uint linearIdx = readUInt32LE(gz);
                    if (linearIdx >= (uint)this.sectorCount)
                    {
                        throw new DiskFileCorrupted($".cscheck sector index out of range: {linearIdx} >= {this.sectorCount}");
                    }
                    ushort[] rawSector = this.sectors[linearIdx];
                    for (int w = 0; w < wordsPerSector; w++)
                    {
                        rawSector[w] = readUInt16LE(gz);
                    }
                    this.sectorsChanged[linearIdx] = true;
                    sectorsLoaded++;
                }
                this.changed = (sectorsLoaded > 0);
            }
            catch (DiskFileCorrupted dfc)
            {
                Console.Write($"** .cscheck corrupted: {dfc.Message}\n");
                Console.Write("** Skipping checkpoint application; running from base disk only.\n");
                return;
            }
            catch (IOException ioe)
            {
                Console.Write($"** .cscheck I/O error: {ioe.Message}\n");
                Console.Write("** Skipping checkpoint application; running from base disk only.\n");
                return;
            }

            Console.Write($"applied .cscheck: {sectorsLoaded} sectors restored from {Path.GetFileName(checkpointPath)}\n");
        }

        // Delete archived checkpoints (.cscheck-<timestamp>) beyond `deltasToKeep`.
        // Keeps the most recent N timestamps based on the file name's
        // lexicographic order (which matches chronological order for the
        // yyyy.MM.dd_HH.mm.ss.fff format).
        private void pruneOldCheckpoints()
        {
            string? dir = Path.GetDirectoryName(this.filePath);
            if (dir == null) { return; }
            string baseName = Path.GetFileName(this.filePath) + EXT_CHECKPOINT + "-";
            string[] archived;
            try
            {
                archived = Directory.GetFiles(dir, baseName + "*");
            }
            catch (IOException)
            {
                return;
            }
            // Sort descending — newest first
            Array.Sort(archived, (a, b) => string.Compare(b, a, StringComparison.Ordinal));
            for (int i = this.deltasToKeep; i < archived.Length; i++)
            {
                try { File.Delete(archived[i]); }
                catch (IOException) { /* swallow — best-effort */ }
            }
        }

        // Little-endian I/O helpers for the .cscheck format
        private static void writeUInt16LE(Stream s, ushort v)
        {
            s.WriteByte((byte)(v & 0xFF));
            s.WriteByte((byte)((v >> 8) & 0xFF));
        }

        private static void writeUInt32LE(Stream s, uint v)
        {
            s.WriteByte((byte)(v & 0xFF));
            s.WriteByte((byte)((v >> 8) & 0xFF));
            s.WriteByte((byte)((v >> 16) & 0xFF));
            s.WriteByte((byte)((v >> 24) & 0xFF));
        }

        private static ushort readUInt16LE(Stream s)
        {
            int b0 = s.ReadByte();
            int b1 = s.ReadByte();
            if (b0 < 0 || b1 < 0) { throw new DiskFileCorrupted(".cscheck truncated"); }
            return (ushort)(b0 | (b1 << 8));
        }

        private static uint readUInt32LE(Stream s)
        {
            int b0 = s.ReadByte();
            int b1 = s.ReadByte();
            int b2 = s.ReadByte();
            int b3 = s.ReadByte();
            if (b0 < 0 || b1 < 0 || b2 < 0 || b3 < 0) { throw new DiskFileCorrupted(".cscheck truncated"); }
            return (uint)b0 | ((uint)b1 << 8) | ((uint)b2 << 16) | ((uint)b3 << 24);
        }

        /*
         * primitive disk i/o operations
         */

        public int getLinearSector(int cyl, int head, int sector)
        {
            if (cyl < 0 || cyl > this.cylCount || head < 0 || head > this.headCount || sector < 0 || sector > sectorsPerTrack)
            {
                throw new ArgumentException("invalid cyl/head/sector: " + cyl + "/" + head + "/" + sector);
            }
            return (cyl * this.sectorsPerCyl) + (head * sectorsPerTrack) + sector;
        }

        public int getLinearSector(DiskAddress da) =>
            this.getLinearSector(da.cylinder.get() & 0xFFFF, da.head.get() & 0xFFFF, da.sector.get() & 0xFFFF);

        public int getLinearSector(ByteSwappedDiskAddress da) =>
            this.getLinearSector(da.cylinder.get() & 0xFFFF, da.head.get() & 0xFFFF, da.sector.get() & 0xFFFF);

        public void linearToDiskAddress(int linearSector, ByteSwappedDiskAddress da)
        {
            int cyl = linearSector / (this.headCount * sectorsPerTrack);
            int cylRelative = linearSector - (cyl * this.headCount * sectorsPerTrack);
            int head = cylRelative / sectorsPerTrack;
            int sect = cylRelative % sectorsPerTrack;
            if (linearSector != this.getLinearSector(cyl, head, sect))
            {
                throw new InvalidOperationException("linearSector != getLinearSector(cyl, head, sect)");
            }
            da.cylinder.set((ushort)cyl);
            da.head.set(head);
            da.sector.set(sect);
        }

        // assuming that linearSector is valid!
        public int /* ErrorType */ readSectorData(int linearSector, int virtualLongPointer)
        {
            try
            {
                Mem.getRealAddress(virtualLongPointer, true);
                Mem.getRealAddress(virtualLongPointer + PrincOpsDefs.WORDS_PER_PAGE - 1, true);
            }
            catch (Exception e)
            {
                Console.Write(string.Format("***** unable to access vPtr = 0x{0:X6} :: {1}\n", virtualLongPointer, e.Message));
                throw;
            }
            ushort[] rawSector = this.sectors[linearSector];
            for (int i = offsetData; i < rawSector.Length; i++, virtualLongPointer++)
            {
                Mem.writeWord(virtualLongPointer, rawSector[i]);
            }
            return ErrorType.noError;
        }

        // assuming that linearSector is valid!
        public int /* ErrorType */ readSectorLabel(int linearSector, CDF_Label label)
        {
            ushort[] rawSector = this.sectors[linearSector];
            int sectorWord = offsetLabel;
            label.fileID_0.set(rawSector[sectorWord++]);
            label.fileID_1.set(rawSector[sectorWord++]);
            label.fileID_2.set(rawSector[sectorWord++]);
            label.fileID_3.set(rawSector[sectorWord++]);
            label.fileID_4.set(rawSector[sectorWord++]);
            label.filePageLo.set(rawSector[sectorWord++]);
            label.filePageHiAndPageZeroAttributes.set(rawSector[sectorWord++]);
            label.attributesInAllPages.set(rawSector[sectorWord++]);
            label.dontCare0.set(rawSector[sectorWord++]);
            label.dontCare1.set(rawSector[sectorWord++]);
            return ErrorType.noError;
        }

        // assuming that linearSector is valid!
        public bool /* same? */ verifySectorData(int linearSector, int virtualLongPointer)
        {
            Mem.getRealAddress(virtualLongPointer, false);
            Mem.getRealAddress(virtualLongPointer + PrincOpsDefs.WORDS_PER_PAGE - 1, false);
            ushort[] rawSector = this.sectors[linearSector];
            for (int i = offsetData; i < rawSector.Length; i++, virtualLongPointer++)
            {
                if (Mem.readWord(virtualLongPointer) != rawSector[i]) { return false; }
            }
            return true;
        }

        // assuming that linearSector is valid!
        public bool /* same? */ verifySectorLabel(int linearSector, CDF_Label label)
        {
            ushort[] rawSector = this.sectors[linearSector];

            /*
             * see: APilot/15.0.1/Faces/Private/CompatibilityDiskFace.mesa, lines 58..62 ::
             *
             *     When verifying labels, the following fields must match and must be
             *     the same in every page of a run of pages:
             *         fileID and attributesInAllPages
             *     The field pageZeroAttributes must match in page zero of a file and
             *     must be zero in every other page of a file.
             */

            if (rawSector[offsetLabel + 6] == 0 && (rawSector[offsetLabel + 7] & 0xFF00) == 0
                    && (label.filePageHiAndPageZeroAttributes.get() & 0x00FF) != (rawSector[offsetLabel + 7] & 0x00FF))
            {
                return false;
            }

            int sectorWord = offsetLabel;
            return label.fileID_0.get() == rawSector[sectorWord++]
                && label.fileID_1.get() == rawSector[sectorWord++]
                && label.fileID_2.get() == rawSector[sectorWord++]
                && label.fileID_3.get() == rawSector[sectorWord++]
                && label.fileID_4.get() == rawSector[sectorWord++]
                && sectorWord++ > 0 // skip: filePageLo
                && sectorWord++ > 0 // skip: filePageHiAndPageZeroAttributes
                && label.attributesInAllPages.get() == rawSector[sectorWord++];
                // ignore: dontCare0 and dontCare1
        }

        // assuming that linearSector is valid!
        public int /* ErrorType */ writeSectorData(int linearSector, int virtualLongPointer)
        {
            Mem.getRealAddress(virtualLongPointer, false);
            Mem.getRealAddress(virtualLongPointer + PrincOpsDefs.WORDS_PER_PAGE - 1, false);
            ushort[] rawSector = this.sectors[linearSector];
            for (int i = offsetData; i < rawSector.Length; i++, virtualLongPointer++)
            {
                rawSector[i] = Mem.readWord(virtualLongPointer);
            }
            this.changed = true;
            this.sectorsChanged[linearSector] = true;
            return ErrorType.noError;
        }

        // assuming that linearSector is valid!
        public int /* ErrorType */ writeSectorLabel(int linearSector, CDF_Label label)
        {
            ushort[] rawSector = this.sectors[linearSector];
            int sectorWord = offsetLabel;
            rawSector[sectorWord++] = label.fileID_0.get();
            rawSector[sectorWord++] = label.fileID_1.get();
            rawSector[sectorWord++] = label.fileID_2.get();
            rawSector[sectorWord++] = label.fileID_3.get();
            rawSector[sectorWord++] = label.fileID_4.get();
            rawSector[sectorWord++] = label.filePageLo.get();
            rawSector[sectorWord++] = label.filePageHiAndPageZeroAttributes.get();
            rawSector[sectorWord++] = label.attributesInAllPages.get();
            rawSector[sectorWord++] = label.dontCare0.get();
            rawSector[sectorWord++] = label.dontCare1.get();
            this.changed = true;
            this.sectorsChanged[linearSector] = true;
            return ErrorType.noError;
        }

        public int /* ErrorType */ readSectorLabelAndDataRaw(int linearSector, ushort[] label, ushort[] data)
        {
            if (label == null || label.Length < 10)
            {
                throw new ArgumentException("invalid label (not 10 words)");
            }
            if (data == null || data.Length < 256)
            {
                throw new ArgumentException("invalid sector data (not 256 words)");
            }

            ushort[] rawSector = this.sectors[linearSector];

            int sectorWord = offsetLabel;
            for (int i = 0; i < 10; i++)
            {
                label[i] = rawSector[sectorWord++];
            }

            sectorWord = offsetData;
            for (int i = 0; i < 256; i++)
            {
                data[i] = rawSector[sectorWord++];
            }

            return ErrorType.noError;
        }

        public int /* ErrorType */ writeSectorLabelAndDataRaw(int linearSector, ushort[] label, ushort[] data)
        {
            if (label == null || label.Length != 10)
            {
                throw new ArgumentException("invalid label (not 10 words)");
            }
            if (data == null || data.Length != 256)
            {
                throw new ArgumentException("invalid sector data (not 256 words)");
            }

            ushort[] rawSector = this.sectors[linearSector];

            int sectorWord = offsetLabel;
            for (int i = 0; i < 10; i++)
            {
                rawSector[sectorWord++] = label[i];
            }

            sectorWord = offsetData;
            for (int i = 0; i < 256; i++)
            {
                rawSector[sectorWord++] = data[i];
            }

            this.changed = true;
            this.sectorsChanged[linearSector] = true;

            return ErrorType.noError;
        }

        public string getLabelString(int linearSector)
        {
            ushort[] rawSector = this.sectors[linearSector];
            int sectorWord = offsetLabel;
            return string.Format(
                "Label(fileID[ {0:X4} {1:X4} {2:X4} {3:X4} {4:X4} ], filePage+page0attrs[ {5:X4} {6:X4} ], attrsInAllPages[ {7:X4} ], dontCare[ {8:X4} {9:X4} ])",
                rawSector[sectorWord++] & 0xFFFF, rawSector[sectorWord++] & 0xFFFF, rawSector[sectorWord++] & 0xFFFF, rawSector[sectorWord++] & 0xFFFF, rawSector[sectorWord++] & 0xFFFF,
                rawSector[sectorWord++] & 0xFFFF, rawSector[sectorWord++] & 0xFFFF,
                rawSector[sectorWord++] & 0xFFFF,
                rawSector[sectorWord++] & 0xFFFF, rawSector[sectorWord++] & 0xFFFF);
        }
    }
}
