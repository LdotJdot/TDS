using System.Diagnostics;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace TDS.ScreenShot.Core;

/// <summary>
/// 纵向滚动长图拼接：固定宽高的 BGRA tile 逐对对齐后合成。
/// <para>
/// 对齐主路径：MathNet FFT 2D 相位相关估计 dh；TextEdge/SAD 验收。
/// 有 scrollHint 时走 hint 路径（跳过 FFT）；无 hint 时 FFT + 全 dh 扫描。
/// </para>
/// </summary>
public static class ScrollStitcher
{
    public sealed class StitchOffset
    {
        public int TileIndex { get; init; }
        public int TopSkip { get; init; }
        public int RowsContributed { get; init; }
        public int DestY { get; init; }
        public string Mode { get; init; } = "anchor";
    }

    public sealed class StitchResult
    {
        public required WriteableBitmap Bitmap { get; init; }
        public required IReadOnlyList<StitchOffset> Offsets { get; init; }
        public required int TotalHeight { get; init; }
    }

    internal enum TileRelation
    {
        FullDuplicate,
        NoOverlap,
        PartialOverlap,
    }

    private const int MinPartialOverlapPx = 4;
    private const double MinPartialMatchRatio = 0.82;
    private const double PeakRatioTolerance = 0.06;
    private const double RawPeakMinFraction = 0.95;
    private const int SadMatchTolerance = 14;
    /// <summary>水平边缘差分匹配容差，用于 TextEdge/EdgeText——区分重复文件夹行与真实滚动重叠。</summary>
    private const int TextEdgeTolerance = 4;

    private const int RefineRadiusPx = 2;
    /// <summary>hint 窗口候选超过此数时先做 step=2 粗搜，再对 top-K 全分辨率精修。</summary>
    private const int CoarseCandidateThreshold = 36;
    private const int CoarseTopK = 5;
    /// <summary>低于此 contrib 的 dh 不写入 scrollHint，避免 tile#1 的 dh=7 之类误估污染后续。</summary>
    private const int MinHintContribPx = 16;
    /// <summary>卡片流等弱纹理场景：全局峰值低于主阈值但仍可接受时的下限。</summary>
    private const double SoftPartialMatchRatio = 0.72;

    private const int SeamBandMaxRows = 56;
    private const int SeamBandMinRows = 12;
    private const double ScrollHintPenaltyPerPx = 2.5;
    private const double ScrollHintSlackRatio = 0.35;

    private readonly struct DhMetrics
    {
        public int Dh { get; init; }
        public double Raw { get; init; }
        public double Sad { get; init; }
        public double EdgeSad { get; init; }
        public double EdgeRaw { get; init; }
        public double TextEdge { get; init; }
        public double EdgeText { get; init; }
    /// <summary>
        /// EdgeText/TextEdge 主导；SAD/Raw 为连续灰度信号，无二值化量化误差。
    /// </summary>
        public double Score(int scrollHintPx)
        {
            double baseScore = EdgeText * 2200.0 + TextEdge * 600.0
                               + EdgeSad * 150.0 + Sad * 80.0
                               + EdgeRaw * 400.0 + Raw * 120.0;
            if (scrollHintPx <= 0) return baseScore;
            double slack = Math.Max(SeamBandMinRows, scrollHintPx * ScrollHintSlackRatio);
            double dev = Math.Abs(Dh - scrollHintPx);
            if (dev > slack)
                baseScore -= (dev - slack) * ScrollHintPenaltyPerPx;
            return baseScore;
        }
    }

    public static StitchResult Stitch(IReadOnlyList<WriteableBitmap> tiles)
        => StitchCore(tiles, 0, null);

    public static StitchResult Stitch(
        IReadOnlyList<WriteableBitmap> tiles,
        int estimatedScrollPxPerTile = 0,
        IReadOnlyList<int>? perTileScrollPx = null)
        => StitchCore(tiles, estimatedScrollPxPerTile, perTileScrollPx);

