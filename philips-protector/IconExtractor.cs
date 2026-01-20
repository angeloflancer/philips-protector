using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

namespace philips_protector
{
    /// <summary>
    /// Helper to extract icons from executable files using resource parsing (RT_GROUP_ICON + RT_ICON),
    /// similar to what tools like Resource Hacker do.
    /// </summary>
    public static class IconExtractor
    {
        private const int RT_ICON = 3;
        private const int RT_GROUP_ICON = 14;
        private const int LOAD_LIBRARY_AS_DATAFILE = 0x00000002;
        private const int LOAD_LIBRARY_AS_IMAGE_RESOURCE = 0x00000020;

        /// <summary>
        /// Extracts the full icon group from the executable and returns both the multi-size ICO and a preview ICO.
        /// </summary>
        public static IconExtractionResult ExtractIconGroup(string exePath)
        {
            if (string.IsNullOrWhiteSpace(exePath) || !File.Exists(exePath))
                throw new FileNotFoundException("Executable not found.", exePath);

            IntPtr hModule = LoadLibraryEx(exePath, IntPtr.Zero, LOAD_LIBRARY_AS_DATAFILE | LOAD_LIBRARY_AS_IMAGE_RESOURCE);
            if (hModule == IntPtr.Zero)
                throw new InvalidOperationException("Failed to load executable for resource inspection.");

            try
            {
                var groupIds = new List<IntPtr>();
                EnumResNameDelegate cb = delegate (IntPtr h, IntPtr type, IntPtr name, IntPtr param)
                {
                    groupIds.Add(name);
                    return true;
                };

                bool enumResult = EnumResourceNames(hModule, new IntPtr(RT_GROUP_ICON), cb, IntPtr.Zero);
                int lastError = Marshal.GetLastWin32Error();

                if ((!enumResult && lastError != 0) || groupIds.Count == 0)
                    throw new InvalidOperationException("No icon groups found in the executable.");

                IntPtr groupId = groupIds[0];

                byte[] groupData = LoadResourceData(hModule, RT_GROUP_ICON, groupId);
                if (groupData == null || groupData.Length < 6)
                    throw new InvalidOperationException("Invalid icon group data.");

                List<GroupIconEntry> entries = ParseGroupEntries(groupData);
                if (entries.Count == 0)
                    throw new InvalidOperationException("No icon entries in group.");

                var iconImages = new List<IconResource>();
                foreach (var entry in entries)
                {
                    byte[] iconImage = LoadResourceData(hModule, RT_ICON, new IntPtr(entry.ResourceId));
                    if (iconImage != null && iconImage.Length > 0)
                    {
                        iconImages.Add(new IconResource { Entry = entry, ImageData = iconImage });
                    }
                }

                if (iconImages.Count == 0)
                    throw new InvalidOperationException("Icon image resource not found.");

                GroupIconEntry best = SelectBestEntry(entries, 256);
                IconResource bestIcon = iconImages.Find(i => i.Entry.ResourceId == best.ResourceId) ?? iconImages[0];

                byte[] fullIcon = BuildMultiIconIco(iconImages);
                byte[] previewIcon = BuildSingleIconIco(bestIcon.Entry, bestIcon.ImageData);

                return new IconExtractionResult
                {
                    FullIcoBytes = fullIcon,
                    PreviewIcoBytes = previewIcon
                };
            }
            finally
            {
                FreeLibrary(hModule);
            }
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

        private static GroupIconEntry SelectBestEntry(List<GroupIconEntry> entries, int preferredSize)
        {
            GroupIconEntry best = entries[0];
            int bestScore = int.MaxValue;
            foreach (var entry in entries)
            {
                int w = entry.Width == 0 ? 256 : entry.Width;
                int h = entry.Height == 0 ? 256 : entry.Height;
                int sizeAvg = (w + h) / 2;
                int score = Math.Abs(sizeAvg - preferredSize);
                if (score < bestScore)
                {
                    bestScore = score;
                    best = entry;
                }
            }
            return best;
        }

        private static byte[] BuildSingleIconIco(GroupIconEntry entry, byte[] imageData)
        {
            using (var ms = new MemoryStream())
            using (var bw = new BinaryWriter(ms))
            {
                bw.Write((ushort)0);
                bw.Write((ushort)1);
                bw.Write((ushort)1);

                bw.Write(entry.Width);
                bw.Write(entry.Height);
                bw.Write(entry.ColorCount);
                bw.Write(entry.Reserved);
                bw.Write(entry.Planes);
                bw.Write(entry.BitCount);
                bw.Write((uint)imageData.Length);
                bw.Write((uint)(6 + 16));

                bw.Write(imageData);
                bw.Flush();
                return ms.ToArray();
            }
        }

        private static byte[] BuildMultiIconIco(List<IconResource> icons)
        {
            using (var ms = new MemoryStream())
            using (var bw = new BinaryWriter(ms))
            {
                bw.Write((ushort)0);
                bw.Write((ushort)1);
                bw.Write((ushort)icons.Count);

                int offset = 6 + (16 * icons.Count);
                foreach (var icon in icons)
                {
                    bw.Write((byte)(icon.Entry.Width == 256 ? 0 : icon.Entry.Width));
                    bw.Write((byte)(icon.Entry.Height == 256 ? 0 : icon.Entry.Height));
                    bw.Write(icon.Entry.ColorCount);
                    bw.Write(icon.Entry.Reserved);
                    bw.Write(icon.Entry.Planes);
                    bw.Write(icon.Entry.BitCount);
                    bw.Write((uint)icon.ImageData.Length);
                    bw.Write((uint)offset);
                    offset += icon.ImageData.Length;
                }

                foreach (var icon in icons)
                {
                    bw.Write(icon.ImageData);
                }

                bw.Flush();
                return ms.ToArray();
            }
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

        #region Native

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

        private class IconResource
        {
            public GroupIconEntry Entry { get; set; }
            public byte[] ImageData { get; set; }
        }

        public class IconExtractionResult
        {
            public byte[] FullIcoBytes { get; set; }
            public byte[] PreviewIcoBytes { get; set; }
        }

        private delegate bool EnumResNameDelegate(IntPtr hModule, IntPtr lpszType, IntPtr lpszName, IntPtr lParam);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr LoadLibraryEx(string lpFileName, IntPtr hFile, int dwFlags);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool FreeLibrary(IntPtr hModule);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool EnumResourceNames(IntPtr hModule, IntPtr lpszType, EnumResNameDelegate lpEnumFunc, IntPtr lParam);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr FindResource(IntPtr hModule, IntPtr lpName, IntPtr lpType);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr LoadResource(IntPtr hModule, IntPtr hResInfo);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr LockResource(IntPtr hResData);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern uint SizeofResource(IntPtr hModule, IntPtr hResInfo);

        #endregion
    }
}
