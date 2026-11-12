using System.Collections;
using System.Collections.Generic;
using System.Reflection;

namespace System.Windows
{
    /// <summary><c>{StaticResource key}</c>: the resource with that key from the nearest enclosing resource dictionary.</summary>
    public class StaticResourceExtension : Markup.MarkupExtension
    {
        public StaticResourceExtension() { }

        public StaticResourceExtension(object resourceKey) => ResourceKey = resourceKey;

        public object ResourceKey { get; set; }

        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            if (serviceProvider is Markup.XamlContext context && context.TryFindResource(ResourceKey, out object value))
            {
                return value;
            }
            if (ResourceKey is SystemResourceKey systemKey)
            {
                return systemKey.Value;     // a system colour or brush that the XAML did not redefine
            }
            Markup.XamlDiagnostics.Report("Resource not found", Convert.ToString(ResourceKey));
            return null;
        }
    }

    /// <summary><c>{DynamicResource key}</c>: resolved once when the XAML is loaded, like a static resource.</summary>
    public class DynamicResourceExtension : StaticResourceExtension
    {
        public DynamicResourceExtension() { }

        public DynamicResourceExtension(object resourceKey) : base(resourceKey) { }
    }

    /// <summary><c>{TemplateBinding X}</c>: control templates are not instantiated, so this never yields a value.</summary>
    public class TemplateBindingExtension : Markup.MarkupExtension
    {
        public TemplateBindingExtension() { }

        public TemplateBindingExtension(object property) { }

        public override object ProvideValue(IServiceProvider serviceProvider) => DependencyProperty.UnsetValue;
    }
}

namespace System.Windows.Markup
{
    /// <summary><c>{x:Static prefix:Type.Member}</c>: the value of a static field, property or enum member.</summary>
    public class StaticExtension : MarkupExtension
    {
        public StaticExtension() { }

        public StaticExtension(string member) => Member = member;

        public string Member { get; set; }

        public Type MemberType { get; set; }

        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            string member = Member ?? "";
            Type type = MemberType;
            int dot = member.LastIndexOf('.');
            if (type == null && dot > 0)
            {
                type = (serviceProvider as IXamlTypeResolver)?.Resolve(member.Substring(0, dot));
                member = member.Substring(dot + 1);
            }
            if (type == null)
            {
                XamlDiagnostics.Report("x:Static type not found", Member);
                return null;
            }

            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.FlattenHierarchy;
            FieldInfo field = type.GetField(member, flags);
            if (field != null)
            {
                return field.GetValue(null);
            }
            PropertyInfo property = type.GetProperty(member, flags);
            if (property != null)
            {
                return property.GetValue(null);
            }
            XamlDiagnostics.Report("x:Static member not found", Member);
            return null;
        }
    }

    public class NullExtension : MarkupExtension
    {
        public override object ProvideValue(IServiceProvider serviceProvider) => null;
    }

    public class TypeExtension : MarkupExtension
    {
        public TypeExtension() { }

        public TypeExtension(string typeName) => TypeName = typeName;

        public TypeExtension(Type type) => Type = type;

        public string TypeName { get; set; }

        public Type Type { get; set; }

        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            return Type ?? (serviceProvider as IXamlTypeResolver)?.Resolve(TypeName);
        }
    }

    [ContentProperty(nameof(Items))]
    public class ArrayExtension : MarkupExtension
    {
        public Type Type { get; set; }

        public IList Items { get; } = new List<object>();

        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            Array array = Array.CreateInstance(Type ?? typeof(object), Items.Count);
            Items.CopyTo(array, 0);
            return array;
        }
    }
}
