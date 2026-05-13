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

using System.Threading;

namespace Dwarf.Engine;

// Minimal port of java.util.concurrent.atomic.AtomicInteger so call sites
// like Cpu.WP.get() / set() / getAndSet() / addAndGet() port verbatim from
// the Java source.
public sealed class AtomicInteger
{
    private int _value;

    public AtomicInteger() { _value = 0; }
    public AtomicInteger(int initial) { _value = initial; }

    public int get() => Volatile.Read(ref _value);
    public void set(int value) => Volatile.Write(ref _value, value);
    public int getAndSet(int value) => Interlocked.Exchange(ref _value, value);
    public int addAndGet(int delta) => Interlocked.Add(ref _value, delta);
    public int incrementAndGet() => Interlocked.Increment(ref _value);
    public int decrementAndGet() => Interlocked.Decrement(ref _value);
    public bool compareAndSet(int expected, int update) =>
        Interlocked.CompareExchange(ref _value, update, expected) == expected;

    public override string ToString() => get().ToString();
}