    private static StitchResult StitchCore(
        IReadOnlyList<WriteableBitmap> tiles,
        int estimatedScrollPxPerTile,
        IReadOnlyList<int>? perTileScrollPx)
    {
        ArgumentNullException.ThrowIfNull(tiles);
        if (tiles.Count == 0) throw new ArgumentException("At least one tile is required.", nameof(tiles));

        var first = tiles[0];
        int width = first.PixelSize.Width;
        int firstHeight = first.PixelSize.Height;
        if (width <= 0 || firstHeight <= 0)
            throw new ArgumentException("First tile has zero area.", nameof(tiles));
        ValidateTiles(tiles, width);

        var offsets = new StitchOffset[tiles.Count];
        int totalHeight = firstHeight;
        int scrollHintPx = estimatedScrollPxPerTile;
        var recentDh = new List<int>(8);
        offsets[0] = new StitchOffset
        {
            TileIndex = 0,
            TopSkip = 0,
            RowsContributed = firstHeight,
            DestY = 0,
            Mode = "anchor",
        };

        for (int i = 1; i < tiles.Count; i++)
        {
            var prev = tiles[i - 1];
            var cur = tiles[i];
            int curHeight = cur.PixelSize.Height;

            if (perTileScrollPx != null && i - 1 < perTileScrollPx.Count && perTileScrollPx[i - 1] > 0)
                scrollHintPx = perTileScrollPx[i - 1];

            var (topSkip, mode, relation) = AlignVerticalTilesLocked(prev, cur, scrollHintPx);

            if (relation == TileRelation.FullDuplicate)
            {
                // contrib=0：采集链里 Forward 被跳过或双路径重复时会产生几乎相同的相邻 tile，此处丢弃。
                // 若 raw tile 数正常但长图仍缺段，查 [stitch] full-duplicate 日志与 [scroll] ForwardWheel。
                offsets[i] = new StitchOffset
                {
                    TileIndex = i,
                    TopSkip = curHeight,
                    RowsContributed = 0,
                    DestY = totalHeight,
                    Mode = mode,
                };
                Debug.WriteLine($"[stitch] tile {i}: full-duplicate — skipped");
                continue;
            }

            int contributed = relation == TileRelation.NoOverlap ? curHeight : curHeight - topSkip;
            offsets[i] = new StitchOffset
            {
                TileIndex = i,
                TopSkip = relation == TileRelation.NoOverlap ? 0 : topSkip,
                RowsContributed = contributed,
                DestY = totalHeight,
                Mode = mode,
            };
            totalHeight += contributed;
            if (relation == TileRelation.PartialOverlap && contributed >= MinHintContribPx)
            {
                recentDh.Add(contributed);
                if (recentDh.Count > 6) recentDh.RemoveAt(0);
                scrollHintPx = Median(recentDh);
            }
            Debug.WriteLine($"[stitch] tile {i}: {relation} skip={offsets[i].TopSkip} contrib={contributed} mode={mode} totalH={totalHeight} hint={scrollHintPx}");
        }

        return ComposeStitchedBitmap(tiles, offsets, width, totalHeight);
    }

    private static StitchResult ComposeStitchedBitmap(
        IReadOnlyList<WriteableBitmap> tiles, StitchOffset[] offsets, int width, int totalHeight)
    {
        var result = new WriteableBitmap(
            new PixelSize(width, totalHeight),
            new Vector(96, 96),
            PixelFormat.Bgra8888,
            AlphaFormat.Opaque);

        using (var lk = result.Lock())
        {
            unsafe
            {
                byte* dstBase = (byte*)lk.Address;
                int dstStride = lk.RowBytes;
                int pixelRowBytes = width * 4;
                new Span<byte>(dstBase, dstStride * totalHeight).Clear();

                for (int i = 0; i < tiles.Count; i++)
                {
                    var tile = tiles[i];
                    var off = offsets[i];
                    if (off.RowsContributed <= 0) continue;
                    using var tlk = tile.Lock();
                    byte* srcBase = (byte*)tlk.Address;
                    int srcStride = tlk.RowBytes;
                    int srcY = off.TopSkip;
                    long dstByteStart = (long)off.DestY * dstStride;
                    for (int row = 0; row < off.RowsContributed; row++)
                    {
                        byte* srcRow = srcBase + (long)(srcY + row) * srcStride;
                        byte* dstRow = dstBase + dstByteStart + (long)row * dstStride;
                        Buffer.MemoryCopy(srcRow, dstRow, pixelRowBytes, pixelRowBytes);
                        if (dstStride > pixelRowBytes)
                            new Span<byte>(dstRow + pixelRowBytes, dstStride - pixelRowBytes).Clear();
                    }
                }
            }
        }

        return new StitchResult
        {
            Bitmap = result,
            Offsets = offsets,
            TotalHeight = totalHeight,
        };
    }

