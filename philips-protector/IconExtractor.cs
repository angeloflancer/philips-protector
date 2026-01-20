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
        /// Extracts ICO bytes for the preferred size (falls back to closest available).
        /// </summary>
        public static byte[] ExtractIconBytes(string exePath, int preferredSize)
        {
            if (string.IsNullOrWhiteSpace(exePath) || !File.Exists(exePath))
                throw new FileNotFoundException("Executable not found.", exePath);

            // Use both DATAFILE and IMAGE_RESOURCE to improve resource access compatibility.
            IntPtr hModule = LoadLibraryEx(exePath, IntPtr.Zero, LOAD_LIBRARY_AS_DATAFILE | LOAD_LIBRARY_AS_IMAGE_RESOURCE);
            if (hModule == IntPtr.Zero)
                throw new InvalidOperationException("Failed to load executable for resource inspection.");

            try
            {
                // Collect all icon groups, pick the first (or best) later.
                var groupIds = new List<IntPtr>();
                EnumResNameDelegate cb = delegate (IntPtr h, IntPtr type, IntPtr name, IntPtr param)
                {
                    groupIds.Add(name);
                    return true; // continue to gather all
                };

                bool enumResult = EnumResourceNames(hModule, new IntPtr(RT_GROUP_ICON), cb, IntPtr.Zero);
                int lastError = Marshal.GetLastWin32Error();

                if ((!enumResult && lastError != 0) || groupIds.Count == 0)
                {
                    throw new InvalidOperationException("No icon groups found in the executable.");
                }

                // Use the first group (consistent with “first available”), or could add selection logic later.
                IntPtr groupId = groupIds[0];

                byte[] groupData = LoadResourceData(hModule, RT_GROUP_ICON, groupId);
                if (groupData == null || groupData.Length < 6)
                    throw new InvalidOperationException("Invalid icon group data.");

                GroupIconEntry chosen = SelectBestEntry(groupData, preferredSize);
                byte[] iconImage = LoadResourceData(hModule, RT_ICON, new IntPtr(chosen.ResourceId));
                if (iconImage == null || iconImage.Length == 0)
                    throw new InvalidOperationException("Icon image resource not found.");

                return BuildSingleIconIco(chosen, iconImage);
            }
            finally
            {
                FreeLibrary(hModule);
            }
        }

        private static GroupIconEntry SelectBestEntry(byte[] groupData, int preferredSize)
        {
            // ICONDIR header: 6 bytes. Then N entries of 14 bytes.
            using (var ms = new MemoryStream(groupData))
            using (var br = new BinaryReader(ms))
            {
                br.ReadUInt16(); // reserved
                br.ReadUInt16(); // type
                ushort count = br.ReadUInt16();
                if (count == 0)
                    throw new InvalidOperationException("No icon entries in group.");

                GroupIconEntry best = default(GroupIconEntry);
                int bestScore = int.MaxValue;

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
        }

        private static byte[] BuildSingleIconIco(GroupIconEntry entry, byte[] imageData)
        {
            using (var ms = new MemoryStream())
            using (var bw = new BinaryWriter(ms))
            {
                // ICONDIR
                bw.Write((ushort)0); // reserved
                bw.Write((ushort)1); // type = icon
                bw.Write((ushort)1); // count

                // ICONDIRENTRY
                bw.Write(entry.Width);
                bw.Write(entry.Height);
                bw.Write(entry.ColorCount);
                bw.Write(entry.Reserved);
                bw.Write(entry.Planes);
                bw.Write(entry.BitCount);
                bw.Write((uint)imageData.Length);
                bw.Write((uint)(6 + 16)); // offset to image data

                // Image data
                bw.Write(imageData);
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
