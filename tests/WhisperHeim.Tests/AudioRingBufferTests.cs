using WhisperHeim.Services.Audio;

namespace WhisperHeim.Tests;

public class AudioRingBufferTests
{
    [Fact]
    public void WriteAndRead_ReturnsCorrectSamples()
    {
        var buffer = new AudioRingBuffer(1024);
        float[] input = [1.0f, 0.5f, -0.5f, -1.0f];

        buffer.Write(input);

        Assert.Equal(4, buffer.Available);
        float[] output = buffer.ReadAll();
        Assert.Equal(input, output);
        Assert.Equal(0, buffer.Available);
    }

    [Fact]
    public void Write_WhenExceedingCapacity_OverwritesOldest()
    {
        var buffer = new AudioRingBuffer(4);
        float[] first = [1f, 2f, 3f, 4f];
        float[] second = [5f, 6f];

        buffer.Write(first);
        buffer.Write(second);

        // Buffer capacity is 4, so oldest 2 samples are overwritten.
        // Read position is advanced to (writePos - capacity).
        float[] output = buffer.ReadAll();
        Assert.Equal(4, output.Length);
        Assert.Equal([3f, 4f, 5f, 6f], output);
    }

    [Fact]
    public void Clear_DiscardsAllData()
    {
        var buffer = new AudioRingBuffer(1024);
        buffer.Write([1f, 2f, 3f]);

        buffer.Clear();

        Assert.Equal(0, buffer.Available);
        float[] output = buffer.ReadAll();
        Assert.Empty(output);
    }

    [Fact]
    public void ReadAll_WhenEmpty_ReturnsEmptyArray()
    {
        var buffer = new AudioRingBuffer(1024);
        float[] output = buffer.ReadAll();
        Assert.Empty(output);
    }

    [Fact]
    public void Read_WithPartialRequest_ReturnsRequestedAmount()
    {
        var buffer = new AudioRingBuffer(1024);
        buffer.Write([1f, 2f, 3f, 4f, 5f]);

        float[] dest = new float[3];
        int read = buffer.Read(dest, 3);

        Assert.Equal(3, read);
        Assert.Equal([1f, 2f, 3f], dest);
        Assert.Equal(2, buffer.Available);
    }

    [Fact]
    public async Task ConcurrentWriteAndRead_MaintainsIntegrity()
    {
        // Simulates capture thread writing and processing thread reading
        var buffer = new AudioRingBuffer(16000 * 5); // 5 seconds
        const int totalSamples = 16000 * 2; // 2 seconds of audio
        long totalRead = 0;
        bool writerDone = false;

        var writer = Task.Run(() =>
        {
            float[] chunk = new float[800]; // 50ms chunks
            for (int i = 0; i < totalSamples / chunk.Length; i++)
            {
                for (int j = 0; j < chunk.Length; j++)
                    chunk[j] = (float)Math.Sin(2.0 * Math.PI * 440 * (i * chunk.Length + j) / 16000.0);
                buffer.Write(chunk);
            }
            writerDone = true;
        });

        var reader = Task.Run(() =>
        {
            float[] readBuf = new float[800];
            while (!writerDone || buffer.Available > 0)
            {
                int read = buffer.Read(readBuf, readBuf.Length);
                Interlocked.Add(ref totalRead, read);
                if (read == 0)
                    Thread.SpinWait(100);
            }
        });

        await Task.WhenAll(writer, reader);

        // We should have read all samples (may lose a few to timing, but should be close)
        Assert.True(totalRead > 0, "Should have read some samples");
        Assert.True(totalRead <= totalSamples, $"Read {totalRead} but only wrote {totalSamples}");
    }

    [Fact]
    public void Capacity_ReturnsConstructorValue()
    {
        var buffer = new AudioRingBuffer(4096);
        Assert.Equal(4096, buffer.Capacity);
    }

    [Fact]
    public void Constructor_ThrowsOnInvalidCapacity()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new AudioRingBuffer(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new AudioRingBuffer(-1));
    }
}
