// Behaviour-carrying parts of the WPF API that plugin code (outside of the XAML views) uses: dependency properties,
// converters, validation rules and the basic geometry structs. Everything that only draws is stubbed elsewhere.
using System;
using System.Collections.Generic;
using System.Globalization;

namespace System.Windows
{
    public struct Point : IEquatable<Point>
    {
        public double X, Y;
        public Point(double x, double y) { X = x; Y = y; }
        public void Offset(double offsetX, double offsetY) { X += offsetX; Y += offsetY; }
        public static Point operator +(Point p, Vector v) => new Point(p.X + v.X, p.Y + v.Y);
        public static Vector operator -(Point a, Point b) => new Vector(a.X - b.X, a.Y - b.Y);
        public static bool operator ==(Point a, Point b) => a.X == b.X && a.Y == b.Y;
        public static bool operator !=(Point a, Point b) => !(a == b);
        public bool Equals(Point o) => this == o;
        public override bool Equals(object o) => o is Point p && this == p;
        public override int GetHashCode() => X.GetHashCode() ^ Y.GetHashCode();
        public override string ToString() => X.ToString(CultureInfo.CurrentCulture) + "," + Y.ToString(CultureInfo.CurrentCulture);
    }
    public struct Vector
    {
        public double X, Y;
        public Vector(double x, double y) { X = x; Y = y; }
        public double Length => Math.Sqrt(X * X + Y * Y);
        public static Vector operator +(Vector a, Vector b) => new Vector(a.X + b.X, a.Y + b.Y);
        public static Vector operator -(Vector a, Vector b) => new Vector(a.X - b.X, a.Y - b.Y);
        public static Vector operator *(Vector a, double d) => new Vector(a.X * d, a.Y * d);
    }
    public struct Size
    {
        public double Width, Height;
        public Size(double w, double h) { Width = w; Height = h; }
        public static Size Empty => new Size(double.NegativeInfinity, double.NegativeInfinity);
        public bool IsEmpty => Width < 0;
    }
    public struct Rect
    {
        public double X, Y, Width, Height;
        public Rect(double x, double y, double w, double h) { X = x; Y = y; Width = w; Height = h; }
        public Rect(Point p, Size s) { X = p.X; Y = p.Y; Width = s.Width; Height = s.Height; }
        public Rect(Size s) { X = 0; Y = 0; Width = s.Width; Height = s.Height; }
        public Rect(Point a, Point b) { X = Math.Min(a.X, b.X); Y = Math.Min(a.Y, b.Y); Width = Math.Abs(a.X - b.X); Height = Math.Abs(a.Y - b.Y); }
        public double Left => X; public double Top => Y; public double Right => X + Width; public double Bottom => Y + Height;
        public Point Location => new Point(X, Y); public Size Size => new Size(Width, Height);
        public bool Contains(Point p) => p.X >= X && p.X <= Right && p.Y >= Y && p.Y <= Bottom;
        public bool IntersectsWith(Rect r) => r.Left <= Right && r.Right >= Left && r.Top <= Bottom && r.Bottom >= Top;
        public static Rect Empty => new Rect(double.PositiveInfinity, double.PositiveInfinity, double.NegativeInfinity, double.NegativeInfinity);
    }

    public delegate void PropertyChangedCallback(DependencyObject d, DependencyPropertyChangedEventArgs e);
    public delegate object CoerceValueCallback(DependencyObject d, object baseValue);
    public delegate bool ValidateValueCallback(object value);

    public readonly struct DependencyPropertyChangedEventArgs
    {
        public DependencyPropertyChangedEventArgs(DependencyProperty property, object oldValue, object newValue) { Property = property; OldValue = oldValue; NewValue = newValue; }
        public DependencyProperty Property { get; }
        public object OldValue { get; }
        public object NewValue { get; }
    }

