using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Windows.Data;
using System.Windows.Documents;
using System.Xml.Linq;

namespace System.Windows.Markup
{
    /// <summary>Creates objects from XAML elements: types, properties, attached properties, events, content and resources.</summary>
    internal sealed class XamlObjectBuilder
    {
        private const BindingFlags InstanceMembers = BindingFlags.Public | BindingFlags.Instance;
        private const BindingFlags AnyInstance = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

        private static readonly Regex Whitespace = new Regex(@"\s+", RegexOptions.Compiled);

        private readonly XamlContext _context;

        public XamlObjectBuilder(XamlContext context) => _context = context;

        // ---- objects ------------------------------------------------------------------------------------------

        /// <summary>Creates the object for an element; null if its type is unknown or cannot be created.</summary>
        public object Create(XElement element)
        {
            Type type = XamlTypeResolver.Resolve(element.Name.NamespaceName, element.Name.LocalName);
            if (type == null)
            {
                XamlDiagnostics.Report("Unknown type", element.Name.LocalName);
                return null;
            }

            if (type == typeof(string) || type.IsPrimitive || type.IsEnum)
            {
                return XamlValueConverter.TryConvert(element.Value, type, out object simple) ? simple : null;
            }
            if (typeof(FrameworkTemplate).IsAssignableFrom(type))
            {
                return CreateTemplate(type, element);
            }

            object instance;
            try
            {
                instance = Activator.CreateInstance(type, nonPublic: true);
            }
            catch (Exception)
            {
                XamlDiagnostics.Report("Cannot create", type.Name);
                return null;
            }

            Populate(instance, element);

            if (instance is MarkupExtension extension)
            {
                XElement previous = _context.CurrentElement;
                _context.CurrentElement = element;
                object provided = extension.ProvideValue(_context);
                _context.CurrentElement = previous;
                return provided;
            }
            return instance;
        }

        /// <summary>Applies attributes, property elements and content of <paramref name="element"/> to <paramref name="instance"/>.</summary>
        public void Populate(object instance, XElement element)
        {
            bool isScope = instance is FrameworkElement || instance is Style || instance is ResourceDictionary;
            if (isScope)
            {
                _context.Scopes.Add(instance);
            }
            XElement previousElement = _context.CurrentElement;
            _context.CurrentElement = element;

            foreach (XAttribute attribute in element.Attributes())
            {
                ApplyAttribute(instance, element, attribute);
                _context.CurrentElement = element;
            }

            var content = new List<object>();
            foreach (XNode node in element.Nodes())
            {
                if (node is XElement child)
                {
                    if (child.Name.LocalName.Contains('.'))
                    {
                        ApplyPropertyElement(instance, child);
                        _context.CurrentElement = element;
                    }
                    else
                    {
                        content.Add(child);
                    }
                }
                else if (node is XText text)
                {
                    content.Add(text.Value);
                }
            }
            ApplyContent(instance, NormalizeText(content));
            _context.CurrentElement = element;

            if (instance is FrameworkElement styled && styled.Style == null
                && _context.TryFindResource(styled.GetType(), out object implicitStyle) && implicitStyle is Style style)
            {
                styled.Style = style;
            }

            _context.CurrentElement = previousElement;
            if (isScope)
            {
                _context.Scopes.RemoveAt(_context.Scopes.Count - 1);
            }
        }

        private object CreateTemplate(Type type, XElement element)
        {
            var template = (FrameworkTemplate)Activator.CreateInstance(type);
            foreach (XAttribute attribute in element.Attributes())
            {
                if (attribute.Name.NamespaceName.Length == 0 && (attribute.Name.LocalName == "DataType" || attribute.Name.LocalName == "TargetType"))
                {
                    SetMember(template, attribute.Name.LocalName, attribute.Value);
                }
            }

            XElement content = element.Elements().FirstOrDefault(e => !e.Name.LocalName.Contains('.'));
            if (content != null)
            {
                XamlContext captured = _context.ForkForTemplate();
                template.ContentFactory = () => new XamlObjectBuilder(captured.ForkForTemplate()).Create(content);
            }
            return template;
        }

        // ---- attributes ---------------------------------------------------------------------------------------

