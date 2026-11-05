using Markdig;
using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using Wpf.Ui;
using TrustNTE.Views.Pages;

namespace TrustNTE.Views.Windows
{
    public partial class DocViewerWindow : Wpf.Ui.Controls.FluentWindow
    {
        public DocViewerWindow(string title, string content)
        {
            InitializeComponent();
            this.Title = $"说明文档 - {title}";

            Loaded += async (_, _) => await LoadMarkdownAsync(content);
        }

        private async Task LoadMarkdownAsync(string markdownText)
        {
            try
            {
                string userDataFolder = Path.Combine(Path.GetTempPath(), "TrustNTE_WebView2_Cache");
                var env = await Microsoft.Web.WebView2.Core.CoreWebView2Environment.CreateAsync(null, userDataFolder);

                await MarkdownWebView.EnsureCoreWebView2Async(env);
                MarkdownWebView.CoreWebView2.Profile.PreferredColorScheme = Microsoft.Web.WebView2.Core.CoreWebView2PreferredColorScheme.Dark;

                // 核心：监听网页发来的消息
                MarkdownWebView.CoreWebView2.WebMessageReceived += OnWebMessageReceived;

                var pipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();
                string htmlBody = Markdown.ToHtml(markdownText, pipeline);

                // 注意底部加入的 <script>，用来拦截超链接点击
                string html = $@"
                    <!DOCTYPE html>
                    <html>
                    <head>
                        <meta charset=""utf-8"">
                        <style>
                            :root {{ color-scheme: dark; }}
                            body {{
                                font-family: 'MiSans', 'Segoe UI', 'Segoe UI Emoji', sans-serif;
                                background-color: transparent;
                                color: #E5E9F0;
                                line-height: 1.6;
                                padding: 20px 24px;
                                margin: 0;
                            }}
                            h1, h2, h3, h4, h5, h6 {{ color: #FFFFFF; margin-top: 1.5em; margin-bottom: 0.5em; }}
                            h1 {{ font-size: 26px; border-bottom: 1px solid #3E4452; padding-bottom: 8px; }}
                            h3 {{ font-size: 16px; color: #C9D1D9; }}
                            p, li {{ color: #B0B8C8; margin-bottom: 0.5em; }}
                            a {{ color: #58A6FF; text-decoration: none; cursor: pointer; font-weight: bold; }}
                            a:hover {{ text-decoration: underline; }}
                            ul, ol {{ padding-left: 1.5em; }}
                            blockquote {{
                                border-left: 4px solid #58A6FF;
                                margin: 1em 0;
                                padding: 0.5em 1em;
                                background-color: #2D333B;
                                border-radius: 0 4px 4px 0;
                                color: #8B949E;
                            }}
                            blockquote p {{ color: #8B949E; margin: 0; }}
                        </style>
                    </head>
                    <body>
                        {htmlBody}
                        
                        <!-- 拦截特定的链接点击并发送消息给 C# -->
                        <script>
                            document.addEventListener('click', function(e) {{
                                if (e.target.tagName.toLowerCase() === 'a') {{
                                    var href = e.target.getAttribute('href');
                                    if (href && href.startsWith('app://')) {{
                                        e.preventDefault();
                                        window.chrome.webview.postMessage(href);
                                    }}
                                }}
                            }});
                        </script>
                    </body>
                    </html>";

                MarkdownWebView.NavigateToString(html);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"WebView2 初始化失败: {ex.Message}", "渲染错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // 处理网页发来的跳转请求（防闪退，无弹窗，解耦处理）
        private void OnWebMessageReceived(object? sender, Microsoft.Web.WebView2.Core.CoreWebView2WebMessageReceivedEventArgs e)
        {
            try
            {
                string message = e.TryGetWebMessageAsString();

                // 只要以 app:// 开头，就说明是内部跳转指令
                if (!string.IsNullOrEmpty(message) && message.StartsWith("app://"))
                {
                    // 1. 提取跳转标识（比如 "goto-interval"）
                    string target = message.Replace("app://", "");

                    // 2. 关闭当前文档窗口
                    this.Close();

                    // 3. 派发跳转任务给主页 VM（主 VM 会自己决定展开哪个面板）
                    var startPageVm = App.ServiceProvider?.GetService(typeof(TrustNTE.ViewModels.Pages.StartPageViewModel)) as TrustNTE.ViewModels.Pages.StartPageViewModel;
                    startPageVm?.HandleAppNavigation(target);

                    // 4. 导航到启动页
                    var navService = App.ServiceProvider?.GetService(typeof(INavigationService)) as INavigationService;
                    navService?.Navigate(typeof(StartPage));
                }
            }
            catch (Exception ex)
            {
                // 防止异步事件未捕获异常导致程序闪退
                System.Diagnostics.Debug.WriteLine($"[DocViewer] 跳转失败: {ex.Message}");
            }
        }

        public static void ShowDoc(string title, string fileName, string fallbackContent = "")
        {
            try
            {
                string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Docs", fileName);
                string content = fallbackContent;

                if (File.Exists(path))
                {
                    content = File.ReadAllText(path, Encoding.UTF8);

                    // 自动替换掉不支持的键帽 Emoji
                    content = content
                        .Replace("1️⃣", "①").Replace("2️⃣", "②").Replace("3️⃣", "③")
                        .Replace("4️⃣", "④").Replace("5️⃣", "⑤").Replace("6️⃣", "⑥")
                        .Replace("7️⃣", "⑦").Replace("8️⃣", "⑧").Replace("9️⃣", "⑨")
                        .Replace("0️⃣", "⓪");
                }

                System.Windows.Application.Current?.Dispatcher.Invoke(() =>
                {
                    var win = new DocViewerWindow(title, content);
                    if (System.Windows.Application.Current?.MainWindow is { IsLoaded: true } owner)
                    {
                        win.Owner = owner;
                    }
                    win.ShowDialog();
                });
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"文档加载失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OnCloseClick(object sender, RoutedEventArgs e) => this.Close();
    }
}