    public class PropertyMetadata
    {
        public PropertyMetadata() { }
        public PropertyMetadata(object defaultValue) { DefaultValue = defaultValue; }
        public PropertyMetadata(PropertyChangedCallback cb) { PropertyChangedCallback = cb; }
        public PropertyMetadata(object defaultValue, PropertyChangedCallback cb) { DefaultValue = defaultValue; PropertyChangedCallback = cb; }
        public PropertyMetadata(object defaultValue, PropertyChangedCallback cb, CoerceValueCallback coerce) { DefaultValue = defaultValue; PropertyChangedCallback = cb; CoerceValueCallback = coerce; }
        public object DefaultValue { get; set; }
        public PropertyChangedCallback PropertyChangedCallback { get; set; }
        public CoerceValueCallback CoerceValueCallback { get; set; }
    }

    [Flags]
    public enum FrameworkPropertyMetadataOptions { None = 0, AffectsMeasure = 1, AffectsArrange = 2, AffectsParentMeasure = 4, AffectsParentArrange = 8, AffectsRender = 16, Inherits = 32, OverridesInheritanceBehavior = 64, NotDataBindable = 128, BindsTwoWayByDefault = 256, Journal = 1024, SubPropertiesDoNotAffectRender = 2048 }

    public class FrameworkPropertyMetadata : PropertyMetadata
    {
        public FrameworkPropertyMetadata() { }
        public FrameworkPropertyMetadata(object defaultValue) : base(defaultValue) { }
        public FrameworkPropertyMetadata(PropertyChangedCallback cb) : base(cb) { }
        public FrameworkPropertyMetadata(object defaultValue, PropertyChangedCallback cb) : base(defaultValue, cb) { }
        public FrameworkPropertyMetadata(object defaultValue, FrameworkPropertyMetadataOptions flags) : base(defaultValue) { Options = flags; }
        public FrameworkPropertyMetadata(object defaultValue, FrameworkPropertyMetadataOptions flags, PropertyChangedCallback cb) : base(defaultValue, cb) { Options = flags; }
        public FrameworkPropertyMetadata(object defaultValue, FrameworkPropertyMetadataOptions flags, PropertyChangedCallback cb, CoerceValueCallback coerce) : base(defaultValue, cb, coerce) { Options = flags; }
        public FrameworkPropertyMetadataOptions Options { get; set; }
    }

    public sealed class DependencyProperty
    {
        public string Name { get; private set; }
        public Type PropertyType { get; private set; }
        public Type OwnerType { get; private set; }
        public PropertyMetadata Metadata { get; private set; }
        public bool ReadOnly { get; private set; }
        public static readonly object UnsetValue = new object();
        DependencyProperty() { }
        static DependencyProperty Make(string n, Type t, Type o, PropertyMetadata m, bool ro) =>
            new DependencyProperty { Name = n, PropertyType = t, OwnerType = o, Metadata = m ?? new PropertyMetadata(t.IsValueType ? Activator.CreateInstance(t) : null), ReadOnly = ro };
        public static DependencyProperty Register(string name, Type type, Type owner) => Make(name, type, owner, null, false);
        public static DependencyProperty Register(string name, Type type, Type owner, PropertyMetadata m) => Make(name, type, owner, m, false);
        public static DependencyProperty Register(string name, Type type, Type owner, PropertyMetadata m, ValidateValueCallback v) => Make(name, type, owner, m, false);
        public static DependencyPropertyKey RegisterReadOnly(string name, Type type, Type owner, PropertyMetadata m) => new DependencyPropertyKey(Make(name, type, owner, m, true));
        public static DependencyProperty RegisterAttached(string name, Type type, Type owner) => Make(name, type, owner, null, false);
        public static DependencyProperty RegisterAttached(string name, Type type, Type owner, PropertyMetadata m) => Make(name, type, owner, m, false);
        public static DependencyProperty RegisterAttached(string name, Type type, Type owner, PropertyMetadata m, ValidateValueCallback v) => Make(name, type, owner, m, false);
        public static DependencyPropertyKey RegisterAttachedReadOnly(string name, Type type, Type owner, PropertyMetadata m) => new DependencyPropertyKey(Make(name, type, owner, m, true));
        public DependencyProperty AddOwner(Type owner) => this;
        public DependencyProperty AddOwner(Type owner, PropertyMetadata m) => this;
        public void OverrideMetadata(Type forType, PropertyMetadata m) { Metadata = m; }
        public override string ToString() => Name;
    }

