using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

if (args.Length < 3)
{
    throw new InvalidOperationException("Usage: MakeLogo <source> <png> <ico>");
}

LogoMaker.Run(args[0], args[1], args[2]);

internal static class LogoMaker
{
    public static void Run(string sourcePath, string pngPath, string icoPath)
    {
        using var original = (Bitmap)Image.FromFile(sourcePath);
        using var logo = Recolor(original);
        Directory.CreateDirectory(Path.GetDirectoryName(pngPath)!);
        logo.Save(pngPath, ImageFormat.Png);
        SaveIco(icoPath, logo, [16, 24, 32, 48, 64, 128, 256]);
        Console.WriteLine($"Wrote {pngPath}");
        Console.WriteLine($"Wrote {icoPath}");
    }

    private static Bitmap Recolor(Bitmap source)
    {
        var dest = new Bitmap(source.Width, source.Height, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(dest))
        {
            g.CompositingMode = CompositingMode.SourceCopy;
            g.DrawImage(source, 0, 0, source.Width, source.Height);
        }

        var rect = new Rectangle(0, 0, dest.Width, dest.Height);
        var data = dest.LockBits(rect, ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
        var stride = Math.Abs(data.Stride);
        var bytes = stride * dest.Height;
        var buffer = new byte[bytes];
        Marshal.Copy(data.Scan0, buffer, 0, bytes);

        for (var y = 0; y < dest.Height; y++)
        {
            var t = dest.Height <= 1 ? 0 : y / (double)(dest.Height - 1);
            SampleBrandGradient(t, out var gr, out var gg, out var gb);
            var row = y * stride;
            for (var x = 0; x < dest.Width; x++)
            {
                var i = row + (x * 4);
                var b = buffer[i];
                var g = buffer[i + 1];
                var r = buffer[i + 2];
                var max = Math.Max(r, Math.Max(g, b));
                if (max < 10)
                {
                    buffer[i] = 0;
                    buffer[i + 1] = 0;
                    buffer[i + 2] = 0;
                    buffer[i + 3] = 0;
                    continue;
                }

                var alpha = max < 48 ? (int)Math.Round(max * 255.0 / 48.0) : 255;
                buffer[i] = (byte)Math.Clamp((int)Math.Round(gb), 0, 255);
                buffer[i + 1] = (byte)Math.Clamp((int)Math.Round(gg), 0, 255);
                buffer[i + 2] = (byte)Math.Clamp((int)Math.Round(gr), 0, 255);
                buffer[i + 3] = (byte)alpha;
            }
        }

        Marshal.Copy(buffer, 0, data.Scan0, bytes);
        dest.UnlockBits(data);
        return dest;
    }

    private static void SampleBrandGradient(double t, out double r, out double g, out double b)
    {
        // App accent family: #B8D4FF → #5A9DFF → #3D8BFF → #2563EB
        double r1, g1, b1, r2, g2, b2, u;
        if (t < 0.38)
        {
            u = t / 0.38;
            r1 = 184; g1 = 212; b1 = 255;
            r2 = 90; g2 = 157; b2 = 255;
        }
        else if (t < 0.7)
        {
            u = (t - 0.38) / 0.32;
            r1 = 90; g1 = 157; b1 = 255;
            r2 = 61; g2 = 139; b2 = 255;
        }
        else
        {
            u = (t - 0.7) / 0.3;
            r1 = 61; g1 = 139; b1 = 255;
            r2 = 37; g2 = 99; b2 = 235;
        }

        r = r1 + ((r2 - r1) * u);
        g = g1 + ((g2 - g1) * u);
        b = b1 + ((b2 - b1) * u);
    }

    private static void SaveIco(string path, Bitmap source, int[] sizes)
    {
        var frames = new List<byte[]>();
        foreach (var size in sizes)
        {
            using var resized = Resize(source, size);
            using var ms = new MemoryStream();
            resized.Save(ms, ImageFormat.Png);
            frames.Add(ms.ToArray());
        }

        using var fs = File.Create(path);
        using var bw = new BinaryWriter(fs);
        bw.Write((short)0);
        bw.Write((short)1);
        bw.Write((short)frames.Count);

        var offset = 6 + (16 * frames.Count);
        for (var i = 0; i < frames.Count; i++)
        {
            var size = sizes[i];
            bw.Write((byte)(size >= 256 ? 0 : size));
            bw.Write((byte)(size >= 256 ? 0 : size));
            bw.Write((byte)0);
            bw.Write((byte)0);
            bw.Write((short)1);
            bw.Write((short)32);
            bw.Write(frames[i].Length);
            bw.Write(offset);
            offset += frames[i].Length;
        }

        foreach (var frame in frames)
        {
            bw.Write(frame);
        }
    }

    private static Bitmap Resize(Bitmap source, int size)
    {
        var dest = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(dest);
        g.Clear(Color.Transparent);
        g.CompositingMode = CompositingMode.SourceCopy;
        g.CompositingQuality = CompositingQuality.HighQuality;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.SmoothingMode = SmoothingMode.HighQuality;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.DrawImage(source, 0, 0, size, size);
        return dest;
    }
}
