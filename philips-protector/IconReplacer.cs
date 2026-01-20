using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Linq;

namespace philips_protector
{
    /// <summary>
    /// Replaces icons inside an executable by updating its resources.
    /// </summary>
    public static class IconReplacer
    {
        private const int RT_ICON = 3;
        private const int RT_GROUP_ICON = 14;
        private const int LOAD_LIBRARY_AS_DATAFILE = 0x00000002;
        private const int LOAD_LIBRARY_AS_IMAGE_RESOURCE = 0x00000020;
        private const ushort LANG_NEUTRAL = 0x0000;

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr BeginUpdateResource(string pFileName, [MarshalAs(UnmanagedType.Bool)] bool bDeleteExistingResources);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool UpdateResource(IntPtr hUpdate, IntPtr lpType, IntPtr lpName, ushort wLanguage, byte[] lpData, uint cbData);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool EndUpdateResource(IntPtr hUpdate, bool fDiscard);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool EnumResourceNames(IntPtr hModule, IntPtr lpszType, EnumResNameDelegate lpEnumFunc, IntPtr lParam);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr LoadLibraryEx(string lpFileName, IntPtr hFile, int dwFlags);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool FreeLibrary(IntPtr hModule);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr FindResource(IntPtr hModule, IntPtr lpName, IntPtr lpType);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr LoadResource(IntPtr hModule, IntPtr hResInfo);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr LockResource(IntPtr hResData);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern uint SizeofResource(IntPtr hModule, IntPtr hResInfo);

        private delegate bool EnumResNameDelegate(IntPtr hModule, IntPtr lpszType, IntPtr lpszName, IntPtr lParam);

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

            // Discover and clean existing icon resources so old resolutions are not kept.
            List<ExistingIconGroup> existingGroups = GetExistingIconGroups(exePath);
            HashSet<ushort> iconIdsToRemove = new HashSet<ushort>();
            foreach (var group in existingGroups)
            {
                foreach (var id in group.IconIds)
                {
                    iconIdsToRemove.Add(id);
                }
            }

            IntPtr groupIdToUse = existingGroups.Count > 0 ? existingGroups[0].GroupId : new IntPtr(1);

            IntPtr hUpdate = BeginUpdateResource(exePath, false);
            if (hUpdate == IntPtr.Zero)
                throw new InvalidOperationException("BeginUpdateResource failed.");

            try
            {
                // Remove previous icon groups and icon images referenced by them.
                foreach (var group in existingGroups)
                {
                    UpdateResource(hUpdate, new IntPtr(RT_GROUP_ICON), group.GroupId, LANG_NEUTRAL, null, 0);
                }
                foreach (ushort iconId in iconIdsToRemove)
                {
                    UpdateResource(hUpdate, new IntPtr(RT_ICON), new IntPtr(iconId), LANG_NEUTRAL, null, 0);
                }

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
                bool groupResult = UpdateResource(hUpdate, new IntPtr(RT_GROUP_ICON), groupIdToUse, LANG_NEUTRAL, grpData, (uint)grpData.Length);
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

        private struct GroupIconEntry
        {
            public byte Width;
            public byte Height;
            public byte ColorCount;
            public byte Reserved;
            public ushort Planes;
            public ushort BitCount;
            public uint BytesInRes;
            public ushort ResourceId;
        }

        private class ExistingIconGroup
        {
            public IntPtr GroupId { get; set; }
            public List<ushort> IconIds { get; set; }

            public ExistingIconGroup()
            {
                IconIds = new List<ushort>();
            }
        }

        private static List<ExistingIconGroup> GetExistingIconGroups(string exePath)
        {
            var groups = new List<ExistingIconGroup>();
            IntPtr hModule = LoadLibraryEx(exePath, IntPtr.Zero, LOAD_LIBRARY_AS_DATAFILE | LOAD_LIBRARY_AS_IMAGE_RESOURCE);
            if (hModule == IntPtr.Zero)
            {
                return groups;
            }

            try
            {
                EnumResNameDelegate cb = delegate (IntPtr h, IntPtr type, IntPtr name, IntPtr l)
                {
                    ushort groupIdNumeric;
                    if (!TryGetNumericId(name, out groupIdNumeric))
                    {
                        return true; // Skip non-numeric resource names for simplicity.
                    }

                    byte[] data = LoadResourceData(hModule, RT_GROUP_ICON, name);
                    if (data == null || data.Length < 6)
                    {
                        return true;
                    }

                    var entries = ParseGroupEntries(data);
                    var group = new ExistingIconGroup
                    {
                        GroupId = new IntPtr(groupIdNumeric),
                        IconIds = entries.Select(e => e.ResourceId).ToList()
                    };
                    groups.Add(group);
                    return true;
                };

                EnumResourceNames(hModule, new IntPtr(RT_GROUP_ICON), cb, IntPtr.Zero);
            }
            finally
            {
                FreeLibrary(hModule);
            }

            return groups;
        }

        private static List<GroupIconEntry> ParseGroupEntries(byte[] groupData)
        {
            var entries = new List<GroupIconEntry>();
            using (var ms = new MemoryStream(groupData))
            using (var br = new BinaryReader(ms))
            {
                br.ReadUInt16(); // reserved
                br.ReadUInt16(); // type
                ushort count = br.ReadUInt16();
                for (int i = 0; i < count; i++)
                {
                    GroupIconEntry entry = new GroupIconEntry();
                    entry.Width = br.ReadByte();
                    entry.Height = br.ReadByte();
                    entry.ColorCount = br.ReadByte();
                    entry.Reserved = br.ReadByte();
                    entry.Planes = br.ReadUInt16();
                    entry.BitCount = br.ReadUInt16();
                    entry.BytesInRes = br.ReadUInt32();
                    entry.ResourceId = br.ReadUInt16();
                    entries.Add(entry);
                }
            }
            return entries;
        }

        private static byte[] LoadResourceData(IntPtr hModule, int type, IntPtr name)
        {
            IntPtr hResInfo = FindResource(hModule, name, new IntPtr(type));
            if (hResInfo == IntPtr.Zero)
                return null;
            uint size = SizeofResource(hModule, hResInfo);
            IntPtr hResData = LoadResource(hModule, hResInfo);
            if (hResData == IntPtr.Zero || size == 0)
                return null;
            IntPtr pResData = LockResource(hResData);
            if (pResData == IntPtr.Zero)
                return null;

            byte[] data = new byte[size];
            Marshal.Copy(pResData, data, 0, (int)size);
            return data;
        }

        private static bool TryGetNumericId(IntPtr name, out ushort id)
        {
            long value = name.ToInt64();
            if ((value >> 16) == 0)
            {
                id = (ushort)value;
                return true;
            }
            id = 0;
            return false;
        }

        #endregion
    }
}
