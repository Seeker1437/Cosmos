﻿#define COSMOSDEBUG

using System;
using System.IO;

using Cosmos.Common;
using Cosmos.Debug.Kernel;
using Cosmos.System.FileSystem.FAT.Listing;

namespace Cosmos.System.FileSystem.FAT
{
    internal class FatStream : Stream
    {
        private readonly FatDirectoryEntry mDirectoryEntry;

        private readonly FatFileSystem mFS;

        protected byte[] mReadBuffer;

        //TODO: In future we might read this in as needed rather than
        // all at once. This structure will also consume 2% of file size in RAM
        // (for default cluster size of 2kb, ie 4 bytes per cluster)
        // so we might consider a way to flush it and only keep parts.
        // Example, a 100 MB file will require 2MB for this structure. That is
        // probably acceptable for the mid term future.
        private ulong[] mFatTable;

        protected ulong? mReadBufferPosition;

        protected ulong mPosition;

        private ulong mSize;

        public FatStream(FatDirectoryEntry aEntry)
        {
            Global.mFileSystemDebugger.SendInternal("FatStream.Ctor");

            mDirectoryEntry = aEntry;
            mFS = mDirectoryEntry.GetFileSystem();
            mSize = mDirectoryEntry.mSize;

            Global.mFileSystemDebugger.SendInternal("mSize =");
            Global.mFileSystemDebugger.SendInternal(mSize.ToString());

            // We get always the FatTable if the file is empty too
            mFatTable = mDirectoryEntry.GetFatTable();
            // What to do if this should happen? Throw exception?
            if (mFatTable == null)
            {
                Global.mFileSystemDebugger.SendInternal("FatTable got but it is null!");
            }
        }

        public override bool CanSeek
        {
            get
            {
                return true;
            }
        }

        public override bool CanRead
        {
            get
            {
                return true;
            }
        }

        public override bool CanWrite
        {
            get
            {
                return true;
            }
        }

        public sealed override long Length
        {
            get
            {
                if (mDirectoryEntry == null)
                {
                    throw new NullReferenceException("The stream does not currently have an open entry.");
                }
                Global.mFileSystemDebugger.SendInternal("FatStream.get_Length:");
                Global.mFileSystemDebugger.SendInternal("Length =");
                Global.mFileSystemDebugger.SendInternal(mSize.ToString());
                return (long)mSize;
            }
        }

        public override long Position
        {
            get
            {
                Global.mFileSystemDebugger.SendInternal("FatStream.get_Position:");
                Global.mFileSystemDebugger.SendInternal("Position =");
                Global.mFileSystemDebugger.SendInternal(mPosition.ToString());
                return (long)mPosition;
            }
            set
            {
                if (value < 0L)
                {
                    throw new ArgumentOutOfRangeException(nameof(value));
                }

                Global.mFileSystemDebugger.SendInternal("FatStream.set_Position:");
                Global.mFileSystemDebugger.SendInternal("Position =");
                Global.mFileSystemDebugger.SendInternal(mPosition.ToString());
                mPosition = (ulong)value;
            }
        }

        public override int Read(byte[] aBuffer, int aOffset, int aCount)
        {
            return Read(aBuffer, aOffset, aCount);
        }

        protected int Read(byte[] aBuffer, long aOffset, long aCount)
        {
            Global.mFileSystemDebugger.SendInternal("FatStream.Read:");

            if (aCount < 0)
            {
                throw new ArgumentOutOfRangeException("aCount");
            }
            if (aOffset < 0)
            {
                throw new ArgumentOutOfRangeException("aOffset");
            }
            if (aBuffer == null || aBuffer.Length - aOffset < aCount)
            {
                throw new ArgumentException("Invalid offset length!");
            }
            if (mFatTable.Length == 0 || mFatTable[0] == 0)
            {
                // FirstSector can be 0 for 0 length files
                return 0;
            }
            if (mPosition == mDirectoryEntry.mSize)
            {
                // EOF
                return 0;
            }

            Global.mFileSystemDebugger.SendInternal("aBuffer.Length =");
            Global.mFileSystemDebugger.SendInternal((uint)aBuffer.Length);
            Global.mFileSystemDebugger.SendInternal("aOffset =");
            Global.mFileSystemDebugger.SendInternal((uint)aOffset);
            Global.mFileSystemDebugger.SendInternal("aCount = ");
            Global.mFileSystemDebugger.SendInternal((uint)aCount);

            // reduce count, so that no out of bound exception occurs if not existing
            // entry is used in line mFS.ReadCluster(mFatTable[(int)xClusterIdx], xCluster);
            ulong xMaxReadableBytes = mDirectoryEntry.mSize - mPosition;
            ulong xCount = (ulong)aCount;
            if (xCount > xMaxReadableBytes)
            {
                xCount = xMaxReadableBytes;
            }

            var xCluster = mFS.NewClusterArray();
            uint xClusterSize = mFS.BytesPerCluster;

            while (xCount > 0)
            {
                ulong xClusterIdx = mPosition / xClusterSize;
                ulong xPosInCluster = mPosition % xClusterSize;
                mFS.Read(mFatTable[(int)xClusterIdx], out xCluster);
                long xReadSize;
                if (xPosInCluster + xCount > xClusterSize)
                {
                    xReadSize = (long)(xClusterSize - xPosInCluster - 1);
                }
                else
                {
                    xReadSize = (long)xCount;
                }
                // no need for a long version, because internal Array.Copy() does a cast down to int, and a range check,
                // or we do a semantic change here
                Array.Copy(xCluster, (long)xPosInCluster, aBuffer, aOffset, xReadSize);

                aOffset += xReadSize;
                xCount -= (ulong)xReadSize;
            }

            mPosition += (ulong)aOffset;
            return (int)aOffset;
        }

