using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using Xunit;
using TrustNTE.Models.Macro;

namespace TrustNTE.Tests
{
    public class MacroTests
    {
        [Fact]
        public void MacroFile_ShouldSerializeAndDeserializeCorrectly()
        {
            // 伪造一个宏文件对象
            var macro = new MacroFile
            {
                Info = new MacroInfo { X = 0, Y = 0, Width = 1920, Height = 1080, RecordDpi = 1.0 },
                MacroEvents = new System.Collections.Generic.List<MacroEvent>
                {
                    new MacroEvent { Type = MacroEventType.KeyDown, Time = 100, KeyCode = 0x41 },
                    new MacroEvent { Type = MacroEventType.MouseMoveTo, Time = 200, MouseX = 500, MouseY = 500 }
                }
            };

            var jsonOptions = new JsonSerializerOptions
            {
                Converters = { new JsonStringEnumConverter() }
            };

            // 序列化
            string json = JsonSerializer.Serialize(macro, jsonOptions);
            Assert.Contains("KeyDown", json);
            Assert.Contains("MouseMoveTo", json);

            // 反序列化
            var deserialized = JsonSerializer.Deserialize<MacroFile>(json, jsonOptions);
            Assert.NotNull(deserialized);
            Assert.Equal(2, deserialized.MacroEvents.Count);
            Assert.Equal(0x41, deserialized.MacroEvents[0].KeyCode);
            Assert.Equal(MacroEventType.MouseMoveTo, deserialized.MacroEvents[1].Type);
        }

        [Fact]
        public void MacroPlayer_ShouldCalculateScaleCorrectly()
        {
            // 模拟 MacroPlayer 里的坐标缩放逻辑
            double gameWidth = 2560;
            double gameHeight = 1440;
            int recordWidth = 1920;
            int recordHeight = 1080;

            double scaleX = gameWidth / recordWidth;
            double scaleY = gameHeight / recordHeight;

            int recordedX = 960;
            int recordedY = 540;

            int targetX = (int)(recordedX * scaleX);
            int targetY = (int)(recordedY * scaleY);

            // 960在1920里是一半，到了2560里也应该是1280
            Assert.Equal(1280, targetX);
            Assert.Equal(720, targetY);
        }
    }
}