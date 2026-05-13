/*
Copyright (c) 2019, Dr. Hans-Walter Latz (original Java implementation)
Copyright (c) 2026, Matthew Dugal (C# .NET 10 port — Phase F-4 partial: headless + GUI scaffolding)
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

using Dwarf.Agents;
using Dwarf.Duchess; // shared PropertiesExt + Utils
using Dwarf.Engine;
using Dwarf.Iop6085;
using EngineXfer = Dwarf.Engine.Xfer;
using OpcodesClass = Dwarf.Engine.Opcodes.Opcodes;

namespace Dwarf.Draco;

// Draco application main program for the Xerox 6085 / Daybreak architecture
// of the Dwarf Mesa emulator — Phase F-4 partial port.
//
// Loads a .properties config, sets up Mem (Daybreak mode), Cpu (MAC), Opcodes,
// HDisk (canonical .zdisk), IOP, and germ. Runs `Cpu.processor()` on a
// background thread. Supports headless (Main) and Avalonia GUI (RunGui)
// orchestration paths, mirroring the Duchess shape.
//
// **Not yet ported (deferred to later Phase F sub-tasks)**:
//   - HEthernet / NetHub interop  → Phase F-5
//   - HFloppy / floppy insert/eject → Phase F-4b
//   - -netinstall / -netexec       → require HEthernet
//   - DebuggerSubstituteMpHandler  → not ported
public static class DracoHost
{
    // defaults for optional engine configuration
    private const bool DEFAULT_LARGE_DISPLAY = true;
    private const string DEFAULT_SWITCHES = "8Wy{|}\\346\\347\\350\\377";
    private const string DEFAULT_MAC = "00-1D-AB-EA-F4-C4";

    /*
     * configuration
     */

    private static string? configFilename;
    private static string title = "Draco";

    private static bool largeScreen = false;

    private static string? diskFile = null;
    private static int oldDeltasToKeep = 5;
    private static string? germFile = null;
    private static string bootSwitches = DEFAULT_SWITCHES;

    private static HDisk.VerifyLabelOp labelOpOnRead = HDisk.VerifyLabelOp.updateDisk;
    private static HDisk.VerifyLabelOp labelOpOnWrite = HDisk.VerifyLabelOp.updateDisk;
    private static HDisk.VerifyLabelOp labelOpOnVerify = HDisk.VerifyLabelOp.verify;
    private static bool logLabelProblems = false;

    private static readonly int[] macBytes = new int[6];
    private static readonly int[] macWords = new int[3];
    private static string recognizedMacId = "";

    private static string? initialFloppy = null;
    private static string? floppyDirectory = null;

    private static string netHubHost = "";
    private static int netHubPort = 3333;
    private static int localTimeOffsetMinutes = 0;

    private static string? keyboardMapFile = null;
    private static bool resetKeysOnFocusLost = true;

    private static int daysBackInTime = 0;

    private static HDisk.VerifyLabelOp scanVerifyLabelOp(string? opText, string what)
    {
        if (string.IsNullOrEmpty(opText)) { return HDisk.VerifyLabelOp.verify; }
        string lc = opText.ToLowerInvariant();
        if ("verify".StartsWith(lc, StringComparison.Ordinal)) { return HDisk.VerifyLabelOp.verify; }
        if ("updatedisk".StartsWith(lc, StringComparison.Ordinal)) { return HDisk.VerifyLabelOp.updateDisk; }
        if ("noverify".StartsWith(lc, StringComparison.Ordinal)) { return HDisk.VerifyLabelOp.noVerify; }
        Console.WriteLine($"Warning: invalid value '{opText}' for {what}, using 'verify'");
        return HDisk.VerifyLabelOp.verify;
    }

    private static bool initializeConfiguration(string filename)
    {
        if (!filename.EndsWith(".properties", StringComparison.OrdinalIgnoreCase))
        {
            filename += ".properties";
        }
        if (!File.Exists(filename))
        {
            Console.Error.WriteLine($"Error: unable to read configuration properties file: {filename}");
            return false;
        }
        configFilename = filename;

        var props = new PropertiesExt();
        try
        {
            props.load(filename);
        }
        catch (Exception e)
        {
            Console.Error.WriteLine($"Error: unable to load configuration from properties file: {filename}");
            Console.Error.WriteLine($"=> {e.Message}");
            return false;
        }

        diskFile = props.getString("boot", null);
        if (!Utils.isFileOk("boot", diskFile)) { return false; }
        oldDeltasToKeep = props.getInt("oldDeltasToKeep", oldDeltasToKeep);

        germFile = props.getString("fallbackGerm", null);
        if (germFile != null && !Utils.isFileOk("germ", germFile)) { return false; }
        bootSwitches = props.getString("switches", DEFAULT_SWITCHES) ?? DEFAULT_SWITCHES;

        labelOpOnRead = scanVerifyLabelOp(props.getString("labelOpOnRead", null), "option labelOpOnRead");
        labelOpOnWrite = scanVerifyLabelOp(props.getString("labelOpOnWrite", null), "option labelOpOnWrite");
        labelOpOnVerify = scanVerifyLabelOp(props.getString("labelOpOnVerify", null), "option labelOpOnVerify");
        logLabelProblems = props.getBoolean("logLabelProblems", logLabelProblems);

        title = props.getString("title", diskFile) ?? title;
        largeScreen = props.getBoolean("largeScreen", DEFAULT_LARGE_DISPLAY);

        initialFloppy = props.getString("initialFloppy", initialFloppy);
        floppyDirectory = props.getString("floppyDirectory", floppyDirectory);

        netHubHost = props.getString("netHubHost", netHubHost) ?? "";
        netHubPort = props.getInt("netHubPort", netHubPort);
        localTimeOffsetMinutes = props.getInt("localTimeOffsetMinutes", localTimeOffsetMinutes);

        daysBackInTime = props.getInt("daysBackInTime", daysBackInTime);

        keyboardMapFile = props.getString("keyboardMapFile", keyboardMapFile);
        resetKeysOnFocusLost = props.getBoolean("resetKeysOnFocusLost", resetKeysOnFocusLost);

        string mac = props.getString("processorId", null) ?? "";
        if (string.IsNullOrEmpty(mac))
        {
            Console.WriteLine("Warning: no processor id specified, using default");
            mac = DEFAULT_MAC;
        }
        recognizedMacId = Utils.parseMac(mac, macBytes, macWords);
        if (string.IsNullOrEmpty(recognizedMacId))
        {
            Console.WriteLine("Warning: invalid processor id specified, using default");
            recognizedMacId = Utils.parseMac(DEFAULT_MAC, macBytes, macWords);
        }

        string? xdeNoBlinkWorkAround = props.getString("xdeNoBlinkWorkAround", null);
        if (!string.IsNullOrEmpty(xdeNoBlinkWorkAround))
        {
            string[] parts = xdeNoBlinkWorkAround.Split(':');
            string htsndPart = parts[0].Trim();
            long xdeNoBlinkInstrCnt = 0;
            if (!int.TryParse(htsndPart, out int hundredThousands))
            {
                Console.WriteLine("Warning: invalid xdeNoBlinkWorkAround/100tsnd-instructions specified, work-around for XDE not activated");
            }
            else
            {
                xdeNoBlinkInstrCnt = hundredThousands * 100_000L;
            }

            if (parts.Length > 1 && xdeNoBlinkInstrCnt > 0)
            {
                string datePart = parts[1].Trim();
                if (DateOnly.TryParse(datePart, out DateOnly xdeTargetDate))
                {
                    HProcessor.installXdeNoBlinkWorkAround(xdeTargetDate, xdeNoBlinkInstrCnt);
                }
                else
                {
                    Console.WriteLine("Warning: invalid xdeNoBlinkWorkAround/target-date, work-around for XDE not activated");
                }
            }
        }

        return true;
    }

    private static void dumpConfiguration()
    {
        Console.WriteLine($"Configuration from {configFilename}");
        Console.WriteLine($" fallbackGerm: {germFile ?? ""}");
        Console.WriteLine($" switches    : {bootSwitches}");
        Console.WriteLine($" boot file   : {diskFile}");
        Console.WriteLine($" deltas limit: {oldDeltasToKeep}");
        Console.WriteLine($" display     : {(largeScreen ? "large - 1152 x 861 (19\")" : "small - 832 x 633 (15\")")}");
        Console.WriteLine($" keyboardMap : {keyboardMapFile ?? ""}");
        Console.WriteLine($" resetKeysOnF: {(resetKeysOnFocusLost ? "yes" : "no")}");
        Console.WriteLine($" mac words   : {macWords[0]:X4} - {macWords[1]:X4} - {macWords[2]:X4} ({recognizedMacId})");
        Console.WriteLine($" floppy      : {initialFloppy ?? ""}  (HFloppy is Phase F-4b)");
        Console.WriteLine($" floppy dir  : {floppyDirectory ?? ""}");
        Console.WriteLine($" netHubHost  : {netHubHost}  (HEthernet is Phase F-5)");
        Console.WriteLine($" netHubPort  : {netHubPort}");
        Console.WriteLine($" localTimeOff: {localTimeOffsetMinutes}");
        Console.WriteLine($" daysBack    : {daysBackInTime}");
    }

    // load the germ file from the rigid disk
    // (simple version, able to load only a germ stored as contiguous sequence of pages)
    private const ushort PVSeal = 0xA28A;

    private static bool scanDiskForGerm(int diskIdx, List<ushort[]> germSectors)
    {
        ushort[] label = new ushort[10];
        ushort[] data = new ushort[256];

        // get physical volume root page
        HDisk.rawRead(diskIdx, 0, label, data);
        if (data[0] != PVSeal)
        {
            Console.WriteLine($"** invalid PVSeal 0x{data[0]:X4} on Pilot disk");
            return false;
        }

        // get germ start location
        int cyl = data[0x21] & 0xFFFF;
        int head = data[0x22] >>> 8;
        int sector = data[0x22] & 0x00FF;
        if (cyl == 0 && head == 0 && sector == 0)
        {
            Console.WriteLine("** no germ file found on Pilot disk");
            return false;
        }

        // get germ sectors
        int absSector = HDisk.getAbsSectNo(diskIdx, cyl, head, sector);
        int reqGermPageNo = (data[0x20] << 16) | (data[0x1F] & 0xFFFF);
        HDisk.rawRead(diskIdx, absSector++, label, data);
        while (reqGermPageNo <= 96) // a germ can have at most 96 pages (95 code + 1 GFT page)
        {
            ushort[] germSector = new ushort[256];
            Array.Copy(data, 0, germSector, 0, germSector.Length);
            germSectors.Add(germSector);

            if (label[8] == 0xFFFF || label[9] == 0xFFFF)
            {
                break; // last sector of pilot boot file
            }

            reqGermPageNo++;
            HDisk.rawRead(diskIdx, absSector++, label, data);
        }
        if (label[8] != 0xFFFF || label[9] != 0xFFFF)
        {
            Console.WriteLine("** did not find plausible germ file on disk (max. 96 pages)");
            return false;
        }

        return true;
    }

    // checks if the given 16 words (must be the first words of the first sector) are a plausible GFT
    // (Global Frame Table) for a germ (i.e. all global frames must be in first memory bank)
    private static bool isPostPrincOps4dot0Germ(ushort[] first16words) =>
        first16words[0] == 0 && first16words[1] == 0 // first entry is reserved and GF *must* be 0
        && first16words[3] == 0                       // CB is in first memory bank
        && first16words[5] == 0                       // GF is in first memory bank
        && first16words[7] == 0                       // CB is in first memory bank
        && first16words[9] == 0                       // GF is in first memory bank
        && first16words[0x0B] == 0                    // CB is in first memory bank
        && first16words[0x0D] == 0                    // GF is in first memory bank
        && first16words[0x0F] == 0;                   // CB is in first memory bank

    private static bool isPostPrincOps4dot0Germ(string? germFilename)
    {
        if (string.IsNullOrEmpty(germFilename)) { return false; }
        try
        {
            using var fis = new FileStream(germFilename, FileMode.Open, FileAccess.Read);
            ushort[] first16words = new ushort[16];
            for (int i = 0; i < first16words.Length; i++)
            {
                int b1 = fis.ReadByte();
                if (b1 < 0) { return false; }
                int b2 = fis.ReadByte();
                if (b2 < 0) { return false; }
                first16words[i] = (ushort)((b1 << 8) | (b2 & 0x00FF));
            }
            return isPostPrincOps4dot0Germ(first16words);
        }
        catch (IOException)
        {
            return false;
        }
    }

    // Set up the mesa machine — shared by Main (headless) and RunGui.
    // Returns the iUiDataConsumer for the UI side.
    private static iUiDataConsumer setupEngine()
    {
        // set processor id (aka MAC address)
        Cpu.setPID(macWords[0], macWords[1], macWords[2]);

        // configure the network handler before the IOP instantiates it
        HEthernet.setHubParameters(netHubHost, netHubPort, localTimeOffsetMinutes);

        // initialize the memory subsystem with the configured display size
        Mem.initializeMemoryDaybreak(largeScreen);

        // initialize the harddisk
        var sb = new System.Text.StringBuilder();
        if (!HDisk.addFile(diskFile!, false, oldDeltasToKeep, sb))
        {
            throw new InvalidOperationException($"error initializing harddisk: {sb}");
        }

        // get the germ to use — first try scanning the disk, fall back to fallbackGerm if available
        var germContent = new List<ushort[]>();
        bool havingGerm = scanDiskForGerm(0, germContent);

        if (!havingGerm && germFile == null)
        {
            throw new InvalidOperationException("unable to boot: no (usable) germ found on disk and no fallback germ file specified");
        }
        else if (havingGerm)
        {
            Console.WriteLine("Booting with germ from pilot disk");
        }
        else if (germFile != null && File.Exists(germFile))
        {
            Console.WriteLine($"Booting with specified fallbackGerm file: {germFile}");
        }
        else
        {
            throw new InvalidOperationException("germ file not readable");
        }

        // check which PrincOps flavor we have this time
        bool post40PrincOps = havingGerm
            ? isPostPrincOps4dot0Germ(germContent[0])
            : isPostPrincOps4dot0Germ(germFile);
        Console.WriteLine($"Germ has {(post40PrincOps ? "post-" : "")}PrincOps 4.0 flavor");

        // initialize the opcodes dispatch engine
        if (post40PrincOps)
        {
            OpcodesClass.initializeInstructionsPrincOpsPost40();
            EngineXfer.switchToNewPrincOps();
        }
        else
        {
            OpcodesClass.initializeInstructionsPrincOps40();
        }

        // initialize the 6085 IOP, allocating the static device handler structures in the IORegion
        IOP.initialize(labelOpOnRead, labelOpOnWrite, labelOpOnVerify, logLabelProblems);

        // prepare booting the machine (setup germ, boot source, boot switches)
        if (havingGerm)
        {
            InitialMesaMicrocode.loadGerm(germContent, post40PrincOps);
        }
        else
        {
            InitialMesaMicrocode.loadGerm(germFile!, post40PrincOps);
        }
        InitialMesaMicrocode.setBootRequestDisk(0);
        InitialMesaMicrocode.setBootSwitches(bootSwitches);

        // adjust absolute date
        long timeShiftSeconds = -86400L * daysBackInTime;
        HProcessor.setTimeShiftSeconds(timeShiftSeconds);
        NetworkInternalTimeService.setTimeShiftSeconds(timeShiftSeconds);

        // TODO Phase F+: Cpu.setMPHandler(new DebuggerSubstituteMpHandler(stopOnNetDebug));

        return IOP.getUiCallbacks();
    }

    // Headless entry point — parses args, sets up the engine, and runs
    // `Cpu.processor()` to exhaustion (or until Ctrl-C). Useful for smoke
    // testing without a window.
    public static int Main(string[] args)
    {
        bool doMerge = false;
        string? cfgFile = null;
        bool dumpConfig = false;

        foreach (string arg in args)
        {
            if (!arg.StartsWith('-'))
            {
                if (cfgFile == null) { cfgFile = arg; }
                else { Console.WriteLine($"Warning: ignoring unknown argument: {arg}"); }
            }
        }

        if (cfgFile == null)
        {
            Console.Error.WriteLine("Error: no configuration specified.");
            Console.Error.WriteLine("Usage: Dwarf -draco <config.properties> [-v] [-merge]");
            return 1;
        }
        if (!initializeConfiguration(cfgFile))
        {
            return 1;
        }

        foreach (string arg in args)
        {
            if (!arg.StartsWith('-') || arg.Equals("-test", StringComparison.OrdinalIgnoreCase)) { continue; }
            string lower = arg.ToLowerInvariant();
            if (lower == "-v") { dumpConfig = true; }
            else if (lower == "-merge") { doMerge = true; }
            else if (lower == "-autoclose" || lower == "-run") { /* honored but no-op for headless */ }
            else if (lower == "-netinstall" || lower == "-netexec")
            {
                Console.Error.WriteLine($"{arg}: netboot requires HEthernet, which is Phase F-5. Skipping.");
            }
            else
            {
                Console.WriteLine($"Warning: ignoring unknown command line argument: {arg}");
            }
        }
        if (dumpConfig) { dumpConfiguration(); }

        // merge mode: not supported in C# (Java -merge is the canonical path)
        if (doMerge)
        {
            var sb = new System.Text.StringBuilder();
            if (!HDisk.addFile(diskFile!, false, 32, sb))
            {
                Console.WriteLine($"## error loading harddisk: {sb}");
                return 1;
            }
            HDisk.mergeDisks(Console.Out);
            return 0;
        }

        // set up engine
        try
        {
            setupEngine();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"## error setting up Draco engine: {ex.Message}");
            return 1;
        }

        // handle Ctrl-C by requesting a graceful engine stop
        Console.CancelKeyPress += (sender, e) =>
        {
            e.Cancel = true;
            Console.WriteLine("\n[harness] Ctrl-C — requesting mesa engine stop...");
            Processes.requestMesaEngineStop();
        };

        Console.WriteLine($"[harness] Booting Draco: {title}");
        Console.WriteLine($"[harness] Press Ctrl-C to stop the mesa engine gracefully.");

        var engineThread = new Thread(() =>
        {
            string finalMessage = Cpu.processor();
            Console.WriteLine($"\n***\n*** processor exited: {finalMessage}\n***");
        })
        {
            IsBackground = false,
            Name = "Mesa-Engine-Draco",
        };
        engineThread.Start();
        engineThread.Join();

        // shutdown
        var errMsgs = new System.Text.StringBuilder();
        IOP.shutdown(errMsgs);
        if (errMsgs.Length > 0)
        {
            Console.Error.WriteLine($"\n***\n*** Error(s) shutting down mesa engine devices: {errMsgs}\n***");
        }

        return 0;
    }

    // GUI entry point — same engine setup as headless Main, then invokes
    // the host-supplied `avaloniaLauncher`. Invoked by Dwarf.Cli when both
    // `-draco` and `-gui` flags are present. Mirrors `Duchess.RunGui`.
    public static int RunGui(string[] args, Func<int> avaloniaLauncher)
    {
        bool doMerge = false;
        string? cfgFile = null;
        bool dumpConfig = false;

        foreach (string arg in args)
        {
            if (!arg.StartsWith('-'))
            {
                if (cfgFile == null) { cfgFile = arg; }
                else { Console.WriteLine($"Warning: ignoring unknown argument: {arg}"); }
            }
        }

        if (cfgFile == null)
        {
            Console.Error.WriteLine("Error: no configuration specified.");
            Console.Error.WriteLine("Usage: Dwarf -draco -gui <config.properties> [-v]");
            return 1;
        }
        if (!initializeConfiguration(cfgFile))
        {
            return 1;
        }

        foreach (string arg in args)
        {
            string lower = arg.ToLowerInvariant();
            if (lower == "-v") { dumpConfig = true; }
            else if (lower == "-merge") { doMerge = true; }
        }
        if (dumpConfig) { dumpConfiguration(); }

        if (doMerge)
        {
            var sb = new System.Text.StringBuilder();
            if (!HDisk.addFile(diskFile!, false, 32, sb))
            {
                Console.WriteLine($"## error loading harddisk: {sb}");
                return 1;
            }
            HDisk.mergeDisks(Console.Out);
            return 0;
        }

        // set up engine
        iUiDataConsumer consumer;
        try
        {
            consumer = setupEngine();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"## error setting up Draco engine: {ex.Message}");
            return 1;
        }

        // build the KeyboardMapper for the UI side
        var keyMapper = new Dwarf.UI.Avalonia.Input.KeyboardMapper(
            consumer, Dwarf.UI.Avalonia.Input.eKeyEventCode.VK_CONTROL.Code, logKeypressed: false);
        if (!string.IsNullOrEmpty(keyboardMapFile))
        {
            keyMapper.loadConfigFile(keyboardMapFile);
        }
        else
        {
            keyMapper.mapDefaults_de_DE();
        }

        // publish the session so MainWindow can wire handlers when constructed
        Dwarf.UI.Avalonia.GuiSession.Current = new Dwarf.UI.Avalonia.GuiSession(
            consumer, keyMapper, Mem.displayPixelWidth, Mem.displayPixelHeight);

        Console.WriteLine($"[harness] Booting Draco: {title} (Avalonia GUI)");

        // start the engine on a background thread
        var engineThread = new Thread(() =>
        {
            string finalMessage = Cpu.processor();
            Console.WriteLine($"\n***\n*** processor exited: {finalMessage}\n***");
        })
        {
            IsBackground = false,
            Name = "Mesa-Engine-Draco",
        };
        engineThread.Start();

        // launch the Avalonia application — blocks until window closes
        int exitCode;
        try
        {
            exitCode = avaloniaLauncher();
        }
        finally
        {
            Processes.requestMesaEngineStop();
            engineThread.Join();

            var errMsgs = new System.Text.StringBuilder();
            IOP.shutdown(errMsgs);
            if (errMsgs.Length > 0)
            {
                Console.Error.WriteLine($"\n***\n*** Error(s) shutting down mesa engine devices: {errMsgs}\n***");
            }
        }

        return exitCode;
    }
}
