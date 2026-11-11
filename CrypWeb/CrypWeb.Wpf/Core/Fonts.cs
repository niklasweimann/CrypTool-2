namespace System.Windows
{
    public struct FontWeight : IEquatable<FontWeight>
    {
        internal FontWeight(int weight) => Weight = weight == 0 ? 400 : weight;

        /// <summary>OpenType weight (400 = normal, 700 = bold); 0 means "not set" and counts as normal.</summary>
        internal int Weight { get; }

        public int ToOpenTypeWeight() => Weight == 0 ? 400 : Weight;

        public static FontWeight FromOpenTypeWeight(int weight) => new FontWeight(weight);

        public bool Equals(FontWeight other) => ToOpenTypeWeight() == other.ToOpenTypeWeight();

        public override bool Equals(object obj) => obj is FontWeight other && Equals(other);

        public override int GetHashCode() => ToOpenTypeWeight();

        public static bool operator ==(FontWeight a, FontWeight b) => a.Equals(b);

        public static bool operator !=(FontWeight a, FontWeight b) => !a.Equals(b);

        public override string ToString() => ToOpenTypeWeight() >= 600 ? "Bold" : "Normal";
    }

    public static class FontWeights
    {
        public static FontWeight Thin => new FontWeight(100);
        public static FontWeight ExtraLight => new FontWeight(200);
        public static FontWeight Light => new FontWeight(300);
        public static FontWeight Normal => new FontWeight(400);
        public static FontWeight Regular => new FontWeight(400);
        public static FontWeight Medium => new FontWeight(500);
        public static FontWeight SemiBold => new FontWeight(600);
        public static FontWeight DemiBold => new FontWeight(600);
        public static FontWeight Bold => new FontWeight(700);
        public static FontWeight ExtraBold => new FontWeight(800);
        public static FontWeight UltraBold => new FontWeight(800);
        public static FontWeight Black => new FontWeight(900);
        public static FontWeight Heavy => new FontWeight(900);

        internal static bool TryParse(string name, out FontWeight weight)
        {
            var property = typeof(FontWeights).GetProperty(name.Trim(), Reflection.BindingFlags.Public | Reflection.BindingFlags.Static | Reflection.BindingFlags.IgnoreCase);
            weight = property != null ? (FontWeight)property.GetValue(null) : Normal;
            return property != null;
        }
    }

    public struct FontStyle : IEquatable<FontStyle>
    {
        internal FontStyle(bool italic) => IsItalic = italic;

        internal bool IsItalic { get; }

        public bool Equals(FontStyle other) => IsItalic == other.IsItalic;

        public override bool Equals(object obj) => obj is FontStyle other && Equals(other);

        public override int GetHashCode() => IsItalic ? 1 : 0;

        public static bool operator ==(FontStyle a, FontStyle b) => a.Equals(b);

        public static bool operator !=(FontStyle a, FontStyle b) => !a.Equals(b);

        public override string ToString() => IsItalic ? "Italic" : "Normal";
    }

    public static class FontStyles
    {
        public static FontStyle Normal => new FontStyle(false);
        public static FontStyle Italic => new FontStyle(true);
        public static FontStyle Oblique => new FontStyle(true);
    }

    public struct FontStretch { }

    public static class FontStretches
    {
        public static FontStretch Normal => default;
    }

    public class FontWeightConverter
    {
        public object ConvertFromString(string text)
        {
            FontWeights.TryParse(text, out FontWeight weight);
            return weight;
        }
    }

    public class FontStyleConverter
    {
        public object ConvertFromString(string text)
        {
            return new FontStyle(text != null && !text.Trim().Equals("Normal", StringComparison.OrdinalIgnoreCase));
        }
    }
}

namespace System.Windows.Media
{
    public class FontFamily
    {
        public FontFamily() : this("Segoe UI") { }

        public FontFamily(string familyName) => Source = familyName;

        public FontFamily(Uri baseUri, string familyName) => Source = familyName;

        public string Source { get; }

        /// <summary>The family name without a resource path ("./Fonts/#Foo" -> "Foo").</summary>
        internal string CssName
        {
            get
            {
                string name = Source ?? "";
                int hash = name.LastIndexOf('#');
                if (hash >= 0)
                {
                    name = name.Substring(hash + 1);
                }
                return name.Split(',')[0].Trim();
            }
        }

        public override string ToString() => Source;

        public override bool Equals(object obj) => obj is FontFamily other && string.Equals(Source, other.Source, StringComparison.OrdinalIgnoreCase);

        public override int GetHashCode() => (Source ?? "").ToLowerInvariant().GetHashCode();

        public static bool operator ==(FontFamily a, FontFamily b) => Equals(a, b);

        public static bool operator !=(FontFamily a, FontFamily b) => !Equals(a, b);
    }
}
