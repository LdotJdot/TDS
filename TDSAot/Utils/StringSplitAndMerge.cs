using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

public readonly struct TextMatch
{
    public readonly int Start;
    public readonly int Length;
    public readonly bool IsMatch;
    public readonly byte GroupId; // 使用byte节省内存

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TextMatch(int start, int length, bool isMatch, byte groupId = 0)
    {
        Start = start;
        Length = length;
        IsMatch = isMatch;
        GroupId = groupId;
    }
}

public static class StringSplitAndMerge
{
    private const int MaxStackAllocSize = 256;

    public static TextMatch[] GetTextMatches(
        ReadOnlySpan<char> text1, string[] group1Words,
        ReadOnlySpan<char> text2, string[] group2Words)
    {
        if (text1.IsEmpty && text2.IsEmpty)
            return Array.Empty<TextMatch>();

        // 检查长度一致性
        if (!text1.IsEmpty && text1.Length != text2.Length)
            ThrowLengthMismatch();

        if (group1Words.Length != group2Words.Length)
            ThrowKeywordsCountMismatch();

        int textLength =text1.Length;

        // 使用栈分配的小缓冲区
        Span<(int Start, int End, byte GroupId)> matchesSpan = stackalloc (int, int, byte)[MaxStackAllocSize];
        var matchesList = new ValueList<(int Start, int End, byte GroupId)>(matchesSpan);

        try
        {
            // 使用bool数组标记哪些关键词已经被匹配
            bool[] matchedKeywords = new bool[group1Words.Length];

            // 先查找group1的匹配
            FindGroupMatchesWithExclusion(ref matchesList, text1, group1Words, 1, matchedKeywords);

            // 然后查找group2中未被排除的关键词
            FindGroupMatchesWithExclusion(ref matchesList, text2, group2Words, 2, matchedKeywords);

            if (matchesList.Count == 0)
            {
                return text1.IsEmpty ?
                    [new TextMatch(0, text2.Length, false, 0)] :
                    [new TextMatch(0, text1.Length, false, 0)];
            }

            // 原地排序和合并
            SortAndMergeIntervals(ref matchesList);

            // 构建结果
            return BuildMatches(textLength, ref matchesList);
        }
        finally
        {
            matchesList.Dispose();
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void FindGroupMatchesWithExclusion(
        ref ValueList<(int Start, int End, byte GroupId)> matchesList,
        ReadOnlySpan<char> text, string[] words, byte groupId,
        bool[] excludedKeywords)
    {
        if (text.IsEmpty || words is not { Length: > 0 })
            return;

        for (int i = 0; i < words.Length; i++)
        {
            // 如果这个关键词已经被匹配过（在另一组中），则跳过
            if (excludedKeywords[i]) continue;

            var word = words[i];
            if (string.IsNullOrEmpty(word)) continue;

            var wordSpan = word.AsSpan();
            FindAllOccurrences(ref matchesList, text, wordSpan, groupId);

            // 保存当前匹配列表的数量，以便检测是否找到了新匹配
            int originalCount = matchesList.Count;

            // 如果找到了匹配，标记这个索引为已匹配，这样另一组就不会再匹配对应的关键词
            // 注意：这里假设如果找到了至少一个匹配，就认为这个关键词被匹配了
            if (matchesList.Count > originalCount && matchesList.AsSpan()[^1].GroupId == groupId)
            {
                excludedKeywords[i] = true;
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void FindAllOccurrences(
        ref ValueList<(int Start, int End, byte GroupId)> matchesList,
        ReadOnlySpan<char> text, ReadOnlySpan<char> pattern, byte groupId)
    {
        int patternLength = pattern.Length;
        if (patternLength == 0 || text.Length < patternLength)
            return;

        int maxIndex = text.Length - patternLength;

        for (int i = 0; i <= maxIndex; i++)
        {
            bool match = true;
            for (int j = 0; j < patternLength; j++)
            {
                if (text[i + j]!= pattern[j])
                {
                    match = false;
                    break;
                }
            }

            if (match)
            {
                matchesList.Add((i, i + patternLength - 1, groupId));
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void SortAndMergeIntervals(ref ValueList<(int Start, int End, byte GroupId)> intervals)
    {
        if (intervals.Count <= 1) return;

        // 使用内联排序
        SortIntervals(ref intervals);

        // 原地合并
        int writeIndex = 0;
        ref var intervalsRef = ref MemoryMarshal.GetReference(intervals.AsSpan());

        for (int i = 1; i < intervals.Count; i++)
        {
            ref var current = ref Unsafe.Add(ref intervalsRef, i);
            ref var last = ref Unsafe.Add(ref intervalsRef, writeIndex);

            if (current.Start <= last.End + 1)
            {
                // 合并区间，优先组1
                last.End = Math.Max(last.End, current.End);
                last.GroupId = last.GroupId == 1 ? (byte)1 : current.GroupId;
            }
            else
            {
                writeIndex++;
                Unsafe.Add(ref intervalsRef, writeIndex) = current;
            }
        }

        intervals.Count = writeIndex + 1;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void SortIntervals(ref ValueList<(int Start, int End, byte GroupId)> intervals)
    {
        var span = intervals.AsSpan();
        span.Sort(Comparer.Instance);
    }

    private sealed class Comparer : IComparer<(int Start, int End, byte GroupId)>
    {
        public static readonly Comparer Instance = new();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int Compare((int Start, int End, byte GroupId) x, (int Start, int End, byte GroupId) y)
            => x.Start.CompareTo(y.Start);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static TextMatch[] BuildMatches(
        int textLength, ref ValueList<(int Start, int End, byte GroupId)> intervals)
    {
        var result = new ValueList<TextMatch>(stackalloc TextMatch[MaxStackAllocSize]);
        int currentPos = 0;

        var intervalsSpan = intervals.AsSpan();
        for (int i = 0; i < intervalsSpan.Length; i++)
        {
            ref var interval = ref intervalsSpan[i];
            int start = interval.Start;
            int end = interval.End;
            int length = end - start + 1;

            // 添加非匹配部分
            if (start > currentPos)
            {
                result.Add(new TextMatch(currentPos, start - currentPos, false, 0));
            }

            // 添加匹配部分
            result.Add(new TextMatch(start, length, true, interval.GroupId));
            currentPos = end + 1;
        }

        // 添加剩余的非匹配部分
        if (currentPos < textLength)
        {
            result.Add(new TextMatch(currentPos, textLength - currentPos, false, 0));
        }

        return result.ToArray();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ThrowLengthMismatch() =>
        throw new ArgumentException("text1 and text2 must have the same length");

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ThrowKeywordsCountMismatch() =>
        throw new ArgumentException("group1Words and group2Words must have the same length");

    // 单独处理单组的方法
    public static TextMatch[] GetTextMatches(ReadOnlySpan<char> text1, string[] group1Words)
    {
        if (text1.IsEmpty)
            return Array.Empty<TextMatch>();

       
        int textLength = text1.Length;

        // 使用栈分配的小缓冲区
        Span<(int Start, int End, byte GroupId)> matchesSpan = stackalloc (int, int, byte)[MaxStackAllocSize];
        var matchesList = new ValueList<(int Start, int End, byte GroupId)>(matchesSpan);

        try
        {
            // 使用bool数组标记哪些关键词已经被匹配
            bool[] matchedKeywords = new bool[group1Words.Length];

            // 先查找group1的匹配
            FindGroupMatchesWithExclusion(ref matchesList, text1, group1Words, 1, matchedKeywords);

            if (matchesList.Count == 0)
            {
                return [new TextMatch(0, text1.Length, false, 0)];
            }

            // 原地排序和合并
            SortAndMergeIntervals(ref matchesList);

            // 构建结果
            return BuildMatches(textLength, ref matchesList);
        }
        finally
        {
            matchesList.Dispose();
        }
    }
}

// 简单的值列表实现，避免堆分配
public ref struct ValueList<T> where T : unmanaged
{
    private Span<T> _span;
    public int Count;

    public ValueList(Span<T> initialBuffer)
    {
        _span = initialBuffer;
        Count = 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Add(T item)
    {
        if (Count >= _span.Length)
        {
            Grow();
        }
        _span[Count++] = item;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void Grow()
    {
        var newSpan = new T[_span.Length * 2];
        _span.CopyTo(newSpan);
        _span = newSpan;
    }

    public Span<T> AsSpan() => _span.Slice(0, Count);

    public void Dispose()
    {
        if (_span.Length > 256) // 如果分配了大数组，需要释放
        {
            _span = default;
        }
    }

    public T[] ToArray() => AsSpan().ToArray();
}