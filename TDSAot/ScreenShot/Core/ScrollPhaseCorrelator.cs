using MathNet.Numerics;
using MathNet.Numerics.IntegralTransforms;

namespace TDS.ScreenShot.Core;

/// <summary>
/// 2D 相位相关（FFT）估计纵向 dh。MathNet 托管 FFT + 可分离 2D 变换，不加载 MKL（AOT 已验证）。
/// 输入为水平边缘强度 + 零均值归一化，减弱纯渐变误峰。
/// </summary>
internal static class ScrollPhaseCorrelator
{
    private const double MinPeakResponse = 0.35;
    private const int MaxHorizontalPeakPx = 2;

    internal readonly struct PhaseEstimate
    {
        public int Dh { get; init; }
        public double Peak { get; init; }
        public bool Success { get; init; }
    }

    /// <summary>
    /// prev 行 dh.. 应对齐 next 行 0..（与 ScrollStitcher 的 dh 语义一致）。
    /// </summary>
    internal static unsafe PhaseEstimate EstimateVerticalShift(
        byte* prevBase, int prevStride, int prevH,
        byte* nextBase, int nextStride, int nextH,
        int width, int minDh, int maxDh, int scrollHintPx)
    {
        int h = Math.Min(prevH, nextH);
        if (h <= 0 || width <= 0 || maxDh < minDh)
            return default;

        int step = width > 320 || h > 520 ? 2 : 1;
        int sw = (width + step - 1) / step;
        int sh = (h + step - 1) / step;
        if (sw < 8 || sh < 8)
            return default;

        int fftRows = NextPow2(sh * 2);
        int fftCols = NextPow2(sw * 2);
        int n = fftRows * fftCols;

        var a = new double[n];
        var b = new double[n];
        FillHorizontalEdge(prevBase, prevStride, prevH, width, step, a, fftCols, sh, sw);
        FillHorizontalEdge(nextBase, nextStride, nextH, width, step, b, fftCols, sh, sw);
        NormalizeZeroMeanInPlace(a, sh, sw, fftCols);
        NormalizeZeroMeanInPlace(b, sh, sw, fftCols);
        ApplyHannInPlace(a, sh, sw, fftRows, fftCols);
        ApplyHannInPlace(b, sh, sw, fftRows, fftCols);

        var surface = PhaseCorrelateSurface(a, b, fftRows, fftCols);

        int minRow = Math.Max(1, (minDh + step - 1) / step);
        int maxRow = Math.Min(sh - 1, maxDh / step);
        if (scrollHintPx > 0)
        {
            int slack = Math.Max(12, (int)(scrollHintPx * 0.35 / step));
            int hintRow = scrollHintPx / step;
            minRow = Math.Max(minRow, hintRow - slack);
            maxRow = Math.Min(maxRow, hintRow + slack);
        }
        if (minRow > maxRow)
            return default;

        FindPeak(surface, fftRows, fftCols, minRow, maxRow, out int peakRow, out int peakCol, out double peakVal);
        if (peakVal < MinPeakResponse || Math.Abs(peakCol) > MaxHorizontalPeakPx)
            return default;

        int dh = peakRow * step;
        if (dh < minDh || dh > maxDh)
            return default;

        return new PhaseEstimate { Dh = dh, Peak = peakVal, Success = true };
    }

    private static Complex32[] PhaseCorrelateSurface(
        double[] a, double[] b, int fftRows, int fftCols)
    {
        int n = fftRows * fftCols;
        var fa = new Complex32[n];
        var fb = new Complex32[n];
        for (int i = 0; i < n; i++)
        {
            fa[i] = new Complex32((float)a[i], 0);
            fb[i] = new Complex32((float)b[i], 0);
        }

        FourierTransform2D(fa, fftRows, fftCols, inverse: false);
        FourierTransform2D(fb, fftRows, fftCols, inverse: false);

        var cross = new Complex32[n];
        for (int i = 0; i < n; i++)
        {
            var c = fa[i] * fb[i].Conjugate();
            float mag = c.Magnitude;
            cross[i] = mag > 1e-6f ? c / mag : Complex32.Zero;
        }

        FourierTransform2D(cross, fftRows, fftCols, inverse: true);
        return cross;
    }