        private void ApplyAttribute(object instance, XElement element, XAttribute attribute)
        {
            if (attribute.IsNamespaceDeclaration)
            {
                return;
            }

            string xmlNamespace = attribute.Name.NamespaceName;
            string name = attribute.Name.LocalName;
            if (xmlNamespace == XamlTypeResolver.XamlLanguageNamespace)
            {
                if (name == "Name")
                {
                    RegisterName(attribute.Value, instance);
                }
                return;     // x:Class, x:Key, x:FieldModifier, ... are handled elsewhere or irrelevant
            }

            bool isClrNamespace = xmlNamespace.StartsWith("clr-namespace:", StringComparison.Ordinal);
            if (xmlNamespace.Length > 0 && !isClrNamespace && !xmlNamespace.Contains("/xaml/presentation"))
            {
                return;     // designer (d:), markup compatibility (mc:), xml: attributes
            }

            int dot = name.IndexOf('.');
            if (dot >= 0)
            {
                string ownerNamespace = xmlNamespace.Length > 0 ? xmlNamespace : element.GetDefaultNamespace().NamespaceName;
                Type ownerType = XamlTypeResolver.Resolve(ownerNamespace, name.Substring(0, dot));
                ApplyAttached(instance, ownerType, name.Substring(dot + 1), attribute.Value, name);
                return;
            }

            if (name == "Name")
            {
                RegisterName(attribute.Value, instance);
            }
            SetMember(instance, name, attribute.Value);
        }

        /// <summary>Sets a property or attaches an event handler by name from attribute text.</summary>
        private void SetMember(object instance, string name, string text)
        {
            Type type = instance.GetType();

            if (instance is Setter setter && name == "Property")
            {
                setter.PropertyName = text;
                setter.Property = DependencyPropertyLookup.Find(_context.CurrentStyleTargetType, text);
                return;
            }

            EventInfo eventInfo = type.GetEvent(name, InstanceMembers);
            if (eventInfo != null)
            {
                AttachHandler(instance, eventInfo, text);
                return;
            }

            PropertyInfo property = FindProperty(type, name);
            if (property == null)
            {
                XamlDiagnostics.Report("Unknown member", type.Name + "." + name);
                return;
            }

            // a setter value is converted when the style is applied, unless its property is already known
            if (instance is Setter valueSetter && name == "Value" && !MarkupExpression.IsExpression(text))
            {
                Type valueType = valueSetter.Property?.PropertyType;
                valueSetter.Value = valueType != null && XamlValueConverter.TryConvert(text, valueType, out object converted) ? converted : text;
                return;
            }

            AssignText(instance, property, text);
        }

        private void AssignText(object instance, PropertyInfo property, string text)
        {
            if (MarkupExpression.IsExpression(text))
            {
                AssignValue(instance, property, Evaluate(text, instance, property));
                return;
            }
            if (text.StartsWith("{}", StringComparison.Ordinal))
            {
                text = text.Substring(2);
            }

            object value;
            if (property.PropertyType == typeof(Type))
            {
                value = _context.Resolve(text);
            }
            else if (!XamlValueConverter.TryConvert(text, property.PropertyType, out value))
            {
                XamlDiagnostics.Report("Cannot convert", instance.GetType().Name + "." + property.Name + " (" + property.PropertyType.Name + ")");
                return;
            }
            SetProperty(instance, property, value);
        }

        /// <summary>Assigns an object value; bindings are attached to the dependency property instead of being stored.</summary>
        private void AssignValue(object instance, PropertyInfo property, object value)
        {
            if (ReferenceEquals(value, DependencyProperty.UnsetValue))
            {
                return;
            }

            if (value is BindingBase binding && !typeof(BindingBase).IsAssignableFrom(property.PropertyType) && property.PropertyType != typeof(object))
            {
                DependencyProperty dp = DependencyPropertyLookup.Find(instance.GetType(), property.Name);
                if (dp != null && instance is DependencyObject target)
                {
                    target.SetValue(dp, binding.CreateExpression());
                }
                else
                {
                    XamlDiagnostics.Report("Binding on a plain property", instance.GetType().Name + "." + property.Name);
                }
                return;
            }

            if (value is string text && property.PropertyType != typeof(string) && property.PropertyType != typeof(object))
            {
                if (!XamlValueConverter.TryConvert(text, property.PropertyType, out value))
                {
                    XamlDiagnostics.Report("Cannot convert", instance.GetType().Name + "." + property.Name + " (" + property.PropertyType.Name + ")");
                    return;
                }
            }
            SetProperty(instance, property, value);
        }

