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
OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ANY THEORY OF LIABILITY,
WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR
OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF
ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
*/

namespace Dwarf.Duchess;

// Minimal `.properties` file reader, modelled on Java's java.util.Properties
// + Dwarf's PropertiesExt extension that adds typed accessors with defaults.
//
// The .properties format we support:
//   - key=value or key:value (whitespace allowed around the separator)
//   - # or ! starts a comment line
//   - blank lines ignored
//
// Java's full .properties supports backslash line-continuation, Unicode
// escapes, and various whitespace-as-separator forms. We support what Dwarf
// configs actually use (simple key=value, # comments) — extend if a real
// config breaks the parser.
public class PropertiesExt
{
    private readonly Dictionary<string, string> _map = new(StringComparer.Ordinal);

    // Load properties from a file. Throws IOException on read failure.
    public void load(string filePath)
    {
        using var reader = new StreamReader(filePath);
        load(reader);
    }

    public void load(TextReader reader)
    {
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            string trimmed = line.Trim();
            if (trimmed.Length == 0) { continue; }
            if (trimmed[0] == '#' || trimmed[0] == '!') { continue; }

            int sep = trimmed.IndexOfAny(new[] { '=', ':' });
            if (sep < 0) { continue; } // skip malformed lines

            string key = trimmed.Substring(0, sep).Trim();
            string value = trimmed.Substring(sep + 1).Trim();
            if (key.Length == 0) { continue; }

            _map[key] = value;
        }
    }

    public bool containsKey(string name) => _map.ContainsKey(name);

    public string? getProperty(string name) => _map.TryGetValue(name, out var v) ? v : null;

    // Read a string property with a default value.
    public string? getString(string name, string? defValue)
    {
        if (!_map.ContainsKey(name)) { return defValue; }
        string? val = _map[name];
        if (string.IsNullOrEmpty(val)) { return null; }
        return val;
    }

    public string? getString(string name) => getString(name, "");

    // Read an integer property with default value.
    public int getInt(string name, int defValue)
    {
        if (!_map.ContainsKey(name)) { return defValue; }
        if (int.TryParse(_map[name], out int v)) { return v; }
        return defValue;
    }

    public int getInt(string name) => getInt(name, 0);

    // Read a boolean property. Java's PropertiesExt accepts 'true', 'yes',
    // or 'y' (case-insensitive); anything else is false.
    public bool getBoolean(string name, bool defValue)
    {
        if (!_map.ContainsKey(name)) { return defValue; }
        string val = _map[name];
        if (string.IsNullOrEmpty(val)) { return false; }
        val = val.ToLowerInvariant();
        return val == "true" || val == "yes" || val == "y";
    }

    public bool getBoolean(string name) => getBoolean(name, false);
}
