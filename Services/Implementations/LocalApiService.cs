using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Threading.Tasks;
using OpenCvSharp;
using TrustNTE.Services.Interfaces;

namespace TrustNTE.Services
{
    public class LocalApiService
    {
        private HttpListener? _listener;
        private readonly AutomationService _automationService;
        private readonly ILogService _logService;

        private readonly ConcurrentQueue<string> _recentLogs = new();
        private const int MaxLogsToKeep = 50;

        private readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            WriteIndented = true
        };

        public bool IsRunning { get; private set; } = false;

        public LocalApiService(AutomationService automationService, ILogService logService)
        {
            _automationService = automationService;
            _logService = logService;

            _logService.OnLogReceived += (msg) =>
            {
                _recentLogs.Enqueue(msg);
                if (_recentLogs.Count > MaxLogsToKeep)
                {
                    _recentLogs.TryDequeue(out _);
                }
            };
        }

        public void Start()
        {
            if (IsRunning) return;

            try
            {
                _listener = new HttpListener();
                _listener.Prefixes.Add("http://localhost:8888/");
                _listener.Start();
                IsRunning = true;

                Task.Run(ListenLoop);
                _logService.Log("✅ 本地 API 接口已启动: http://localhost:8888/api/v1/");
            }
            catch (Exception ex)
            {
                IsRunning = false;
                _logService.LogError($"❌ 本地API启动失败 (可能端口被占用): {ex.Message}");
            }
        }

        public void Stop()
        {
            if (!IsRunning) return;

            try
            {
                IsRunning = false;
                _listener?.Stop();
                _listener?.Close();
                _listener = null;
                _logService.Log("🛑 本地 API 接口已关闭。");
            }
            catch (Exception ex)
            {
                _logService.LogError($"关闭本地API时发生错误: {ex.Message}");
            }
        }

        private async Task ListenLoop()
        {
            while (IsRunning && _listener != null && _listener.IsListening)
            {
                try
                {
                    var context = await _listener.GetContextAsync();
                    _ = Task.Run(() => HandleRequestAsync(context));
                }
                catch (HttpListenerException) { break; }
                catch { }
            }
        }

        // [   #15] 跨站请求校验：浏览器里的任意网页不能再打本 API。
        // 本地程序（curl / PowerShell / AI 工具）不带 Origin/Referer → 放行；
        // localhost 页面（viewer.html 同源）→ 放行；其它网站的页面 → 403。
        private static bool IsLocalOrigin(HttpListenerRequest request)
        {
            string? origin = request.Headers["Origin"];
            string? referer = request.Headers["Referer"];

            static bool IsLocalUrl(string? url)
            {
                if (string.IsNullOrEmpty(url)) return true;
                return Uri.TryCreate(url, UriKind.Absolute, out var u)
                    && (u.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
                        || u.Host == "127.0.0.1");
            }

            // 带 Origin 的请求（fetch 等）：必须是 localhost
            if (origin != null && !IsLocalUrl(origin)) return false;
            // 不带 Origin 但带 Referer（<img> 等 no-cors 请求）：Referer 必须是 localhost 或空
            if (origin == null && !IsLocalUrl(referer)) return false;
            return true;
        }

