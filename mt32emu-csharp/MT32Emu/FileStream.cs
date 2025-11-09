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

namespace MT32Emu;

using Bit8u = System.Byte;

public class FileStream : AbstractFile, IDisposable
{
    private System.IO.FileStream? fileStream;
    private byte[]? data;
    private nuint size;
    private bool disposed;

    public FileStream()
    {
        fileStream = null;
        data = null;
        size = 0;
        disposed = false;
    }

    ~FileStream()
    {
        Dispose(false);
    }

    public override nuint GetSize()
    {
        if (size != 0)
        {
            return size;
        }
        if (fileStream == null || !fileStream.CanRead)
        {
            return 0;
        }
        size = (nuint)fileStream.Length;
        return size;
    }

    public override ReadOnlySpan<Bit8u> GetData()
    {
        if (data != null)
        {
            return data;
        }
        if (fileStream == null || !fileStream.CanRead)
        {
            return ReadOnlySpan<Bit8u>.Empty;
        }
        if (GetSize() == 0)
        {
            return ReadOnlySpan<Bit8u>.Empty;
        }

        byte[] fileData = new byte[size];
        fileStream.Seek(0, SeekOrigin.Begin);
        int bytesRead = fileStream.Read(fileData, 0, (int)size);
        if (bytesRead != (int)size)
        {
            return ReadOnlySpan<Bit8u>.Empty;
        }
        data = fileData;
        Close();
        return data;
    }

    public bool Open(string filename)
    {
        try
        {
            fileStream?.Close();
            fileStream = System.IO.File.OpenRead(filename);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public override void Close()
    {
        fileStream?.Close();
        fileStream = null;
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!disposed)
        {
            if (disposing)
            {
                fileStream?.Dispose();
            }
            disposed = true;
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}