        public override void Flush()
        {
            throw new NotImplementedException();
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotImplementedException();
        }

        public override void SetLength(long value)
        {
            Global.mFileSystemDebugger.SendInternal("FatStream.SetLength:");
            Global.mFileSystemDebugger.SendInternal("value =");
            Global.mFileSystemDebugger.SendInternal(value.ToString());

            mDirectoryEntry.SetSize(value);
            mSize = (ulong)value;
        }

        public override void Write(byte[] aBuffer, int aOffset, int aCount)
        {
            Write(aBuffer, aOffset, aCount);
        }

        protected void Write(byte[] aBuffer, long aOffset, long aCount)
        {
            Global.mFileSystemDebugger.SendInternal("FatStream.Write:");

            if (aCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(aCount));
            }
            if (aOffset < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(aOffset));
            }
            if (aBuffer == null || aBuffer.Length - aOffset < aCount)
            {
                throw new ArgumentException("Invalid offset length!");
            }

            Global.mFileSystemDebugger.SendInternal("aBuffer.Length =");
            Global.mFileSystemDebugger.SendInternal(aBuffer.Length);
            Global.mFileSystemDebugger.SendInternal("aOffset =");
            Global.mFileSystemDebugger.SendInternal(aOffset);
            Global.mFileSystemDebugger.SendInternal("aCount =");
            Global.mFileSystemDebugger.SendInternal(aCount);
            Global.mFileSystemDebugger.SendInternal("mPosition =");
            Global.mFileSystemDebugger.SendInternal(mPosition);

            ulong xCount = (ulong)aCount;
            Global.mFileSystemDebugger.SendInternal("xCount =");
            Global.mFileSystemDebugger.SendInternal(xCount);

            var xCluster = mFS.NewClusterArray();
            uint xClusterSize = mFS.BytesPerCluster;

            long xTotalLength = (long)(mPosition + xCount);
            Global.mFileSystemDebugger.SendInternal("xTotalLength =");
            Global.mFileSystemDebugger.SendInternal(xTotalLength);
            Global.mFileSystemDebugger.SendInternal("Length =");
            Global.mFileSystemDebugger.SendInternal(Length);

            if (xTotalLength > Length)
            {
                SetLength(xTotalLength);
            }

            while (xCount > 0)
            {
                long xWriteSize;
                ulong xClusterIdx = mPosition / xClusterSize;
                ulong xPosInCluster = mPosition % xClusterSize;
                if (xPosInCluster + xCount > xClusterSize)
                {
                    xWriteSize = (long)(xClusterSize - xPosInCluster - 1);
                }
                else
                {
                    xWriteSize = (long)xCount;
                }

                mFS.Read(xClusterIdx, out xCluster);

                Global.mFileSystemDebugger.SendInternal("Cluster index =");
                Global.mFileSystemDebugger.SendInternal(xClusterIdx.ToString());
                Global.mFileSystemDebugger.SendInternal("Cluster write offset =");
                Global.mFileSystemDebugger.SendInternal(xPosInCluster.ToString());
                Global.mFileSystemDebugger.SendInternal("Write buffer offset =");
                Global.mFileSystemDebugger.SendInternal(aOffset.ToString());

                Array.Copy(aBuffer, aOffset, xCluster, (long)xPosInCluster, xWriteSize);

                mFS.Write(mFatTable[(int)xClusterIdx], xCluster);
                Global.mFileSystemDebugger.SendInternal("Data written");

                aOffset += xWriteSize;
                xCount -= (ulong)xWriteSize;
            }

            mPosition += (ulong)aOffset;
        }
    }
}
