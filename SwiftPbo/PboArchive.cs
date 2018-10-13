﻿#region

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

#endregion

namespace SwiftPbo
{
    public enum PackingType
    {
        Uncompressed,
        Packed,
        Encrypted
    }

    public class PboArchive : IDisposable
    {
        private static readonly List<char> InvaildFile = Path.GetInvalidFileNameChars().ToList();

        private static readonly List<char> InvaildPath = Path.GetInvalidPathChars().ToList();
        private static byte[] _file;

        private static readonly List<char> _literalList = new List<char>
            {'\'', '\"', '\\', '\0', '\a', '\b', '\f', '\n', '\r', '\t', '\v'};

        private readonly FileStream _stream;

        public PboArchive(string path, bool close = true)
        {
            if (!File.Exists(path))
                throw new FileNotFoundException("File not Found");
            PboPath = path;
            _stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 8,
                FileOptions.SequentialScan);
            if (_stream.ReadByte() != 0x0)
                return;
            if (!ReadHeader(_stream))
                _stream.Position = 0;
            while (true)
            {
                if (!ReadEntry(_stream))
                    break;
            }

            DataStart = _stream.Position;
            ReadChecksum(_stream);
            if (close)
            {
                _stream.Dispose();
                _stream = null;
            }
        }

        public List<FileEntry> Files { get; } = new List<FileEntry>();

        public ProductEntry ProductEntry { get; private set; } = new ProductEntry("", "", "", new List<string>());

        public byte[] Checksum { get; private set; }

        public string PboPath { get; }

        public long DataStart { get; }

        public void Dispose()
        {
            _stream?.Dispose();
        }

        public static bool Create(string directoryPath, string outpath)
        {
            DirectoryInfo dir = new DirectoryInfo(directoryPath);
            if (!dir.Exists)
                throw new DirectoryNotFoundException();
            directoryPath = dir.FullName;
            ProductEntry entry = new ProductEntry("prefix", "", "", new List<string>());
            string[] files = Directory.GetFiles(directoryPath, "$*$");
            foreach (string file in files)
            {
                string varname = Path.GetFileNameWithoutExtension(file).Trim('$');
                string data = File.ReadAllText(file).Split('\n')[0];
                switch (varname.ToLowerInvariant())
                {
                    case "pboprefix":
                        entry.Prefix = data;
                        break;

                    case "prefix":
                        entry.Prefix = data;
                        break;

                    case "version":
                        entry.ProductVersion = data;
                        break;

                    default:
                        entry.Addtional.Add(data);
                        break;
                }
            }

            return Create(directoryPath, outpath, entry);
        }

