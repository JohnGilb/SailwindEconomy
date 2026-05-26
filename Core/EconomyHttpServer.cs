using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace EconomyRevamp
{
    internal sealed class EconomyHttpServer
    {
        private readonly Action<object> _logInfo;
        private readonly Action<object> _logWarning;
        private readonly Action<object> _logError;
        private TcpListener _listener;
        private Thread _thread;
        private Func<string> _snapshotProvider;
        private string _webRoot;
        private volatile bool _running;
        private int _port;

        public EconomyHttpServer(Action<object> logInfo, Action<object> logWarning, Action<object> logError)
        {
            _logInfo = logInfo;
            _logWarning = logWarning;
            _logError = logError;
        }

        public void Start(int port, Func<string> snapshotProvider, string webRoot)
        {
            if (_running)
            {
                return;
            }

            _port = port;
            _snapshotProvider = snapshotProvider;
            _webRoot = webRoot;
            _listener = new TcpListener(IPAddress.Loopback, port);
            _listener.Start();
            _running = true;
            _thread = new Thread(ListenLoop);
            _thread.IsBackground = true;
            _thread.Name = "EconomyRevamp HTTP";
            _thread.Start();
        }

        public void Stop()
        {
            _running = false;

            try
            {
                if (_listener != null)
                {
                    _listener.Stop();
                }
            }
            catch
            {
            }

            _listener = null;
        }

        private void ListenLoop()
        {
            while (_running)
            {
                try
                {
                    TcpClient client = _listener.AcceptTcpClient();
                    ProcessClient(client);
                }
                catch (SocketException)
                {
                    if (_running)
                    {
                        _logWarning("Economy HTTP listener socket interrupted.");
                    }
                }
                catch (Exception ex)
                {
                    if (_running)
                    {
                        _logError("Economy HTTP listener failed: " + ex);
                    }
                }
            }
        }

        private void ProcessClient(TcpClient client)
        {
            using (client)
            {
                client.ReceiveTimeout = 2000;
                client.SendTimeout = 2000;

                NetworkStream stream = client.GetStream();
                StreamReader reader = new StreamReader(stream, Encoding.ASCII);
                string requestLine = reader.ReadLine();
                if (string.IsNullOrEmpty(requestLine))
                {
                    return;
                }

                string[] parts = requestLine.Split(' ');
                if (parts.Length < 2)
                {
                    WriteResponse(stream, 400, "Bad Request", "text/plain; charset=utf-8", "Bad Request");
                    return;
                }

                string method = parts[0];
                string path = parts[1];
                while (!string.IsNullOrEmpty(reader.ReadLine()))
                {
                }

                if (method == "OPTIONS")
                {
                    WriteResponse(stream, 204, "No Content", "text/plain; charset=utf-8", string.Empty);
                    return;
                }

                if (method != "GET")
                {
                    WriteResponse(stream, 405, "Method Not Allowed", "text/plain; charset=utf-8", "Only GET is supported.");
                    return;
                }

                ServePath(stream, path);
            }
        }

        private void ServePath(Stream stream, string rawPath)
        {
            string path = rawPath;
            int queryIndex = path.IndexOf('?');
            if (queryIndex >= 0)
            {
                path = path.Substring(0, queryIndex);
            }

            if (path == "/api/economy")
            {
                string snapshot = _snapshotProvider == null ? "{}" : _snapshotProvider();
                WriteResponse(stream, 200, "OK", "application/json; charset=utf-8", snapshot ?? "{}");
                return;
            }

            if (path == "/api/status")
            {
                WriteResponse(stream, 200, "OK", "application/json; charset=utf-8", "{\"status\":\"ok\",\"port\":" + _port + "}");
                return;
            }

            if (path == "/" || path == "/index.html")
            {
                string index = TryReadWebFile("index.html");
                if (index != null)
                {
                    WriteResponse(stream, 200, "OK", "text/html; charset=utf-8", index);
                    return;
                }

                WriteResponse(stream, 200, "OK", "text/html; charset=utf-8", FallbackIndexHtml());
                return;
            }

            string trimmed = path.TrimStart('/');
            string staticContent = TryReadWebFile(trimmed);
            if (staticContent != null)
            {
                WriteResponse(stream, 200, "OK", GetContentType(trimmed), staticContent);
                return;
            }

            WriteResponse(stream, 404, "Not Found", "text/plain; charset=utf-8", "Not Found");
        }

        private string TryReadWebFile(string relativePath)
        {
            if (string.IsNullOrEmpty(_webRoot) || string.IsNullOrEmpty(relativePath))
            {
                return null;
            }

            try
            {
                string root = Path.GetFullPath(_webRoot);
                string fullPath = Path.GetFullPath(Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar)));
                string rootWithSeparator = root.EndsWith(Path.DirectorySeparatorChar.ToString())
                    ? root
                    : root + Path.DirectorySeparatorChar;
                if (!fullPath.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase) || !File.Exists(fullPath))
                {
                    return null;
                }

                return File.ReadAllText(fullPath, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                _logWarning("Could not read web file: " + ex.Message);
                return null;
            }
        }

        private static string GetContentType(string path)
        {
            string extension = Path.GetExtension(path).ToLowerInvariant();
            if (extension == ".css")
            {
                return "text/css; charset=utf-8";
            }
            if (extension == ".js")
            {
                return "application/javascript; charset=utf-8";
            }
            if (extension == ".json")
            {
                return "application/json; charset=utf-8";
            }
            if (extension == ".svg")
            {
                return "image/svg+xml; charset=utf-8";
            }

            return "text/plain; charset=utf-8";
        }

        private static void WriteResponse(Stream stream, int statusCode, string statusText, string contentType, string body)
        {
            byte[] bodyBytes = Encoding.UTF8.GetBytes(body ?? string.Empty);
            string headers =
                "HTTP/1.1 " + statusCode + " " + statusText + "\r\n" +
                "Content-Type: " + contentType + "\r\n" +
                "Content-Length: " + bodyBytes.Length + "\r\n" +
                "Access-Control-Allow-Origin: *\r\n" +
                "Access-Control-Allow-Methods: GET, OPTIONS\r\n" +
                "Access-Control-Allow-Headers: Content-Type\r\n" +
                "Cache-Control: no-store\r\n" +
                "Connection: close\r\n\r\n";
            byte[] headerBytes = Encoding.ASCII.GetBytes(headers);
            stream.Write(headerBytes, 0, headerBytes.Length);
            if (bodyBytes.Length > 0)
            {
                stream.Write(bodyBytes, 0, bodyBytes.Length);
            }
        }

        private static string FallbackIndexHtml()
        {
            return "<!doctype html><html><head><meta charset=\"utf-8\"><title>Economy Revamp</title></head><body><pre id=\"out\">Loading...</pre><script>fetch('/api/economy').then(r=>r.json()).then(j=>out.textContent=JSON.stringify(j,null,2)).catch(e=>out.textContent=e)</script></body></html>";
        }
    }
}
