using System.Net.Http;

var http = new HttpClient();
http.Timeout = TimeSpan.FromSeconds(30);
http.DefaultRequestHeaders.Add("User-Agent", "Test/1.0");

var urls = new[]
{
    "https://arxiv.org/pdf/2103.14030.pdf",
    "https://arxiv.org/pdf/1706.03762.pdf",
    "https://arxiv.org/pdf/2210.03629.pdf"
};

foreach (var url in urls)
{
    Console.WriteLine($"Downloading: {url}");
    try
    {
        var resp = await http.GetAsync(url);
        Console.WriteLine($"  Status: {(int)resp.StatusCode} {resp.StatusCode}");
        Console.WriteLine($"  ContentType: {resp.Content.Headers.ContentType?.MediaType}");
        Console.WriteLine($"  ContentLength: {resp.Content.Headers.ContentLength}");
        var bytes = await resp.Content.ReadAsByteArrayAsync();
        Console.WriteLine($"  Actual size: {bytes.Length} bytes");
        Console.WriteLine($"  First 10 bytes (hex): {BitConverter.ToString(bytes.Take(10).ToArray())}");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"  ERROR: {ex.Message}");
    }
    Console.WriteLine();
}