    private static int Median(IReadOnlyList<int> values)
    {
        if (values.Count == 0) return 0;
        var sorted = values.OrderBy(v => v).ToArray();
        return sorted[sorted.Length / 2];
    }

    public static int MeasureTopSkip(WriteableBitmap prev, WriteableBitmap next, int hintPx = 0)
    {
        var (skip, _, relation) = AlignVerticalTilesLocked(prev, next, hintPx);
        if (relation is TileRelation.FullDuplicate or TileRelation.NoOverlap) return 0;
        return skip;
    }

    private static (int topSkip, string mode, TileRelation relation) AlignVerticalTilesLocked(
        WriteableBitmap prev, WriteableBitmap next, int scrollHintPx = 0)
    {
        int width = prev.PixelSize.Width;
        int prevH = prev.PixelSize.Height;
        int nextH = next.PixelSize.Height;
        if (prevH <= 0 || nextH <= 0 || width <= 0)
            return (0, "no-overlap", TileRelation.NoOverlap);

        using var pLock = prev.Lock();
        using var nLock = next.Lock();
        unsafe
        {
            return AlignVerticalTilesFromPointers(
                (byte*)pLock.Address, pLock.RowBytes, prevH,
                (byte*)nLock.Address, nLock.RowBytes, nextH,
                width, scrollHintPx);
        }
    }

    internal static unsafe (int topSkip, string mode) DeduceTopSkipFromPointers(
        byte* prevBase, int prevStride, int prevH,
        byte* nextBase, int nextStride, int nextH,
        int width, int estimatedScrollPxPerTile)
    {
        var (skip, mode, _) = AlignVerticalTilesFromPointers(
            prevBase, prevStride, prevH, nextBase, nextStride, nextH, width, estimatedScrollPxPerTile);
        return (skip, mode);
    }

    internal static unsafe (int topSkip, string mode, TileRelation relation) AlignVerticalTilesFromPointers(
        byte* prevBase, int prevStride, int prevH,
        byte* nextBase, int nextStride, int nextH,
        int width, int scrollHintPx = 0)
    {
        if (prevH <= 0 || nextH <= 0 || width <= 0)
            return (0, "no-overlap", TileRelation.NoOverlap);

        int h = Math.Min(prevH, nextH);
        int maxDh = h - MinPartialOverlapPx;
        if (maxDh < MinPartialOverlapPx)
            return (0, "no-overlap", TileRelation.NoOverlap);

        // FullDuplicate：两 tile 同行逐像素几乎相同（Raw≥99.9%），说明滚动未生效或重复截图。
        double rawIdentical = ComputeRawMatchRatio(
            prevBase, prevStride, prevH, 0,
            nextBase, nextStride, nextH, 0,
            h, width, stepX: 1, stepY: 1);
        if (rawIdentical >= 0.999)
            return (nextH, "raw-dup", TileRelation.FullDuplicate);

        int effectiveHint = scrollHintPx;
        List<int> candidates;
        string modeTag;

        if (scrollHintPx > 0)
        {
            // 有 hint 时跳过 FFT——hint 窗口 + SAD 已足够，FFT 是 tile#1 无 hint 时的冷启动路径。
            modeTag = "hint";
            candidates = BuildHintWindowCandidates(maxDh, scrollHintPx);
        }
        else
        {
            var phase = ScrollPhaseCorrelator.EstimateVerticalShift(
                prevBase, prevStride, prevH, nextBase, nextStride, nextH,
                width, MinPartialOverlapPx, maxDh, scrollHintPx: 0);
            effectiveHint = phase.Success ? phase.Dh : 0;
            modeTag = phase.Success ? "fft" : "fft-scan";
            candidates = BuildAllDhCandidates(maxDh);
        }

        var finals = ScoreAndClassify(
            prevBase, prevStride, prevH, nextBase, nextStride, nextH,
            width, h, maxDh, effectiveHint, candidates, modeTag);

        if (finals.relation == TileRelation.NoOverlap && scrollHintPx > 0)
        {
            Debug.WriteLine(
                $"[stitch] hint-fallback: dh={scrollHintPx} (avoid full-tile stack after weak match)");
            return (h - scrollHintPx, "hint-fallback", TileRelation.PartialOverlap);
        }

        return finals;
    }

