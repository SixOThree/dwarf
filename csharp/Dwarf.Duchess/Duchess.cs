/*
Copyright (c) 2017, Dr. Hans-Walter Latz (original Java implementation)
Copyright (c) 2026, Matthew Dugal (C# .NET 10 port — Phase D-12 headless variant)
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
using Dwarf.Agents;
using EngineXfer = Dwarf.Engine.Xfer;
using OpcodesClass = Dwarf.Engine.Opcodes.Opcodes;
using AgentsClass = Dwarf.Agents.Agents;

namespace Dwarf.Duchess;

// Duchess application main program for the Guam machine architecture of
// the Dwarf Mesa emulator — **headless variant** for Phase D-12.
//
// Loads the configuration for the mesa engine, wires the device-interface
// agents (disk + floppy + display + keyboard + mouse + beep + processor +
// stream + network), and runs the mesa engine on a background thread. A
// `HeadlessDisplaySink` consumes the periodic UI-refresh callback to log
// MP / statistics and optionally dump the framebuffer to a file.
//
// Phase E will add the Avalonia UI atop this orchestration.
public static class Duchess
{
    // defaults for optional engine configuration
    private const int DEFAULT_DISPLAY_WIDTH = 1024;
    private const int DEFAULT_DISPLAY_HEIGHT = 640;
    private const string DEFAULT_SWITCHES = "8Wy{|}\\346\\347\\350\\377";
    private const string DEFAULT_MAC = "00-1D-BA-AE-04-C3";

    // the loaded configuration of the mesa engine to run including some defaults
    private static string? configFilename;
    private static string title = "unknown disk/system";
    private static string? bootFile = null;
    private static string? germFile = null;
    private static int addressBitsVirtual = 23;
    private static int addressBitsReal = 22;
    private static int displayWidth = DEFAULT_DISPLAY_WIDTH;
    private static int displayHeight = DEFAULT_DISPLAY_HEIGHT;
    private static bool displayTypeColor = false;
    private static string switches = DEFAULT_SWITCHES;
    private static readonly int[] macBytes = new int[6];
    private static readonly int[] macWords = new int[3];
    private static string recognizedMacId = "";
    private static int oldDeltasToKeep = 5;
    private static string? initialFloppy = null;
    private static string? floppyDirectory = null;
    private static string? keyboardMapFile = null;
    private static bool resetKeysOnFocusLost = true;
    private static string netHubHost = "";
    private static int netHubPort = 3333;
    private static int localTimeOffsetMinutes = 0;

    // headless-specific options
    private static string? framebufferOutPath = null;
    private static long framebufferIntervalMs = 1000;

    // load the mesa engine configuration from the given file
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

        bootFile = props.getString("boot", null);
        if (!Utils.isFileOk("boot", bootFile)) { return false; }
        germFile = props.getString("germ", null);
        if (!Utils.isFileOk("germ", germFile)) { return false; }
        addressBitsVirtual = props.getInt("addressBitsVirtual", addressBitsVirtual);
        addressBitsReal = props.getInt("addressBitsReal", addressBitsReal);
        displayWidth = props.getInt("displayWidth", displayWidth);
        displayHeight = props.getInt("displayHeight", displayHeight);
        displayTypeColor = props.getBoolean("displayTypeColor", displayTypeColor);
        switches = props.getString("switches", switches) ?? switches;
        title = props.getString("title", bootFile) ?? title;
        oldDeltasToKeep = props.getInt("oldDeltasToKeep", oldDeltasToKeep);
        initialFloppy = props.getString("initialFloppy", initialFloppy);
        floppyDirectory = props.getString("floppyDirectory", floppyDirectory);
        keyboardMapFile = props.getString("keyboardMapFile", keyboardMapFile);
        netHubHost = props.getString("netHubHost", netHubHost) ?? netHubHost;
        netHubPort = props.getInt("netHubPort", netHubPort);
        localTimeOffsetMinutes = props.getInt("localTimeOffsetMinutes", localTimeOffsetMinutes);

        resetKeysOnFocusLost = props.getBoolean("resetKeysOnFocusLost", resetKeysOnFocusLost);

        addressBitsReal = Math.Max(PrincOpsDefs.MIN_REAL_ADDRESSBITS, Math.Min(PrincOpsDefs.MAX_REAL_ADDRESSBITS, addressBitsReal));
        addressBitsVirtual = Math.Max(addressBitsReal, Math.Min(PrincOpsDefs.MAX_VIRTUAL_ADDRESSBITS, addressBitsVirtual));

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
                    ProcessorAgent.installXdeNoBlinkWorkAround(xdeTargetDate, xdeNoBlinkInstrCnt);
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
        Console.WriteLine($" germ file   : {germFile}");
        Console.WriteLine($" switches    : {switches}");
        Console.WriteLine($" boot file   : {bootFile}");
        Console.WriteLine($" deltas limit: {oldDeltasToKeep}");
        Console.WriteLine($" bits virtual: {addressBitsVirtual}");
        Console.WriteLine($" bits real   : {addressBitsReal}");
        Console.WriteLine($" display     : w( {displayWidth} ) x h( {displayHeight} ) - {(displayTypeColor ? "color" : "b/w")} display");
        Console.WriteLine($" keyboardMap : {keyboardMapFile ?? ""}  (not wired in headless harness)");
        Console.WriteLine($" mac words   : {macWords[0]:X4} - {macWords[1]:X4} - {macWords[2]:X4} ({recognizedMacId})");
        Console.WriteLine($" floppy      : {initialFloppy ?? ""}");
        Console.WriteLine($" floppy dir  : {floppyDirectory ?? ""}  (not wired in headless harness)");
        Console.WriteLine($" netHubHost  : {netHubHost}");
        Console.WriteLine($" netHubPort  : {netHubPort}");
        Console.WriteLine($" localTimeOff: {localTimeOffsetMinutes}");
        Console.WriteLine($" framebuffer : {framebufferOutPath ?? "(none)"}  (interval: {framebufferIntervalMs} ms)");
    }

    // The main entry point for the headless Duchess harness. Parses args,
    // initializes the engine + agents, and runs `Cpu.processor()` to
    // exhaustion (or until Ctrl-C).
    public static int Main(string[] args)
    {
        bool doMerge = false;
        string? cfgFile = null;
        bool dumpConfig = false;

        // command line parameters pass 1: pick out the positional config file
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
            Console.Error.WriteLine("Usage: Dwarf -duchess <config.properties> [-v] [-merge] [-frames-out <path>] [-frames-interval-ms <ms>]");
            return 1;
        }
        if (!initializeConfiguration(cfgFile))
        {
            return 1;
        }

        // command line parameters pass 2: optional flags
        for (int i = 0; i < args.Length; i++)
        {
            string arg = args[i];
            if (!arg.StartsWith('-')) { continue; }
            string lower = arg.ToLowerInvariant();

            if (lower == "-v")
            {
                dumpConfig = true;
            }
            else if (lower == "-merge")
            {
                doMerge = true;
            }
            else if (lower == "-frames-out")
            {
                if (i + 1 < args.Length) { framebufferOutPath = args[++i]; }
            }
            else if (lower == "-frames-interval-ms")
            {
                if (i + 1 < args.Length && long.TryParse(args[++i], out long ms))
                {
                    framebufferIntervalMs = Math.Max(50, ms);
                }
            }
            else
            {
                Console.WriteLine($"Warning: ignoring unknown command line argument: {arg}");
            }
        }
        if (dumpConfig) { dumpConfiguration(); }

        // merge disks if requested, doing nothing else afterwards
        if (doMerge)
        {
            DiskState diskState = DiskAgent.addFile(bootFile!, false, oldDeltasToKeep);
            if (diskState == DiskState.ReadOnly)
            {
                Console.WriteLine($"Bootdisk '{bootFile}' is readonly and cannot be merged, aborting!");
            }
            else if (diskState == DiskState.Corrupted)
            {
                Console.WriteLine($"Bootdisk '{bootFile}' has corrupted delta and cannot be merged, aborting!");
            }
            else
            {
                DiskAgent.mergeDisks(Console.Out);
            }
            return 0;
        }

        // setup the mesa machine

        // set processor id (aka MAC address)
        Cpu.setPID(macWords[0], macWords[1], macWords[2]);

        // initialize the memory subsystem with the configured display configuration
        Mem.initializeMemoryGuam(
            addressBitsVirtual, addressBitsReal,
            displayTypeColor ? DisplayType.byteColor : DisplayType.monochrome,
            displayWidth, displayHeight);

        // initialize the opcodes dispatch engine for the new princops (mds relieved),
        // as used by available germ and boot disks
        OpcodesClass.initializeInstructionsPrincOpsPost40();
        EngineXfer.switchToNewPrincOps();

        // initialize the device-interface agents
        DiskState bootDiskState = DiskAgent.addFile(bootFile!, false, oldDeltasToKeep);
        if (bootDiskState == DiskState.ReadOnly)
        {
            title += " [read-only]";
        }
        else if (bootDiskState == DiskState.Corrupted)
        {
            title += " [ CORRUPTED ; don't use this disk delta ]";
        }
        NetworkAgent.setHubParameters(netHubHost, netHubPort, localTimeOffsetMinutes);
        AgentsClass.initialize();

        // try to load the initially inserted floppy if configured so
        if (!string.IsNullOrEmpty(initialFloppy))
        {
            try
            {
                AgentsClass.insertFloppy(initialFloppy, false);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Warning: invalid initial floppy '{initialFloppy}': {ex.Message}");
            }
        }

        // perform the initial microcode pre-boot actions (simulating the IOP on a 8000/6085)
        InitialMesaMicrocode.loadGerm(germFile!, true);
        InitialMesaMicrocode.setBootRequestDisk(0); // boot the (first) disk
        InitialMesaMicrocode.setBootSwitches(switches);

        // wire the headless display sink. The engine's checkForTimeouts will
        // call back to this at ~37 ms intervals.
        var sink = new HeadlessDisplaySink(framebufferOutPath, framebufferIntervalMs);
        Processes.registerUiRefreshCallback(sink);

        // handle Ctrl-C by requesting a graceful engine stop
        Console.CancelKeyPress += (sender, e) =>
        {
            e.Cancel = true; // don't terminate the process immediately
            Console.WriteLine("\n[harness] Ctrl-C — requesting mesa engine stop...");
            Processes.requestMesaEngineStop();
        };

        // run the mesa engine on a background thread. The main thread waits
        // for it to exit.
        Console.WriteLine($"[harness] Booting {title}");
        Console.WriteLine($"[harness] Press Ctrl-C to stop the mesa engine gracefully.");

        var engineThread = new Thread(() =>
        {
            string finalMessage = Cpu.processor();
            Console.WriteLine($"\n***\n*** processor exited: {finalMessage}\n***");
        });
        engineThread.IsBackground = false;
        engineThread.Name = "Mesa-Engine";
        engineThread.Start();
        engineThread.Join();

        // shutdown the agents to save changes to the harddisk and floppy
        var errMsgs = new System.Text.StringBuilder();
        AgentsClass.shutdown(errMsgs);
        if (errMsgs.Length > 0)
        {
            Console.Error.WriteLine($"\n***\n*** Error(s) shutting down mesa engine devices: {errMsgs}\n***");
        }

        return 0;
    }
}