        private static void SetProperty(object instance, PropertyInfo property, object value)
        {
            try
            {
                if (property.CanWrite)
                {
                    property.SetValue(instance, ValueCoercion.ToPropertyType(value, property.PropertyType));
                }
                else if (property.GetValue(instance) is IList list && value is IEnumerable items && value is not string)
                {
                    foreach (object item in items)
                    {
                        list.Add(item);
                    }
                }
                else
                {
                    XamlDiagnostics.Report("Read-only member", instance.GetType().Name + "." + property.Name);
                }
            }
            catch (Exception)
            {
                XamlDiagnostics.Report("Cannot assign", instance.GetType().Name + "." + property.Name);
            }
        }

        private void AttachHandler(object instance, EventInfo eventInfo, string methodName)
        {
            object root = _context.RootObject;
            MethodInfo method = null;
            for (Type type = root?.GetType(); type != null && method == null; type = type.BaseType)
            {
                method = type.GetMethods(AnyInstance | BindingFlags.DeclaredOnly).FirstOrDefault(m => m.Name == methodName);
            }

            Delegate handler = method == null ? null : Delegate.CreateDelegate(eventInfo.EventHandlerType, root, method, throwOnBindFailure: false);
            if (handler == null)
            {
                XamlDiagnostics.Report("Event handler not found", methodName);
                return;
            }
            eventInfo.AddEventHandler(instance, handler);
        }

        /// <summary>An attached property ("Grid.Row") or an own property written with its owner ("TextBlock.FontSize").</summary>
        private void ApplyAttached(object instance, Type ownerType, string name, string text, string displayName)
        {
            if (ownerType == null)
            {
                XamlDiagnostics.Report("Unknown attached property", displayName);
                return;
            }

            MethodInfo setter = ownerType.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
                .FirstOrDefault(m => m.Name == "Set" + name && m.GetParameters().Length == 2 && m.GetParameters()[0].ParameterType.IsInstanceOfType(instance));
            DependencyProperty dp = DependencyPropertyLookup.Find(ownerType, name);
            Type valueType = setter?.GetParameters()[1].ParameterType ?? dp?.PropertyType;
            if (valueType == null)
            {
                XamlDiagnostics.Report(ownerType.GetEvent(name) != null ? "Attached event" : "Unknown attached property", displayName);
                return;
            }

            object value;
            if (MarkupExpression.IsExpression(text))
            {
                value = Evaluate(text, instance, dp);
                if (value is BindingBase binding)
                {
                    value = binding.CreateExpression();
                }
            }
            else if (!XamlValueConverter.TryConvert(text, valueType, out value))
            {
                XamlDiagnostics.Report("Cannot convert", displayName + " (" + valueType.Name + ")");
                return;
            }
            SetAttachedValue(instance, setter, dp, value, displayName);
        }

        private static void SetAttachedValue(object instance, MethodInfo setter, DependencyProperty dp, object value, string displayName)
        {
            try
            {
                if (value is IDeferredValue || setter == null)
                {
                    (instance as DependencyObject)?.SetValue(dp, value);
                }
                else
                {
                    setter.Invoke(null, new[] { instance, ValueCoercion.ToPropertyType(value, setter.GetParameters()[1].ParameterType) });
                }
            }
            catch (Exception)
            {
                XamlDiagnostics.Report("Cannot assign", displayName);
            }
        }

        // ---- property elements ----------------------------------------------------------------------------------

