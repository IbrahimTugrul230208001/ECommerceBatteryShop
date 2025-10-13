using System.Net.Http;

sealed class HttpLogHandler : DelegatingHandler
{
    public HttpLogHandler(HttpMessageHandler inner) : base(inner) { }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage r, CancellationToken ct)
    {
        // Only log Google token exchange
        if (r.RequestUri is { Host: "oauth2.googleapis.com" } && r.RequestUri.AbsolutePath.Contains("/token"))
        {
            var body = await r.Content!.ReadAsStringAsync(ct);
            Console.WriteLine("TOKEN POST → " + body);       // contains client_id, redirect_uri, *and* client_secret
        }

        var resp = await base.SendAsync(r, ct);

        if (r.RequestUri is { Host: "oauth2.googleapis.com" } && r.RequestUri.AbsolutePath.Contains("/token"))
        {
            var respBody = await resp.Content.ReadAsStringAsync(ct);
            Console.WriteLine($"TOKEN RESP ← {(int)resp.StatusCode} {respBody}");
        }

        return resp;
    }
}
