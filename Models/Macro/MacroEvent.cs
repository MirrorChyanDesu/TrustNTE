using System.Text.Json.Serialization;

namespace TrustNTE.Models.Macro
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum MacroEventType
    {
        KeyDown, KeyUp,
        MouseMoveTo, MouseMoveBy,
        MouseDown, MouseUp,
        MouseWheel
    }

    public class MacroEvent
    {
        public MacroEventType Type { get; set; }
        public long Time { get; set; } 
        public int? KeyCode { get; set; }
        public int? MouseX { get; set; }
        public int? MouseY { get; set; }
        public string? MouseButton { get; set; }
        public int? CameraOrientation { get; set; }
    }
}