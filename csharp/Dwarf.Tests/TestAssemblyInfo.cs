/*
Copyright (c) 2026, Matthew Dugal (C# .NET 10 port)
All rights reserved.

See csharp/BSD3-HEADER.txt for the full BSD-3 license terms.
*/

using Xunit;

// All tests in this assembly mutate static state in Cpu / Mem / Opcodes
// (the same way the Java tests do via @Before). Running them in parallel
// would race that state, so collection parallelization is disabled.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
