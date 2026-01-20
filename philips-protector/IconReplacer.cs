using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

namespace philips_protector
{
    /// <summary>
    /// Replaces icons inside an executable by updating its resources.
    /// </summary>
    public static class IconReplacer
    {
        private const int RT_ICON = 3;
        private const int RT_GROUP_ICON = 14;
        private const ushort LANG_NEUTRAL = 0x0000;

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr BeginUpdateResource(string pFileName, [MarshalAs(UnmanagedType.Bool)] bool bDeleteExistingResources);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool UpdateResource(IntPtr hUpdate, IntPtr lpType, IntPtr lpName, ushort wLanguage, byte[] lpData, uint cbData);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool EndUpdateResource(IntPtr hUpdate, bool fDiscard);

        /// <summary>
        /// Replace the icon of an executable with the supplied ICO file.
        /// </summary>
        public static void ReplaceIcon(string exePath, string icoPath)
        {
            if (string.IsNullOrWhiteSpace(exePath) || !File.Exists(exePath))
                throw new FileNotFoundException("Executable not found.", exePath);
            if (string.IsNullOrWhiteSpace(icoPath) || !File.Exists(icoPath))
                throw new FileNotFoundException("Icon file not found.", icoPath);

            byte[] icoBytes = File.ReadAllBytes(icoPath);
            IconFile iconFile = IconFile.FromBytes(icoBytes);

            IntPtr hUpdate = BeginUpdateResource(exePath, false);
            if (hUpdate == IntPtr.Zero)
                throw new InvalidOperationException("BeginUpdateResource failed.");

            try
            {
                // Update each RT_ICON resource with a unique id.
                for (int i = 0; i < iconFile.Images.Count; i++)
                {
                    IconImage img = iconFile.Images[i];
                    IntPtr iconId = new IntPtr(i + 1); // resource id starts at 1
                    bool iconResult = UpdateResource(hUpdate, new IntPtr(RT_ICON), iconId, LANG_NEUTRAL, img.ImageData, (uint)img.ImageData.Length);
                    if (!iconResult)
                        throw new InvalidOperationException("UpdateResource for RT_ICON failed.");
                }

                // Build and update the group icon resource (RT_GROUP_ICON).
                byte[] grpData = iconFile.CreateGroupIconResource();
                IntPtr groupId = new IntPtr(1);
                bool groupResult = UpdateResource(hUpdate, new IntPtr(RT_GROUP_ICON), groupId, LANG_NEUTRAL, grpData, (uint)grpData.Length);
                if (!groupResult)
                    throw new InvalidOperationException("UpdateResource for RT_GROUP_ICON failed.");
            }
            finally
            {
                if (!EndUpdateResource(hUpdate, false))
                {
                    throw new InvalidOperationException("EndUpdateResource failed.");
                }
            }
        }

        #region ICO parsing helpers

        private class IconFile
        {
            public List<IconImage> Images { get; private set; }

            private IconFile()
            {
                Images = new List<IconImage>();
            }

            public static IconFile FromBytes(byte[] bytes)
            {
                using (var ms = new MemoryStream(bytes))
                using (var br = new BinaryReader(ms))
                {
                    IconFile iconFile = new IconFile();

                    // ICONDIR
                    br.ReadUInt16(); // reserved
                    br.ReadUInt16(); // type
                    ushort count = br.ReadUInt16();

                    var entries = new IconDirEntry[count];
                    for (int i = 0; i < count; i++)
                    {
                        entries[i] = IconDirEntry.Read(br);
                    }

                    for (int i = 0; i < count; i++)
                    {
                        ms.Position = entries[i].ImageOffset;
                        byte[] data = br.ReadBytes(entries[i].BytesInRes);
                        var img = new IconImage
                        {
                            Width = entries[i].Width == 0 ? 256 : entries[i].Width,
                            Height = entries[i].Height == 0 ? 256 : entries[i].Height,
                            ColorCount = entries[i].ColorCount,
                            Planes = entries[i].Planes,
                            BitCount = entries[i].BitCount,
                            BytesInRes = entries[i].BytesInRes,
                            ImageData = data
                        };
                        iconFile.Images.Add(img);
                    }
                    return iconFile;
                }
            }

            public byte[] CreateGroupIconResource()
            {
                using (var ms = new MemoryStream())
                using (var bw = new BinaryWriter(ms))
                {
                    // GRPICONDIR
                    bw.Write((ushort)0); // reserved
                    bw.Write((ushort)1); // type
                    bw.Write((ushort)Images.Count);

                    for (int i = 0; i < Images.Count; i++)
                    {
                        IconImage img = Images[i];
                        bw.Write((byte)(img.Width == 256 ? 0 : img.Width));
                        bw.Write((byte)(img.Height == 256 ? 0 : img.Height));
                        bw.Write((byte)img.ColorCount);
                        bw.Write((byte)0); // reserved
                        bw.Write((ushort)img.Planes);
                        bw.Write((ushort)img.BitCount);
                        bw.Write(img.BytesInRes);
                        bw.Write((ushort)(i + 1)); // resource id matches UpdateResource for RT_ICON
                    }

                    bw.Flush();
                    return ms.ToArray();
                }
            }
        }

        private class IconImage
        {
            public int Width { get; set; }
            public int Height { get; set; }
            public byte ColorCount { get; set; }
            public ushort Planes { get; set; }
            public ushort BitCount { get; set; }
            public int BytesInRes { get; set; }
            public byte[] ImageData { get; set; }
        }

        private struct IconDirEntry
        {
            public byte Width;
            public byte Height;
            public byte ColorCount;
            public byte Reserved;
            public ushort Planes;
            public ushort BitCount;
            public int BytesInRes;
            public int ImageOffset;

            public static IconDirEntry Read(BinaryReader br)
            {
                IconDirEntry e = new IconDirEntry
                {
                    Width = br.ReadByte(),
                    Height = br.ReadByte(),
                    ColorCount = br.ReadByte(),
                    Reserved = br.ReadByte(),
                    Planes = br.ReadUInt16(),
                    BitCount = br.ReadUInt16(),
                    BytesInRes = br.ReadInt32(),
                    ImageOffset = br.ReadInt32()
                };
                return e;
            }
        }

        #endregion
    }
}
