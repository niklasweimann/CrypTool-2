namespace System.Windows.Markup
{
    /// <summary>Names the property that receives the child nodes of an element in XAML.</summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
    public sealed class ContentPropertyAttribute : Attribute
    {
        public ContentPropertyAttribute() { }

        public ContentPropertyAttribute(string name) => Name = name;

        public string Name { get; }
    }

    /// <summary>Maps an XML namespace to a CLR namespace of the assembly (so XAML can use its types without prefix).</summary>
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
    public sealed class XmlnsDefinitionAttribute : Attribute
    {
        public XmlnsDefinitionAttribute(string xmlNamespace, string clrNamespace)
        {
            XmlNamespace = xmlNamespace;
            ClrNamespace = clrNamespace;
        }

        public string XmlNamespace { get; }
        public string ClrNamespace { get; }
        public string AssemblyName { get; set; }
    }

    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
    public sealed class XmlnsPrefixAttribute : Attribute
    {
        public XmlnsPrefixAttribute(string xmlNamespace, string prefix) { }
    }

    [AttributeUsage(AttributeTargets.Class)]
    public sealed class MarkupExtensionReturnTypeAttribute : Attribute
    {
        public MarkupExtensionReturnTypeAttribute() { }

        public MarkupExtensionReturnTypeAttribute(Type returnType) => ReturnType = returnType;

        public Type ReturnType { get; }
    }

    [AttributeUsage(AttributeTargets.Property)]
    public sealed class ConstructorArgumentAttribute : Attribute
    {
        public ConstructorArgumentAttribute(string argumentName) => ArgumentName = argumentName;

        public string ArgumentName { get; }
    }

    public interface IAddChild
    {
        void AddChild(object value);

        void AddText(string text);
    }

    /// <summary>A <c>{Name ...}</c> expression in a XAML attribute that computes the attribute value.</summary>
    public abstract class MarkupExtension
    {
        public abstract object ProvideValue(IServiceProvider serviceProvider);
    }

    public interface IProvideValueTarget
    {
        object TargetObject { get; }

        object TargetProperty { get; }
    }

    public interface IXamlTypeResolver
    {
        Type Resolve(string qualifiedTypeName);
    }
}

namespace System.Xaml
{
    /// <summary>Gives a markup extension the root object of the XAML file that is being loaded.</summary>
    public interface IRootObjectProvider
    {
        object RootObject { get; }
    }
}
