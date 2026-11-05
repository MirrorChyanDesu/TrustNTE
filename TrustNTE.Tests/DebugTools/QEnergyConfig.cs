using System.IO;
using System.Text.Json;

namespace TrustNTE.Tests.DebugTools
{
    public class QEnergyConfig
    {
        public int RoiX { get; set; } = 1767;
        public int RoiY { get; set; } = 915;
        public int RoiWidth { get; set; } = 111;
        public int RoiHeight { get; set; } = 111;
        public bool AnchorRight { get; set; } = true;
        public int ExcludeBottomPx { get; set; } = 22;

        public byte BrightMinV { get; set; } = 190;
        public double BrightReadyRatio { get; set; } = 0.15;
        public int ConfirmFrames { get; set; } = 2;

        private static readonly string FilePath = "qenergy_roi.json";

        public static QEnergyConfig Load()
        {
            if (File.Exists(FilePath))
            {
                try
                {
                    var json = File.ReadAllText(FilePath);
                    return JsonSerializer.Deserialize<QEnergyConfig>(json) ?? new QEnergyConfig();
                }
                catch { }
            }
            return new QEnergyConfig();
        }

        public void Save()
        {
            try
            {
                var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(FilePath, json);
            }
            catch { }
        }
    }
}