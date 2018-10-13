#region

using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

#endregion

namespace SwiftPbo.Tests
{
    [TestFixture]
    internal class PboTest
    {
        [SetUp]
        protected void SetUp()
        {
            const string Sha = "2DEA9A198FDCF0FE70473C079F1036B6E16FBFCE";
            _checksum = Enumerable.Range(0, Sha.Length)
                .Where(x => x % 2 == 0)
                .Select(x => Convert.ToByte(Sha.Substring(x, 2), 16))
                .ToArray();
        }

        private byte[] _checksum;

        [Test]
        public void CloneArchiveTest()
        {
            PboArchive pboArchive = new PboArchive("testdata/cba_common.pbo");
            Dictionary<FileEntry, string> files = new Dictionary<FileEntry, string>();

            foreach (FileEntry entry in pboArchive.Files)
            {
                FileInfo info = new FileInfo(Path.Combine("testdata\\cba_common", entry.FileName));
                Assert.That(info.Exists);
                files.Add(entry, info.FullName);
            }

            PboArchive.Clone("clone_common.pbo", pboArchive.ProductEntry, files, pboArchive.Checksum);

            PboArchive cloneArchive = new PboArchive("clone_common.pbo");

            Assert.That(pboArchive.Checksum.SequenceEqual(cloneArchive.Checksum), "Checksum dosen't match");

            Assert.That(pboArchive.Files.Count == cloneArchive.Files.Count, "Checksum dosen't match");

            Assert.That(pboArchive.ProductEntry.Name == cloneArchive.ProductEntry.Name);

            Assert.That(pboArchive.ProductEntry.Prefix == cloneArchive.ProductEntry.Prefix);

            Assert.That(pboArchive.ProductEntry.Addtional.Count == cloneArchive.ProductEntry.Addtional.Count);
        }

        [Test]
        public void CreateArchiveTest()
        {
            Assert.That(PboArchive.Create("testdata\\cba_common", "cba_common.pbo"));

            PboArchive pbo = new PboArchive("cba_common.pbo");

            Assert.That(pbo.Files.Count == 113);

            // checksums shoulden't match due to the time.
            Assert.False(pbo.Checksum.SequenceEqual(_checksum), "Checksum match");

            Assert.That(pbo.ProductEntry.Name == "prefix");

            Assert.That(pbo.ProductEntry.Prefix == @"x\cba\addons\common");

            Assert.That(pbo.ProductEntry.Addtional.Count == 1); // i don't add wonky shit like mikero.
        }

        [Test]
        public void OpenArchiveTest()
        {
            PboArchive pboArchive = new PboArchive("testdata/cba_common.pbo");
            Assert.That(pboArchive.Files.Count == 113);

            Assert.That(pboArchive.Checksum.SequenceEqual(_checksum), "Checksum dosen't match");

            Assert.That(pboArchive.ProductEntry.Name == "prefix");

            Assert.That(pboArchive.ProductEntry.Prefix == @"x\cba\addons\common");

            Assert.That(pboArchive.ProductEntry.Addtional.Count == 3);
        }
    }
}