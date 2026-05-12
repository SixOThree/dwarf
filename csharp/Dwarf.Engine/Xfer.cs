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

namespace Dwarf.Engine;

// Mesa control transfer machinery.
//
// Phase B: stubbed — provides type surface needed by Ch09 opcodes to compile.
// All method calls throw `NotImplementedException("Phase C: Xfer")` since
// Phase B unit tests don't exercise the control-transfer paths. Phase C
// implements the real Xfer-based trap and call dispatch.
public static class Xfer
{
    public enum XferType
    {
        xtrap,
        xreturn,
        xcall,
        xport,
        xfer,
        xlocalCall,
    }

    // Allocate a local frame of the given fsi.
    public static int alloc(int fsi) => throw new NotImplementedException("Phase C: Xfer.alloc");

    // Free a previously allocated local frame.
    public static void free(int frame) => throw new NotImplementedException("Phase C: Xfer.free");

    // The installed implementation. Phase B: stub. Phase C: real RealXferImpl.
    public static IXferImpl impl = new StubXferImpl();

    public interface IXferImpl
    {
        void xfer(int controlLink, int lf, XferType type, bool freeLF);
        int fetchLink(int idx);
        void checkForXferTraps(int longPC, XferType type);
    }

    private sealed class StubXferImpl : IXferImpl
    {
        public void xfer(int controlLink, int lf, XferType type, bool freeLF) =>
            throw new NotImplementedException("Phase C: Xfer.impl.xfer");
        public int fetchLink(int idx) =>
            throw new NotImplementedException("Phase C: Xfer.impl.fetchLink");
        public void checkForXferTraps(int longPC, XferType type) =>
            throw new NotImplementedException("Phase C: Xfer.impl.checkForXferTraps");
    }
}
