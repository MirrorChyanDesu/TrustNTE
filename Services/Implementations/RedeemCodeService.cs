using System;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using TrustNTE.Models;
using TrustNTE.Services.Interfaces;

namespace TrustNTE.Services.Implementations
{
    public class RedeemCodeService : IRedeemCodeService
    {
        private readonly HttpClient _httpClient;
        // 文件名   改为 redeem-codes.json
        private const string CodesJsonUrl = "https://raw.githubusercontent.com/daoqi/MintNTE/main/redeem-codes.json";
        private static readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

        public RedeemCodeService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<RedeemCodeFeed?> GetLatestFeedAsync(CancellationToken token = default)
        {
            try
            {
                using var response = await _httpClient.GetAsync(CodesJsonUrl, token);
                response.EnsureSuccessStatusCode();
                string json = await response.Content.ReadAsStringAsync(token);

                var feed = JsonSerializer.Deserialize<RedeemCodeFeed>(json, _jsonOptions);

                if (feed != null)
                {
                    // 核心：如果远端没写 Version，直接使用 JSON 内容的 MD5 哈希作为版本号
                    if (string.IsNullOrWhiteSpace(feed.Version))
                    {
                        using var md5 = MD5.Create();
                        var hash = md5.ComputeHash(Encoding.UTF8.GetBytes(json));
                        feed.Version = Convert.ToHexString(hash);
                    }

                    // 限制上限防止 UI 卡死
                    if (feed.Items != null && feed.Items.Count > 20)
                    {
                        feed.Items = feed.Items.GetRange(0, 20);
                    }
                }
                return feed;
            }
            catch (OperationCanceledException) { return null; }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"拉取兑换码失败: {ex.Message}");
                return null;
            }
        }
    }
}