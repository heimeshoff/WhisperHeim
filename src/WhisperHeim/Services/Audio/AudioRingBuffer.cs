using System.Threading;

namespace WhisperHeim.Services.Audio;

/// <summary>
/// Thread-safe lock-free ring buffer for float audio samples.
/// Producer (capture thread) writes, consumer (processing thread) reads.
/// </summary>
public sealed class AudioRingBuffer
{
    private readonly float[] _buffer;
    private readonly int _capacity;
    private long _writePosition;
    private long _readPosition;

    public AudioRingBuffer(int capacity)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(capacity, 0);
        _capacity = capacity;
        _buffer = new float[capacity];
    }

    /// <summary>
    /// Number of samples available to read.
    /// </summary>
    public int Available
    {
        get
        {
            long w = Interlocked.Read(ref _writePosition);
            long r = Interlocked.Read(ref _readPosition);
            return (int)(w - r);
        }
    }

    /// <summary>
    /// Total capacity in samples.
    /// </summary>
    public int Capacity => _capacity;

    /// <summary>
    /// Writes samples into the ring buffer. If there is not enough space,
    /// the oldest unread samples are silently dropped (overwritten).
    /// </summary>
    public void Write(ReadOnlySpan<float> samples)
    {
        for (int i = 0; i < samples.Length; i++)
        {
            long pos = Interlocked.Read(ref _writePosition);
            _buffer[pos % _capacity] = samples[i];
            Interlocked.Increment(ref _writePosition);
        }

        // Advance read position if writer has lapped reader
        long write = Interlocked.Read(ref _writePosition);
        long read = Interlocked.Read(ref _readPosition);
        if (write - read > _capacity)
        {
            Interlocked.Exchange(ref _readPosition, write - _capacity);
        }
    }

    /// <summary>
    /// Reads up to <paramref name="maxSamples"/> samples into the destination span.
    /// Returns the number of samples actually read.
    /// </summary>
    public int Read(Span<float> destination, int maxSamples)
    {
        int toRead = Math.Min(maxSamples, Math.Min(destination.Length, Available));
        for (int i = 0; i < toRead; i++)
        {
            long pos = Interlocked.Read(ref _readPosition);
            destination[i] = _buffer[pos % _capacity];
            Interlocked.Increment(ref _readPosition);
        }
        return toRead;
    }

    /// <summary>
    /// Reads all available samples and returns them as a new array.
    /// </summary>
    public float[] ReadAll()
    {
        int count = Available;
        if (count == 0)
            return [];

        float[] result = new float[count];
        Read(result, count);
        return result;
    }

    /// <summary>
    /// Resets the buffer, discarding all unread data.
    /// </summary>
    public void Clear()
    {
        Interlocked.Exchange(ref _readPosition, Interlocked.Read(ref _writePosition));
    }
}
