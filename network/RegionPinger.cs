using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

// Times the TCP round trip to each region's ping echo (lobby ServePing) every two seconds
// and keeps the median of the last five. It runs off the main thread so a frame does not
// add to the measured time; Snapshot is safe to call from anywhere.
public sealed class RegionPinger : IDisposable
{
    private const int Window = 5, IntervalMs = 2000, TimeoutMs = 1500, RetryMs = 3000;
    private readonly CancellationTokenSource _stop = new();
    private readonly object _lock = new();
    private readonly Dictionary<string, int> _medians = new();

    public RegionPinger(IEnumerable<RegionInfo> regions)
    {
        foreach (var region in regions)
            if (!string.IsNullOrEmpty(region.Ping)) _ = Task.Run(() => Measure(region.Id, region.Ping, _stop.Token));
    }

    public Dictionary<string, int> Snapshot() { lock (_lock) return new Dictionary<string, int>(_medians); }

    public void Dispose() { _stop.Cancel(); }

    private async Task Measure(string id, string address, CancellationToken stop)
    {
        int split = address.LastIndexOf(':');
        if (split <= 0 || !int.TryParse(address[(split + 1)..], out int port)) return;
        string host = address[..split].Trim('[', ']');
        var samples = new List<int>();
        int token = 0;
        while (!stop.IsCancellationRequested)
        {
            try
            {
                using var client = new TcpClient { NoDelay = true };
                using (var connect = CancellationTokenSource.CreateLinkedTokenSource(stop))
                {
                    connect.CancelAfter(TimeoutMs);
                    await client.ConnectAsync(host, port, connect.Token).ConfigureAwait(false);
                }
                using var stream = client.GetStream();
                using var reader = new StreamReader(stream, Encoding.ASCII);
                using var writer = new StreamWriter(stream, Encoding.ASCII) { NewLine = "\n", AutoFlush = true };
                while (!stop.IsCancellationRequested)
                {
                    string expected = "PONG " + ++token;
                    var watch = Stopwatch.StartNew();
                    await writer.WriteLineAsync("PING " + token).ConfigureAwait(false);
                    using (var read = CancellationTokenSource.CreateLinkedTokenSource(stop))
                    {
                        read.CancelAfter(TimeoutMs);
                        if (await reader.ReadLineAsync(read.Token).ConfigureAwait(false) != expected) throw new IOException("bad echo");
                    }
                    samples.Add((int)watch.ElapsedMilliseconds);
                    if (samples.Count > Window) samples.RemoveAt(0);
                    int median = samples.OrderBy(ms => ms).ElementAt(samples.Count / 2);
                    lock (_lock) _medians[id] = median;
                    await Task.Delay(IntervalMs, stop).ConfigureAwait(false);
                }
            }
            catch (Exception) when (!stop.IsCancellationRequested)
            {
                lock (_lock) _medians.Remove(id); // Unreachable now; an old figure would mislead.
                samples.Clear();
                try { await Task.Delay(RetryMs, stop).ConfigureAwait(false); }
                catch (OperationCanceledException) { return; }
            }
            catch (Exception) { return; }
        }
    }
}
