using System.Runtime.InteropServices;
using System.IO;
using System.Windows.Media.Imaging;
using OpenCvSharp;

// Offline landmark registration. Both images are warped to the same intermediate
// silhouette and feature positions before their shaded textures are blended.
internal sealed class FlowMorph : IDisposable
{
    readonly Mat a, b;
    readonly Point2d[] pa, pb;
    readonly bool singleSurface;
    public FlowMorph(BitmapSource first, BitmapSource second, Point2d[]? featuresA = null, Point2d[]? featuresB = null, bool singleSurface = false)
    {
        this.singleSurface = singleSurface;
        a = ToMat(first); b = ToMat(second);
        pa = Landmarks(first).Concat(featuresA ?? []).ToArray();
        pb = Landmarks(second).Concat(featuresB ?? []).ToArray();
        if (pa.Length != pb.Length) throw new ArgumentException("Pose landmarks must correspond.");
    }
    static Mat ToMat(BitmapSource source)
    {
        var mat = new Mat(512, 512, MatType.CV_8UC4); var pixels = SpriteAssets.Pixels(source);
        Marshal.Copy(pixels, 0, mat.Data, pixels.Length); return mat;
    }
    static Point2d[] Landmarks(BitmapSource image)
    {
        var points = new List<Point2d> { new(0, 0), new(511, 0), new(0, 511), new(511, 511), new(0, 256), new(511, 256) };
        var px = SpriteAssets.Pixels(image);
        foreach (var y in new[] { 70, 140, 200, 260, 320, 390, 430, 467 })
        {
            var left = 511; var right = 0;
            for (var x = 0; x < 512; x++) if (px[(y * 512 + x) * 4 + 3] > 96) { left = Math.Min(left, x); right = Math.Max(right, x); }
            points.Add(new(left, y)); points.Add(new(right, y));
        }
        return points.ToArray();
    }
    public BitmapSource At(double t)
    {
        var intermediate = pa.Zip(pb, (x, y) => new Point2d(x.X * (1 - t) + y.X * t, x.Y * (1 - t) + y.Y * t)).ToArray();
        using var wa = Warp(a, intermediate, pa); using var wb = Warp(b, intermediate, pb);
        using var ga = Gray(wa); using var gb = Gray(wb); using var forward = new Mat(); using var backward = new Mat();
        Cv2.CalcOpticalFlowFarneback(ga, gb, forward, .5, 4, 25, 6, 7, 1.5, OpticalFlowFlags.None);
        Cv2.CalcOpticalFlowFarneback(gb, ga, backward, .5, 4, 25, 6, 7, 1.5, OpticalFlowFlags.None);
        using var refinedA = Refine(wa, forward, t); using var refinedB = Refine(wb, backward, 1 - t);
        using var result = new Mat();
        if (singleSurface) (t < .5 ? refinedA : refinedB).CopyTo(result);
        else Cv2.AddWeighted(refinedA, 1 - t, refinedB, t, 0, result);
        var px = new byte[512 * 512 * 4]; Marshal.Copy(result.Data, px, 0, px.Length);
        for (var k = 0; k < px.Length; k += 4) if (px[k + 3] < 8) Array.Clear(px, k, 4);
        return SpriteAssets.FromPixels(px);
    }
    static Mat Gray(Mat source)
    {
        using var gray = new Mat(); Cv2.CvtColor(source, gray, ColorConversionCodes.BGRA2GRAY);
        using var alpha = new Mat(); Cv2.ExtractChannel(source, alpha, 3);
        var result = new Mat(); Cv2.AddWeighted(gray, .7, alpha, .3, 0, result); return result;
    }
    static Mat Refine(Mat source, Mat flow, double fraction)
    {
        using var map = new Mat(512, 512, MatType.CV_32FC2);
        for (var y = 0; y < 512; y++) for (var x = 0; x < 512; x++)
        {
            var f = flow.At<Vec2f>(y, x); map.Set(y, x, new Vec2f(x - (float)fraction * f.Item0, y - (float)fraction * f.Item1));
        }
        using var empty = new Mat(); var result = new Mat(); Cv2.Remap(source, result, map, empty, InterpolationFlags.Linear, BorderTypes.Constant); return result;
    }
    static double Kernel(double dx, double dy) { var r = dx * dx + dy * dy; return r < 1e-12 ? 0 : r * Math.Log(r); }
    static Mat Warp(Mat source, Point2d[] from, Point2d[] to)
    {
        var n = from.Length; var size = n + 3; var matrix = new double[size, size + 2];
        for (var i = 0; i < n; i++)
        {
            for (var j = 0; j < n; j++) matrix[i, j] = Kernel((from[i].X - from[j].X) / 512, (from[i].Y - from[j].Y) / 512);
            matrix[i, i] += 1e-7;
            matrix[i, n] = matrix[n, i] = 1; matrix[i, n + 1] = matrix[n + 1, i] = from[i].X / 512; matrix[i, n + 2] = matrix[n + 2, i] = from[i].Y / 512;
            matrix[i, size] = to[i].X / 512; matrix[i, size + 1] = to[i].Y / 512;
        }
        // Solve the thin plate spline with partial pivoting, for both output coordinates.
        for (var p = 0; p < size; p++)
        {
            var pivot = p;
            for (var r = p + 1; r < size; r++) if (Math.Abs(matrix[r, p]) > Math.Abs(matrix[pivot, p])) pivot = r;
            for (var col = p; col < size + 2; col++) (matrix[p, col], matrix[pivot, col]) = (matrix[pivot, col], matrix[p, col]);
            var divisor = matrix[p, p]; if (Math.Abs(divisor) < 1e-14) throw new InvalidDataException("Degenerate pose landmarks");
            for (var col = p; col < size + 2; col++) matrix[p, col] /= divisor;
            for (var r = 0; r < size; r++) if (r != p)
            { var factor = matrix[r, p]; for (var col = p; col < size + 2; col++) matrix[r, col] -= factor * matrix[p, col]; }
        }
        using var grid = new Mat(65, 65, MatType.CV_32FC2);
        for (var gy = 0; gy <= 64; gy++) for (var gx = 0; gx <= 64; gx++)
        {
            var x = gx / 64d; var y = gy / 64d;
            var sx = matrix[n, size] + x * matrix[n + 1, size] + y * matrix[n + 2, size];
            var sy = matrix[n, size + 1] + x * matrix[n + 1, size + 1] + y * matrix[n + 2, size + 1];
            for (var i = 0; i < n; i++) { var k = Kernel(x - from[i].X / 512, y - from[i].Y / 512); sx += k * matrix[i, size]; sy += k * matrix[i, size + 1]; }
            grid.Set(gy, gx, new Vec2f((float)(sx * 512), (float)(sy * 512)));
        }
        using var map = new Mat(512, 512, MatType.CV_32FC2);
        for (var y = 0; y < 512; y++) for (var x = 0; x < 512; x++)
        {
            var gx = x / 8; var gy = y / 8; var fx = (x % 8) / 8f; var fy = (y % 8) / 8f;
            var p00 = grid.At<Vec2f>(gy, gx); var p10 = grid.At<Vec2f>(gy, gx + 1); var p01 = grid.At<Vec2f>(gy + 1, gx); var p11 = grid.At<Vec2f>(gy + 1, gx + 1);
            map.Set(y, x, new Vec2f((1 - fy) * ((1 - fx) * p00.Item0 + fx * p10.Item0) + fy * ((1 - fx) * p01.Item0 + fx * p11.Item0), (1 - fy) * ((1 - fx) * p00.Item1 + fx * p10.Item1) + fy * ((1 - fx) * p01.Item1 + fx * p11.Item1)));
        }
        using var empty = new Mat(); var result = new Mat(); Cv2.Remap(source, result, map, empty, InterpolationFlags.Linear, BorderTypes.Constant); return result;
    }
    public void Dispose() { a.Dispose(); b.Dispose(); }
}
