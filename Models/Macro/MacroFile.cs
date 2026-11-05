using System.Collections.Generic;

namespace TrustNTE.Models.Macro
{
    public class MacroFile
    {
        public int SchemaVersion { get; set; } = 1;
        public MacroInfo Info { get; set; } = new();
        public List<MacroEvent> MacroEvents { get; set; } = new();
    }
}