    public sealed class DependencyPropertyKey
    {
        internal DependencyPropertyKey(DependencyProperty dp) { DependencyProperty = dp; }
        public DependencyProperty DependencyProperty { get; }
        public void OverrideMetadata(Type forType, PropertyMetadata m) => DependencyProperty.OverrideMetadata(forType, m);
    }

    /// <summary>What every call on a browser-less UI stand-in returns: accepts any member access, call, operator or conversion.</summary>
    public sealed class StubValue : System.Dynamic.DynamicObject
    {
        public static readonly StubValue Instance = new StubValue();
        readonly DependencyObject owner; readonly string path;
        StubValue() { }
        /// <summary>Named element of a UI stand-in: property writes / calls on it are recorded as "name.Property=" / "name.Method()" so the web view can apply them to the drawn XAML.</summary>
        public StubValue(DependencyObject owner, string path) { this.owner = owner; this.path = path; }
        public override bool TryGetMember(System.Dynamic.GetMemberBinder b, out object r)
        {
            if (owner == null || path.Split('.').Length >= 4) { r = Instance; return true; }
            var p = path + "." + b.Name;
            r = owner.__Props.TryGetValue(p, out var stored) ? stored : new StubValue(owner, p); return true; // reads return what the plugin wrote before
        }
        public override bool TrySetMember(System.Dynamic.SetMemberBinder b, object v) { if (owner != null) { owner.__Props[path + "." + b.Name] = v; owner.__Record(path + "." + b.Name + "=", new[] { v }); } return true; }
        public override bool TryInvokeMember(System.Dynamic.InvokeMemberBinder b, object[] a, out object r) { owner?.__Record(path + "." + b.Name + "()", a); r = Instance; return true; }
        public override bool TryInvoke(System.Dynamic.InvokeBinder b, object[] a, out object r) { r = Instance; return true; }
        public override bool TryGetIndex(System.Dynamic.GetIndexBinder b, object[] i, out object r) { r = Instance; return true; }
        public override bool TrySetIndex(System.Dynamic.SetIndexBinder b, object[] i, object v) => true;
        // comparisons and logical operators must yield a bool for the C# binder, arithmetic keeps the stand-in
        static bool IsLogical(System.Linq.Expressions.ExpressionType t) => t is System.Linq.Expressions.ExpressionType.Equal or System.Linq.Expressions.ExpressionType.NotEqual or System.Linq.Expressions.ExpressionType.LessThan or System.Linq.Expressions.ExpressionType.LessThanOrEqual or System.Linq.Expressions.ExpressionType.GreaterThan or System.Linq.Expressions.ExpressionType.GreaterThanOrEqual or System.Linq.Expressions.ExpressionType.Not or System.Linq.Expressions.ExpressionType.IsTrue or System.Linq.Expressions.ExpressionType.IsFalse;
        public override bool TryBinaryOperation(System.Dynamic.BinaryOperationBinder b, object arg, out object r) { r = IsLogical(b.Operation) ? (object)false : Instance; return true; }
        public override bool TryUnaryOperation(System.Dynamic.UnaryOperationBinder b, out object r) { r = IsLogical(b.Operation) ? (object)false : Instance; return true; }
        public override bool TryConvert(System.Dynamic.ConvertBinder b, out object r) { r = b.Type.IsValueType ? Activator.CreateInstance(b.Type) : null; return true; }
        public override string ToString() => "";
    }