        public static bool Create(string directoryPath, string outpath, ProductEntry productEntry)
        {
            DirectoryInfo dir = new DirectoryInfo(directoryPath);
            if (!dir.Exists)
                throw new DirectoryNotFoundException();
            directoryPath = dir.FullName;
            string[] files = Directory.GetFiles(directoryPath, "*", SearchOption.AllDirectories);
            List<FileEntry> entries = new List<FileEntry>();
            foreach (string file in files)
            {
                if (Path.GetFileName(file).StartsWith("$") && Path.GetFileName(file).EndsWith("$"))
                    continue;
                FileInfo info = new FileInfo(file);
                string path = PboUtilities.GetRelativePath(info.FullName, directoryPath);
                entries.Add(new FileEntry(path, 0x0, (ulong)info.Length,
                    (ulong)DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalSeconds, (ulong)info.Length));
            }

            try
            {
                using (FileStream stream = File.Create(outpath))
                {
                    stream.WriteByte(0x0);
                    WriteProductEntry(productEntry, stream);
                    stream.WriteByte(0x0);
                    entries.Add(new FileEntry(null, "", 0, 0, 0, 0, _file));
                    foreach (FileEntry entry in entries)
                    {
                        WriteFileEntry(stream, entry);
                    }

                    entries.Remove(entries.Last());
                    foreach (FileEntry entry in entries)
                    {
                        byte[] buffer = new byte[2949120];
                        using (FileStream open = File.OpenRead(Path.Combine(directoryPath, entry.FileName)))
                        {
                            int read = 4324324;
                            while (read > 0)
                            {
                                read = open.Read(buffer, 0, buffer.Length);
                                stream.Write(buffer, 0, read);
                            }
                        }
                    }

                    stream.Position = 0;
                    byte[] hash;
                    using (SHA1Managed sha1 = new SHA1Managed())
                    {
                        hash = sha1.ComputeHash(stream);
                    }

                    stream.WriteByte(0x0);
                    stream.Write(hash, 0, 20);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                return false;
            }

            return true;
        }

        public static string SterilizePath(string path)
        {
            char[] arr = Path.GetDirectoryName(path).ToCharArray();
            StringBuilder builder = new StringBuilder(arr.Count());
            string dirpath = Path.GetDirectoryName(path);
            for (int i = 0; i < dirpath.Length; i++)
            {
                if (!InvaildPath.Contains(path[i]) && path[i] != Path.AltDirectorySeparatorChar)
                    builder.Append(path[i]);
                if (path[i] == Path.AltDirectorySeparatorChar)
                    builder.Append(Path.DirectorySeparatorChar);
            }

            char[] filename = Path.GetFileName(path).ToCharArray();
            for (int i = 0; i < filename.Length; i++)
            {
                char ch = filename[i];
                if (!InvaildFile.Contains(ch) && ch != '*' && !IsLiteral(ch))
                {
                    continue;
                }

                filename[i] = (char)Math.Min(90, 65 + ch % 5);
            }

            return Path.Combine(builder.ToString(), new string(filename));
        }

        private static bool IsLiteral(char ch)
        {
            return _literalList.Contains(ch);
        }

        public static void Clone(string path, ProductEntry productEntry, Dictionary<FileEntry, string> files,
            byte[] checksum = null)
        {
            if (!Directory.Exists(Path.GetDirectoryName(path)) && !string.IsNullOrEmpty(Path.GetDirectoryName(path)))
                Directory.CreateDirectory(Path.GetDirectoryName(path));
            using (FileStream stream = File.Create(path))
            {
                stream.WriteByte(0x0);
                WriteProductEntry(productEntry, stream);
                stream.WriteByte(0x0);
                files.Add(new FileEntry(null, "", 0, 0, 0, 0, _file), "");
                foreach (FileEntry entry in files.Keys)
                {
                    WriteFileEntry(stream, entry);
                }

                files.Remove(files.Last().Key);
                foreach (string file in files.Values)
                {
                    byte[] buffer = new byte[2949120];
                    using (FileStream open = File.OpenRead(file))
                    {
                        int bytesRead;
                        while ((bytesRead =
                                   open.Read(buffer, 0, 2949120)) > 0)
                        {
                            stream.Write(buffer, 0, bytesRead);
                        }
                    }
                }

                if (checksum != null && checksum.Any(b => b != 0))
                {
                    stream.WriteByte(0x0);
                    stream.Write(checksum, 0, checksum.Length);
                }
                else if (checksum == null)
                {
                    stream.Position = 0;
                    byte[] hash;
                    using (SHA1Managed sha1 = new SHA1Managed())
                    {
                        hash = sha1.ComputeHash(stream);
                    }

                    stream.WriteByte(0x0);
                    stream.Write(hash, 0, 20);
                }
            }
        }

        private static void WriteFileEntry(FileStream stream, FileEntry entry)
        {
            if (entry.OrgName != null)
                PboUtilities.WriteASIIZ(stream, entry.OrgName);
            else
                PboUtilities.WriteString(stream, entry.FileName);
            long packing = 0x0;
            switch (entry.PackingMethod)
            {
                case PackingType.Packed:
                    packing = 0x43707273;
                    break;

                case PackingType.Encrypted:
                    packing = 0x456e6372;
                    break;
            }

            PboUtilities.WriteLong(stream, packing);
            PboUtilities.WriteLong(stream, (long)entry.OriginalSize);
            PboUtilities.WriteLong(stream, (long)entry.StartOffset);
            PboUtilities.WriteLong(stream, (long)entry.TimeStamp);
            PboUtilities.WriteLong(stream, (long)entry.DataSize);
        }

        private static void WriteProductEntry(ProductEntry productEntry, FileStream stream)
        {
            PboUtilities.WriteString(stream, "sreV");
            stream.Write(new byte[15], 0, 15);
            if (!string.IsNullOrEmpty(productEntry.Name))
                PboUtilities.WriteString(stream, productEntry.Name);
            else
                return;
            if (!string.IsNullOrEmpty(productEntry.Prefix))
                PboUtilities.WriteString(stream, productEntry.Prefix);
            else
                return;
            if (!string.IsNullOrEmpty(productEntry.ProductVersion))
                PboUtilities.WriteString(stream, productEntry.ProductVersion);
            else
                return;
            foreach (string str in productEntry.Addtional)
            {
                PboUtilities.WriteString(stream, str);
            }
        }

        private void ReadChecksum(FileStream stream)
        {
            long pos = DataStart + Files.Sum(fileEntry => (long)fileEntry.DataSize) + 1;
            stream.Position = pos;
            Checksum = new byte[20];
            stream.Read(Checksum, 0, 20);
            stream.Position = DataStart;
        }

        private bool ReadEntry(FileStream stream)
        {
            byte[] file = PboUtilities.ReadStringArray(stream);
            string filename = Encoding.UTF8.GetString(file).Replace("\t", "\\t");

            ulong packing = PboUtilities.ReadLong(stream);

            ulong size = PboUtilities.ReadLong(stream);

            ulong startOffset = PboUtilities.ReadLong(stream);

            ulong timestamp = PboUtilities.ReadLong(stream);
            ulong datasize = PboUtilities.ReadLong(stream);
            FileEntry entry = new FileEntry(this, filename, packing, size, timestamp, datasize, file, startOffset);
            if (entry.FileName == "")
            {
                entry.OrgName = new byte[0];
                return false;
            }

            Files.Add(entry);
            return true;
        }

        private bool ReadHeader(FileStream stream)
        {
            // TODO FIX SO BROKEN
            string str = PboUtilities.ReadString(stream);
            if (str != "sreV")
                return false;
            int count = 0;
            while (count < 15)
            {
                stream.ReadByte();
                count++;
            }

            List<string> list = new List<string>();
            string pboname = "";
            string version = "";
            string prefix = PboUtilities.ReadString(stream);
            if (!string.IsNullOrEmpty(prefix))
            {
                pboname = PboUtilities.ReadString(stream);
                if (!string.IsNullOrEmpty(pboname))
                {
                    version = PboUtilities.ReadString(stream);

                    if (!string.IsNullOrEmpty(version))
                    {
                        while (stream.ReadByte() != 0x0)
                        {
                            stream.Position--;
                            string s = PboUtilities.ReadString(stream);
                            list.Add(s);
                        }
                    }
                }
            }

            ProductEntry = new ProductEntry(prefix, pboname, version, list);

            return true;
        }

        public bool ExtractAll(string outpath)
        {
            if (!Directory.Exists(outpath))
                Directory.CreateDirectory(outpath);
            byte[] buffer = new byte[10000000];
            int files = 0;
            foreach (FileEntry file in Files)
            {
                Stream stream = GetFileStream(file);

                Console.WriteLine("FILE START");
                files++;
                long totalread = (long)file.DataSize;
                string pboPath =
                    SterilizePath(Path.Combine(outpath, file.FileName));
                if (!Directory.Exists(Path.GetDirectoryName(pboPath)))
                    Directory.CreateDirectory(Path.GetDirectoryName(pboPath));
                using (FileStream outfile = File.Create(pboPath))
                {
                    while (totalread > 0)
                    {
                        int read = stream.Read(buffer, 0, (int)Math.Min(10000000, totalread));
                        if (read <= 0)
                            return true;
                        outfile.Write(buffer, 0, read);
                        totalread -= read;
                    }
                }

                Console.WriteLine("FILE END " + files);
            }

            return true;
        }

        public bool Extract(FileEntry fileEntry, string outpath)
        {
            if (string.IsNullOrEmpty(outpath))
                throw new NullReferenceException("Is null or empty");
            Stream mem = GetFileStream(fileEntry);
            if (mem == null)
                throw new Exception("WTF no stream");
            if (!Directory.Exists(Path.GetDirectoryName(outpath)))
                Directory.CreateDirectory(Path.GetDirectoryName(outpath));
            ulong totalread = fileEntry.DataSize;
            using (FileStream outfile = File.OpenWrite(outpath))
            {
                byte[] buffer = new byte[2949120];
                while (totalread > 0)
                {
                    int read = mem.Read(buffer, 0, (int)Math.Min(2949120, totalread));
                    outfile.Write(buffer, 0, read);
                    totalread -= (ulong)read;
                }
            }

            mem.Close();
            return true;
        }

        private Stream GetFileStream(FileEntry fileEntry)
        {
            if (_stream != null)
            {
                _stream.Position = (long)GetFileStreamPos(fileEntry);
                return _stream;
            }

            FileStream mem = File.OpenRead(PboPath);
            mem.Position = (long)GetFileStreamPos(fileEntry);
            return mem;
        }

        private ulong GetFileStreamPos(FileEntry fileEntry)
        {
            ulong start = (ulong)DataStart;
            return Files.TakeWhile(entry => entry != fileEntry)
                .Aggregate(start, (current, entry) => current + entry.DataSize);
        }

        // returns a stream
        /// <summary>
        ///     Returns a filestream to the ENTIRE pbo set at the file entry pos.
        /// </summary>
        /// <param name="fileEntry"></param>
        /// <returns></returns>
        public Stream Extract(FileEntry fileEntry)
        {
            return GetFileStream(fileEntry);
        }
    }
}