        private void ApplyPropertyElement(object instance, XElement propertyElement)
        {
            string qualified = propertyElement.Name.LocalName;
            int dot = qualified.IndexOf('.');
            string name = qualified.Substring(dot + 1);
            Type ownerType = XamlTypeResolver.Resolve(propertyElement.Name.NamespaceName, qualified.Substring(0, dot));
            List<XElement> children = propertyElement.Elements().ToList();
            PropertyInfo property = ownerType != null && !ownerType.IsInstanceOfType(instance) ? null : FindProperty(instance.GetType(), name);

            if (property == null)
            {
                ApplyAttachedPropertyElement(instance, ownerType, name, propertyElement, children);
                return;
            }

            if (children.Count == 0)
            {
                string text = propertyElement.Value.Trim();
                if (text.Length > 0)
                {
                    AssignText(instance, property, text);
                }
                return;
            }

            if (children.Count == 1 && property.CanWrite && IsValueOf(property, children[0]))
            {
                AssignValue(instance, property, Create(children[0]));
                return;
            }

            object collection = property.CanRead ? property.GetValue(instance) : null;
            if (collection == null && property.CanWrite)
            {
                try
                {
                    collection = Activator.CreateInstance(property.PropertyType);
                    property.SetValue(instance, collection);
                }
                catch (Exception)
                {
                    collection = null;
                }
            }
            if (collection == null)
            {
                XamlDiagnostics.Report("Cannot fill", instance.GetType().Name + "." + name);
                return;
            }
            foreach (XElement child in children)
            {
                AddToCollection(collection, child);
            }
        }

        /// <summary>True when the single child element is the property value itself rather than an item of its collection.</summary>
        private static bool IsValueOf(PropertyInfo property, XElement child)
        {
            Type childType = XamlTypeResolver.Resolve(child.Name.NamespaceName, child.Name.LocalName);
            if (childType == null)
            {
                return false;
            }
            return property.PropertyType.IsAssignableFrom(childType)
                || typeof(MarkupExtension).IsAssignableFrom(childType)
                || typeof(BindingBase).IsAssignableFrom(childType);
        }

        private void ApplyAttachedPropertyElement(object instance, Type ownerType, string name, XElement propertyElement, List<XElement> children)
        {
            string displayName = propertyElement.Name.LocalName;
            if (ownerType == null)
            {
                XamlDiagnostics.Report("Unknown member", displayName);
                return;
            }
            if (children.Count == 0)
            {
                ApplyAttached(instance, ownerType, name, propertyElement.Value.Trim(), displayName);
                return;
            }

            MethodInfo setter = ownerType.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
                .FirstOrDefault(m => m.Name == "Set" + name && m.GetParameters().Length == 2 && m.GetParameters()[0].ParameterType.IsInstanceOfType(instance));
            DependencyProperty dp = DependencyPropertyLookup.Find(ownerType, name);
            if (setter == null && dp == null)
            {
                XamlDiagnostics.Report("Unknown member", displayName);
                return;
            }
            object value = Create(children[0]);
            SetAttachedValue(instance, setter, dp, value is BindingBase binding ? binding.CreateExpression() : value, displayName);
        }

        private void AddToCollection(object collection, XElement child)
        {
            if (collection is ResourceDictionary resources)
            {
                object resource = Create(child);
                object key = ResourceKeyOf(child, resource);
                if (resource != null && key != null)
                {
                    resources.Add(key, resource);
                }
                else if (resource is ResourceDictionary merged)
                {
                    resources.MergedDictionaries.Add(merged);
                }
                return;
            }

            object item = Create(child);
            if (item == null)
            {
                return;
            }
            try
            {
                if (collection is IList list)
                {
                    list.Add(item);
                }
                else
                {
                    collection.GetType().GetMethod("Add", new[] { item.GetType() })?.Invoke(collection, new[] { item });
                }
            }
            catch (Exception)
            {
                XamlDiagnostics.Report("Cannot add", item.GetType().Name + " to " + collection.GetType().Name);
            }
        }

        private object ResourceKeyOf(XElement element, object resource)
        {
            string key = element.Attribute(XName.Get("Key", XamlTypeResolver.XamlLanguageNamespace))?.Value;
            if (key != null)
            {
                _context.CurrentElement = element;
                return MarkupExpression.IsExpression(key) ? Evaluate(key, null, null) : key;
            }
            if (resource is Style style && style.TargetType != null)
            {
                return style.TargetType;
            }
            if (resource is DataTemplate template && template.DataType != null)
            {
                return template.DataType;
            }
            return element.Attribute(XName.Get("Name", XamlTypeResolver.XamlLanguageNamespace))?.Value;
        }

        // ---- content ------------------------------------------------------------------------------------------

