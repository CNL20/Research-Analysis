using System.Threading.Channels;

namespace ScholarTrend.Application.Interfaces;

/// <summary>
/// Singleton wrapper cho bounded Channel<int> dùng bởi BackgroundService worker.
/// Tách ra khỏi PaperPdfDownloadService để:
///   - Host (Singleton) có thể đọc channel mà không cần inject Scoped service
///   - Enqueuer (Scoped) ghi vào channel
///   - Processor (Scoped) đọc từ channel
/// </summary>
public interface IPaperPdfChannel
{
    ChannelWriter<int> Writer { get; }
    ChannelReader<int> Reader { get; }
}

public class PaperPdfChannel : IPaperPdfChannel
{
    private readonly Channel<int> _channel;

    public PaperPdfChannel()
    {
        _channel = Channel.CreateBounded<int>(new BoundedChannelOptions(50)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = false,
            SingleWriter = false
        });
    }

    public ChannelWriter<int> Writer => _channel.Writer;
    public ChannelReader<int> Reader => _channel.Reader;
}