    private static void FourierTransform2D(Complex32[] data, int rows, int cols, bool inverse)
    {
        var rowBuf = new Complex32[cols];
        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
                rowBuf[c] = data[r * cols + c];
            if (inverse)
                Fourier.Inverse(rowBuf, FourierOptions.NoScaling);
            else
                Fourier.Forward(rowBuf, FourierOptions.NoScaling);
            for (int c = 0; c < cols; c++)
                data[r * cols + c] = rowBuf[c];
        }

        var colBuf = new Complex32[rows];
        for (int c = 0; c < cols; c++)
        {
            for (int r = 0; r < rows; r++)
                colBuf[r] = data[r * cols + c];
            if (inverse)
                Fourier.Inverse(colBuf, FourierOptions.NoScaling);
            else
                Fourier.Forward(colBuf, FourierOptions.NoScaling);
            for (int r = 0; r < rows; r++)
                data[r * cols + c] = colBuf[r];
        }
    }

    private static void FindPeak(
        Complex32[] surface, int fftRows, int fftCols,
        int minRow, int maxRow,
        out int peakRow, out int peakCol, out double peakVal)
    {
        peakRow = 0;
        peakCol = 0;
        peakVal = double.MinValue;

        for (int row = minRow; row <= maxRow; row++)
        {
            for (int col = 0; col <= MaxHorizontalPeakPx; col++)
            {
                double v = surface[row * fftCols + col].Real;
                if (v > peakVal)
                {
                    peakVal = v;
                    peakRow = row;
                    peakCol = col;
                }
            }
        }

        if (peakVal == double.MinValue)
        {
            for (int row = fftRows - maxRow; row <= fftRows - minRow; row++)
            {
                if (row < 0) continue;
                int dhRow = fftRows - row;
                for (int col = 0; col <= MaxHorizontalPeakPx; col++)
                {
                    double v = surface[row * fftCols + col].Real;
                    if (v > peakVal)
                    {
                        peakVal = v;
                        peakRow = dhRow;
                        peakCol = col;
                    }
                }
            }
        }
    }

    private static unsafe void FillHorizontalEdge(
        byte* srcBase, int srcStride, int srcH, int width, int step,
        double[] dst, int dstStride, int dstH, int dstW)
    {
        for (int y = 0; y < dstH; y++)
        {
            int sy = Math.Min(y * step, srcH - 1);
            byte* row = srcBase + (long)sy * srcStride;
            for (int x = 0; x < dstW; x++)
            {
                int sx0 = Math.Min(x * step, width - 1);
                int sx1 = Math.Min(sx0 + step, width - 1);
                int o0 = sx0 * 4;
                int o1 = sx1 * 4;
                int l0 = (299 * row[o0 + 2] + 587 * row[o0 + 1] + 114 * row[o0]) / 1000;
                int l1 = (299 * row[o1 + 2] + 587 * row[o1 + 1] + 114 * row[o1]) / 1000;
                dst[y * dstStride + x] = Math.Abs(l1 - l0);
            }
        }
    }

    private static void NormalizeZeroMeanInPlace(double[] buf, int activeH, int activeW, int stride)
    {
        double sum = 0;
        int count = activeH * activeW;
        for (int y = 0; y < activeH; y++)
        for (int x = 0; x < activeW; x++)
            sum += buf[y * stride + x];
        double mean = count > 0 ? sum / count : 0;
        double sq = 0;
        for (int y = 0; y < activeH; y++)
        for (int x = 0; x < activeW; x++)
        {
            buf[y * stride + x] -= mean;
            sq += buf[y * stride + x] * buf[y * stride + x];
        }
        double norm = Math.Sqrt(sq / Math.Max(1, count));
        if (norm < 1e-6) return;
        for (int y = 0; y < activeH; y++)
        for (int x = 0; x < activeW; x++)
            buf[y * stride + x] /= norm;
    }

    private static void ApplyHannInPlace(
        double[] buf, int activeH, int activeW, int fftRows, int fftCols)
    {
        for (int y = 0; y < activeH; y++)
        {
            double wy = activeH <= 1 ? 1.0 : 0.5 * (1.0 - Math.Cos(2.0 * Math.PI * y / (activeH - 1)));
            for (int x = 0; x < activeW; x++)
            {
                double wx = activeW <= 1 ? 1.0 : 0.5 * (1.0 - Math.Cos(2.0 * Math.PI * x / (activeW - 1)));
                buf[y * fftCols + x] *= wx * wy;
            }
        }
    }

    private static int NextPow2(int n)
    {
        int p = 1;
        while (p < n) p <<= 1;
        return p;
    }
}