    private static List<int> BuildHintWindowCandidates(int maxDh, int scrollHintPx)
    {
        int slack = Math.Max(SeamBandMinRows, (int)(scrollHintPx * ScrollHintSlackRatio));
        int lo = Math.Max(MinPartialOverlapPx, scrollHintPx - slack);
        int hi = Math.Min(maxDh, scrollHintPx + slack);
        var list = new List<int>(Math.Max(0, hi - lo + 1));
        for (int dh = lo; dh <= hi; dh++)
            list.Add(dh);
        return list;
    }

    private static unsafe (int topSkip, string mode, TileRelation relation) ScoreAndClassify(
        byte* prevBase, int prevStride, int prevH,
        byte* nextBase, int nextStride, int nextH,
        int width, int h, int maxDh, int effectiveHint,
        List<int> candidates, string modeTag)
    {
        DhMetrics[] finals = ScoreCandidatesAdaptive(
            prevBase, prevStride, prevH, nextBase, nextStride, nextH,
            width, h, effectiveHint, candidates, maxDh);

        var result = TryClassify(finals, h, modeTag, effectiveHint);
        if (result.relation != TileRelation.NoOverlap && IsViable(finals, effectiveHint))
            return result;

        if (candidates.Count >= maxDh - MinPartialOverlapPx)
            return result;

        Debug.WriteLine($"[stitch] rescan full dh range (was {modeTag}, viable={IsViable(finals, effectiveHint)})");
        finals = ScoreCandidatesAdaptive(
            prevBase, prevStride, prevH, nextBase, nextStride, nextH,
            width, h, effectiveHint, BuildAllDhCandidates(maxDh), maxDh);
        return TryClassify(finals, h, "rescan", effectiveHint);
    }

    /// <summary>大窗口先 step=2 粗搜，再对 top-K 做 step=1 精修。</summary>
    private static unsafe DhMetrics[] ScoreCandidatesAdaptive(
        byte* prevBase, int prevStride, int prevH,
        byte* nextBase, int nextStride, int nextH,
        int width, int h, int scrollHintPx, IReadOnlyList<int> candidates, int maxDh)
    {
        if (candidates.Count <= CoarseCandidateThreshold)
        {
            return ScoreCandidates(
                prevBase, prevStride, prevH, nextBase, nextStride, nextH,
                width, h, scrollHintPx, candidates, stepX: 1, stepY: 1);
        }

        var coarse = ScoreCandidates(
            prevBase, prevStride, prevH, nextBase, nextStride, nextH,
            width, h, scrollHintPx, candidates, stepX: 2, stepY: 2);
        if (coarse.Length == 0)
            return coarse;

        var refineSet = new SortedSet<int>();
        foreach (var m in coarse.Take(CoarseTopK))
        {
            for (int dh = m.Dh - RefineRadiusPx; dh <= m.Dh + RefineRadiusPx; dh++)
            {
                if (dh >= MinPartialOverlapPx && dh <= maxDh)
                    refineSet.Add(dh);
            }
        }
        if (refineSet.Count == 0)
            return coarse;

        return ScoreCandidates(
            prevBase, prevStride, prevH, nextBase, nextStride, nextH,
            width, h, scrollHintPx, refineSet.ToList(), stepX: 1, stepY: 1);
    }