        private void ApplyContent(object instance, List<object> nodes)
        {
            if (nodes.Count == 0)
            {
                return;
            }

            if (instance is ResourceDictionary)
            {
                foreach (XElement child in nodes.OfType<XElement>())
                {
                    AddToCollection(instance, child);
                }
                return;
            }

            Type type = instance.GetType();
            string contentName = type.GetCustomAttribute<ContentPropertyAttribute>(inherit: true)?.Name;
            PropertyInfo property = contentName == null ? null : FindProperty(type, contentName);
            if (property == null)
            {
                ApplyContentWithoutProperty(instance, nodes);
                return;
            }

            object current = property.CanRead ? property.GetValue(instance) : null;
            if (current is IList list && current is not string && property.PropertyType != typeof(object))
            {
                foreach (object node in nodes)
                {
                    AddContentItem(list, node);
                }
                return;
            }

            List<XElement> elements = nodes.OfType<XElement>().ToList();
            if (elements.Count == 0)
            {
                AssignText(instance, property, string.Concat(nodes.OfType<string>()));
                return;
            }
            if (elements.Count > 1 || nodes.Count > elements.Count)
            {
                XamlDiagnostics.Report("More than one content", type.Name);
            }
            AssignValue(instance, property, Create(elements[0]));
        }

        private void AddContentItem(IList list, object node)
        {
            if (node is string text)
            {
                if (list is InlineCollection inlines)
                {
                    inlines.Add(text);
                }
                else if (list is Controls.ItemCollection || list.GetType() == typeof(List<object>))
                {
                    list.Add(text);
                }
                return;
            }

            object item = Create((XElement)node);
            if (item == null)
            {
                return;
            }
            try
            {
                if (item is UIElement element && list is InlineCollection inlineList)
                {
                    inlineList.Add(element);
                }
                else
                {
                    list.Add(item);
                }
            }
            catch (Exception)
            {
                XamlDiagnostics.Report("Cannot add", item.GetType().Name + " to " + list.GetType().Name);
            }
        }

        private void ApplyContentWithoutProperty(object instance, List<object> nodes)
        {
            if (instance is IAddChild addChild)
            {
                foreach (object node in nodes)
                {
                    if (node is string text)
                    {
                        addChild.AddText(text);
                    }
                    else
                    {
                        addChild.AddChild(Create((XElement)node));
                    }
                }
            }
            else if (instance is IList list)
            {
                foreach (XElement child in nodes.OfType<XElement>())
                {
                    AddToCollection(list, child);
                }
            }
            else if (nodes.OfType<XElement>().Any())
            {
                XamlDiagnostics.Report("No content property", instance.GetType().Name);
            }
        }

        /// <summary>Collapses white space as XAML does and drops text that is only white space.</summary>
        private static List<object> NormalizeText(List<object> nodes)
        {
            var result = new List<object>();
            for (int i = 0; i < nodes.Count; i++)
            {
                if (nodes[i] is not string text)
                {
                    result.Add(nodes[i]);
                    continue;
                }

                text = Whitespace.Replace(text, " ");
                if (i == 0)
                {
                    text = text.TrimStart();
                }
                if (i == nodes.Count - 1)
                {
                    text = text.TrimEnd();
                }
                if (text.Length > 0 && (text != " " || (i > 0 && i < nodes.Count - 1 && IsInline(nodes[i - 1]) && IsInline(nodes[i + 1]))))
                {
                    result.Add(text);
                }
            }
            return result;
        }

        /// <summary>A space between two inline elements is significant; between other elements it is formatting.</summary>
        private static bool IsInline(object node)
        {
            return node is XElement element
                && typeof(Inline).IsAssignableFrom(XamlTypeResolver.Resolve(element.Name.NamespaceName, element.Name.LocalName) ?? typeof(object));
        }

        // ---- markup extensions --------------------------------------------------------------------------------

        private object Evaluate(string text, object targetObject, object targetProperty)
        {
            MarkupExpression expression = MarkupExpression.Parse(text);
            switch (expression.Name)
            {
                case "Binding":
                    return BuildBinding(expression);
                case "RelativeSource":
                    return BuildRelativeSource(expression);
            }

            Type type = _context.Resolve(expression.Name + "Extension") ?? _context.Resolve(expression.Name);
            if (type == null)
            {
                XamlDiagnostics.Report("Unknown markup extension", expression.Name);
                return DependencyProperty.UnsetValue;
            }

