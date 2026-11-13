using System.Globalization;

namespace System.Windows.Media
{
    public struct Color : IEquatable<Color>
    {
        public byte A { get; set; }
        public byte R { get; set; }
        public byte G { get; set; }
        public byte B { get; set; }

        public static Color FromArgb(byte a, byte r, byte g, byte b) => new Color { A = a, R = r, G = g, B = b };

        public static Color FromRgb(byte r, byte g, byte b) => FromArgb(255, r, g, b);

        /// <summary>Channels as 0..1 values (scRGB; treated as plain linear scaling here).</summary>
        public float ScA { get => A / 255f; set => A = (byte)Math.Clamp(Math.Round(value * 255), 0, 255); }
        public float ScR { get => R / 255f; set => R = (byte)Math.Clamp(Math.Round(value * 255), 0, 255); }
        public float ScG { get => G / 255f; set => G = (byte)Math.Clamp(Math.Round(value * 255), 0, 255); }
        public float ScB { get => B / 255f; set => B = (byte)Math.Clamp(Math.Round(value * 255), 0, 255); }

        public static Color FromScRgb(float a, float r, float g, float b)
        {
            static byte Channel(float value) => (byte)Math.Clamp(Math.Round(value * 255), 0, 255);
            return FromArgb(Channel(a), Channel(r), Channel(g), Channel(b));
        }

        public bool Equals(Color other) => A == other.A && R == other.R && G == other.G && B == other.B;

        public override bool Equals(object obj) => obj is Color other && Equals(other);

        public override int GetHashCode() => (A << 24) | (R << 16) | (G << 8) | B;

        public static bool operator ==(Color a, Color b) => a.Equals(b);

        public static bool operator !=(Color a, Color b) => !a.Equals(b);

        /// <summary>"#AARRGGBB", as in WPF.</summary>
        public override string ToString() => $"#{A:X2}{R:X2}{G:X2}{B:X2}";

        /// <summary>Parses "#RGB", "#ARGB", "#RRGGBB", "#AARRGGBB" or a colour name.</summary>
        public static bool TryParse(string text, out Color color)
        {
            color = default;
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }
            text = text.Trim();
            if (text[0] != '#')
            {
                return Colors.ByName.TryGetValue(text, out color);
            }

            string hex = text.Substring(1);
            if (hex.Length == 3 || hex.Length == 4)
            {
                var doubled = new Text.StringBuilder();
                foreach (char c in hex)
                {
                    doubled.Append(c).Append(c);
                }
                hex = doubled.ToString();
            }
            if (hex.Length == 6)
            {
                hex = "FF" + hex;
            }
            if (hex.Length != 8 || !uint.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint argb))
            {
                return false;
            }
            color = FromArgb((byte)(argb >> 24), (byte)(argb >> 16), (byte)(argb >> 8), (byte)argb);
            return true;
        }
    }
}
