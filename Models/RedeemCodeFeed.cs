using System.Collections.Generic;

namespace TrustNTE.Models
{
    public class RedeemCodeItem
    {
        public string Title { get; set; } = string.Empty;
        public List<string> Codes { get; set; } = new();
        public string Date { get; set; } = string.Empty;
        public string Server { get; set; } = "CN"; // 区分国服/国际服
    }

    public class RedeemCodeFeed
    {
        // 加上这个，解决报错
        public string Version { get; set; } = string.Empty; 
        public List<RedeemCodeItem> Items { get; set; } = new();
    }
}