        private async Task HandleRequestAsync(HttpListenerContext context)
        {
            var request = context.Request;
            var response = context.Response;

            try
            {
                // [   #15] 入口校验：只放行本地工具与 localhost 页面，跨站网页一律 403。
                // （原 Access-Control-Allow-Origin:* 已删除——它等于允许任意网站读走截图）
                if (!IsLocalOrigin(request))
                {
                    response.StatusCode = 403;
                    await SendJsonResponse(response, new { code = 403, msg = "cross-origin request rejected" });
                    return;
                }

                if (request.HttpMethod == "GET")
                {
                    string path = request.Url?.AbsolutePath ?? "";

                    if (path == "/api/v1/start")
                    {
                        _automationService.Start();
                        await SendJsonResponse(response, new { code = 200, msg = "started" });
                    }
                    else if (path == "/api/v1/stop")
                    {
                        _automationService.Stop();
                        await SendJsonResponse(response, new { code = 200, msg = "stopped" });
                    }
                    else if (path == "/api/v1/status")
                    {
                        bool isRunning = _automationService.CurrentHwnd != IntPtr.Zero;
                        await SendJsonResponse(response, new { code = 200, isRunning, hwnd = _automationService.CurrentHwnd.ToString() });
                    }
                    else if (path == "/api/v1/logs")
                    {
                        await SendJsonResponse(response, new { code = 200, logs = _recentLogs.ToArray() });
                    }
                    else if (path == "/api/v1/screenshot")
                    {
                        using var mat = _automationService.GetLatestFrame();
                        if (mat == null || mat.Empty()) await SendJsonResponse(response, new { code = 500, msg = "画面尚未就绪" });
                        else
                        {
                            Cv2.ImEncode(".jpg", mat, out byte[] imgBytes, new ImageEncodingParam(ImwriteFlags.JpegQuality, 80));
                            response.ContentType = "image/jpeg";
                            response.ContentLength64 = imgBytes.Length;
                            await response.OutputStream.WriteAsync(imgBytes, 0, imgBytes.Length);
                        }
                    }
                    else if (path == "/api/v1/screenshot_base64")
                    {
                        using var mat = _automationService.GetLatestFrame();
                        if (mat == null || mat.Empty()) await SendJsonResponse(response, new { code = 500, msg = "画面尚未就绪" });
                        else
                        {
                            Cv2.ImEncode(".jpg", mat, out byte[] imgBytes, new ImageEncodingParam(ImwriteFlags.JpegQuality, 80));
                            await SendJsonResponse(response, new { code = 200, msg = "ok", width = mat.Width, height = mat.Height, imageBase64 = Convert.ToBase64String(imgBytes) });
                        }
                    }
                    else if (path == "/api/v1/viewer")
                    {
                        //     读取独立的 HTML 文件
                        string htmlPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Web", "viewer.html");
                        if (File.Exists(htmlPath))
                        {
                            string html = await File.ReadAllTextAsync(htmlPath);
                            byte[] buffer = Encoding.UTF8.GetBytes(html);
                            response.ContentType = "text/html; charset=utf-8";
                            response.ContentLength64 = buffer.Length;
                            await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
                        }
                        else
                        {
                            await SendJsonResponse(response, new { code = 404, msg = "未找到 viewer.html 文件，请检查 Assets/Web 目录" });
                        }
                    }
                    else if (path == "/api/v1/stream")
                    {
                        if (_automationService.CurrentHwnd == IntPtr.Zero)
                        {
                            await SendJsonResponse(response, new { code = 500, msg = "画面尚未就绪" });
                            return;
                        }

                        int targetFps = 20;
                        int quality = 80;

                        if (int.TryParse(request.QueryString["fps"], out int qFps)) targetFps = Math.Clamp(qFps, 1, 120);
                        if (int.TryParse(request.QueryString["quality"], out int qQua)) quality = Math.Clamp(qQua, 10, 100);

                        int delayMs = 1000 / targetFps;

                        response.ContentType = "multipart/x-mixed-replace; boundary=--frame";
                        response.Headers.Add("Cache-Control", "no-cache");
                        response.SendChunked = true;

                        try
                        {
                            while (IsRunning && response.OutputStream != null)
                            {
                                using var mat = _automationService.GetLatestFrame();
                                if (mat != null && !mat.Empty())
                                {
                                    Cv2.ImEncode(".jpg", mat, out byte[] imgBytes, new ImageEncodingParam(ImwriteFlags.JpegQuality, quality));

                                    byte[] boundary = Encoding.ASCII.GetBytes("--frame\r\nContent-Type: image/jpeg\r\nContent-Length: " + imgBytes.Length + "\r\n\r\n");
                                    await response.OutputStream.WriteAsync(boundary, 0, boundary.Length);
                                    await response.OutputStream.WriteAsync(imgBytes, 0, imgBytes.Length);
                                    await response.OutputStream.WriteAsync(Encoding.ASCII.GetBytes("\r\n"), 0, 2);
                                    await response.OutputStream.FlushAsync();
                                }
                                await Task.Delay(delayMs);
                            }
                        }
                        catch { }
                        finally { try { response.Abort(); } catch { } }
                        return;
                    }
                    else
                    {
                        await SendJsonResponse(response, new { code = 404, msg = "Not Found" });
                    }
                }
                else
                {
                    await SendJsonResponse(response, new { code = 405, msg = "Method Not Allowed" });
                }
            }
            catch { }
            finally { try { response.Close(); } catch { } }
        }

        private async Task SendJsonResponse(HttpListenerResponse response, object Data)
        {
            string json = JsonSerializer.Serialize(Data, _jsonOptions);
            byte[] buffer = Encoding.UTF8.GetBytes(json);
            response.ContentType = "application/json; charset=utf-8";
            response.ContentLength64 = buffer.Length;
            await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
        }
    }
}
