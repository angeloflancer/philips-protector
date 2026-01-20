using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

// Simple utility to turn the existing SVG asset into an ICO that can be used
// as the application icon. Intended to be run manually when the SVG changes.
namespace philips_protector.Assets
{
    internal static class IconGenerator
    {
        [STAThread]
        private static void Main(string[] args)
        {
            string svgPath = args.Length > 0 ? args[0] : "Assets\\noun-hearts-973300.svg";
            string outputPath = args.Length > 1 ? args[1] : "Assets\\app.ico";

            if (!File.Exists(svgPath))
            {
                Console.Error.WriteLine("SVG file not found: " + svgPath);
                Environment.Exit(1);
            }

            string svgContent = File.ReadAllText(svgPath);
            string pathData = ExtractPathData(svgContent);

            if (string.IsNullOrWhiteSpace(pathData))
            {
                Console.Error.WriteLine("Unable to find a <path> d=\"...\" segment in the SVG.");
                Environment.Exit(1);
            }

            Geometry geometry = Geometry.Parse(pathData);
            int[] sizes = new int[] { 16, 32, 48, 256 }; // include XP-friendly sizes
            List<IconImage> images = new List<IconImage>();

            for (int i = 0; i < sizes.Length; i++)
            {
                Geometry prepared = PrepareGeometry(geometry, sizes[i]);
                byte[] bmpData = RenderBmp(prepared, sizes[i]);
                IconImage img = new IconImage();
                img.Size = sizes[i];
                img.Data = bmpData;
                images.Add(img);
            }

            WriteIco(outputPath, images);

            Console.WriteLine("Icon written to " + outputPath);
        }

        private static string ExtractPathData(string svgContent)
        {
            Match match = Regex.Match(svgContent, "d=\"([^\"]+)\"", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            return match.Success ? match.Groups[1].Value : string.Empty;
        }

        private static Geometry PrepareGeometry(Geometry geometry, int targetSize)
        {
            Geometry copy = geometry.Clone();
            Rect bounds = copy.Bounds;

            double usableSize = targetSize * 0.82;
            double scale = Math.Min(usableSize / bounds.Width, usableSize / bounds.Height);
            double offsetX = (targetSize - bounds.Width * scale) / 2 - bounds.Left * scale;
            double offsetY = (targetSize - bounds.Height * scale) / 2 - bounds.Top * scale;

            TransformGroup group = new TransformGroup();
            group.Children.Add(new ScaleTransform(scale, scale));
            group.Children.Add(new TranslateTransform(offsetX, offsetY));

            copy.Transform = group;
            return copy;
        }

        private static byte[] RenderBmp(Geometry geometry, int size)
        {
            DrawingVisual visual = new DrawingVisual();

            using (DrawingContext dc = visual.RenderOpen())
            {
                dc.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, size, size));
                // Sky blue color: RGB(135, 206, 235)
                SolidColorBrush skyBlueBrush = new SolidColorBrush(Color.FromRgb(135, 206, 235));
                dc.DrawGeometry(skyBlueBrush, null, geometry);
            }

            RenderTargetBitmap rtb = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(visual);

            int stride = size * 4;
            byte[] pixels = new byte[stride * size];
            rtb.CopyPixels(pixels, stride, 0);

            int maskStride = ((size + 31) / 32) * 4;
            int maskSize = maskStride * size;

            using (MemoryStream ms = new MemoryStream())
            {
                BinaryWriter bw = new BinaryWriter(ms);

                // BITMAPINFOHEADER (40 bytes)
                bw.Write(40); // header size
                bw.Write(size); // width
                bw.Write(size * 2); // height (includes XOR+AND)
                bw.Write((short)1); // planes
                bw.Write((short)32); // bit count
                bw.Write(0); // compression (BI_RGB)
                bw.Write(pixels.Length + maskSize); // size image
                bw.Write(0); // x pixels per meter
                bw.Write(0); // y pixels per meter
                bw.Write(0); // colors used
                bw.Write(0); // important colors

                // XOR bitmap (BGRA), bottom-up
                for (int y = size - 1; y >= 0; y--)
                {
                    int offset = y * stride;
                    bw.Write(pixels, offset, stride);
                }

                // AND mask (all zero for 32bpp with alpha)
                bw.Write(new byte[maskSize]);

                return ms.ToArray();
            }
        }

        private static void WriteIco(string outputPath, List<IconImage> images)
        {
            using (FileStream fs = new FileStream(outputPath, FileMode.Create, FileAccess.Write))
            {
                // ICONDIR header
                fs.WriteByte(0);
                fs.WriteByte(0);
                fs.WriteByte(1); // icon type
                fs.WriteByte(0);
                fs.WriteByte((byte)images.Count);
                fs.WriteByte(0);

                int offset = 6 + (16 * images.Count);
                for (int i = 0; i < images.Count; i++)
                {
                    IconImage img = images[i];
                    byte widthByte = img.Size == 256 ? (byte)0 : (byte)img.Size;
                    fs.WriteByte(widthByte);
                    fs.WriteByte(widthByte); // height
                    fs.WriteByte(0); // color count
                    fs.WriteByte(0); // reserved
                    fs.WriteByte(1);
                    fs.WriteByte(0); // planes
                    fs.WriteByte(32);
                    fs.WriteByte(0); // bit count
                    fs.Write(BitConverter.GetBytes(img.Data.Length), 0, 4);
                    fs.Write(BitConverter.GetBytes(offset), 0, 4);
                    offset += img.Data.Length;
                }

                for (int i = 0; i < images.Count; i++)
                {
                    fs.Write(images[i].Data, 0, images[i].Data.Length);
                }
            }
        }

        private class IconImage
        {
            public int Size;
            public byte[] Data;
        }
    }
}
