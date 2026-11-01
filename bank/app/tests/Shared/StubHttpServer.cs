namespace BankSystem.Tests.Shared
{
    using System;
    using System.Collections.Concurrent;
    using System.IO;
    using System.Net;
    using System.Net.Sockets;
    using System.Threading.Tasks;

    /// <summary>
    /// Real loopback HTTP server standing in for the next hop, because the apps create
    /// their own HttpClient and cannot be given a fake handler.
    /// </summary>
    public sealed class StubHttpServer : IDisposable
    {
        private readonly HttpListener listener = new HttpListener();

        public StubHttpServer()
        {
            var portFinder = new TcpListener(IPAddress.Loopback, 0);
            portFinder.Start();
            var port = ((IPEndPoint)portFinder.LocalEndpoint).Port;
            portFinder.Stop();

            this.Url = $"http://127.0.0.1:{port}/";
            this.listener.Prefixes.Add(this.Url);
            this.listener.Start();
            _ = Task.Run(this.ServeAsync);
        }

        public string Url { get; }

        public HttpStatusCode ResponseStatus { get; set; } = HttpStatusCode.OK;

        public string ResponseBody { get; set; }

        public ConcurrentQueue<string> Requests { get; } = new ConcurrentQueue<string>();

        public void Dispose() => this.listener.Close();

        private async Task ServeAsync()
        {
            while (this.listener.IsListening)
            {
                HttpListenerContext context;

                try
                {
                    context = await this.listener.GetContextAsync();
                }
                catch (Exception e) when (e is HttpListenerException || e is ObjectDisposedException)
                {
                    return;
                }

                using (var reader = new StreamReader(context.Request.InputStream))
                {
                    this.Requests.Enqueue(await reader.ReadToEndAsync());
                }

                context.Response.StatusCode = (int)this.ResponseStatus;

                if (this.ResponseBody != null)
                {
                    context.Response.ContentType = "application/json";
                    await context.Response.OutputStream.WriteAsync(System.Text.Encoding.UTF8.GetBytes(this.ResponseBody));
                }

                context.Response.Close();
            }
        }
    }
}
