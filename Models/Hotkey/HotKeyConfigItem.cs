namespace TrustNTE.Models
{
    public class HotKeyConfigItem
    {
        public string Id { get; set; } = string.Empty;
        public int Key { get; set; }
        public int Modifiers { get; set; }
        public string HotKeyType { get; set; } = "KeyboardMonitor"; 
    }
}