            object extension = Construct(type, expression.Positional);
            if (extension == null)
            {
                XamlDiagnostics.Report("Cannot create", type.Name);
                return DependencyProperty.UnsetValue;
            }
            foreach (KeyValuePair<string, string> argument in expression.Named)
            {
                SetExpressionProperty(extension, argument.Key, argument.Value);
            }

            if (extension is not MarkupExtension markupExtension)
            {
                return extension;
            }
            _context.TargetObject = targetObject;
            _context.TargetProperty = targetProperty;
            try
            {
                return markupExtension.ProvideValue(_context);
            }
            catch (Exception)
            {
                XamlDiagnostics.Report("Markup extension failed", expression.Name);
                return DependencyProperty.UnsetValue;
            }
        }

        private object Construct(Type type, List<string> arguments)
        {
            foreach (ConstructorInfo constructor in type.GetConstructors())
            {
                ParameterInfo[] parameters = constructor.GetParameters();
                if (parameters.Length != arguments.Count)
                {
                    continue;
                }
                object[] values = new object[parameters.Length];
                bool usable = true;
                for (int i = 0; i < parameters.Length && usable; i++)
                {
                    usable = TryConvertArgument(arguments[i], parameters[i].ParameterType, out values[i]);
                }
                if (usable)
                {
                    return constructor.Invoke(values);
                }
            }
            return null;
        }

        private bool TryConvertArgument(string text, Type type, out object value)
        {
            if (MarkupExpression.IsExpression(text))
            {
                value = Evaluate(text, null, null);
                return value == null || type.IsInstanceOfType(value);
            }
            if (type == typeof(Type))
            {
                value = _context.Resolve(text);
                return value != null;
            }
            return XamlValueConverter.TryConvert(text, type, out value);
        }

        private void SetExpressionProperty(object target, string name, string text)
        {
            PropertyInfo property = FindProperty(target.GetType(), name);
            if (property == null || !property.CanWrite)
            {
                XamlDiagnostics.Report("Unknown member", target.GetType().Name + "." + name);
                return;
            }
            if (TryConvertArgument(text, property.PropertyType, out object value))
            {
                property.SetValue(target, value);
            }
            else
            {
                XamlDiagnostics.Report("Cannot convert", target.GetType().Name + "." + name + " (" + property.PropertyType.Name + ")");
            }
        }

        private Binding BuildBinding(MarkupExpression expression)
        {
            var binding = new Binding();
            if (expression.Positional.Count > 0)
            {
                binding.Path = new PropertyPath(expression.Positional[0]);
            }
            foreach (KeyValuePair<string, string> argument in expression.Named)
            {
                SetExpressionProperty(binding, argument.Key, argument.Value);
            }
            return binding;
        }

        private RelativeSource BuildRelativeSource(MarkupExpression expression)
        {
            var relative = new RelativeSource { Mode = RelativeSourceMode.FindAncestor };
            if (expression.Positional.Count > 0 && Enum.TryParse(expression.Positional[0], ignoreCase: true, out RelativeSourceMode mode))
            {
                relative.Mode = mode;
            }
            foreach (KeyValuePair<string, string> argument in expression.Named)
            {
                SetExpressionProperty(relative, argument.Key, argument.Value);
            }
            return relative;
        }

        // ---- helpers ------------------------------------------------------------------------------------------

        private void RegisterName(string name, object instance)
        {
            if (string.IsNullOrEmpty(name))
            {
                return;
            }
            _context.Names[name] = instance;
            if (!_context.AssignsRootFields || _context.RootObject == null)
            {
                return;
            }

            // the generated partial class declares a field for every named element
            for (Type type = _context.RootObject.GetType(); type != null; type = type.BaseType)
            {
                FieldInfo field = type.GetField(name, AnyInstance | BindingFlags.DeclaredOnly);
                if (field != null)
                {
                    if (field.FieldType.IsInstanceOfType(instance))
                    {
                        field.SetValue(_context.RootObject, instance);
                    }
                    return;
                }
            }
        }

        private static PropertyInfo FindProperty(Type type, string name)
        {
            for (Type current = type; current != null; current = current.BaseType)
            {
                PropertyInfo property = current.GetProperty(name, InstanceMembers | BindingFlags.DeclaredOnly);
                if (property != null && property.GetIndexParameters().Length == 0)
                {
                    return property;
                }
            }
            return null;
        }
    }
}