    private static (int topSkip, string mode, TileRelation relation) TryClassify(
        DhMetrics[] finals, int h, string modeTag, int scrollHintPx)
    {
        if (finals.Length == 0)
            return (0, "no-overlap", TileRelation.NoOverlap);
        return ClassifyFromMetrics(finals, h, modeTag, scrollHintPx);
    }

    private static List<int> BuildAllDhCandidates(int maxDh)
    {
        var list = new List<int>(maxDh);
        for (int dh = MinPartialOverlapPx; dh <= maxDh; dh++)
            list.Add(dh);
        return list;
    }

    private static bool IsViable(DhMetrics[] ranked, int scrollHintPx)
    {
        if (ranked.Length == 0) return false;
        var top = ranked[0];
        return top.EdgeText >= MinPartialMatchRatio
               || top.TextEdge >= MinPartialMatchRatio
               || top.EdgeSad >= MinPartialMatchRatio
               || top.Sad >= MinPartialMatchRatio
               || top.Score(scrollHintPx) >= MinPartialMatchRatio * 1000.0;
    }

    private static int SeamBandRows(int overlapRows)
        => overlapRows <= 0 ? 0 : Math.Min(overlapRows, SeamBandMaxRows);

    private static unsafe DhMetrics[] ScoreCandidates(
        byte* prevBase, int prevStride, int prevH,
        byte* nextBase, int nextStride, int nextH,
        int width, int h, int scrollHintPx, IReadOnlyList<int> candidates,
        int stepX, int stepY)
    {
        var metrics = new DhMetrics[candidates.Count];
        Parallel.For(0, candidates.Count, i =>
        {
            int dh = candidates[i];
            int overlapRows = h - dh;
            if (overlapRows < MinPartialOverlapPx)
            {
                metrics[i] = new DhMetrics { Dh = dh };
                return;
            }
            int band = SeamBandRows(overlapRows);
            int edgeStart = overlapRows - band;
            metrics[i] = new DhMetrics
            {
                Dh = dh,
                Raw = ComputeRawMatchRatio(
                    prevBase, prevStride, prevH, dh,
                    nextBase, nextStride, nextH, 0,
                    overlapRows, width, stepX, stepY),
                Sad = ComputeSadMatchRatio(
                    prevBase, prevStride, prevH, dh,
                    nextBase, nextStride, nextH, 0,
                    overlapRows, width, stepX, stepY),
                EdgeRaw = ComputeRawMatchRatio(
                    prevBase, prevStride, prevH, dh + edgeStart,
                    nextBase, nextStride, nextH, edgeStart,
                    band, width, stepX, Math.Max(1, stepY)),
                EdgeSad = ComputeSadMatchRatio(
                    prevBase, prevStride, prevH, dh + edgeStart,
                    nextBase, nextStride, nextH, edgeStart,
                    band, width, stepX, Math.Max(1, stepY)),
                TextEdge = ComputeTextEdgeMatchRatio(
                    prevBase, prevStride, prevH, dh,
                    nextBase, nextStride, nextH, 0,
                    overlapRows, width, stepX, stepY),
                EdgeText = ComputeTextEdgeMatchRatio(
                    prevBase, prevStride, prevH, dh + edgeStart,
                    nextBase, nextStride, nextH, edgeStart,
                    band, width, stepX, Math.Max(1, stepY)),
            };
        });
        return metrics
            .OrderByDescending(m => m.Score(scrollHintPx))
            .ThenByDescending(m => m.EdgeText)
            .ThenByDescending(m => m.TextEdge)
            .ThenByDescending(m => m.EdgeSad)
            .ThenByDescending(m => m.Dh)
            .ToArray();
    }

