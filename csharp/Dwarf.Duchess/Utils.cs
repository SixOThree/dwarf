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

namespace Dwarf.Duchess;

// Utilities for Dwarf main programs.
public static class Utils
{
    // Check if the filename identifies a readable file.
    public static bool isFileOk(string kind, string? filename)
    {
        if (filename == null)
        {
            Console.Error.WriteLine($"Error: no filename given for {kind}");
            return false;
        }
        if (!File.Exists(filename))
        {
            Console.Error.WriteLine($"Error: file '{filename}' given for {kind} does not exist or is not readable");
            return false;
        }
        return true;
    }

    // Parse the MAC (machine address / processor id) from the given
    // XX-XX-XX-XX-XX-XX string. Fills macBytes[6] and macWords[3] arrays.
    // Returns the canonical re-formatted string on success, or "" on error.
    public static string parseMac(string mac, int[] macBytes, int[] macWords)
    {
        string[] submacs = mac.Split('-');
        if (submacs.Length != macBytes.Length)
        {
            Console.Error.WriteLine($"Error: invalid processor id format (not XX-XX-XX-XX-XX-XX): {mac}");
            return "";
        }

        for (int i = 0; i < macBytes.Length; i++)
        {
            try
            {
                macBytes[i] = Convert.ToInt32(submacs[i], 16) & 0xFF;
            }
            catch (Exception)
            {
                Console.Error.WriteLine($"Error: invalid processor id format (not XX-XX-XX-XX-XX-XX): {mac}");
                return "";
            }
        }

        string recognizedMacId = string.Format(
            "{0:X2}-{1:X2}-{2:X2}-{3:X2}-{4:X2}-{5:X2}",
            macBytes[0], macBytes[1], macBytes[2], macBytes[3], macBytes[4], macBytes[5]);
        macWords[0] = (macBytes[0] << 8) | macBytes[1];
        macWords[1] = (macBytes[2] << 8) | macBytes[3];
        macWords[2] = (macBytes[4] << 8) | macBytes[5];
        return recognizedMacId;
    }

    // parseKeycode is deferred — it maps Java AWT VK_xxx names to int codes
    // for keyboard mapping. In the headless harness (Phase D-12) we don't
    // process keyboard input, so this isn't needed yet. Phase E will add it
    // alongside the Avalonia keyboard mapper.
}
