#region

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

#endregion

namespace SwiftPbo
{
    internal static class PboUtilities
    {
        public static ulong ReadLong(Stream reader)
        {
            byte[] buffer = new byte[4];
            reader.Read(buffer, 0, 4);
            return BitConverter.ToUInt32(buffer, 0);
        }

        public static void WriteLong(Stream writer, long num)
        {
            byte[] buffer = BitConverter.GetBytes(num);
            writer.Write(buffer, 0, 4);
        }

        public static string ReadString(Stream reader)
        {
            string str = "";
            while (true)
            {
                byte ch = (byte)reader.ReadByte();
                if (ch == 0x0)
                    break;
                str += (char)ch;
            }

            return str;
        }

        public static void WriteString(FileStream stream, string str)
        {
            byte[] buffer = Encoding.UTF8.GetBytes(str + "\0");
            stream.Write(buffer, 0, buffer.Length);
        }

        public static string GetRelativePath(string filespec, string folder)
        {
            Uri pathUri = new Uri(filespec);

            // Folders must end in a slash
            if (!folder.EndsWith(Path.DirectorySeparatorChar.ToString(CultureInfo.InvariantCulture)))
            {
                folder += Path.DirectorySeparatorChar;
            }

            Uri folderUri = new Uri(folder);
            return Uri.UnescapeDataString(folderUri.MakeRelativeUri(pathUri).ToString()
                .Replace('/', Path.DirectorySeparatorChar));
        }

        public static byte[] ReadStringArray(Stream reader)
        {
            List<byte> list = new List<byte>();
            while (true)
            {
                byte ch = (byte)reader.ReadByte();
                if (ch == 0x0)
                    break;
                list.Add(ch);
            }

            return list.ToArray();
        }

        public static void WriteASIIZ(FileStream stream, byte[] fileName)
        {
            byte[] copy = new byte[fileName.Count() + 1];
            fileName.CopyTo(copy, 0);
            copy[fileName.Length] = 0x0;
            stream.Write(copy, 0, copy.Length);
        }
    }
}