    private static (int topSkip, string mode, TileRelation relation) ClassifyFromMetrics(
        DhMetrics[] ranked, int h, string modeTag, int scrollHintPx)
    {
        // ranked 已在 ScoreCandidates 排好序，避免重复 LINQ。
        double maxEdgeText = ranked[0].EdgeText;
        double maxText = ranked[0].TextEdge;
        double maxEdgeSad = ranked[0].EdgeSad;
        for (int i = 1; i < ranked.Length; i++)
        {
            if (ranked[i].EdgeText > maxEdgeText) maxEdgeText = ranked[i].EdgeText;
            if (ranked[i].TextEdge > maxText) maxText = ranked[i].TextEdge;
            if (ranked[i].EdgeSad > maxEdgeSad) maxEdgeSad = ranked[i].EdgeSad;
        }
        double softFloor = scrollHintPx > 0 ? SoftPartialMatchRatio : MinPartialMatchRatio;

        if (maxEdgeText < softFloor && maxText < softFloor && maxEdgeSad < softFloor)
        {
            Debug.WriteLine($"[stitch] no-overlap: text={maxText:F3} edgeSad={maxEdgeSad:F3} edgeText={maxEdgeText:F3}");
            return (0, "no-overlap", TileRelation.NoOverlap);
        }

        double ratioFloor = Math.Max(softFloor, Math.Max(maxEdgeText, maxText) - PeakRatioTolerance);

        var viable = ranked
            .Where(m => m.EdgeText >= ratioFloor || m.TextEdge >= ratioFloor
                        || m.EdgeSad >= ratioFloor || m.Sad >= ratioFloor)
            .ToArray();
        if (viable.Length == 0)
        {
            // 弱匹配：有 hint 时取 hint 窗口内得分最高者，避免整 tile 叠加。
            if (scrollHintPx > 0)
            {
                var softBest = ranked[0];
                int softSkip = h - softBest.Dh;
                Debug.WriteLine(
                    $"[stitch] partial-soft: dh={softBest.Dh} skip={softSkip} edgeText={softBest.EdgeText:F3} text={softBest.TextEdge:F3} hint={scrollHintPx} mode={modeTag}");
                return (softSkip, modeTag + "-soft", TileRelation.PartialOverlap);
            }
            Debug.WriteLine($"[stitch] no-overlap: floor={ratioFloor:F3} maxEdgeSad={maxEdgeSad:F3}");
            return (0, "no-overlap", TileRelation.NoOverlap);
        }

        int bestDh = viable[0].Dh;
        if (scrollHintPx > 0)
        {
            double slack = Math.Max(SeamBandMinRows, scrollHintPx * ScrollHintSlackRatio);
            var hinted = viable
                .Where(m => Math.Abs(m.Dh - scrollHintPx) <= slack)
                .OrderByDescending(m => m.Score(scrollHintPx))
                .ThenByDescending(m => m.EdgeText)
                .ThenByDescending(m => m.TextEdge)
                .ToArray();
            if (hinted.Length > 0)
                bestDh = hinted[0].Dh;
        }

        double peakRaw = viable.Max(m => m.EdgeRaw);
        var bestRawDh = viable.OrderByDescending(m => m.EdgeRaw).First().Dh;
        var bestCandidate = viable.First(m => m.Dh == bestDh);
        if (peakRaw > 0 && bestCandidate.EdgeRaw < peakRaw * RawPeakMinFraction)
            bestDh = bestRawDh;

        var best = viable.First(m => m.Dh == bestDh);
        int topSkip = h - bestDh;
        Debug.WriteLine(
            $"[stitch] partial: dh={bestDh} skip={topSkip} edgeText={best.EdgeText:F3} text={best.TextEdge:F3} sad={best.EdgeSad:F3} raw={best.EdgeRaw:F3} hint={scrollHintPx} mode={modeTag}");
        return (topSkip, modeTag, TileRelation.PartialOverlap);
    }

