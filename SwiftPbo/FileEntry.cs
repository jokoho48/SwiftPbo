#region

using System;
using System.IO;

#endregion

namespace SwiftPbo
{
    [Serializable]
    public class FileEntry
    {
        [NonSerialized] public readonly PboArchive ParentArchive;

        public string _fileName;

        public ulong DataSize;

        public byte[] OrgName;

        public ulong OriginalSize;

        public PackingType PackingMethod = PackingType.Uncompressed;

        public ulong StartOffset;

        public ulong TimeStamp;

        public FileEntry(string filename, ulong type, ulong osize, ulong timestamp, ulong datasize,
            ulong startOffset = 0x0)
        {
            _fileName = filename;
            switch (type)
            {
                case 0x0:
                    PackingMethod = PackingType.Uncompressed;
                    break;

                case 0x43707273:
                    PackingMethod = PackingType.Packed;
                    break;

                case 0x56657273: //Vers
                    PackingMethod = PackingType.Uncompressed;
                    break;

                case 0x456e6372: //Encr
                    PackingMethod = PackingType.Encrypted;
                    break;
            }

            OriginalSize = osize;
            TimeStamp = timestamp;
            DataSize = datasize;
            StartOffset = startOffset;
        }

        public FileEntry(PboArchive parent, string filename, ulong type, ulong osize, ulong timestamp, ulong datasize,
            byte[] file, ulong startOffset = 0x0)
            : this(filename, type, osize, timestamp, datasize)
        {
            ParentArchive = parent;
            OrgName = file;
        }

        public string FileName
        {
            get => _fileName;
            set => _fileName = value;
        }

        public override string ToString()
        {
            return string.Format("{0} ({1})", _fileName, OriginalSize);
        }

        public bool Extract(string outpath)
        {
            if (ParentArchive == null)
                throw new Exception("No parent Archive");
            if (!Directory.Exists(Path.GetDirectoryName(outpath)))
                Directory.CreateDirectory(Path.GetDirectoryName(outpath));
            return ParentArchive.Extract(this, outpath);
        }

        public Stream Extract()
        {
            if (ParentArchive == null)
                throw new Exception("No parent Archive");
            return ParentArchive.Extract(this);
        }
    }
}