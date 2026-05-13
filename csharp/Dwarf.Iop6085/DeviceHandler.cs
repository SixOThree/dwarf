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

namespace Dwarf.Iop6085;

// Common interface and functionality of the Daybreak/6085 device handlers.
public abstract class DeviceHandler
{
    // generation of bit masks for identifying a single device for
    // notifications by the mesa engine
    private static int nextNotifyMaskLow = 1;
    private static int nextNotifyMaskHigh = 1;

    protected static ushort mkMask()
    {
        int res = (nextNotifyMaskHigh << 8) | nextNotifyMaskLow;
        nextNotifyMaskLow <<= 1;
        if (nextNotifyMaskLow > 0x0080)
        {
            nextNotifyMaskLow = 1;
            nextNotifyMaskHigh++;
        }
        return (ushort)res;
    }

    // handler name for logging
    private readonly string handlerName;

    // should this agent log the own actions?
    protected bool logging = false;

    protected DeviceHandler(string handlerName, bool loggingEnabled)
    {
        this.handlerName = handlerName;
        this.logging = loggingEnabled;
    }

    public void enableLogging(bool enabled)
    {
        this.logging = enabled;
    }

    // Logging with a line prefix identifying the handler type.
    protected void logf(string template, params object[] args)
    {
        if (!this.logging) { return; }
        Console.Write("DevHandler " + this.handlerName + ": " + string.Format(template, args));
    }

    // Logging without a line prefix.
    protected void slogf(string template, params object[] args)
    {
        if (!this.logging) { return; }
        Console.Write(string.Format(template, args));
    }

    // Real-memory address of the FCB.
    public abstract int getFcbRealAddress();

    // The segment of the FCB in the IOP memory address space.
    public abstract ushort getFcbSegment();

    // Check if the `notifyMask` identifies this device handler and if so
    // process the request(s) currently present in the FCB.
    //
    // When a NotifyIOP instruction is executed, this method is called for
    // each device handler up to the first one returning `true`.
    public abstract bool processNotify(ushort notifyMask);

    // Operation codes for LOCKMEM instruction.
    public enum MemOperation
    {
        add = 0,
        and = 1,
        or = 2,
        xchg = 3,
        overwriteIfNil = 4,
    }

    // Handle synchronized access to memory between the mesa machine and the IOP.
    public abstract void handleLockmem(ushort lockMask, int realAddress, MemOperation memOp, ushort oldValue, ushort newValue);

    // Optional cleanup at the end of the LOCKMEM processing.
    public virtual void cleanupAfterLockmem(ushort lockMask, int realAddress)
    {
        // default: do nothing
    }

    public abstract void handleLockqueue(int vAddr, int rAddr);

    // Copy all buffered new external data into mesa memory space.
    public abstract void refreshMesaMemory();

    // Stop usage of the device and save buffers or device state if necessary.
    public abstract void shutdown(System.Text.StringBuilder errMsgTarget);
}
