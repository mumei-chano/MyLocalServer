using System;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;

namespace LocalServer
{
    public class LocalServer
    {
        static string Port = "8000";
        static void PrintLocalIPs()
        {
            Console.WriteLine("=== Local IP Addresses ===");

            foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.OperationalStatus != OperationalStatus.Up)
                    continue;

                var props = ni.GetIPProperties();
                foreach (var addr in props.UnicastAddresses)
                {
                    if (addr.Address.AddressFamily == AddressFamily.InterNetwork)
                    {
                        Console.WriteLine($"[{ni.Name}] {addr.Address}");
                    }
                }
            }

            Console.WriteLine("==========================");
        }

        public static void Main()
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            Directory.SetCurrentDirectory(AppDomain.CurrentDomain.BaseDirectory);

            Console.OutputEncoding = Encoding.UTF8;
            Console.ForegroundColor = ConsoleColor.Cyan;

            PrintLocalIPs();

            Console.WriteLine("LocalServer starting...");
            Console.ResetColor();

            Directory.CreateDirectory("wwwroot");

            HttpListener listener = new HttpListener();
            listener.Prefixes.Add($"http://+:{Port}/");
            listener.Start();

            Console.WriteLine("Server started!");
            Console.WriteLine($" http://localhost:{Port}/");

            while (true)
            {
                HttpListenerContext ctx = null;

                try
                {
                    ctx = listener.GetContext();
                    var req = ctx.Request;
                    var res = ctx.Response;

                    Console.WriteLine(
                        $"[INFO] {req.HttpMethod} {req.RawUrl} [{DateTime.Now:yyyy-MM-dd HH:mm:ss}]"
                    );

                    string relativePath = req.Url.AbsolutePath
                        .TrimStart('/')
                        .Replace('/', Path.DirectorySeparatorChar);

                    if (string.IsNullOrEmpty(relativePath))
                        relativePath = "index.html";

                    string fullPath = Path.Combine("wwwroot", relativePath);

                    string? dir = Path.GetDirectoryName(fullPath);
                    if (!string.IsNullOrEmpty(dir))
                        Directory.CreateDirectory(dir);

                    if (req.HttpMethod == "PUT" || req.HttpMethod == "POST")
                    {
                        using var ms = new MemoryStream();
                        req.InputStream.CopyTo(ms);
                        byte[] body = ms.ToArray();

                        File.WriteAllBytes(fullPath, body);

                        byte[] responseBytes = Encoding.UTF8.GetBytes("OK");
                        res.StatusCode = 200;
                        res.ContentType = "text/plain; charset=utf-8";
                        res.ContentLength64 = responseBytes.Length;
                        res.OutputStream.Write(responseBytes, 0, responseBytes.Length);
                        continue;
                    }

                    if (req.HttpMethod == "GET")
                    {
                        if (File.Exists(fullPath))
                        {
                            byte[] data = File.ReadAllBytes(fullPath);

                            if (Path.GetExtension(fullPath)
                                .Equals(".json", StringComparison.OrdinalIgnoreCase))
                            {
                                res.ContentType = "application/json; charset=utf-8";
                            }
                            else
                            {
                                res.ContentType = "application/octet-stream";
                            }

                            res.ContentLength64 = data.Length;
                            res.OutputStream.Write(data, 0, data.Length);
                        }
                        else
                        {
                            res.StatusCode = 404;
                        }

                        continue;
                    }

                    res.StatusCode = 405;
                }
                catch (Exception ex)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"[ERROR] {ex}");
                    Console.ResetColor();

                    if (ctx != null)
                        ctx.Response.StatusCode = 500;
                }
                finally
                {
                    ctx?.Response.Close();
                }
            }
        }
    }
}
