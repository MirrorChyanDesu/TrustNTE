using System;
using System.Windows.Input;

namespace TrustNTE.Models
{
    public sealed class HotKey
    {
        public Key Key { get; set; } = Key.None;
        public ModifierKeys Modifiers { get; set; } = ModifierKeys.None;

        public static HotKey None => new() { Key = Key.None, Modifiers = ModifierKeys.None };
        public bool IsEmpty => Key == Key.None && Modifiers == ModifierKeys.None;

        public override string ToString()
        {
            //    改这里：为空时输出 < None >
            if (IsEmpty) return "< None >";

            string modifierStr = string.Empty;
            if (Modifiers.HasFlag(ModifierKeys.Control)) modifierStr += "Ctrl+";
            if (Modifiers.HasFlag(ModifierKeys.Alt)) modifierStr += "Alt+";
            if (Modifiers.HasFlag(ModifierKeys.Shift)) modifierStr += "Shift+";
            if (Modifiers.HasFlag(ModifierKeys.Windows)) modifierStr += "Win+";
            return $"{modifierStr}{Key}";
        }

        public static HotKey Parse(string str)
        {
            if (string.IsNullOrWhiteSpace(str) || str == "< None >") return None;
            var hk = new HotKey();
            var parts = str.Split('+');
            foreach (var part in parts)
            {
                var p = part.Trim();
                if (p == "Ctrl") hk.Modifiers |= ModifierKeys.Control;
                else if (p == "Alt") hk.Modifiers |= ModifierKeys.Alt;
                else if (p == "Shift") hk.Modifiers |= ModifierKeys.Shift;
                else if (p == "Win") hk.Modifiers |= ModifierKeys.Windows;
                else if (Enum.TryParse<Key>(p, out var key)) hk.Key = key;
            }
            return hk;
        }

        public override bool Equals(object? obj)
        {
            if (obj is not HotKey other) return false;
            return Key == other.Key && Modifiers == other.Modifiers;
        }
        public override int GetHashCode() => HashCode.Combine(Key, Modifiers);
    }
}