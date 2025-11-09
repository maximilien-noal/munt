/* Copyright (C) 2003, 2004, 2005, 2006, 2008, 2009 Dean Beeler, Jerome Fisher
 * Copyright (C) 2011-2022 Dean Beeler, Jerome Fisher, Sergey V. Mikayev
 *
 *  This program is free software: you can redistribute it and/or modify
 *  it under the terms of the GNU Lesser General Public License as published by
 *  the Free Software Foundation, either version 2.1 of the License, or
 *  (at your option) any later version.
 *
 *  This program is distributed in the hope that it will be useful,
 *  but WITHOUT ANY WARRANTY; without even the implied warranty of
 *  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 *  GNU Lesser General Public License for more details.
 *
 *  You should have received a copy of the GNU Lesser General Public License
 *  along with this program.  If not, see <http://www.gnu.org/licenses/>.
 */

using System.Security.Cryptography;

namespace MT32Emu;

using Bit8u = System.Byte;

public interface IFile
{
    // Includes terminator char - 40 hex digits + null terminator
    string GetSHA1();
    nuint GetSize();
    ReadOnlySpan<Bit8u> GetData();
    void Close();
}

public abstract class AbstractFile : IFile
{
    private bool sha1DigestCalculated;
    private string sha1Digest;

    protected AbstractFile()
    {
        sha1DigestCalculated = false;
        sha1Digest = string.Empty;
    }

    protected AbstractFile(string sha1DigestValue)
    {
        sha1DigestCalculated = true;
        sha1Digest = sha1DigestValue;
    }

    public string GetSHA1()
    {
        if (sha1DigestCalculated)
        {
            return sha1Digest;
        }
        sha1DigestCalculated = true;

        nuint size = GetSize();
        if (size == 0)
        {
            return sha1Digest;
        }

        ReadOnlySpan<Bit8u> data = GetData();
        if (data.IsEmpty)
        {
            return sha1Digest;
        }

        // Calculate SHA1 using .NET's SHA1
        byte[] hashBytes = SHA1.HashData(data);
        sha1Digest = Convert.ToHexString(hashBytes).ToLowerInvariant();
        return sha1Digest;
    }

    public abstract nuint GetSize();
    public abstract ReadOnlySpan<Bit8u> GetData();
    public abstract void Close();
}

public class ArrayFile : AbstractFile
{
    private readonly byte[] data;
    private readonly nuint size;

    public ArrayFile(ReadOnlySpan<Bit8u> useData)
    {
        data = useData.ToArray();
        size = (nuint)data.Length;
    }

    public ArrayFile(ReadOnlySpan<Bit8u> useData, string useSHA1Digest) : base(useSHA1Digest)
    {
        data = useData.ToArray();
        size = (nuint)data.Length;
    }

    public override nuint GetSize()
    {
        return size;
    }

    public override ReadOnlySpan<Bit8u> GetData()
    {
        return data;
    }

    public override void Close()
    {
        // Nothing to do for array-backed files
    }
}
