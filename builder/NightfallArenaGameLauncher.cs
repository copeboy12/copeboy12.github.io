using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace NightfallArenaGame
{
    internal class GameHostContext : ApplicationContext
    {
        private readonly string root;
        private readonly TcpListener listener;
        private readonly Thread serverThread;
        private readonly NotifyIcon tray;
        private readonly int port;
        private bool running = true;

        public GameHostContext()
        {
            root = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "www");
            if (!File.Exists(Path.Combine(root, "index.html")))
            {
                MessageBox.Show("Missing www\\index.html beside NightfallArenaGame.exe.", "Nightfall Arena");
                ExitThread();
                return;
            }

            listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            port = ((IPEndPoint)listener.LocalEndpoint).Port;

            tray = new NotifyIcon();
            tray.Text = "Nightfall Arena";
            tray.Icon = System.Drawing.SystemIcons.Application;
            tray.Visible = true;
            tray.ContextMenu = new ContextMenu(new MenuItem[] {
                new MenuItem("Open Game", delegate { OpenGame(); }),
                new MenuItem("Exit", delegate { ExitThread(); })
            });
            tray.DoubleClick += delegate { OpenGame(); };

            serverThread = new Thread(ServerLoop);
            serverThread.IsBackground = true;
            serverThread.Start();

            OpenGame();
        }

        private void OpenGame()
        {
            Process.Start("http://127.0.0.1:" + port + "/");
        }

        private void ServerLoop()
        {
            while (running)
            {
                try
                {
                    var client = listener.AcceptTcpClient();
                    ThreadPool.QueueUserWorkItem(delegate { HandleClient(client); });
                }
                catch
                {
                    if (running) Thread.Sleep(100);
                }
            }
        }

        private void HandleClient(TcpClient client)
        {
            using (client)
            {
                try
                {
                    var stream = client.GetStream();
                    var buffer = new byte[8192];
                    int count = stream.Read(buffer, 0, buffer.Length);
                    if (count <= 0) return;

                    string request = Encoding.ASCII.GetString(buffer, 0, count);
                    string[] firstLine = request.Split(new[] { "\r\n" }, StringSplitOptions.None)[0].Split(' ');
                    if (firstLine.Length < 2)
                    {
                        WriteResponse(stream, 400, "text/plain", Encoding.UTF8.GetBytes("Bad request"));
                        return;
                    }

                    string urlPath = firstLine[1].Split('?')[0];
                    if (urlPath == "/") urlPath = "/index.html";
                    urlPath = Uri.UnescapeDataString(urlPath).Replace('/', Path.DirectorySeparatorChar).TrimStart(Path.DirectorySeparatorChar);
                    string filePath = Path.GetFullPath(Path.Combine(root, urlPath));

                    if (!filePath.StartsWith(Path.GetFullPath(root), StringComparison.OrdinalIgnoreCase) || !File.Exists(filePath))
                    {
                        WriteResponse(stream, 404, "text/plain", Encoding.UTF8.GetBytes("Not found"));
                        return;
                    }

                    WriteResponse(stream, 200, GetContentType(Path.GetExtension(filePath)), File.ReadAllBytes(filePath));
                }
                catch
                {
                }
            }
        }

        private void WriteResponse(NetworkStream stream, int status, string contentType, byte[] body)
        {
            string statusText = status == 200 ? "OK" : status == 404 ? "Not Found" : "Bad Request";
            string header =
                "HTTP/1.1 " + status + " " + statusText + "\r\n" +
                "Content-Type: " + contentType + "\r\n" +
                "Content-Length: " + body.Length + "\r\n" +
                "Cache-Control: no-store\r\n" +
                "Connection: close\r\n\r\n";
            byte[] headerBytes = Encoding.ASCII.GetBytes(header);
            stream.Write(headerBytes, 0, headerBytes.Length);
            stream.Write(body, 0, body.Length);
        }

        private string GetContentType(string ext)
        {
            var types = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
                { ".html", "text/html; charset=utf-8" },
                { ".css", "text/css; charset=utf-8" },
                { ".js", "text/javascript; charset=utf-8" },
                { ".json", "application/json; charset=utf-8" },
                { ".png", "image/png" },
                { ".jpg", "image/jpeg" },
                { ".jpeg", "image/jpeg" },
                { ".gif", "image/gif" },
                { ".webp", "image/webp" },
                { ".svg", "image/svg+xml" },
                { ".mp3", "audio/mpeg" },
                { ".wav", "audio/wav" }
            };
            return types.ContainsKey(ext) ? types[ext] : "application/octet-stream";
        }

        protected override void ExitThreadCore()
        {
            running = false;
            try { listener.Stop(); } catch { }
            if (tray != null)
            {
                tray.Visible = false;
                tray.Dispose();
            }
            base.ExitThreadCore();
        }

        [STAThread]
        public static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new GameHostContext());
        }
    }
}
