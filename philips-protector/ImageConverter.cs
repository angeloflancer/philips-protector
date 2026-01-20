using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

namespace philips_protector
{
    /// <summary>
    /// Converts images (PNG/BMP/JPEG, etc.) to ICO files with multiple sizes.
    /// </summary>
    public static class ImageConverter
    {
        /// <summary>
        /// Converts an image to ICO format containing the requested sizes.
        /// </summary>
        public static void ConvertToIco(string imagePath, string outputPath, int[] sizes)
        {
            if (string.IsNullOrWhiteSpace(imagePath) || !File.Exists(imagePath))
                throw new FileNotFoundException("Image not found.", imagePath);
            if (sizes == null || sizes.Length == 0)
                throw new ArgumentException("At least one size must be provided.", "sizes");

            List<byte[]> pngImages = new List<byte[]>();

            using (var source = new Bitmap(imagePath))
            {
                foreach (int size in sizes)
                {
                    using (var resized = new Bitmap(source, new Size(size, size)))
                    using (var ms = new MemoryStream())
                    {
                        resized.Save(ms, ImageFormat.Png);
                        pngImages.Add(ms.ToArray());
                    }
                }
            }

            using (var fs = new FileStream(outputPath, FileMode.Create, FileAccess.Write))
            using (var bw = new BinaryWriter(fs))
            {
                // ICONDIR
                bw.Write((ushort)0); // reserved
                bw.Write((ushort)1); // type
                bw.Write((ushort)pngImages.Count); // count

                int offset = 6 + (16 * pngImages.Count);
                for (int i = 0; i < pngImages.Count; i++)
                {
                    byte[] pngData = pngImages[i];
                    int size = sizes[i];
                    bw.Write((byte)(size >= 256 ? 0 : size)); // width
                    bw.Write((byte)(size >= 256 ? 0 : size)); // height
                    bw.Write((byte)0); // color count
                    bw.Write((byte)0); // reserved
                    bw.Write((ushort)1); // planes
                    bw.Write((ushort)32); // bit count
                    bw.Write(pngData.Length); // bytes in res
                    bw.Write(offset); // image offset
                    offset += pngData.Length;
                }

                // Image data
                for (int i = 0; i < pngImages.Count; i++)
                {
                    bw.Write(pngImages[i]);
                }

                bw.Flush();
            }
        }
    }
}