    /// <summary>
    /// Compare horizontal luminance edges — sensitive to text strokes, ignores flat backgrounds.
    /// </summary>
    private static unsafe double ComputeTextEdgeMatchRatio(
        byte* prevBase, int prevStride, int prevH, int prevStartY,
        byte* nextBase, int nextStride, int nextH, int nextStartY,
        int overlapRows, int width, int stepX, int stepY)
    {
        if (width < 2) return 0;
        int matches = 0;
        int total = 0;

        for (int r = 0; r < overlapRows; r += stepY)
        {
            int py = prevStartY + r;
            int ny = nextStartY + r;
            if (py >= prevH || ny >= nextH) continue;

            byte* prevRow = prevBase + (long)py * prevStride;
            byte* nextRow = nextBase + (long)ny * nextStride;
            for (int x = 0; x < width - 1; x += stepX)
            {
                int o0 = x * 4;
                int o1 = (x + 1) * 4;
                int edgeP = RowLum(prevRow, o1) - RowLum(prevRow, o0);
                int edgeN = RowLum(nextRow, o1) - RowLum(nextRow, o0);
                total++;
                if (Math.Abs(edgeP - edgeN) <= TextEdgeTolerance)
                    matches++;
            }
        }
        return total > 0 ? (double)matches / total : 0;
    }

    private static unsafe int RowLum(byte* row, int o)
        => (299 * row[o + 2] + 587 * row[o + 1] + 114 * row[o]) / 1000;

    private static unsafe double ComputeRawMatchRatio(
        byte* prevBase, int prevStride, int prevH, int prevStartY,
        byte* nextBase, int nextStride, int nextH, int nextStartY,
        int overlapRows, int width, int stepX, int stepY)
    {
        int matches = 0;
        int total = 0;

        for (int r = 0; r < overlapRows; r += stepY)
        {
            int py = prevStartY + r;
            int ny = nextStartY + r;
            if (py >= prevH || ny >= nextH) continue;

            byte* prevRow = prevBase + (long)py * prevStride;
            byte* nextRow = nextBase + (long)ny * nextStride;
            for (int x = 0; x < width; x += stepX)
            {
                total++;
                int o = x * 4;
                if (prevRow[o] == nextRow[o]
                    && prevRow[o + 1] == nextRow[o + 1]
                    && prevRow[o + 2] == nextRow[o + 2])
                    matches++;
            }
        }
        return total > 0 ? (double)matches / total : 0;
    }

    private static unsafe double ComputeSadMatchRatio(
        byte* prevBase, int prevStride, int prevH, int prevStartY,
        byte* nextBase, int nextStride, int nextH, int nextStartY,
        int overlapRows, int width, int stepX, int stepY)
    {
        long sad = 0;
        int total = 0;

        for (int r = 0; r < overlapRows; r += stepY)
        {
            int py = prevStartY + r;
            int ny = nextStartY + r;
            if (py >= prevH || ny >= nextH) continue;

            byte* prevRow = prevBase + (long)py * prevStride;
            byte* nextRow = nextBase + (long)ny * nextStride;
            for (int x = 0; x < width; x += stepX)
            {
                total++;
                int o = x * 4;
                int lumP = (299 * prevRow[o + 2] + 587 * prevRow[o + 1] + 114 * prevRow[o]) / 1000;
                int lumN = (299 * nextRow[o + 2] + 587 * nextRow[o + 1] + 114 * nextRow[o]) / 1000;
                int d = Math.Abs(lumP - lumN);
                sad += d <= SadMatchTolerance ? 0 : d - SadMatchTolerance;
            }
        }
        if (total == 0) return 0;
        double meanSad = (double)sad / total;
        return Math.Clamp(1.0 - meanSad / 64.0, 0, 1);
    }

    private static void ValidateTiles(IReadOnlyList<WriteableBitmap> tiles, int width)
    {
        for (int i = 1; i < tiles.Count; i++)
        {
            var t = tiles[i];
            if (t.PixelSize.Width != width)
                throw new ArgumentException(
                    $"Tile {i} has width {t.PixelSize.Width}, expected {width}.",
                    nameof(tiles));
            if (t.PixelSize.Height != tiles[i - 1].PixelSize.Height)
                throw new ArgumentException(
                    $"Tile {i} height {t.PixelSize.Height} differs from previous tile.",
                    nameof(tiles));
            if (t.PixelSize.Height <= 0)
                throw new ArgumentException($"Tile {i} has zero height.", nameof(tiles));
        }
    }
}