    public class StubEventArgs : RoutedEventArgs
    {
        readonly Dictionary<string, object> __bag = new Dictionary<string, object>();
        public System.Action<string, object[]> BridgeSink { get; set; }
        public void __Record(string name, object[] args) { BridgeSink?.Invoke(name, args); }
        public object __Get(string n) => __bag.TryGetValue(n, out var v) ? v : StubValue.Instance;
        public void __Set(string n, object v) { __bag[n] = v; __Record(n + "=", new[] { v }); }
    }

    /// <summary>Members used by the generated presentation stand-ins (stubs.g.cs); obsolete once all views are real.</summary>
    public partial class DependencyObject
    {
        public readonly Dictionary<string, object> __Props = new Dictionary<string, object>();
        readonly Dictionary<string, object> __bag = new Dictionary<string, object>();
        public object __Get(string n) => __bag.TryGetValue(n, out var v) ? v : new StubValue(this, n);
        public void __Set(string n, object v) { __bag[n] = v; __Record(n + "=", new[] { v }); }
        /// <summary>Receives every call/property write that plugin code makes on a generated UI stand-in (see tools-prune-plugins.py).</summary>
        public System.Action<string, object[]> BridgeSink { get; set; }
        public void __Record(string name, object[] args) { BridgeSink?.Invoke(name, args); }
    }

}

namespace System.Windows.Data
{
    public interface IValueConverter
    {
        object Convert(object value, Type targetType, object parameter, CultureInfo culture);
        object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture);
    }
    public interface IMultiValueConverter
    {
        object Convert(object[] values, Type targetType, object parameter, CultureInfo culture);
        object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture);
    }
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class ValueConversionAttribute : Attribute
    {
        public ValueConversionAttribute(Type sourceType, Type targetType) { SourceType = sourceType; TargetType = targetType; }
        public ValueConversionAttribute(Type sourceType, Type targetType, Type parameterType) : this(sourceType, targetType) { ParameterType = parameterType; }
        public Type SourceType { get; } public Type TargetType { get; } public Type ParameterType { get; }
    }
    public enum BindingMode { TwoWay, OneWay, OneTime, OneWayToSource, Default }
}

namespace System.Windows.Controls
{
    public class ValidationResult
    {
        public ValidationResult(bool isValid, object errorContent) { IsValid = isValid; ErrorContent = errorContent; }
        public bool IsValid { get; }
        public object ErrorContent { get; }
        public static readonly ValidationResult ValidResult = new ValidationResult(true, null);
    }
    public abstract class ValidationRule
    {
        public abstract ValidationResult Validate(object value, CultureInfo cultureInfo);
        public virtual ValidationResult Validate(object value, CultureInfo cultureInfo, object owner) => Validate(value, cultureInfo);
        public bool ValidatesOnTargetUpdated { get; set; }
    }
}

// namespaces that exist in WPF; plugin code imports them even if it only needs a few types from them
namespace System.Windows.Media.Media3D { public abstract class Visual3D : System.Windows.DependencyObject { } }

// .NET Remoting leftovers used by a few plugins (synchronization contexts); there is only one thread here, so they do nothing.
namespace System.Runtime.Remoting.Contexts
{
    [AttributeUsage(AttributeTargets.Class)]
    public class SynchronizationAttribute : Attribute
    {
        public SynchronizationAttribute() { }
        public SynchronizationAttribute(bool reEntrant) { }
        public SynchronizationAttribute(int flag) { }
        public SynchronizationAttribute(int flag, bool reEntrant) { }
    }
}

namespace System.Windows { /// <summary>Event member of a UI stand-in: subscriptions are accepted and ignored.</summary>
    public sealed class StubEvent { public static StubEvent operator +(StubEvent e, System.Delegate d) => e; public static StubEvent operator -(StubEvent e, System.Delegate d) => e; } }
