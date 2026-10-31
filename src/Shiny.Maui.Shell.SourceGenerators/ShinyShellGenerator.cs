using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Immutable;
using System.Linq;
using System.Text;

namespace Shiny.Maui.Shell.SourceGenerators;


[Generator(LanguageNames.CSharp)]
public class ShinyShellGenerator : IIncrementalGenerator
{
    static readonly DiagnosticDescriptor InvalidRouteIdentifier = new(
        "SHINY001",
        "Invalid route name",
        "The route '{0}' does not produce a valid C# identifier '{1}'. Route must contain at least one letter and cannot start with a digit after conversion.",
        "Shiny.Shell",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    static readonly DiagnosticDescriptor NavExtensionsDisabledWithMaps = new(
        "SHINY002",
        "Navigation extensions disabled but ShellMap attributes detected",
        "ShinyMauiShell_GenerateNavExtensions is set to false but {0} ShellMap attribute(s) were detected. AddGeneratedMaps will not be generated.",
        "Shiny.Shell",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true
    );

    static readonly DiagnosticDescriptor AiExtensionsMissingPackage = new(
        "SHINY003",
        "Microsoft.Extensions.AI is required for AI extensions",
        "ShinyMauiShell_GenerateAiExtensions is enabled but Microsoft.Extensions.AI is not referenced. Install the Microsoft.Extensions.AI NuGet package or set ShinyMauiShell_GenerateAiExtensions to false.",
        "Shiny.Shell",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    static readonly DiagnosticDescriptor AiPropertyDescriptionsWithoutRouteDescription = new(
        "SHINY004",
        "ShellProperty descriptions without ShellMap description",
        "The route '{0}' has ShellProperty attributes with descriptions but the ShellMap attribute has no description. AI tools cannot determine when to navigate to this route without a description. Add a description to the ShellMap attribute.",
        "Shiny.Shell",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true
    );
    static readonly DiagnosticDescriptor AppLinkTokenNotFound = new(
        "SHINY005",
        "App link token has no matching ShellProperty",
        "The app link template '{0}' contains the token '{{{1}}}' but '{2}' has no [ShellProperty] property with that name.",
        "Shiny.Shell",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    static readonly DiagnosticDescriptor AppLinkUnsupportedType = new(
        "SHINY006",
        "App link property type cannot be converted from a URL value",
        "Property '{0}' of type '{1}' is used by app link template '{2}' but there is no supported conversion from a URL string.",
        "Shiny.Shell",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    static readonly DiagnosticDescriptor AppLinkAmbiguousTemplate = new(
        "SHINY007",
        "Ambiguous app link templates",
        "App link template '{0}' on route '{1}' has the same shape as '{2}' on route '{3}' - an inbound URL could match either.",
        "Shiny.Shell",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    static readonly DiagnosticDescriptor AppLinkNoSchemeOrDomain = new(
        "SHINY008",
        "App links declared without a scheme or domain",
        "{0} app link template(s) are declared but neither ShinyAppLinkSchemes nor ShinyAppLinkDomains is set - no platform manifest entries can be generated or validated.",
        "Shiny.Shell",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true
    );

    static readonly DiagnosticDescriptor AppLinkRequiredNotInPath = new(
        "SHINY009",
        "Required property is not part of the app link path",
        "Property '{0}' is required but is not a token in app link template '{1}' - inbound links must supply it as a query value or navigation will be skipped.",
        "Shiny.Shell",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true
    );

    static readonly DiagnosticDescriptor ShortcutOnParameterisedRoute = new(
        "SHINY010",
        "App shortcut on a route that requires parameters",
        "Route '{0}' declares a shortcut but property '{1}' is required, and an attribute cannot supply a runtime value. Remove the Shortcut property, make '{1}' optional, or register it with ShinyAppBuilder.AddAppShortcut<{2}>(configure: x => x.{1} = ...).",
        "Shiny.Shell",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    static readonly DiagnosticDescriptor TooManyShortcuts = new(
        "SHINY011",
        "More app shortcuts than the platform will show",
        "{0} app shortcuts are declared but at most 4 are shown - iOS drops the excess silently. Reduce the count or set ShortcutOrder so the important ones come first.",
        "Shiny.Shell",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true
    );

    static readonly DiagnosticDescriptor ShortcutMissingTitle = new(
        "SHINY012",
        "App shortcut properties set without a title",
        "Route '{0}' sets {1} but not Shortcut, so no quick action is declared. Set Shortcut to the title, or remove the other Shortcut properties.",
        "Shiny.Shell",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Find classes with ShellMapAttribute
        var shellMapClasses = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (s, _) => s is ClassDeclarationSyntax,
                transform: static (ctx, _) => GetShellMapClass(ctx))
            .Where(static m => m is not null)
            .Collect();

        var options = context.AnalyzerConfigOptionsProvider
            .Select(static (provider, _) =>
            {
                provider.GlobalOptions.TryGetValue("build_property.ShinyMauiShell_GenerateRouteConstants", out var routeValue);
                provider.GlobalOptions.TryGetValue("build_property.ShinyMauiShell_GenerateNavExtensions", out var navValue);
                provider.GlobalOptions.TryGetValue("build_property.ShinyMauiShell_GenerateAiExtensions", out var aiValue);
                provider.GlobalOptions.TryGetValue("build_property.ShinyMauiShell_AiExtensionsClassName", out var aiClassName);
                provider.GlobalOptions.TryGetValue("build_property.ShinyMauiShell_AiNavigateMethodName", out var aiNavigateMethodName);
                provider.GlobalOptions.TryGetValue("build_property.ShinyMauiShell_AiToolsClassName", out var aiToolsClassName);
                provider.GlobalOptions.TryGetValue("build_property.ShinyAppLinkSchemes", out var appLinkSchemes);
                provider.GlobalOptions.TryGetValue("build_property.ShinyAppLinkDomains", out var appLinkDomains);
                // empty or missing is considered true for route/nav, but false for ai (opt-in)
                return new GeneratorOptions(
                    GenerateRouteConstants: !string.Equals(routeValue, "false", StringComparison.OrdinalIgnoreCase),
                    GenerateNavExtensions: !string.Equals(navValue, "false", StringComparison.OrdinalIgnoreCase),
                    GenerateAiExtensions: string.Equals(aiValue, "true", StringComparison.OrdinalIgnoreCase),
                    AiExtensionsClassName: string.IsNullOrWhiteSpace(aiClassName) ? "AiExtensions" : aiClassName!.Trim(),
                    AiNavigateMethodName: string.IsNullOrWhiteSpace(aiNavigateMethodName) ? "NavigateToRoute" : aiNavigateMethodName!.Trim(),
                    AiToolsClassName: string.IsNullOrWhiteSpace(aiToolsClassName) ? "AiMauiShellTools" : aiToolsClassName!.Trim(),
                    AppLinkSchemes: appLinkSchemes ?? "",
                    AppLinkDomains: appLinkDomains ?? ""
                );
            });

        var hasAiPackage = context.CompilationProvider.Select(static (compilation, _) =>
            compilation.GetTypeByMetadataName("Microsoft.Extensions.AI.AITool") != null);

        var combined = shellMapClasses.Combine(options).Combine(hasAiPackage);

        context.RegisterSourceOutput(combined, (spc, data) => GenerateCode(spc, data.Left.Left, data.Left.Right, data.Right));
    }
    static ShellMapInfo? GetShellMapClass(GeneratorSyntaxContext context)
    {
        var classDeclaration = (ClassDeclarationSyntax)context.Node;
        if (context.SemanticModel.GetDeclaredSymbol(classDeclaration) is not INamedTypeSymbol viewModelSymbol)
            return null;

        foreach (var attr in viewModelSymbol.GetAttributes())
        {
            var attributeClass = attr.AttributeClass;
            if (attributeClass?.Name != "ShellMapAttribute" || !attributeClass.IsGenericType)
                continue;

            var pageType = attributeClass.TypeArguments[0];
            var route = GetStringArg(attr, 0, "route");
            var description = GetStringArg(attr, 2, "description");
            var generatedName = route ?? pageType.Name.Replace("Page", "");

            return new ShellMapInfo(
                classDeclaration.Identifier.ValueText,
                viewModelSymbol.ToDisplayString(),
                pageType.Name,
                pageType.ToDisplayString(),
                route ?? pageType.Name,
                generatedName,
                GetBoolArg(attr, 1, "registerRoute", true),
                description,
                GetShellProperties(viewModelSymbol),
                attr.ApplicationSyntaxReference?.GetSyntax().GetLocation() ?? classDeclaration.GetLocation(),
                GetDialogResultType(viewModelSymbol),
                GetStringArrayArg(attr, 3, "appLinks"),
                GetNamedString(attr, "Shortcut"),
                GetNamedString(attr, "ShortcutSubtitle"),
                GetNamedString(attr, "ShortcutIcon"),
                GetNamedInt(attr, "ShortcutOrder")
            );
        }

        return null;
    }

    // Roslyn fills ConstructorArguments for every parameter of the attribute constructor,
    // defaults included, and places a `name:` argument into its own positional slot. That makes
    // a positional read correct however the attribute was written at the call site - including
    // `new[] { ... }` vs a collection expression for array arguments. The named-argument sweep
    // is the fallback for a property/field initializer of the same name.

    static string? GetStringArg(AttributeData attr, int index, string name)
    {
        if (attr.ConstructorArguments.Length > index)
        {
            var arg = attr.ConstructorArguments[index];
            if (arg.Kind == TypedConstantKind.Primitive && arg.Value is string s)
                return s;
        }

        foreach (var named in attr.NamedArguments)
        {
            if (named.Key == name && named.Value.Value is string s)
                return s;
        }
        return null;
    }

    static bool GetBoolArg(AttributeData attr, int index, string name, bool defaultValue)
    {
        if (attr.ConstructorArguments.Length > index)
        {
            var arg = attr.ConstructorArguments[index];
            if (arg.Kind == TypedConstantKind.Primitive && arg.Value is bool b)
                return b;
        }

        foreach (var named in attr.NamedArguments)
        {
            if (named.Key == name && named.Value.Value is bool b)
                return b;
        }
        return defaultValue;
    }

    /// <summary>
    /// Named property initializers land in NamedArguments, never in ConstructorArguments - so
    /// these deliberately do not take a positional index the way the constructor readers do.
    /// </summary>
    static string? GetNamedString(AttributeData attr, string name)
    {
        foreach (var named in attr.NamedArguments)
        {
            if (named.Key == name && named.Value.Value is string s && !string.IsNullOrWhiteSpace(s))
                return s;
        }
        return null;
    }

    static int GetNamedInt(AttributeData attr, string name)
    {
        foreach (var named in attr.NamedArguments)
        {
            if (named.Key == name && named.Value.Value is int i)
                return i;
        }
        return 0;
    }

    static ImmutableArray<string> GetStringArrayArg(AttributeData attr, int index, string name)
    {
        var arg = default(TypedConstant);
        var found = false;

        if (attr.ConstructorArguments.Length > index && attr.ConstructorArguments[index].Kind == TypedConstantKind.Array)
        {
            arg = attr.ConstructorArguments[index];
            found = true;
        }
        else
        {
            foreach (var named in attr.NamedArguments)
            {
                if (named.Key == name && named.Value.Kind == TypedConstantKind.Array)
                {
                    arg = named.Value;
                    found = true;
                    break;
                }
            }
        }

        if (!found || arg.IsNull)
            return ImmutableArray<string>.Empty;

        var builder = ImmutableArray.CreateBuilder<string>();
        foreach (var item in arg.Values)
        {
            if (item.Value is string s && !string.IsNullOrWhiteSpace(s))
                builder.Add(s.Trim());
        }
        return builder.ToImmutable();
    }

    /// <summary>
    /// Returns the T of Shiny.IDialogAware&lt;T&gt; when the viewmodel implements it, otherwise null.
    /// Drives the Show{Route}Dialog extension generation.
    /// </summary>
    static string? GetDialogResultType(INamedTypeSymbol? viewModelSymbol)
    {
        if (viewModelSymbol == null)
            return null;

        foreach (var iface in viewModelSymbol.AllInterfaces)
        {
            if (iface.Name == "IDialogAware" &&
                iface.IsGenericType &&
                iface.TypeArguments.Length == 1 &&
                iface.ContainingNamespace?.ToDisplayString() == "Shiny")
            {
                return iface.TypeArguments[0].ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            }
        }
        return null;
    }

    static ImmutableArray<ShellPropertyInfo> GetShellProperties(INamedTypeSymbol viewModelSymbol)
    {
        var properties = ImmutableArray.CreateBuilder<ShellPropertyInfo>();

        foreach (var member in viewModelSymbol.GetMembers())
        {
            if (member is not IPropertySymbol propertySymbol)
                continue;

            AttributeData? shellProperty = null;
            foreach (var attr in propertySymbol.GetAttributes())
            {
                if (attr.AttributeClass?.Name == "ShellPropertyAttribute")
                {
                    shellProperty = attr;
                    break;
                }
            }
            if (shellProperty == null)
                continue;

            // Navigation assigns these from outside the viewmodel, so both accessors must be
            // public. Anything else is silently skipped rather than reported - see SHINY009.
            if (propertySymbol.GetMethod?.DeclaredAccessibility != Accessibility.Public ||
                propertySymbol.SetMethod?.DeclaredAccessibility != Accessibility.Public)
                continue;

            var typeSymbol = propertySymbol.Type;
            var enumType = typeSymbol.TypeKind == TypeKind.Enum
                ? (INamedTypeSymbol)typeSymbol
                : typeSymbol is INamedTypeSymbol { IsGenericType: true, ConstructedFrom.SpecialType: SpecialType.System_Nullable_T } nullableType &&
                  nullableType.TypeArguments[0].TypeKind == TypeKind.Enum
                    ? (INamedTypeSymbol)nullableType.TypeArguments[0]
                    : null;

            var enumValues = enumType != null
                ? enumType.GetMembers().OfType<IFieldSymbol>().Where(f => f.HasConstantValue).Select(f => f.Name).ToImmutableArray()
                : ImmutableArray<string>.Empty;

            properties.Add(new ShellPropertyInfo(
                propertySymbol.Name,
                typeSymbol.ToDisplayString(),
                GetBoolArg(shellProperty, 1, "required", true),
                GetStringArg(shellProperty, 0, "description"),
                enumType != null,
                enumValues
            ));
        }

        return properties.ToImmutable();
    }


    static void GenerateCode(SourceProductionContext context, ImmutableArray<ShellMapInfo?> classes, GeneratorOptions options, bool hasAiPackage)
    {
        var validClasses = classes.Where(c => c != null).Cast<ShellMapInfo>().ToImmutableArray();

        // Validate generated names are valid C# identifiers
        var checkedClasses = ImmutableArray.CreateBuilder<ShellMapInfo>();
        foreach (var cls in validClasses)
        {
            if (!SyntaxFacts.IsValidIdentifier(cls.GeneratedName))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    InvalidRouteIdentifier,
                    cls.AttributeLocation,
                    cls.Route,
                    cls.GeneratedName
                ));
            }
            else
            {
                checkedClasses.Add(cls);
            }
        }
        var filtered = checkedClasses.ToImmutable();

        if (filtered.IsEmpty)
            return;

        // Validate AI extensions configuration
        if (options.GenerateAiExtensions && !hasAiPackage)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                AiExtensionsMissingPackage,
                Location.None));
        }

        if (options.GenerateAiExtensions)
        {
            foreach (var cls in filtered)
            {
                if (cls.Description == null && cls.Properties.Any(p => p.Description != null))
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        AiPropertyDescriptionsWithoutRouteDescription,
                        cls.AttributeLocation,
                        cls.Route));
                }
            }
        }

        ValidateAppLinks(context, filtered, options);
        ValidateAppShortcuts(context, filtered);

        // Generate AddGeneratedMaps and nav extensions only if enabled
        if (options.GenerateNavExtensions)
        {
            GenerateNavigationBuilderExtensions(context, filtered);
            GenerateNavigationExtensions(context, filtered);
            GenerateNavigationBuilderNavExtensions(context, filtered);
            GenerateDialogExtensions(context, filtered);
            GenerateAppLinkUriExtensions(context, filtered, options);
        }
        else
        {
            context.ReportDiagnostic(Diagnostic.Create(
                NavExtensionsDisabledWithMaps,
                Location.None,
                filtered.Length
            ));
        }

        if (options.GenerateAiExtensions && hasAiPackage)
        {
            GenerateAiExtensions(context, filtered, options);
        }

        // Generate Routes class only if enabled
        if (options.GenerateRouteConstants)
            GenerateRoutesClass(context, filtered);
    }

    static string[] SplitTemplate(string template)
        => template.Trim('/').Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);

    static bool IsToken(string segment)
        => segment.Length > 1 && segment[0] == '{' && segment[segment.Length - 1] == '}';

    static string TokenName(string segment)
        => segment.Substring(1, segment.Length - 2);

    /// <summary>
    /// Two templates are ambiguous only when they have the same shape - same length, tokens in the
    /// same positions, identical literals. Overlaps like "product/featured" vs "product/{id}" are
    /// resolved deterministically at runtime by specificity, so they are not reported.
    /// </summary>
    static bool SameShape(string[] a, string[] b)
    {
        if (a.Length != b.Length)
            return false;

        for (var i = 0; i < a.Length; i++)
        {
            var aToken = IsToken(a[i]);
            if (aToken != IsToken(b[i]))
                return false;

            if (!aToken && !string.Equals(a[i], b[i], StringComparison.OrdinalIgnoreCase))
                return false;
        }
        return true;
    }

    static void ValidateAppLinks(SourceProductionContext context, ImmutableArray<ShellMapInfo> classes, GeneratorOptions options)
    {
        var withLinks = classes.Where(c => !c.AppLinks.IsDefaultOrEmpty).ToList();
        if (withLinks.Count == 0)
            return;

        var total = withLinks.Sum(c => c.AppLinks.Length);
        if (string.IsNullOrWhiteSpace(options.AppLinkSchemes) && string.IsNullOrWhiteSpace(options.AppLinkDomains))
        {
            context.ReportDiagnostic(Diagnostic.Create(AppLinkNoSchemeOrDomain, Location.None, total));
        }

        var seen = new System.Collections.Generic.List<(string Template, string Route, string[] Segments)>();

        foreach (var cls in withLinks)
        {
            foreach (var template in cls.AppLinks)
            {
                var segments = SplitTemplate(template);

                foreach (var segment in segments)
                {
                    if (!IsToken(segment))
                        continue;

                    var token = TokenName(segment);
                    var prop = cls.Properties.FirstOrDefault(p => string.Equals(p.Name, token, StringComparison.OrdinalIgnoreCase));
                    if (prop == null)
                    {
                        context.ReportDiagnostic(Diagnostic.Create(
                            AppLinkTokenNotFound, cls.AttributeLocation, template, token, cls.ViewModelName));
                    }
                    else if (GetAppLinkParse(prop, "s", "v") == null && !IsStringType(prop))
                    {
                        context.ReportDiagnostic(Diagnostic.Create(
                            AppLinkUnsupportedType, cls.AttributeLocation, prop.Name, prop.TypeName, template));
                    }
                }

                foreach (var prop in cls.Properties.Where(p => p.IsRequired))
                {
                    var inPath = segments.Any(x => IsToken(x) && string.Equals(TokenName(x), prop.Name, StringComparison.OrdinalIgnoreCase));
                    if (!inPath)
                    {
                        context.ReportDiagnostic(Diagnostic.Create(
                            AppLinkRequiredNotInPath, cls.AttributeLocation, prop.Name, template));
                    }

                    // A required property still has to be convertible even when it arrives via query.
                    if (GetAppLinkParse(prop, "s", "v") == null && !IsStringType(prop))
                    {
                        context.ReportDiagnostic(Diagnostic.Create(
                            AppLinkUnsupportedType, cls.AttributeLocation, prop.Name, prop.TypeName, template));
                    }
                }

                foreach (var prior in seen)
                {
                    if (SameShape(prior.Segments, segments))
                    {
                        context.ReportDiagnostic(Diagnostic.Create(
                            AppLinkAmbiguousTemplate, cls.AttributeLocation, template, cls.Route, prior.Template, prior.Route));
                    }
                }
                seen.Add((template, cls.Route, segments));
            }
        }
    }

    static bool IsStringType(ShellPropertyInfo prop)
    {
        var baseType = prop.TypeName.EndsWith("?") ? prop.TypeName.Substring(0, prop.TypeName.Length - 1) : prop.TypeName;
        return !prop.IsEnum && (baseType == "string" || baseType == "System.String");
    }

    /// <summary>
    /// A TryParse-shaped test for one app link value. Returns null when the type has no supported
    /// conversion (SHINY006) or is a string, which needs no parse at all.
    /// </summary>
    static string? GetAppLinkParse(ShellPropertyInfo prop, string source, string target)
    {
        const string Invariant = "global::System.Globalization.CultureInfo.InvariantCulture";
        var typeName = prop.TypeName;
        var baseType = typeName.EndsWith("?") ? typeName.Substring(0, typeName.Length - 1) : typeName;

        if (prop.IsEnum)
            return $"global::System.Enum.TryParse<global::{baseType}>({source}, true, out var {target})";

        return baseType switch
        {
            "int" or "System.Int32" => $"global::System.Int32.TryParse({source}, {Invariant}, out var {target})",
            "long" or "System.Int64" => $"global::System.Int64.TryParse({source}, {Invariant}, out var {target})",
            "short" or "System.Int16" => $"global::System.Int16.TryParse({source}, {Invariant}, out var {target})",
            "byte" or "System.Byte" => $"global::System.Byte.TryParse({source}, {Invariant}, out var {target})",
            "sbyte" or "System.SByte" => $"global::System.SByte.TryParse({source}, {Invariant}, out var {target})",
            "uint" or "System.UInt32" => $"global::System.UInt32.TryParse({source}, {Invariant}, out var {target})",
            "ulong" or "System.UInt64" => $"global::System.UInt64.TryParse({source}, {Invariant}, out var {target})",
            "ushort" or "System.UInt16" => $"global::System.UInt16.TryParse({source}, {Invariant}, out var {target})",
            "float" or "System.Single" => $"global::System.Single.TryParse({source}, {Invariant}, out var {target})",
            "double" or "System.Double" => $"global::System.Double.TryParse({source}, {Invariant}, out var {target})",
            "decimal" or "System.Decimal" => $"global::System.Decimal.TryParse({source}, {Invariant}, out var {target})",
            "bool" or "System.Boolean" => $"global::System.Boolean.TryParse({source}, out var {target})",
            "System.Guid" => $"global::System.Guid.TryParse({source}, out var {target})",
            "System.DateTime" => $"global::System.DateTime.TryParse({source}, {Invariant}, out var {target})",
            "System.DateTimeOffset" => $"global::System.DateTimeOffset.TryParse({source}, {Invariant}, out var {target})",
            "System.DateOnly" => $"global::System.DateOnly.TryParse({source}, {Invariant}, out var {target})",
            "System.TimeOnly" => $"global::System.TimeOnly.TryParse({source}, {Invariant}, out var {target})",
            "System.TimeSpan" => $"global::System.TimeSpan.TryParse({source}, {Invariant}, out var {target})",
            "System.Uri" => $"global::System.Uri.TryCreate({source}, global::System.UriKind.RelativeOrAbsolute, out var {target})",
            _ => null
        };
    }

    /// <summary>
    /// Emits the AddAppLink calls for one route. The binder is identical across a route's
    /// templates - a value is looked up by property name in a case-insensitive dictionary, so a
    /// path token and a query value of the same name are read the same way.
    /// </summary>
    static void GenerateAppLinkRegistrations(StringBuilder sb, ShellMapInfo cls)
    {
        foreach (var template in cls.AppLinks)
        {
            sb.AppendLine($"        builder.AddAppLink<{cls.ViewModelFullName}>(");
            sb.AppendLine($"            \"{EscapeString(template)}\",");
            sb.AppendLine("            static (vm, values) =>");
            sb.AppendLine("            {");

            var index = 0;
            foreach (var prop in cls.Properties)
            {
                var raw = $"__raw{index}";
                var parsed = $"__val{index}";
                index++;

                var parse = GetAppLinkParse(prop, raw, parsed);
                var isString = IsStringType(prop);

                if (prop.IsRequired)
                {
                    if (isString)
                    {
                        sb.AppendLine($"                if (!values.TryGetValue(\"{EscapeString(prop.Name)}\", out var {raw}))");
                        sb.AppendLine("                    return false;");
                        sb.AppendLine($"                vm.{prop.Name} = {raw};");
                    }
                    else if (parse != null)
                    {
                        sb.AppendLine($"                if (!values.TryGetValue(\"{EscapeString(prop.Name)}\", out var {raw}) || !{parse})");
                        sb.AppendLine("                    return false;");
                        sb.AppendLine($"                vm.{prop.Name} = {parsed};");
                    }
                }
                else
                {
                    if (isString)
                    {
                        sb.AppendLine($"                if (values.TryGetValue(\"{EscapeString(prop.Name)}\", out var {raw}))");
                        sb.AppendLine($"                    vm.{prop.Name} = {raw};");
                    }
                    else if (parse != null)
                    {
                        sb.AppendLine($"                if (values.TryGetValue(\"{EscapeString(prop.Name)}\", out var {raw}) && {parse})");
                        sb.AppendLine($"                    vm.{prop.Name} = {parsed};");
                    }
                }
            }

            sb.AppendLine("                return true;");
            sb.AppendLine("            }");
            sb.AppendLine("        );");
        }
    }

    /// <summary>
    /// Emits Create{Route}AppLink for building an outbound URL - the share-sheet direction. Only
    /// generated when there is exactly one scheme (or, failing that, exactly one domain), because
    /// with several configured there is no single correct base to build against.
    /// </summary>
    static void GenerateAppLinkUriExtensions(SourceProductionContext context, ImmutableArray<ShellMapInfo> classes, GeneratorOptions options)
    {
        var withLinks = classes.Where(c => !c.AppLinks.IsDefaultOrEmpty).ToList();
        if (withLinks.Count == 0)
            return;

        var schemes = options.AppLinkSchemes.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()).Where(x => x.Length > 0).ToList();
        var domains = options.AppLinkDomains.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()).Where(x => x.Length > 0).ToList();

        string baseUri;
        if (schemes.Count == 1)
            baseUri = schemes[0] + "://";
        else if (schemes.Count == 0 && domains.Count == 1)
            baseUri = "https://" + domains[0] + "/";
        else
            return;

        var sb = new StringBuilder();
        sb.AppendLine("#nullable enable");
        sb.AppendLine("public static class AppLinkExtensions");
        sb.AppendLine("{");

        foreach (var cls in withLinks)
        {
            var template = cls.AppLinks[0];
            var segments = SplitTemplate(template);
            var tokenNames = segments.Where(IsToken).Select(TokenName).ToList();

            var pathProps = new System.Collections.Generic.List<ShellPropertyInfo>();
            foreach (var token in tokenNames)
            {
                var prop = cls.Properties.FirstOrDefault(p => string.Equals(p.Name, token, StringComparison.OrdinalIgnoreCase));
                if (prop != null)
                    pathProps.Add(prop);
            }

            // A token with no matching property is already SHINY005 - don't emit broken code too.
            if (pathProps.Count != tokenNames.Count)
                continue;

            var queryProps = cls.Properties.Where(p => !tokenNames.Any(t => string.Equals(t, p.Name, StringComparison.OrdinalIgnoreCase))).ToList();

            sb.AppendLine("    /// <summary>");
            sb.AppendLine($"    /// Builds the app link URL for '{EscapeXml(cls.Route)}' from template '{EscapeXml(template)}'.");
            sb.AppendLine("    /// </summary>");
            sb.Append($"    public static global::System.Uri Create{cls.GeneratedName}AppLink(this global::Shiny.INavigator navigator");

            foreach (var prop in pathProps)
                sb.Append($", {prop.TypeName} {ToCamelCase(prop.Name)}");

            foreach (var prop in queryProps)
                sb.Append($", {prop.TypeName} {ToCamelCase(prop.Name)} = {GetDefaultValue(prop.TypeName)}");

            sb.AppendLine(")");
            sb.AppendLine("    {");
            sb.AppendLine("        var __sb = new global::System.Text.StringBuilder();");
            sb.AppendLine($"        __sb.Append(\"{EscapeString(baseUri)}\");");

            for (var i = 0; i < segments.Length; i++)
            {
                if (i > 0)
                    sb.AppendLine("        __sb.Append('/');");

                var segment = segments[i];
                if (IsToken(segment))
                {
                    var prop = pathProps.First(p => string.Equals(p.Name, TokenName(segment), StringComparison.OrdinalIgnoreCase));
                    sb.AppendLine($"        __sb.Append(global::System.Uri.EscapeDataString(global::System.Convert.ToString({ToCamelCase(prop.Name)}, global::System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty));");
                }
                else
                {
                    sb.AppendLine($"        __sb.Append(\"{EscapeString(segment)}\");");
                }
            }

            if (queryProps.Count > 0)
            {
                sb.AppendLine("        var __first = true;");
                foreach (var prop in queryProps)
                {
                    var name = ToCamelCase(prop.Name);
                    sb.AppendLine($"        if (!global::System.Collections.Generic.EqualityComparer<{prop.TypeName}>.Default.Equals({name}, default))");
                    sb.AppendLine("        {");
                    sb.AppendLine("            __sb.Append(__first ? '?' : '&');");
                    sb.AppendLine("            __first = false;");
                    sb.AppendLine($"            __sb.Append(\"{EscapeString(prop.Name)}=\");");
                    sb.AppendLine($"            __sb.Append(global::System.Uri.EscapeDataString(global::System.Convert.ToString({name}, global::System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty));");
                    sb.AppendLine("        }");
                }
            }

            sb.AppendLine("        return new global::System.Uri(__sb.ToString());");
            sb.AppendLine("    }");
            sb.AppendLine();
        }

        sb.AppendLine("}");
        context.AddSource("AppLinkExtensions.g.cs", sb.ToString());
    }

    static void ValidateAppShortcuts(SourceProductionContext context, ImmutableArray<ShellMapInfo> classes)
    {
        var declared = 0;

        foreach (var cls in classes)
        {
            if (cls.Shortcut == null)
            {
                // The other Shortcut* properties mean nothing on their own - a constructor
                // parameter would have made the title unmissable, a named property cannot.
                var orphans = new System.Collections.Generic.List<string>();
                if (cls.ShortcutSubtitle != null) orphans.Add("ShortcutSubtitle");
                if (cls.ShortcutIcon != null) orphans.Add("ShortcutIcon");
                if (cls.ShortcutOrder != 0) orphans.Add("ShortcutOrder");

                if (orphans.Count > 0)
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        ShortcutMissingTitle, cls.AttributeLocation, cls.Route, string.Join(", ", orphans)));
                }
                continue;
            }

            declared++;

            foreach (var prop in cls.Properties)
            {
                if (prop.IsRequired)
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        ShortcutOnParameterisedRoute, cls.AttributeLocation, cls.Route, prop.Name, cls.ViewModelName));
                }
            }
        }

        if (declared > 4)
            context.ReportDiagnostic(Diagnostic.Create(TooManyShortcuts, Location.None, declared));
    }

    /// <summary>
    /// Emits AddAppShortcut calls - the same public API a consumer calls by hand when source
    /// generation is turned off, so disabling generation does not take the feature with it.
    /// </summary>
    static void GenerateAppShortcutRegistrations(StringBuilder sb, ShellMapInfo cls)
    {
        if (cls.Shortcut == null)
            return;

        sb.Append($"        builder.AddAppShortcut<{cls.ViewModelFullName}>(");
        sb.Append($"\"{EscapeString(cls.Shortcut)}\"");
        sb.Append(cls.ShortcutSubtitle != null ? $", \"{EscapeString(cls.ShortcutSubtitle)}\"" : ", null");
        sb.Append(cls.ShortcutIcon != null ? $", \"{EscapeString(cls.ShortcutIcon)}\"" : ", null");
        sb.AppendLine($", {cls.ShortcutOrder});");
    }

    static void GenerateRoutesClass(SourceProductionContext context, ImmutableArray<ShellMapInfo> classes)
    {
        var sb = new StringBuilder();
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        sb.AppendLine("public static class Routes");
        sb.AppendLine("{");
        
        foreach (var cls in classes)
        {
            var constantName = cls.GeneratedName;
            sb.AppendLine($"    public const string {constantName} = \"{cls.Route}\";");
        }
        
        sb.AppendLine("}");
        
        context.AddSource("Routes.g.cs", sb.ToString());
    }

    static void GenerateNavigationExtensions(SourceProductionContext context, ImmutableArray<ShellMapInfo> classes)
    {
        var sb = new StringBuilder();
        sb.AppendLine("#nullable enable");
        sb.AppendLine("public static class NavigationExtensions");
        sb.AppendLine("{");
        
        foreach (var cls in classes)
        {
            var methodName = $"NavigateTo{cls.GeneratedName}";
            var requiredParams = cls.Properties.Where(p => p.IsRequired).ToList();
            var optionalParams = cls.Properties.Where(p => !p.IsRequired).ToList();

            // XML doc comment
            if (cls.Description != null)
            {
                sb.AppendLine($"    /// <summary>");
                sb.AppendLine($"    /// {EscapeXml(cls.Description)}");
                sb.AppendLine($"    /// </summary>");

                foreach (var prop in requiredParams.Concat(optionalParams))
                {
                    var paramDesc = prop.Description != null ? EscapeXml(prop.Description) : "";
                    sb.AppendLine($"    /// <param name=\"{ToCamelCase(prop.Name)}\">{paramDesc}</param>");
                }

                sb.AppendLine($"    /// <param name=\"relativeNavigation\">If true, it will navigate/stack from where the application currently is otherwise, it will reset the stack to this new route</param>");
                sb.AppendLine($"    /// <param name=\"bypassInterceptors\">Skips the registered INavigationInterceptors</param>");
                sb.AppendLine($"    /// <param name=\"cancellationToken\">Passed to the interceptors</param>");
                sb.AppendLine($"    /// <returns>True when the navigation happened; false when an interceptor cancelled it</returns>");
            }

            // [Description] on method
            if (cls.Description != null)
                sb.AppendLine($"    [global::System.ComponentModel.Description(\"{EscapeString(cls.Description)}\")]");

            sb.Append($"    public static global::System.Threading.Tasks.Task<bool> {methodName}(this global::Shiny.INavigator navigator");

            // Add required parameters first
            foreach (var prop in requiredParams)
            {
                if (prop.Description != null)
                    sb.Append($", [global::System.ComponentModel.Description(\"{EscapeString(prop.Description)}\")] {prop.TypeName} {ToCamelCase(prop.Name)}");
                else
                    sb.Append($", {prop.TypeName} {ToCamelCase(prop.Name)}");
            }

            // Add optional parameters last
            foreach (var prop in optionalParams)
            {
                var defaultValue = GetDefaultValue(prop.TypeName);
                if (prop.Description != null)
                    sb.Append($", [global::System.ComponentModel.Description(\"{EscapeString(prop.Description)}\")] {prop.TypeName} {ToCamelCase(prop.Name)} = {defaultValue}");
                else
                    sb.Append($", {prop.TypeName} {ToCamelCase(prop.Name)} = {defaultValue}");
            }

            if (cls.Description != null)
                sb.Append(", [global::System.ComponentModel.Description(\"If true, it will navigate/stack from where the application currently is otherwise, it will reset the stack to this new route\")] bool relativeNavigation = true");
            else
                sb.Append(", bool relativeNavigation = true");

            // Interceptor controls mirror INavigator so a generated call is never the weaker option.
            sb.Append(", bool bypassInterceptors = false");
            sb.Append(", global::System.Threading.CancellationToken cancellationToken = default");

            // If no properties, add the params argument
            if (!cls.Properties.Any())
            {
                sb.Append(", params global::System.Collections.Generic.IEnumerable<(string Key, object Value)> args");
            }

            sb.AppendLine(")");
            sb.AppendLine("    {");

            if (cls.Properties.Any())
            {
                sb.Append($"        return navigator.NavigateTo<{cls.ViewModelFullName}>(x => ");
                sb.Append("{ ");

                var assignments = cls.Properties.Select(p => $"x.{p.Name} = {ToCamelCase(p.Name)}");
                sb.Append(string.Join("; ", assignments));
                sb.Append(";");

                sb.AppendLine($" }}, relativeNavigation, bypassInterceptors, cancellationToken);");
            }
            else
            {
                sb.AppendLine($"        return navigator.NavigateTo<{cls.ViewModelFullName}>(configure: null, relativeNavigation: relativeNavigation, bypassInterceptors: bypassInterceptors, cancellationToken: cancellationToken, args: args);");
            }

            sb.AppendLine("    }");
            sb.AppendLine();
        }
        
        sb.AppendLine("}");
        
        context.AddSource("NavigationExtensions.g.cs", sb.ToString());
    }

    /// <summary>
    /// Emits a fully-inferred Show{Route}Dialog extension for every ShellMap viewmodel that also
    /// implements IDialogAware&lt;T&gt;. C# cannot infer type arguments from a constraint, so calling
    /// INavigator.ShowDialog directly always requires both type arguments spelled out - these
    /// wrappers close both of them and surface ShellProperty values as method parameters.
    /// </summary>
    static void GenerateDialogExtensions(SourceProductionContext context, ImmutableArray<ShellMapInfo> classes)
    {
        var dialogs = classes.Where(x => x.DialogResultTypeFullName != null).ToList();
        if (dialogs.Count == 0)
            return;

        var sb = new StringBuilder();
        sb.AppendLine("#nullable enable");
        sb.AppendLine("public static class DialogExtensions");
        sb.AppendLine("{");

        foreach (var cls in dialogs)
        {
            var methodName = $"Show{cls.GeneratedName}Dialog";
            var resultType = cls.DialogResultTypeFullName!;
            var requiredParams = cls.Properties.Where(p => p.IsRequired).ToList();
            var optionalParams = cls.Properties.Where(p => !p.IsRequired).ToList();

            sb.AppendLine("    /// <summary>");
            sb.AppendLine(cls.Description != null
                ? $"    /// {EscapeXml(cls.Description)}"
                : $"    /// Presents {EscapeXml(cls.ViewModelName)} as a dialog and awaits its result.");
            sb.AppendLine("    /// </summary>");

            foreach (var prop in requiredParams.Concat(optionalParams))
            {
                var paramDesc = prop.Description != null ? EscapeXml(prop.Description) : "";
                sb.AppendLine($"    /// <param name=\"{ToCamelCase(prop.Name)}\">{paramDesc}</param>");
            }
            sb.AppendLine("    /// <param name=\"cancellationToken\">Dismisses the dialog and throws OperationCanceledException. Distinct from the user cancelling, which returns a cancelled DialogResult.</param>");

            if (cls.Description != null)
                sb.AppendLine($"    [global::System.ComponentModel.Description(\"{EscapeString(cls.Description)}\")]");

            sb.Append($"    public static global::System.Threading.Tasks.Task<global::Shiny.DialogResult<{resultType}>> {methodName}(this global::Shiny.INavigator navigator");

            foreach (var prop in requiredParams)
            {
                if (prop.Description != null)
                    sb.Append($", [global::System.ComponentModel.Description(\"{EscapeString(prop.Description)}\")] {prop.TypeName} {ToCamelCase(prop.Name)}");
                else
                    sb.Append($", {prop.TypeName} {ToCamelCase(prop.Name)}");
            }

            foreach (var prop in optionalParams)
            {
                var defaultValue = GetDefaultValue(prop.TypeName);
                if (prop.Description != null)
                    sb.Append($", [global::System.ComponentModel.Description(\"{EscapeString(prop.Description)}\")] {prop.TypeName} {ToCamelCase(prop.Name)} = {defaultValue}");
                else
                    sb.Append($", {prop.TypeName} {ToCamelCase(prop.Name)} = {defaultValue}");
            }

            sb.Append(", global::System.Threading.CancellationToken cancellationToken = default");
            sb.AppendLine(")");
            sb.AppendLine("    {");

            if (cls.Properties.Any())
            {
                sb.Append($"        return navigator.ShowDialog<{cls.ViewModelFullName}, {resultType}>(x => ");
                sb.Append("{ ");
                sb.Append(string.Join("; ", cls.Properties.Select(p => $"x.{p.Name} = {ToCamelCase(p.Name)}")));
                sb.Append(";");
                sb.AppendLine(" }, cancellationToken);");
            }
            else
            {
                sb.AppendLine($"        return navigator.ShowDialog<{cls.ViewModelFullName}, {resultType}>(configure: null, cancellationToken: cancellationToken);");
            }

            sb.AppendLine("    }");
            sb.AppendLine();
        }

        sb.AppendLine("}");

        context.AddSource("DialogExtensions.g.cs", sb.ToString());
    }

    static void GenerateNavigationBuilderExtensions(SourceProductionContext context, ImmutableArray<ShellMapInfo> classes)
    {
        var sb = new StringBuilder();
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        sb.AppendLine("public static class NavigationBuilderExtensions");
        sb.AppendLine("{");
        sb.AppendLine("    public static global::Shiny.ShinyAppBuilder AddGeneratedMaps(this global::Shiny.ShinyAppBuilder builder)");
        sb.AppendLine("    {");
        
        foreach (var cls in classes)
        {
            if (cls.RegisterRoute)
            {
                sb.AppendLine($"        builder.Add<{cls.PageTypeFullName}, {cls.ViewModelFullName}>(\"{cls.Route}\");");
            }
            else
            {
                sb.AppendLine($"        builder.Add<{cls.PageTypeFullName}, {cls.ViewModelFullName}>(\"{cls.Route}\", registerRoute: false);");
            }
        }

        // Descriptions and [ShellProperty] parameters, so IRouteRegistry can list them at runtime
        // without the opt-in AI extensions.
        foreach (var cls in classes)
        {
            sb.AppendLine("        builder.AddRouteInfo(");
            AppendGeneratedRouteInfo(sb, cls, "            ");
            sb.AppendLine(");");
        }

        foreach (var cls in classes)
            GenerateAppLinkRegistrations(sb, cls);

        foreach (var cls in classes.OrderBy(x => x.ShortcutOrder).ThenBy(x => x.Route, StringComparer.Ordinal))
            GenerateAppShortcutRegistrations(sb, cls);

        
        sb.AppendLine("        return builder;");
        sb.AppendLine("    }");
        sb.AppendLine("}");
        
        context.AddSource("NavigationBuilderExtensions.g.cs", sb.ToString());
    }

    static void GenerateNavigationBuilderNavExtensions(SourceProductionContext context, ImmutableArray<ShellMapInfo> classes)
    {
        var sb = new StringBuilder();
        sb.AppendLine("#nullable enable");
        sb.AppendLine("public static class NavigationBuilderNavExtensions");
        sb.AppendLine("{");

        foreach (var cls in classes)
        {
            var methodName = $"Add{cls.GeneratedName}";
            var requiredParams = cls.Properties.Where(p => p.IsRequired).ToList();
            var optionalParams = cls.Properties.Where(p => !p.IsRequired).ToList();

            if (cls.Properties.Any())
            {
                sb.Append($"    public static global::Shiny.INavigationBuilder {methodName}(this global::Shiny.INavigationBuilder builder");

                foreach (var prop in requiredParams)
                    sb.Append($", {prop.TypeName} {ToCamelCase(prop.Name)}");

                foreach (var prop in optionalParams)
                {
                    var defaultValue = GetDefaultValue(prop.TypeName);
                    sb.Append($", {prop.TypeName} {ToCamelCase(prop.Name)} = {defaultValue}");
                }

                sb.AppendLine(")");
                sb.AppendLine("    {");
                sb.Append($"        return builder.Add<{cls.ViewModelFullName}>(x => {{ ");

                var assignments = cls.Properties.Select(p => $"x.{p.Name} = {ToCamelCase(p.Name)}");
                sb.Append(string.Join("; ", assignments));
                sb.Append(";");

                sb.AppendLine(" });");
                sb.AppendLine("    }");
            }
            else
            {
                sb.AppendLine($"    public static global::Shiny.INavigationBuilder {methodName}(this global::Shiny.INavigationBuilder builder)");
                sb.AppendLine("    {");
                sb.AppendLine($"        return builder.Add<{cls.ViewModelFullName}>();");
                sb.AppendLine("    }");
            }

            sb.AppendLine();
        }

        sb.AppendLine("}");
        context.AddSource("NavigationBuilderNavExtensions.g.cs", sb.ToString());
    }

    static void GenerateAiExtensions(SourceProductionContext context, ImmutableArray<ShellMapInfo> classes, GeneratorOptions options)
    {
        var sb = new StringBuilder();
        sb.AppendLine("#nullable enable");
        sb.AppendLine($"public static class {options.AiExtensionsClassName}");
        sb.AppendLine("{");
        sb.AppendLine("    [global::System.ComponentModel.Description(\"This provides a list of routes throughout the application\")]");
        sb.AppendLine("    public static global::Shiny.Infrastructure.GeneratedRouteInfo[] GetGeneratedRouteInfo(this global::Shiny.INavigator navigator) =>");
        sb.AppendLine("    [");

        for (int i = 0; i < classes.Length; i++)
        {
            AppendGeneratedRouteInfo(sb, classes[i], "        ");
            if (i < classes.Length - 1)
                sb.Append(",");
            sb.AppendLine();
        }

        sb.AppendLine("    ];");

        var aiClasses = classes.Where(c => c.Description != null).ToList();
        GenerateAiMauiShellToolsClass(sb, aiClasses, options);

        sb.AppendLine("}");

        context.AddSource("AiExtensions.g.cs", sb.ToString());
    }

    /// <summary>
    /// Writes a <c>GeneratedRouteInfo</c> constructor expression for one map, starting at
    /// <paramref name="indent"/> and ending at its closing parenthesis. Shared by
    /// <c>GetGeneratedRouteInfo()</c> and <c>AddGeneratedMaps()</c> so the two never disagree.
    /// </summary>
    static void AppendGeneratedRouteInfo(StringBuilder sb, ShellMapInfo cls, string indent)
    {
        var descriptionArg = cls.Description != null
            ? $"\"{EscapeString(cls.Description)}\""
            : "\"\"";

        sb.AppendLine($"{indent}new global::Shiny.Infrastructure.GeneratedRouteInfo(");
        sb.AppendLine($"{indent}    \"{EscapeString(cls.Route)}\",");
        sb.AppendLine($"{indent}    {descriptionArg},");

        if (cls.Properties.Any())
        {
            sb.AppendLine($"{indent}    [");
            var properties = cls.Properties.ToList();
            for (int j = 0; j < properties.Count; j++)
            {
                var p = properties[j];
                var requiredLiteral = p.IsRequired ? "true" : "false";
                sb.Append($"{indent}        new global::Shiny.Infrastructure.GeneratedRouteParameter(");
                sb.Append($"\"{EscapeString(p.Name)}\", \"{GetParameterDescription(p)}\", \"{EscapeString(GetParameterTypeName(p))}\", {requiredLiteral})");
                if (j < properties.Count - 1)
                    sb.Append(",");
                sb.AppendLine();
            }
            sb.Append($"{indent}    ]");
        }
        else
        {
            sb.Append($"{indent}    []");
        }

        sb.Append(")");
    }

    static void GenerateAiMauiShellToolsClass(StringBuilder sb, System.Collections.Generic.List<ShellMapInfo> aiClasses, GeneratorOptions options)
    {
        sb.AppendLine();
        sb.AppendLine("}"); // close the static extensions class
        sb.AppendLine();

        sb.AppendLine("/// <summary>");
        sb.AppendLine("/// Provides AI tools and a pre-formatted prompt for route discovery and navigation.");
        sb.AppendLine("/// Register this class in DI and inject it where AI chat functionality is needed.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine($"public class {options.AiToolsClassName}");
        sb.AppendLine("{");
        sb.AppendLine("    readonly global::Shiny.INavigator _navigator;");
        sb.AppendLine();

        // Prompt property
        var promptBuilder = new StringBuilder();
        promptBuilder.Append("Available routes:\\n");
        foreach (var cls in aiClasses)
        {
            promptBuilder.Append($"- Route \\\"{EscapeString(cls.Route)}\\\": {EscapeString(cls.Description)}\\n");
            promptBuilder.Append("  Parameters:\\n");
            foreach (var p in cls.Properties)
            {
                var desc = GetParameterDescription(p);
                var typeName = GetParameterTypeName(p);
                var req = p.IsRequired ? "required" : "optional";
                promptBuilder.Append($"    - {EscapeString(p.Name)} ({EscapeString(typeName)}, {req}): {desc}\\n");
            }
        }

        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// A pre-formatted prompt string describing all AI-applicable routes, their descriptions, and parameters.");
        sb.AppendLine("    /// Designed to be included in an AI system message so the model knows which routes are available without calling a discovery tool first.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine($"    public string Prompt {{ get; }} = \"{promptBuilder}\";");
        sb.AppendLine();

        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// AI tools for route discovery and navigation, ready to use with Microsoft.Extensions.AI ChatOptions.Tools.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    public global::Microsoft.Extensions.AI.AITool[] Tools { get; }");
        sb.AppendLine();

        // Constructor
        sb.AppendLine($"    public {options.AiToolsClassName}(global::Shiny.INavigator navigator)");
        sb.AppendLine("    {");
        sb.AppendLine("        _navigator = navigator;");
        sb.AppendLine("        Tools =");
        sb.AppendLine("        [");
        sb.AppendLine("            global::Microsoft.Extensions.AI.AIFunctionFactory.Create(");
        sb.AppendLine("                () => GetAiToolApplicableGeneratedRoutes(),");
        sb.AppendLine("                name: \"GetRoutes\",");
        sb.AppendLine("                description: \"Returns a list of available application routes with their descriptions and parameter schemas\"),");
        sb.AppendLine("            global::Microsoft.Extensions.AI.AIFunctionFactory.Create(");
        sb.AppendLine($"                (string route, global::System.Collections.Generic.Dictionary<string, string>? args) => {options.AiNavigateMethodName}(route, args),");
        sb.AppendLine($"                name: \"{options.AiNavigateMethodName}\",");

        // Build a rich description for the navigate tool
        var navDescBuilder = new StringBuilder();
        navDescBuilder.Append("Navigate to a route in the application. The 'args' parameter is a dictionary of key-value pairs where keys are parameter names from the route schema. ");
        navDescBuilder.Append("Available routes and their parameters: ");
        foreach (var cls in aiClasses)
        {
            navDescBuilder.Append($"{cls.Route}(");
            var props = cls.Properties.ToList();
            for (int j = 0; j < props.Count; j++)
            {
                var p = props[j];
                navDescBuilder.Append(p.Name);
                if (p.IsEnum && !p.EnumValues.IsDefaultOrEmpty)
                    navDescBuilder.Append($": {string.Join("|", p.EnumValues)}");
                if (!p.IsRequired)
                    navDescBuilder.Append("?");
                if (j < props.Count - 1)
                    navDescBuilder.Append(", ");
            }
            navDescBuilder.Append(") ");
        }

        sb.AppendLine($"                description: \"{EscapeString(navDescBuilder.ToString().TrimEnd())}\")");
        sb.AppendLine("        ];");
        sb.AppendLine("    }");
        sb.AppendLine();

        // GetAiToolApplicableGeneratedRoutes
        sb.AppendLine("    [global::System.ComponentModel.Description(\"This provides a list of AI tool applicable routes - routes that have descriptions and parameters that an AI can populate from user intent\")]");
        sb.AppendLine("    public global::Shiny.Infrastructure.GeneratedRouteInfo[] GetAiToolApplicableGeneratedRoutes() =>");
        sb.AppendLine("    [");

        for (int i = 0; i < aiClasses.Count; i++)
        {
            var cls = aiClasses[i];
            sb.AppendLine($"        new global::Shiny.Infrastructure.GeneratedRouteInfo(");
            sb.AppendLine($"            \"{EscapeString(cls.Route)}\",");
            sb.AppendLine($"            \"{EscapeString(cls.Description)}\",");
            sb.AppendLine("            [");

            var properties = cls.Properties.ToList();
            for (int j = 0; j < properties.Count; j++)
            {
                var p = properties[j];
                var requiredLiteral = p.IsRequired ? "true" : "false";
                sb.Append($"                new global::Shiny.Infrastructure.GeneratedRouteParameter(");
                sb.Append($"\"{EscapeString(p.Name)}\", \"{GetParameterDescription(p)}\", \"{EscapeString(GetParameterTypeName(p))}\", {requiredLiteral})");
                if (j < properties.Count - 1)
                    sb.Append(",");
                sb.AppendLine();
            }

            sb.Append("            ]");
            sb.Append(")");
            if (i < aiClasses.Count - 1)
                sb.Append(",");
            sb.AppendLine();
        }

        sb.AppendLine("    ];");
        sb.AppendLine();

        // NavigateToRoute method
        sb.AppendLine($"    [global::System.ComponentModel.Description(\"Navigate to a route in the application, passing parameters as key-value pairs. Returns a confirmation message.\")]");
        sb.AppendLine($"    public async global::System.Threading.Tasks.Task<string> {options.AiNavigateMethodName}(");
        sb.AppendLine("        [global::System.ComponentModel.Description(\"The route name to navigate to\")] string route,");
        sb.AppendLine("        [global::System.ComponentModel.Description(\"Route parameters as key-value pairs where keys are parameter names from GetGeneratedRouteInfo\")] global::System.Collections.Generic.Dictionary<string, string>? args = null)");
        sb.AppendLine("    {");
        // Declared once outside the switch - every case assigns it, and case labels share a scope.
        sb.AppendLine("        bool navigated;");
        sb.AppendLine("        switch (route)");
        sb.AppendLine("        {");

        foreach (var cls in aiClasses)
        {
            sb.AppendLine($"            case \"{EscapeString(cls.Route)}\":");
            if (cls.Properties.Any())
            {
                sb.AppendLine($"                navigated = await _navigator.NavigateTo<{cls.ViewModelFullName}>(vm =>");
                sb.AppendLine("                {");
                sb.AppendLine("                    if (args != null)");
                sb.AppendLine("                    {");

                foreach (var p in cls.Properties)
                {
                    sb.AppendLine($"                        if (args.TryGetValue(\"{EscapeString(p.Name)}\", out var _{ToCamelCase(p.Name)}))");
                    sb.AppendLine($"                            vm.{p.Name} = {GenerateConversion(p, $"_{ToCamelCase(p.Name)}")};");
                }

                sb.AppendLine("                    }");
                sb.AppendLine("                });");
            }
            else
            {
                sb.AppendLine($"                navigated = await _navigator.NavigateTo<{cls.ViewModelFullName}>();");
            }
            // A guard can turn the agent away just like it turns a button tap away - saying so is
            // more useful to the model than a success message that was not true.
            sb.AppendLine($"                return navigated");
            sb.AppendLine($"                    ? $\"Successfully navigated to {EscapeString(cls.Route)}\"");
            sb.AppendLine($"                    : $\"Navigation to {EscapeString(cls.Route)} was blocked by the application\";");
        }

        sb.AppendLine("            default:");
        sb.AppendLine("                return $\"Unknown route: {route}\";");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine("}"); // close AiMauiShellTools class
        sb.AppendLine();

        // Generate AddAiTools extension method on ShinyAppBuilder
        sb.AppendLine($"public static class {options.AiToolsClassName}Extensions");
        sb.AppendLine("{");
        sb.AppendLine("    /// <summary>");
        sb.AppendLine($"    /// Registers <see cref=\"{options.AiToolsClassName}\"/> as a singleton in the service collection.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine($"    public static global::Shiny.ShinyAppBuilder AddAiTools(this global::Shiny.ShinyAppBuilder builder)");
        sb.AppendLine("    {");
        sb.AppendLine($"        builder.MauiBuilder.Services.AddSingleton<{options.AiToolsClassName}>();");
        sb.AppendLine("        return builder;");
        sb.AppendLine("    }");

        // The caller adds the final "}" to close this class
    }

    static string ToCamelCase(string text)
    {
        if (string.IsNullOrEmpty(text) || char.IsLower(text[0]))
            return text;
        return char.ToLower(text[0]) + text.Substring(1);
    }

    static string EscapeXml(string text)
    {
        return text
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;");
    }

    static string EscapeString(string text)
    {
        return text
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"");
    }

    static string GetDefaultValue(string typeName)
    {
        return typeName.EndsWith("?") || typeName == "string" ? "null" : "default";
    }

    static string GetParameterTypeName(ShellPropertyInfo p)
    {
        if (p.IsEnum)
            return "string";
        return p.TypeName;
    }

    static string GetParameterDescription(ShellPropertyInfo p)
    {
        var desc = p.Description != null ? EscapeString(p.Description) : "";
        if (p.IsEnum && !p.EnumValues.IsDefaultOrEmpty)
        {
            var values = string.Join(", ", p.EnumValues);
            desc = string.IsNullOrEmpty(desc)
                ? $"Must be one of: {values}"
                : $"{desc}. Must be one of: {values}";
        }
        return desc;
    }

    static string GenerateConversion(ShellPropertyInfo prop, string varName)
    {
        var typeName = prop.TypeName;

        // Strip nullable wrapper for conversion logic
        var baseType = typeName.EndsWith("?") ? typeName.Substring(0, typeName.Length - 1) : typeName;

        if (prop.IsEnum)
            return $"(global::{baseType})global::System.Enum.Parse(typeof(global::{baseType}), {varName}, true)";

        return baseType switch
        {
            "string" => varName,
            "int" or "System.Int32" => $"int.Parse({varName})",
            "long" or "System.Int64" => $"long.Parse({varName})",
            "short" or "System.Int16" => $"short.Parse({varName})",
            "byte" or "System.Byte" => $"byte.Parse({varName})",
            "float" or "System.Single" => $"float.Parse({varName})",
            "double" or "System.Double" => $"double.Parse({varName})",
            "decimal" or "System.Decimal" => $"decimal.Parse({varName})",
            "bool" or "System.Boolean" => $"bool.Parse({varName})",
            "System.Guid" => $"global::System.Guid.Parse({varName})",
            "System.DateTime" => $"global::System.DateTime.Parse({varName})",
            "System.DateTimeOffset" => $"global::System.DateTimeOffset.Parse({varName})",
            "System.TimeSpan" => $"global::System.TimeSpan.Parse({varName})",
            "System.Uri" => $"new global::System.Uri({varName})",
            _ => $"({typeName})global::System.Convert.ChangeType({varName}, typeof({baseType}))"
        };
    }
}

record GeneratorOptions(
    bool GenerateRouteConstants,
    bool GenerateNavExtensions,
    bool GenerateAiExtensions,
    string AiExtensionsClassName,
    string AiNavigateMethodName,
    string AiToolsClassName,
    string AppLinkSchemes,
    string AppLinkDomains
);

record ShellMapInfo(
    string ViewModelName,
    string ViewModelFullName,
    string PageTypeName,
    string PageTypeFullName,
    string Route,
    string GeneratedName,
    bool RegisterRoute,
    string Description,
    ImmutableArray<ShellPropertyInfo> Properties,
    Location? AttributeLocation,
    string? DialogResultTypeFullName,
    ImmutableArray<string> AppLinks,
    string? Shortcut,
    string? ShortcutSubtitle,
    string? ShortcutIcon,
    int ShortcutOrder
);

record ShellPropertyInfo(
    string Name,
    string TypeName,
    bool IsRequired,
    string Description,
    bool IsEnum = false,
    ImmutableArray<string> EnumValues = default
);