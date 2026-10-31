using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Shiny.Maui.Shell.SourceGenerators;
using Shouldly;

namespace Shiny.Maui.Shell.Tests;

public class ShinyShellGeneratorTests
{
    const string StubTypes = @"
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;
using Shiny;

namespace Shiny
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public sealed class ShellMapAttribute<TPage> : Attribute
    {
        public ShellMapAttribute(string route = null, bool registerRoute = true, string description = null, string[] appLinks = null) { }
        public string Shortcut { get; set; }
        public string ShortcutSubtitle { get; set; }
        public string ShortcutIcon { get; set; }
        public int ShortcutOrder { get; set; }
    }

    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public sealed class ShellPropertyAttribute : Attribute
    {
        public ShellPropertyAttribute(string description = null, bool required = true) { }
    }

    public interface INavigationBuilder
    {
        INavigationBuilder PopBack(int count = 1);
        INavigationBuilder Add<TViewModel>() where TViewModel : class;
        INavigationBuilder Add<TViewModel>(Action<TViewModel> configure) where TViewModel : class;
        INavigationBuilder Add(string routeName);
        Task Navigate();
    }

    public interface IDialogAware<out T>
    {
        event EventHandler<T> Completed;
        event EventHandler Cancelled;
    }

    public readonly struct DialogResult<T>
    {
        public bool IsCancelled { get; }
        public T Value { get; }
    }

    public interface INavigator
    {
        INavigationBuilder CreateBuilder(bool fromRoot = false);
        Task NavigateTo(string route, bool relativeNavigation = true, params IEnumerable<(string Key, object Value)> args);
        Task NavigateTo<TViewModel>(Action<TViewModel> configure = null, bool relativeNavigation = true);
        Task NavigateTo<TViewModel>(Action<TViewModel> configure = null, bool relativeNavigation = true, params IEnumerable<(string Key, object Value)> args);
        Task<DialogResult<T>> ShowDialog<TViewModel, T>(Action<TViewModel> configure = null, System.Threading.CancellationToken cancellationToken = default) where TViewModel : class, IDialogAware<T>;
    }

    public sealed class ShinyAppBuilder
    {
        public ShinyAppBuilder Add<TPage, TViewModel>(string route = null, bool registerRoute = true) => this;
        public ShinyAppBuilder AddAppLink<TViewModel>(string template, System.Func<TViewModel, System.Collections.Generic.IReadOnlyDictionary<string, string>, bool> apply) where TViewModel : class => this;
        public ShinyAppBuilder AddAppShortcut<TViewModel>(string title, string subtitle = null, string icon = null, int order = 0, string id = null, System.Action<TViewModel> configure = null) where TViewModel : class => this;
        public ShinyAppBuilder AddRouteInfo(Shiny.Infrastructure.GeneratedRouteInfo info) => this;
    }
}

namespace Shiny.Infrastructure
{
    public record GeneratedRouteInfo(string Route, string Description, GeneratedRouteParameter[] Parameters);
    public record GeneratedRouteParameter(string ParameterName, string Description, string TypeName, bool IsRequired);
}

namespace Microsoft.Maui.Controls
{
    public class Page { }
}

namespace Microsoft.Extensions.AI
{
    public class AITool { }
    public static class AIFunctionFactory
    {
        public static AITool Create(Delegate func, string name = null, string description = null) => new AITool();
    }
}
";

    #region Route Constant Generation

    [Fact]
    public void RouteConstants_DefaultRoute_UsesPageNameWithoutPageSuffix()
    {
        var source = StubTypes + @"

namespace TestApp
{
    public class HomePage : Microsoft.Maui.Controls.Page { }

    [ShellMap<HomePage>]
    public class HomeViewModel : System.ComponentModel.INotifyPropertyChanged
    {
        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    }
}";
        var result = RunGenerator(source);
        var routesSource = GetGeneratedSource(result, "Routes.g.cs");

        routesSource.ShouldContain("public const string Home = \"HomePage\";");
    }

    [Fact]
    public void RouteConstants_ExplicitRoute_UsesRouteAsConstantName()
    {
        var source = StubTypes + @"

namespace TestApp
{
    public class HomePage : Microsoft.Maui.Controls.Page { }

    [ShellMap<HomePage>(""Dashboard"")]
    public class HomeViewModel : System.ComponentModel.INotifyPropertyChanged
    {
        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    }
}";
        var result = RunGenerator(source);
        var routesSource = GetGeneratedSource(result, "Routes.g.cs");

        routesSource.ShouldContain("public const string Dashboard = \"Dashboard\";");
        routesSource.ShouldNotContain("Home");
    }

    [Fact]
    public void RouteConstants_NamedRouteParameter_UsesRouteAsConstantName()
    {
        var source = StubTypes + @"

namespace TestApp
{
    public class SettingsPage : Microsoft.Maui.Controls.Page { }

    [ShellMap<SettingsPage>(route: ""Preferences"")]
    public class SettingsViewModel : System.ComponentModel.INotifyPropertyChanged
    {
        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    }
}";
        var result = RunGenerator(source);
        var routesSource = GetGeneratedSource(result, "Routes.g.cs");

        routesSource.ShouldContain("public const string Preferences = \"Preferences\";");
    }

    #endregion

    #region Disable Route Constants

    [Fact]
    public void RouteConstants_DisabledViaProperty_NotGenerated()
    {
        var source = StubTypes + @"

namespace TestApp
{
    public class HomePage : Microsoft.Maui.Controls.Page { }

    [ShellMap<HomePage>(""Dashboard"")]
    public class HomeViewModel : System.ComponentModel.INotifyPropertyChanged
    {
        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    }
}";
        var result = RunGenerator(source, ("ShinyMauiShell_GenerateRouteConstants", "false"));

        GetGeneratedSourceOrDefault(result, "Routes.g.cs").ShouldBeNull();
        GetGeneratedSource(result, "NavigationBuilderExtensions.g.cs").ShouldNotBeNull();
    }

    [Fact]
    public void RouteConstants_EmptyProperty_StillGenerated()
    {
        var source = StubTypes + @"

namespace TestApp
{
    public class HomePage : Microsoft.Maui.Controls.Page { }

    [ShellMap<HomePage>(""Dashboard"")]
    public class HomeViewModel : System.ComponentModel.INotifyPropertyChanged
    {
        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    }
}";
        var result = RunGenerator(source, ("ShinyMauiShell_GenerateRouteConstants", ""));

        GetGeneratedSource(result, "Routes.g.cs").ShouldNotBeNull();
    }

    [Fact]
    public void RouteConstants_MissingProperty_StillGenerated()
    {
        var source = StubTypes + @"

namespace TestApp
{
    public class HomePage : Microsoft.Maui.Controls.Page { }

    [ShellMap<HomePage>(""Dashboard"")]
    public class HomeViewModel : System.ComponentModel.INotifyPropertyChanged
    {
        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    }
}";
        var result = RunGenerator(source);

        GetGeneratedSource(result, "Routes.g.cs").ShouldNotBeNull();
    }

    #endregion

    #region Disable Nav Extensions

    [Fact]
    public void NavExtensions_DisabledViaProperty_NotGenerated()
    {
        var source = StubTypes + @"

namespace TestApp
{
    public class HomePage : Microsoft.Maui.Controls.Page { }

    [ShellMap<HomePage>(""Dashboard"")]
    public class HomeViewModel : System.ComponentModel.INotifyPropertyChanged
    {
        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    }
}";
        var result = RunGenerator(source, ("ShinyMauiShell_GenerateNavExtensions", "false"));

        GetGeneratedSourceOrDefault(result, "NavigationExtensions.g.cs").ShouldBeNull();
        GetGeneratedSourceOrDefault(result, "NavigationBuilderExtensions.g.cs").ShouldBeNull();
        GetGeneratedSourceOrDefault(result, "NavigationBuilderNavExtensions.g.cs").ShouldBeNull();
        GetGeneratedSource(result, "Routes.g.cs").ShouldNotBeNull();
        result.Diagnostics.ShouldContain(d => d.Id == "SHINY002");
    }

    [Fact]
    public void NavExtensions_EmptyProperty_StillGenerated()
    {
        var source = StubTypes + @"

namespace TestApp
{
    public class HomePage : Microsoft.Maui.Controls.Page { }

    [ShellMap<HomePage>(""Dashboard"")]
    public class HomeViewModel : System.ComponentModel.INotifyPropertyChanged
    {
        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    }
}";
        var result = RunGenerator(source, ("ShinyMauiShell_GenerateNavExtensions", ""));

        GetGeneratedSource(result, "NavigationExtensions.g.cs").ShouldNotBeNull();
    }

    [Fact]
    public void BothDisabled_GeneratesNothing()
    {
        var source = StubTypes + @"

namespace TestApp
{
    public class HomePage : Microsoft.Maui.Controls.Page { }

    [ShellMap<HomePage>]
    public class HomeViewModel : System.ComponentModel.INotifyPropertyChanged
    {
        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    }
}";
        var result = RunGenerator(source,
            ("ShinyMauiShell_GenerateRouteConstants", "false"),
            ("ShinyMauiShell_GenerateNavExtensions", "false"));

        GetGeneratedSourceOrDefault(result, "Routes.g.cs").ShouldBeNull();
        GetGeneratedSourceOrDefault(result, "NavigationExtensions.g.cs").ShouldBeNull();
        GetGeneratedSourceOrDefault(result, "NavigationBuilderExtensions.g.cs").ShouldBeNull();
        GetGeneratedSourceOrDefault(result, "NavigationBuilderNavExtensions.g.cs").ShouldBeNull();
        result.Diagnostics.ShouldContain(d => d.Id == "SHINY002");
    }

    [Fact]
    public void NavExtensions_Enabled_DoesNotReportDisabledDiagnostic()
    {
        // SHINY002 signals that AddGeneratedMaps was skipped because nav extensions are disabled.
        // With nav extensions on (the default) and AI extensions off (also the default), it must
        // NOT fire — AddGeneratedMaps is generated.
        var source = StubTypes + @"

namespace TestApp
{
    public class HomePage : Microsoft.Maui.Controls.Page { }

    [ShellMap<HomePage>(""Dashboard"")]
    public class HomeViewModel : System.ComponentModel.INotifyPropertyChanged
    {
        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    }
}";
        var result = RunGenerator(source);

        GetGeneratedSource(result, "NavigationBuilderExtensions.g.cs").ShouldNotBeNull();
        result.Diagnostics.ShouldNotContain(d => d.Id == "SHINY002");
    }

    #endregion

    #region Navigation Extension Method Naming

    [Fact]
    public void NavExtensions_DefaultRoute_MethodUsesPageName()
    {
        var source = StubTypes + @"

namespace TestApp
{
    public class HomePage : Microsoft.Maui.Controls.Page { }

    [ShellMap<HomePage>]
    public class HomeViewModel : System.ComponentModel.INotifyPropertyChanged
    {
        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    }
}";
        var result = RunGenerator(source);
        var navSource = GetGeneratedSource(result, "NavigationExtensions.g.cs");

        navSource.ShouldContain("NavigateToHome");
    }

    [Fact]
    public void NavExtensions_ExplicitRoute_MethodUsesRouteName()
    {
        var source = StubTypes + @"

namespace TestApp
{
    public class HomePage : Microsoft.Maui.Controls.Page { }

    [ShellMap<HomePage>(""Dashboard"")]
    public class HomeViewModel : System.ComponentModel.INotifyPropertyChanged
    {
        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    }
}";
        var result = RunGenerator(source);
        var navSource = GetGeneratedSource(result, "NavigationExtensions.g.cs");

        navSource.ShouldContain("NavigateToDashboard");
        navSource.ShouldNotContain("NavigateToHome");
    }

    #endregion

    #region Builder Extensions Use String Literals

    [Fact]
    public void BuilderExtensions_UsesStringLiterals_NotRouteConstants()
    {
        var source = StubTypes + @"

namespace TestApp
{
    public class HomePage : Microsoft.Maui.Controls.Page { }

    [ShellMap<HomePage>(""Dashboard"")]
    public class HomeViewModel : System.ComponentModel.INotifyPropertyChanged
    {
        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    }
}";
        var result = RunGenerator(source);
        var builderSource = GetGeneratedSource(result, "NavigationBuilderExtensions.g.cs");

        builderSource.ShouldContain("\"Dashboard\"");
        builderSource.ShouldNotContain("Routes.");
    }

    [Fact]
    public void BuilderExtensions_RegisterRouteFalse_PassesParameter()
    {
        var source = StubTypes + @"

namespace TestApp
{
    public class MainPage : Microsoft.Maui.Controls.Page { }

    [ShellMap<MainPage>(registerRoute: false)]
    public class MainViewModel : System.ComponentModel.INotifyPropertyChanged
    {
        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    }
}";
        var result = RunGenerator(source);
        var builderSource = GetGeneratedSource(result, "NavigationBuilderExtensions.g.cs");

        builderSource.ShouldContain("registerRoute: false");
    }

    #endregion

    #region No Maps Does Not Generate Builder

    [Fact]
    public void NoShellMaps_DoesNotGenerateBuilderExtensions()
    {
        var source = StubTypes + @"

namespace TestApp
{
    public class HomePage : Microsoft.Maui.Controls.Page { }
}";
        var result = RunGenerator(source);

        GetGeneratedSourceOrDefault(result, "NavigationBuilderExtensions.g.cs").ShouldBeNull();
    }

    #endregion

    #region Invalid Route Name Diagnostic

    [Fact]
    public void InvalidRoute_StartsWithDigit_ReportsError()
    {
        var source = StubTypes + @"

namespace TestApp
{
    public class HomePage : Microsoft.Maui.Controls.Page { }

    [ShellMap<HomePage>(""123invalid"")]
    public class HomeViewModel : System.ComponentModel.INotifyPropertyChanged
    {
        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    }
}";
        var result = RunGenerator(source);

        result.Diagnostics.ShouldContain(d => d.Id == "SHINY001");
    }

    [Fact]
    public void InvalidRoute_ContainsHyphen_ReportsError()
    {
        var source = StubTypes + @"

namespace TestApp
{
    public class MyPage : Microsoft.Maui.Controls.Page { }

    [ShellMap<MyPage>(""my-route"")]
    public class MyViewModel : System.ComponentModel.INotifyPropertyChanged
    {
        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    }
}";
        var result = RunGenerator(source);

        result.Diagnostics.ShouldContain(d => d.Id == "SHINY001");
    }

    [Fact]
    public void InvalidRoute_ExcludedFromGeneration()
    {
        var source = StubTypes + @"

namespace TestApp
{
    public class GoodPage : Microsoft.Maui.Controls.Page { }
    public class BadPage : Microsoft.Maui.Controls.Page { }

    [ShellMap<GoodPage>(""Valid"")]
    public class GoodViewModel : System.ComponentModel.INotifyPropertyChanged
    {
        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    }

    [ShellMap<BadPage>(""123bad"")]
    public class BadViewModel : System.ComponentModel.INotifyPropertyChanged
    {
        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    }
}";
        var result = RunGenerator(source);
        var routesSource = GetGeneratedSource(result, "Routes.g.cs");

        routesSource.ShouldContain("Valid");
        routesSource.ShouldNotContain("123bad");
        routesSource.ShouldNotContain("Bad");
    }

    [Fact]
    public void ValidRoute_NoDiagnostic()
    {
        var source = StubTypes + @"

namespace TestApp
{
    public class HomePage : Microsoft.Maui.Controls.Page { }

    [ShellMap<HomePage>(""Dashboard"")]
    public class HomeViewModel : System.ComponentModel.INotifyPropertyChanged
    {
        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    }
}";
        var result = RunGenerator(source);

        result.Diagnostics.ShouldNotContain(d => d.Id == "SHINY001");
    }

    #endregion

    #region NavigationBuilder Extensions

    [Fact]
    public void BuilderNavExtensions_DefaultRoute_GeneratesAddMethod()
    {
        var source = StubTypes + @"

namespace TestApp
{
    public class HomePage : Microsoft.Maui.Controls.Page { }

    [ShellMap<HomePage>]
    public class HomeViewModel : System.ComponentModel.INotifyPropertyChanged
    {
        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    }
}";
        var result = RunGenerator(source);
        var builderNavSource = GetGeneratedSource(result, "NavigationBuilderNavExtensions.g.cs");

        builderNavSource.ShouldContain("AddHome");
        builderNavSource.ShouldContain("this global::Shiny.INavigationBuilder builder");
        builderNavSource.ShouldContain("builder.Add<TestApp.HomeViewModel>()");
    }

    [Fact]
    public void BuilderNavExtensions_WithProperties_GeneratesParameterizedMethod()
    {
        var source = StubTypes + @"

namespace TestApp
{
    public class DetailPage : Microsoft.Maui.Controls.Page { }

    [ShellMap<DetailPage>]
    public class DetailViewModel : System.ComponentModel.INotifyPropertyChanged
    {
        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

        [ShellProperty(required: true)]
        public int Id { get; set; }

        [ShellProperty]
        public string? Name { get; set; }
    }
}";
        var result = RunGenerator(source);
        var builderNavSource = GetGeneratedSource(result, "NavigationBuilderNavExtensions.g.cs");

        builderNavSource.ShouldContain("AddDetail");
        builderNavSource.ShouldContain("int id");
        builderNavSource.ShouldContain("string? name");
        builderNavSource.ShouldContain("builder.Add<TestApp.DetailViewModel>(x => {");
    }

    [Fact]
    public void BuilderNavExtensions_DisabledViaProperty_NotGenerated()
    {
        var source = StubTypes + @"

namespace TestApp
{
    public class HomePage : Microsoft.Maui.Controls.Page { }

    [ShellMap<HomePage>]
    public class HomeViewModel : System.ComponentModel.INotifyPropertyChanged
    {
        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    }
}";
        var result = RunGenerator(source, ("ShinyMauiShell_GenerateNavExtensions", "false"));

        GetGeneratedSourceOrDefault(result, "NavigationBuilderNavExtensions.g.cs").ShouldBeNull();
    }

    [Fact]
    public void BuilderNavExtensions_ExplicitRoute_UsesRouteName()
    {
        var source = StubTypes + @"

namespace TestApp
{
    public class HomePage : Microsoft.Maui.Controls.Page { }

    [ShellMap<HomePage>(""Dashboard"")]
    public class HomeViewModel : System.ComponentModel.INotifyPropertyChanged
    {
        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    }
}";
        var result = RunGenerator(source);
        var builderNavSource = GetGeneratedSource(result, "NavigationBuilderNavExtensions.g.cs");

        builderNavSource.ShouldContain("AddDashboard");
        builderNavSource.ShouldNotContain("AddHome");
    }

    #endregion

    #region Description Attributes

    [Fact]
    public void NavExtensions_WithDescription_GeneratesXmlDocAndDescriptionAttribute()
    {
        var source = StubTypes + @"
namespace TestApp
{
    public class DetailPage : Microsoft.Maui.Controls.Page { }

    [ShellMap<DetailPage>(description: ""Navigate to the detail page"")]
    public class DetailViewModel : System.ComponentModel.INotifyPropertyChanged
    {
        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

        [ShellProperty(""The item text"", true)]
        public string Text { get; set; }
    }
}";
        var result = RunGenerator(source);
        var navSource = GetGeneratedSource(result, "NavigationExtensions.g.cs");

        navSource.ShouldContain("/// <summary>");
        navSource.ShouldContain("/// Navigate to the detail page");
        navSource.ShouldContain("/// </summary>");
        navSource.ShouldContain("/// <param name=\"text\">The item text</param>");
        navSource.ShouldContain("/// <param name=\"relativeNavigation\">");
        navSource.ShouldContain("[global::System.ComponentModel.Description(\"Navigate to the detail page\")]");
        navSource.ShouldContain("[global::System.ComponentModel.Description(\"The item text\")] string text");
    }

    [Fact]
    public void NavExtensions_WithoutDescription_NoXmlDocOrDescriptionAttribute()
    {
        var source = StubTypes + @"
namespace TestApp
{
    public class HomePage : Microsoft.Maui.Controls.Page { }

    [ShellMap<HomePage>]
    public class HomeViewModel : System.ComponentModel.INotifyPropertyChanged
    {
        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    }
}";
        var result = RunGenerator(source);
        var navSource = GetGeneratedSource(result, "NavigationExtensions.g.cs");

        navSource.ShouldNotContain("/// <summary>");
        navSource.ShouldNotContain("[global::System.ComponentModel.Description");
    }

    [Fact]
    public void NavExtensions_PropertyWithoutDescription_NoDescriptionAttributeOnParam()
    {
        var source = StubTypes + @"
namespace TestApp
{
    public class DetailPage : Microsoft.Maui.Controls.Page { }

    [ShellMap<DetailPage>(description: ""A detail page"")]
    public class DetailViewModel : System.ComponentModel.INotifyPropertyChanged
    {
        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

        [ShellProperty(required: true)]
        public string Text { get; set; }
    }
}";
        var result = RunGenerator(source);
        var navSource = GetGeneratedSource(result, "NavigationExtensions.g.cs");

        navSource.ShouldContain("[global::System.ComponentModel.Description(\"A detail page\")]");
        navSource.ShouldContain("/// <param name=\"text\"></param>");
        navSource.ShouldNotContain("[global::System.ComponentModel.Description(\"\")] string text");
        // The param should NOT have a Description attribute since no description was provided
        navSource.ShouldContain(", string text,");
    }

    [Fact]
    public void NavExtensions_RelativeNavigationParam_GetsDescriptionWhenMethodHasDescription()
    {
        var source = StubTypes + @"
namespace TestApp
{
    public class DetailPage : Microsoft.Maui.Controls.Page { }

    [ShellMap<DetailPage>(description: ""Go to detail"")]
    public class DetailViewModel : System.ComponentModel.INotifyPropertyChanged
    {
        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    }
}";
        var result = RunGenerator(source);
        var navSource = GetGeneratedSource(result, "NavigationExtensions.g.cs");

        navSource.ShouldContain("[global::System.ComponentModel.Description(\"If true, it will navigate/stack from where the application currently is otherwise, it will reset the stack to this new route\")] bool relativeNavigation = true");
    }

    [Fact]
    public void NavExtensions_CarryTheInterceptorControlsAndReturnWhetherNavigationHappened()
    {
        var source = StubTypes + @"
namespace TestApp
{
    public class DetailPage : Microsoft.Maui.Controls.Page { }

    [ShellMap<DetailPage>]
    public class DetailViewModel : System.ComponentModel.INotifyPropertyChanged
    {
        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

        [ShellProperty]
        public string Text { get; set; }
    }
}";
        var result = RunGenerator(source);
        var navSource = GetGeneratedSource(result, "NavigationExtensions.g.cs");

        // A generated call must never be the weaker option next to INavigator itself - it returns
        // the same "did this happen" answer and takes the same interceptor controls.
        navSource.ShouldContain("global::System.Threading.Tasks.Task<bool> NavigateToDetail");
        navSource.ShouldContain("bool bypassInterceptors = false");
        navSource.ShouldContain("global::System.Threading.CancellationToken cancellationToken = default");
        navSource.ShouldContain("relativeNavigation, bypassInterceptors, cancellationToken);");
    }

    #endregion

    #region GeneratedRouteInfo Extension

    [Fact]
    public void RouteInfo_GeneratesExtensionMethod()
    {
        var source = StubTypes + @"
namespace TestApp
{
    public class HomePage : Microsoft.Maui.Controls.Page { }

    [ShellMap<HomePage>]
    public class HomeViewModel : System.ComponentModel.INotifyPropertyChanged
    {
        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    }
}";
        var result = RunGenerator(source, ("ShinyMauiShell_GenerateAiExtensions", "true"));
        var routeInfoSource = GetGeneratedSource(result, "AiExtensions.g.cs");

        routeInfoSource.ShouldContain("GetGeneratedRouteInfo");
        routeInfoSource.ShouldContain("this global::Shiny.INavigator navigator");
        routeInfoSource.ShouldContain("[global::System.ComponentModel.Description(\"This provides a list of routes throughout the application\")]");
    }

    [Fact]
    public void RouteInfo_WithDescriptions_IncludesParameters()
    {
        var source = StubTypes + @"
namespace TestApp
{
    public class DetailPage : Microsoft.Maui.Controls.Page { }

    [ShellMap<DetailPage>(description: ""Navigate to detail"")]
    public class DetailViewModel : System.ComponentModel.INotifyPropertyChanged
    {
        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

        [ShellProperty(""Show this text"", true)]
        public string Text { get; set; }

        [ShellProperty(""Show this text2"", true)]
        public string Text2 { get; set; }
    }
}";
        var result = RunGenerator(source, ("ShinyMauiShell_GenerateAiExtensions", "true"));
        var routeInfoSource = GetGeneratedSource(result, "AiExtensions.g.cs");

        routeInfoSource.ShouldContain("\"DetailPage\",");
        routeInfoSource.ShouldContain("\"Navigate to detail\",");
        routeInfoSource.ShouldContain("new global::Shiny.Infrastructure.GeneratedRouteParameter(\"Text\", \"Show this text\", \"string\", true)");
        routeInfoSource.ShouldContain("new global::Shiny.Infrastructure.GeneratedRouteParameter(\"Text2\", \"Show this text2\", \"string\", true)");
    }

    [Fact]
    public void RouteInfo_WithoutPropertyDescriptions_EmptyParameterArray()
    {
        var source = StubTypes + @"
namespace TestApp
{
    public class DetailPage : Microsoft.Maui.Controls.Page { }

    [ShellMap<DetailPage>]
    public class DetailViewModel : System.ComponentModel.INotifyPropertyChanged
    {
        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

        [ShellProperty(required: true)]
        public string Text { get; set; }
    }
}";
        var result = RunGenerator(source, ("ShinyMauiShell_GenerateAiExtensions", "true"));
        var routeInfoSource = GetGeneratedSource(result, "AiExtensions.g.cs");

        routeInfoSource.ShouldContain("new global::Shiny.Infrastructure.GeneratedRouteParameter(\"Text\", \"\", \"string\", true)");
    }

    [Fact]
    public void RouteInfo_MultipleRoutes_GeneratesAll()
    {
        var source = StubTypes + @"
namespace TestApp
{
    public class HomePage : Microsoft.Maui.Controls.Page { }
    public class DetailPage : Microsoft.Maui.Controls.Page { }

    [ShellMap<HomePage>]
    public class HomeViewModel : System.ComponentModel.INotifyPropertyChanged
    {
        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    }

    [ShellMap<DetailPage>(description: ""Detail"")]
    public class DetailViewModel : System.ComponentModel.INotifyPropertyChanged
    {
        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

        [ShellProperty(""The ID"")]
        public int Id { get; set; }
    }
}";
        var result = RunGenerator(source, ("ShinyMauiShell_GenerateAiExtensions", "true"));
        var routeInfoSource = GetGeneratedSource(result, "AiExtensions.g.cs");

        routeInfoSource.ShouldContain("\"HomePage\",");
        routeInfoSource.ShouldContain("\"DetailPage\",");
        routeInfoSource.ShouldContain("\"Detail\",");
        routeInfoSource.ShouldContain("new global::Shiny.Infrastructure.GeneratedRouteParameter(\"Id\", \"The ID\", \"int\", true)");
    }

    [Fact]
    public void RouteInfo_NoDescription_EmptyDescriptionString()
    {
        var source = StubTypes + @"
namespace TestApp
{
    public class HomePage : Microsoft.Maui.Controls.Page { }

    [ShellMap<HomePage>]
    public class HomeViewModel : System.ComponentModel.INotifyPropertyChanged
    {
        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    }
}";
        var result = RunGenerator(source, ("ShinyMauiShell_GenerateAiExtensions", "true"));
        var routeInfoSource = GetGeneratedSource(result, "AiExtensions.g.cs");

        routeInfoSource.ShouldContain("\"HomePage\",");
        routeInfoSource.ShouldContain("\"\",");
        routeInfoSource.ShouldContain("[]");
    }

    [Fact]
    public void RouteInfo_WithDescription_IncludesDescriptionInConstructor()
    {
        var source = StubTypes + @"
namespace TestApp
{
    public class DetailPage : Microsoft.Maui.Controls.Page { }

    [ShellMap<DetailPage>(description: ""This is detail"")]
    public class DetailViewModel : System.ComponentModel.INotifyPropertyChanged
    {
        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    }
}";
        var result = RunGenerator(source, ("ShinyMauiShell_GenerateAiExtensions", "true"));
        var routeInfoSource = GetGeneratedSource(result, "AiExtensions.g.cs");

        routeInfoSource.ShouldContain("\"DetailPage\",");
        routeInfoSource.ShouldContain("\"This is detail\",");
        routeInfoSource.ShouldContain("[]");
    }

    [Fact]
    public void RouteInfo_MethodHasDescriptionAttribute()
    {
        var source = StubTypes + @"
namespace TestApp
{
    public class HomePage : Microsoft.Maui.Controls.Page { }

    [ShellMap<HomePage>]
    public class HomeViewModel : System.ComponentModel.INotifyPropertyChanged
    {
        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    }
}";
        var result = RunGenerator(source, ("ShinyMauiShell_GenerateAiExtensions", "true"));
        var routeInfoSource = GetGeneratedSource(result, "AiExtensions.g.cs");

        routeInfoSource.ShouldContain("[global::System.ComponentModel.Description(\"This provides a list of routes throughout the application\")]");
    }

    [Fact]
    public void RouteInfo_UsesFullyQualifiedTypes()
    {
        var source = StubTypes + @"
namespace TestApp
{
    public class DetailPage : Microsoft.Maui.Controls.Page { }

    [ShellMap<DetailPage>(description: ""A page"")]
    public class DetailViewModel : System.ComponentModel.INotifyPropertyChanged
    {
        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

        [ShellProperty(""An id"")]
        public int Id { get; set; }
    }
}";
        var result = RunGenerator(source, ("ShinyMauiShell_GenerateAiExtensions", "true"));
        var routeInfoSource = GetGeneratedSource(result, "AiExtensions.g.cs");

        routeInfoSource.ShouldContain("global::Shiny.Infrastructure.GeneratedRouteInfo[]");
        routeInfoSource.ShouldContain("global::Shiny.INavigator");
        routeInfoSource.ShouldContain("new global::Shiny.Infrastructure.GeneratedRouteInfo(");
        routeInfoSource.ShouldContain("new global::Shiny.Infrastructure.GeneratedRouteParameter(");
        routeInfoSource.ShouldContain("global::System.ComponentModel.Description");
    }

    [Fact]
    public void RouteInfo_MixedDescriptions_AllPropsIncludedWithTypeAndRequired()
    {
        var source = StubTypes + @"
namespace TestApp
{
    public class DetailPage : Microsoft.Maui.Controls.Page { }

    [ShellMap<DetailPage>(description: ""Detail"")]
    public class DetailViewModel : System.ComponentModel.INotifyPropertyChanged
    {
        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

        [ShellProperty(""Has description"")]
        public string Name { get; set; }

        [ShellProperty(required: true)]
        public int Id { get; set; }
    }
}";
        var result = RunGenerator(source, ("ShinyMauiShell_GenerateAiExtensions", "true"));
        var routeInfoSource = GetGeneratedSource(result, "AiExtensions.g.cs");

        routeInfoSource.ShouldContain("new global::Shiny.Infrastructure.GeneratedRouteParameter(\"Name\", \"Has description\", \"string\", true)");
        routeInfoSource.ShouldContain("new global::Shiny.Infrastructure.GeneratedRouteParameter(\"Id\", \"\", \"int\", true)");
    }

    [Fact]
    public void RouteInfo_DisabledViaProperty_NotGenerated()
    {
        var source = StubTypes + @"
namespace TestApp
{
    public class HomePage : Microsoft.Maui.Controls.Page { }

    [ShellMap<HomePage>]
    public class HomeViewModel : System.ComponentModel.INotifyPropertyChanged
    {
        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    }
}";
        var result = RunGenerator(source, ("ShinyMauiShell_GenerateNavExtensions", "false"));

        GetGeneratedSourceOrDefault(result, "AiExtensions.g.cs").ShouldBeNull();
    }

    [Fact]
    public void AiExtensions_DisabledViaProperty_NotGenerated()
    {
        var source = StubTypes + @"
namespace TestApp
{
    public class WorkOrderPage : Microsoft.Maui.Controls.Page { }

    [ShellMap<WorkOrderPage>(description: ""Report something broken"")]
    public class WorkOrderViewModel : System.ComponentModel.INotifyPropertyChanged
    {
        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

        [ShellProperty(""Summarize what is broken"")]
        public string Description { get; set; }
    }
}";
        var result = RunGenerator(source, ("ShinyMauiShell_GenerateAiExtensions", "false"));

        GetGeneratedSourceOrDefault(result, "AiExtensions.g.cs").ShouldBeNull();
    }

    [Fact]
    public void AiExtensions_CustomClassName_UsesCustomName()
    {
        var source = StubTypes + @"
namespace TestApp
{
    public class WorkOrderPage : Microsoft.Maui.Controls.Page { }

    [ShellMap<WorkOrderPage>(description: ""Report something broken"")]
    public class WorkOrderViewModel : System.ComponentModel.INotifyPropertyChanged
    {
        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

        [ShellProperty(""Summarize what is broken"")]
        public string Description { get; set; }
    }
}";
        var result = RunGenerator(source,
            ("ShinyMauiShell_GenerateAiExtensions", "true"),
            ("ShinyMauiShell_AiExtensionsClassName", "MyAppRouteExtensions"));
        var routeInfoSource = GetGeneratedSource(result, "AiExtensions.g.cs");

        routeInfoSource.ShouldContain("public static class MyAppRouteExtensions");
        routeInfoSource.ShouldNotContain("public static class AiExtensions");
    }

    [Fact]
    public void AiExtensions_CustomNavigateMethodName_UsesCustomName()
    {
        var source = StubTypes + @"
namespace TestApp
{
    public class WorkOrderPage : Microsoft.Maui.Controls.Page { }

    [ShellMap<WorkOrderPage>(description: ""Report something broken"")]
    public class WorkOrderViewModel : System.ComponentModel.INotifyPropertyChanged
    {
        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

        [ShellProperty(""Summarize what is broken"")]
        public string Description { get; set; }
    }
}";
        var result = RunGenerator(source,
            ("ShinyMauiShell_GenerateAiExtensions", "true"),
            ("ShinyMauiShell_AiNavigateMethodName", "GoToPage"));
        var routeInfoSource = GetGeneratedSource(result, "AiExtensions.g.cs");

        routeInfoSource.ShouldContain("GoToPage(");
        routeInfoSource.ShouldNotContain("NavigateToRoute(");
    }

    [Fact]
    public void AiExtensions_NotSet_NotGenerated()
    {
        var source = StubTypes + @"
namespace TestApp
{
    public class WorkOrderPage : Microsoft.Maui.Controls.Page { }

    [ShellMap<WorkOrderPage>(description: ""Report something broken"")]
    public class WorkOrderViewModel : System.ComponentModel.INotifyPropertyChanged
    {
        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

        [ShellProperty(""Summarize what is broken"")]
        public string Description { get; set; }
    }
}";
        var result = RunGenerator(source);

        GetGeneratedSourceOrDefault(result, "AiExtensions.g.cs").ShouldBeNull();
    }

    [Fact]
    public void AiExtensions_EmptyProperty_NotGenerated()
    {
        var source = StubTypes + @"
namespace TestApp
{
    public class WorkOrderPage : Microsoft.Maui.Controls.Page { }

    [ShellMap<WorkOrderPage>(description: ""Report something broken"")]
    public class WorkOrderViewModel : System.ComponentModel.INotifyPropertyChanged
    {
        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

        [ShellProperty(""Summarize what is broken"")]
        public string Description { get; set; }
    }
}";
        var result = RunGenerator(source, ("ShinyMauiShell_GenerateAiExtensions", ""));

        GetGeneratedSourceOrDefault(result, "AiExtensions.g.cs").ShouldBeNull();
    }

    [Fact]
    public void AiExtensions_ExplicitTrue_Generated()
    {
        var source = StubTypes + @"
namespace TestApp
{
    public class WorkOrderPage : Microsoft.Maui.Controls.Page { }

    [ShellMap<WorkOrderPage>(description: ""Report something broken"")]
    public class WorkOrderViewModel : System.ComponentModel.INotifyPropertyChanged
    {
        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

        [ShellProperty(""Summarize what is broken"")]
        public string Description { get; set; }
    }
}";
        var result = RunGenerator(source, ("ShinyMauiShell_GenerateAiExtensions", "true"));
        var routeInfoSource = GetGeneratedSource(result, "AiExtensions.g.cs");

        routeInfoSource.ShouldContain("public static class AiExtensions");
        routeInfoSource.ShouldContain("GetGeneratedRouteInfo");
        routeInfoSource.ShouldContain("class AiMauiShellTools");
        routeInfoSource.ShouldContain("GetAiToolApplicableGeneratedRoutes");
        routeInfoSource.ShouldContain("NavigateToRoute(");
        routeInfoSource.ShouldContain("AddAiTools");

        // An interceptor can turn the agent away exactly as it turns a button tap away, and the
        // tool has to say so - a success message that was not true is worse than no tool at all.
        routeInfoSource.ShouldContain("was blocked by the application");
    }

    [Fact]
    public void AiExtensions_EnabledButNoDescriptions_StillGeneratesAiMauiShellToolsClass()
    {
        var source = StubTypes + @"
namespace TestApp
{
    public class HomePage : Microsoft.Maui.Controls.Page { }
    public class SettingsPage : Microsoft.Maui.Controls.Page { }

    [ShellMap<HomePage>]
    public class HomeViewModel : System.ComponentModel.INotifyPropertyChanged
    {
        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    }

    [ShellMap<SettingsPage>(registerRoute: false)]
    public class SettingsViewModel : System.ComponentModel.INotifyPropertyChanged
    {
        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    }
}";
        var result = RunGenerator(source, ("ShinyMauiShell_GenerateAiExtensions", "true"));
        var routeInfoSource = GetGeneratedSource(result, "AiExtensions.g.cs");

        routeInfoSource.ShouldContain("class AiMauiShellTools");
        routeInfoSource.ShouldContain("AddAiTools");
        routeInfoSource.ShouldContain("NavigateToRoute(");
        routeInfoSource.ShouldContain("GetAiToolApplicableGeneratedRoutes");
    }

    [Fact]
    public void AiExtensions_DescriptionWithoutProperties_IncludedInAiTools()
    {
        var source = StubTypes + @"
namespace TestApp
{
    public class HomePage : Microsoft.Maui.Controls.Page { }

    [ShellMap<HomePage>(description: ""Go to the home page"")]
    public class HomeViewModel : System.ComponentModel.INotifyPropertyChanged
    {
        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    }
}";
        var result = RunGenerator(source, ("ShinyMauiShell_GenerateAiExtensions", "true"));
        var routeInfoSource = GetGeneratedSource(result, "AiExtensions.g.cs");

        routeInfoSource.ShouldContain("class AiMauiShellTools");
        routeInfoSource.ShouldContain("Go to the home page");
        routeInfoSource.ShouldContain("case \"HomePage\":");
    }

    [Fact]
    public void AiExtensions_PropertyDescriptionsWithoutRouteDescription_ReportsSHINY004()
    {
        var source = StubTypes + @"
namespace TestApp
{
    public class DetailPage : Microsoft.Maui.Controls.Page { }

    [ShellMap<DetailPage>]
    public class DetailViewModel : System.ComponentModel.INotifyPropertyChanged
    {
        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

        [ShellProperty(""The item ID"")]
        public int Id { get; set; }
    }
}";
        var result = RunGenerator(source, ("ShinyMauiShell_GenerateAiExtensions", "true"));

        result.Diagnostics.ShouldContain(d => d.Id == "SHINY004");
    }

    [Fact]
    public void AiExtensions_PropertyDescriptionsWithRouteDescription_NoSHINY004()
    {
        var source = StubTypes + @"
namespace TestApp
{
    public class DetailPage : Microsoft.Maui.Controls.Page { }

    [ShellMap<DetailPage>(description: ""View details"")]
    public class DetailViewModel : System.ComponentModel.INotifyPropertyChanged
    {
        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

        [ShellProperty(""The item ID"")]
        public int Id { get; set; }
    }
}";
        var result = RunGenerator(source, ("ShinyMauiShell_GenerateAiExtensions", "true"));

        result.Diagnostics.ShouldNotContain(d => d.Id == "SHINY004");
    }

    [Fact]
    public void AiExtensions_PropertyDescriptionsWithoutRouteDescription_NoSHINY004WhenAiDisabled()
    {
        var source = StubTypes + @"
namespace TestApp
{
    public class DetailPage : Microsoft.Maui.Controls.Page { }

    [ShellMap<DetailPage>]
    public class DetailViewModel : System.ComponentModel.INotifyPropertyChanged
    {
        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

        [ShellProperty(""The item ID"")]
        public int Id { get; set; }
    }
}";
        var result = RunGenerator(source);

        result.Diagnostics.ShouldNotContain(d => d.Id == "SHINY004");
    }

    [Fact]
    public void AiExtensions_NotSet_NoSHINY003WhenAiPackageMissing()
    {
        var source = StubTypes.Replace(
            "namespace Microsoft.Extensions.AI", "namespace Microsoft.Extensions.AI.Disabled") + @"
namespace TestApp
{
    public class HomePage : Microsoft.Maui.Controls.Page { }

    [ShellMap<HomePage>]
    public class HomeViewModel : System.ComponentModel.INotifyPropertyChanged
    {
        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    }
}";
        var result = RunGenerator(source);

        result.Diagnostics.ShouldNotContain(d => d.Id == "SHINY003");
    }

    [Fact]
    public void AiExtensions_Enabled_GeneratesAiMauiShellToolsClass()
    {
        var source = StubTypes + @"
namespace TestApp
{
    public class WorkOrderPage : Microsoft.Maui.Controls.Page { }

    [ShellMap<WorkOrderPage>(description: ""Report something broken"")]
    public class WorkOrderViewModel : System.ComponentModel.INotifyPropertyChanged
    {
        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

        [ShellProperty(""Summarize what is broken"")]
        public string Description { get; set; }
    }
}";
        var result = RunGenerator(source, ("ShinyMauiShell_GenerateAiExtensions", "true"));
        var routeInfoSource = GetGeneratedSource(result, "AiExtensions.g.cs");

        routeInfoSource.ShouldContain("class AiMauiShellTools");
        routeInfoSource.ShouldContain("GetAiToolApplicableGeneratedRoutes");
        routeInfoSource.ShouldContain("NavigateToRoute(");
        routeInfoSource.ShouldContain("public string Prompt");
        routeInfoSource.ShouldContain("public global::Microsoft.Extensions.AI.AITool[] Tools");
        routeInfoSource.ShouldContain("AiMauiShellTools(global::Shiny.INavigator navigator)");
        routeInfoSource.ShouldContain("Microsoft.Extensions.AI.AIFunctionFactory");
        routeInfoSource.ShouldContain("AddAiTools(this global::Shiny.ShinyAppBuilder builder)");
        routeInfoSource.ShouldContain("AddSingleton<AiMauiShellTools>");
    }

    [Fact]
    public void AiExtensions_AiRoutePrompt_ContainsRouteAndParameterInfo()
    {
        var source = StubTypes + @"
namespace TestApp
{
    public class WorkOrderPage : Microsoft.Maui.Controls.Page { }

    [ShellMap<WorkOrderPage>(description: ""Report something broken"")]
    public class WorkOrderViewModel : System.ComponentModel.INotifyPropertyChanged
    {
        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

        [ShellProperty(""Summarize what is broken"", required: true)]
        public string Description { get; set; }

        [ShellProperty(""The location"", required: false)]
        public string Location { get; set; }
    }
}";
        var result = RunGenerator(source, ("ShinyMauiShell_GenerateAiExtensions", "true"));
        var routeInfoSource = GetGeneratedSource(result, "AiExtensions.g.cs");

        routeInfoSource.ShouldContain("public string Prompt");
        routeInfoSource.ShouldContain("WorkOrderPage");
        routeInfoSource.ShouldContain("Report something broken");
        routeInfoSource.ShouldContain("Description (string, required): Summarize what is broken");
        routeInfoSource.ShouldContain("Location (string, optional): The location");
    }

    [Fact]
    public void AiExtensions_ExplicitlyDisabled_NoAiMethods()
    {
        var source = StubTypes + @"
namespace TestApp
{
    public class WorkOrderPage : Microsoft.Maui.Controls.Page { }

    [ShellMap<WorkOrderPage>(description: ""Report something broken"")]
    public class WorkOrderViewModel : System.ComponentModel.INotifyPropertyChanged
    {
        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

        [ShellProperty(""Summarize what is broken"")]
        public string Description { get; set; }
    }
}";
        var result = RunGenerator(source, ("ShinyMauiShell_GenerateAiExtensions", "false"));

        GetGeneratedSourceOrDefault(result, "AiExtensions.g.cs").ShouldBeNull();
    }

    [Fact]
    public void AiExtensions_CustomToolsClassName_UsesCustomName()
    {
        var source = StubTypes + @"
namespace TestApp
{
    public class WorkOrderPage : Microsoft.Maui.Controls.Page { }

    [ShellMap<WorkOrderPage>(description: ""Report something broken"")]
    public class WorkOrderViewModel : System.ComponentModel.INotifyPropertyChanged
    {
        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

        [ShellProperty(""Summarize what is broken"")]
        public string Description { get; set; }
    }
}";
        var result = RunGenerator(source,
            ("ShinyMauiShell_GenerateAiExtensions", "true"),
            ("ShinyMauiShell_AiToolsClassName", "MyCustomAiTools"));
        var routeInfoSource = GetGeneratedSource(result, "AiExtensions.g.cs");

        routeInfoSource.ShouldContain("class MyCustomAiTools");
        routeInfoSource.ShouldNotContain("class AiMauiShellTools");
        routeInfoSource.ShouldContain("MyCustomAiTools(global::Shiny.INavigator navigator)");
        routeInfoSource.ShouldContain("AddSingleton<MyCustomAiTools>");
    }

    [Fact]
    public void AiExtensions_NavigateToRoute_ConvertsTypesCorrectly()
    {
        var source = StubTypes + @"
namespace TestApp
{
    public class OrderPage : Microsoft.Maui.Controls.Page { }

    [ShellMap<OrderPage>(description: ""Place an order"")]
    public class OrderViewModel : System.ComponentModel.INotifyPropertyChanged
    {
        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

        [ShellProperty(""Item name"")]
        public string Name { get; set; }

        [ShellProperty(""Quantity"", required: true)]
        public int Quantity { get; set; }

        [ShellProperty(""Is urgent"", required: false)]
        public bool IsUrgent { get; set; }

        [ShellProperty(""Unit price"", required: false)]
        public double Price { get; set; }
    }
}";
        var result = RunGenerator(source, ("ShinyMauiShell_GenerateAiExtensions", "true"));
        var routeInfoSource = GetGeneratedSource(result, "AiExtensions.g.cs");

        // string should be assigned directly
        routeInfoSource.ShouldContain("vm.Name = _name;");

        // int should use int.Parse
        routeInfoSource.ShouldContain("vm.Quantity = int.Parse(_quantity);");

        // bool should use bool.Parse
        routeInfoSource.ShouldContain("vm.IsUrgent = bool.Parse(_isUrgent);");

        // double should use double.Parse
        routeInfoSource.ShouldContain("vm.Price = double.Parse(_price);");
    }

    [Fact]
    public void AiExtensions_NavigateToRoute_ConvertsEnumsCorrectly()
    {
        var source = StubTypes + @"
namespace TestApp
{
    public enum Priority { Low, Medium, High, Urgent }

    public class TicketPage : Microsoft.Maui.Controls.Page { }

    [ShellMap<TicketPage>(description: ""Submit a ticket"")]
    public class TicketViewModel : System.ComponentModel.INotifyPropertyChanged
    {
        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

        [ShellProperty(""Summary"")]
        public string Title { get; set; }

        [ShellProperty(""Priority level"")]
        public Priority Priority { get; set; }
    }
}";
        var result = RunGenerator(source, ("ShinyMauiShell_GenerateAiExtensions", "true"));
        var routeInfoSource = GetGeneratedSource(result, "AiExtensions.g.cs");

        // enum should use Enum.Parse with case-insensitive flag
        routeInfoSource.ShouldContain("vm.Priority = (global::TestApp.Priority)global::System.Enum.Parse(typeof(global::TestApp.Priority), _priority, true);");

        // enum metadata should report as "string" type with values in description
        routeInfoSource.ShouldContain("\"string\"");
        routeInfoSource.ShouldContain("Priority level. Must be one of: Low, Medium, High, Urgent");
    }

    #endregion

    #region Dialog Extensions

    const string PickColorSource = @"

namespace TestApp
{
    public class PickColorPage : Microsoft.Maui.Controls.Page { }

    [ShellMap<PickColorPage>(""PickColor"")]
    public class PickColorViewModel : System.ComponentModel.INotifyPropertyChanged, Shiny.IDialogAware<string>
    {
        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
        public event System.EventHandler<string>? Completed;
        public event System.EventHandler? Cancelled;
    }
}";

    [Fact]
    public void DialogExtensions_DialogAwareViewModel_GeneratesInferredWrapper()
    {
        var result = RunGenerator(StubTypes + PickColorSource);
        var dialogSource = GetGeneratedSource(result, "DialogExtensions.g.cs");

        dialogSource.ShouldContain("global::System.Threading.Tasks.Task<global::Shiny.DialogResult<string>> ShowPickColorDialog(this global::Shiny.INavigator navigator");
        dialogSource.ShouldContain("navigator.ShowDialog<TestApp.PickColorViewModel, string>");
        dialogSource.ShouldContain("global::System.Threading.CancellationToken cancellationToken = default");
    }

    [Fact]
    public void DialogExtensions_NonDialogViewModel_NotGenerated()
    {
        var source = StubTypes + @"

namespace TestApp
{
    public class HomePage : Microsoft.Maui.Controls.Page { }

    [ShellMap<HomePage>]
    public class HomeViewModel : System.ComponentModel.INotifyPropertyChanged
    {
        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    }
}";
        var result = RunGenerator(source);

        GetGeneratedSourceOrDefault(result, "DialogExtensions.g.cs").ShouldBeNull();
        GetGeneratedSource(result, "NavigationExtensions.g.cs").ShouldNotBeNull();
    }

    [Fact]
    public void DialogExtensions_ShellProperties_BecomeMethodParameters()
    {
        var source = StubTypes + @"

namespace TestApp
{
    public class ConfirmPage : Microsoft.Maui.Controls.Page { }

    [ShellMap<ConfirmPage>(""Confirm"")]
    public class ConfirmViewModel : System.ComponentModel.INotifyPropertyChanged, Shiny.IDialogAware<bool>
    {
        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
        public event System.EventHandler<bool>? Completed;
        public event System.EventHandler? Cancelled;

        [ShellProperty(required: true)]
        public string Message { get; set; }

        [ShellProperty(required: false)]
        public string AcceptText { get; set; }
    }
}";
        var result = RunGenerator(source);
        var dialogSource = GetGeneratedSource(result, "DialogExtensions.g.cs");

        // required first, then optional with a default, then the token - all defaults trailing
        dialogSource.ShouldContain("ShowConfirmDialog(this global::Shiny.INavigator navigator, string message, string acceptText = null, global::System.Threading.CancellationToken cancellationToken = default)");
        dialogSource.ShouldContain("x.Message = message; x.AcceptText = acceptText;");
        dialogSource.ShouldContain("global::Shiny.DialogResult<bool>");
    }

    [Fact]
    public void DialogExtensions_InheritedDialogAware_IsDetected()
    {
        var source = StubTypes + @"

namespace TestApp
{
    public class PickPage : Microsoft.Maui.Controls.Page { }

    public abstract class DialogBase<T> : System.ComponentModel.INotifyPropertyChanged, Shiny.IDialogAware<T>
    {
        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
        public event System.EventHandler<T>? Completed;
        public event System.EventHandler? Cancelled;
    }

    [ShellMap<PickPage>(""Pick"")]
    public class PickViewModel : DialogBase<int>
    {
    }
}";
        var result = RunGenerator(source);
        var dialogSource = GetGeneratedSource(result, "DialogExtensions.g.cs");

        dialogSource.ShouldContain("global::Shiny.DialogResult<int>");
        dialogSource.ShouldContain("navigator.ShowDialog<TestApp.PickViewModel, int>");
    }

    [Fact]
    public void DialogExtensions_DisabledWithNavExtensions_NotGenerated()
    {
        var result = RunGenerator(StubTypes + PickColorSource, ("ShinyMauiShell_GenerateNavExtensions", "false"));

        GetGeneratedSourceOrDefault(result, "DialogExtensions.g.cs").ShouldBeNull();
    }

    #endregion

    #region Helpers


    #region App Links

    const string AppLinkPage = @"
namespace TestApp
{
    public class ProductPage : Microsoft.Maui.Controls.Page { }
}";

    static string AppLinkSource(string attribute, string properties) => StubTypes + @"
namespace TestApp
{
    public class ProductPage : Microsoft.Maui.Controls.Page { }

    " + attribute + @"
    public class ProductViewModel : System.ComponentModel.INotifyPropertyChanged
    {
        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
        " + properties + @"
    }
}";

    [Fact]
    public void AppLinks_NoTemplates_EmitsNoRegistration()
    {
        var result = RunGenerator(AppLinkSource("[ShellMap<ProductPage>]", ""));
        var maps = GetGeneratedSource(result, "NavigationBuilderExtensions.g.cs");

        maps.ShouldNotContain("AddAppLink");
    }

    [Fact]
    public void AppLinks_SingleTemplate_EmitsRegistration()
    {
        var source = AppLinkSource(
            @"[ShellMap<ProductPage>(appLinks: new[] { ""product/{id}"" })]",
            @"[ShellProperty] public int Id { get; set; }");

        var result = RunGenerator(source, ("ShinyAppLinkSchemes", "myapp"));
        var maps = GetGeneratedSource(result, "NavigationBuilderExtensions.g.cs");

        maps.ShouldContain(@"builder.AddAppLink<TestApp.ProductViewModel>(");
        maps.ShouldContain(@"""product/{id}""");
    }

    [Fact]
    public void AppLinks_CollectionExpressionArgument_IsReadTheSameAsArrayCreation()
    {
        var source = AppLinkSource(
            @"[ShellMap<ProductPage>(appLinks: [""product/{id}""])]",
            @"[ShellProperty] public int Id { get; set; }");

        var result = RunGenerator(source, ("ShinyAppLinkSchemes", "myapp"));
        var maps = GetGeneratedSource(result, "NavigationBuilderExtensions.g.cs");

        maps.ShouldContain(@"""product/{id}""");
    }

    [Fact]
    public void AppLinks_MultipleTemplates_EmitOneRegistrationEach()
    {
        var source = AppLinkSource(
            @"[ShellMap<ProductPage>(appLinks: new[] { ""product/{id}"", ""p/{id}"" })]",
            @"[ShellProperty] public int Id { get; set; }");

        var result = RunGenerator(source, ("ShinyAppLinkSchemes", "myapp"));
        var maps = GetGeneratedSource(result, "NavigationBuilderExtensions.g.cs");

        maps.ShouldContain(@"""product/{id}""");
        maps.ShouldContain(@"""p/{id}""");
    }

    [Fact]
    public void AppLinks_RequiredValue_FailsTheBindWhenMissing()
    {
        var source = AppLinkSource(
            @"[ShellMap<ProductPage>(appLinks: new[] { ""product/{id}"" })]",
            @"[ShellProperty] public int Id { get; set; }");

        var result = RunGenerator(source, ("ShinyAppLinkSchemes", "myapp"));
        var maps = GetGeneratedSource(result, "NavigationBuilderExtensions.g.cs");

        // A miss returns false so the router can fall through to the next template
        maps.ShouldContain("return false;");
        maps.ShouldContain(@"values.TryGetValue(""Id"", out var __raw0)");
    }

    [Fact]
    public void AppLinks_OptionalValue_IsAppliedOnlyWhenPresent()
    {
        var source = AppLinkSource(
            @"[ShellMap<ProductPage>(appLinks: new[] { ""product/{id}"" })]",
            @"[ShellProperty] public int Id { get; set; }
              [ShellProperty(required: false)] public string? Tab { get; set; }");

        var result = RunGenerator(source, ("ShinyAppLinkSchemes", "myapp"));
        var maps = GetGeneratedSource(result, "NavigationBuilderExtensions.g.cs");

        maps.ShouldContain(@"if (values.TryGetValue(""Tab"", out var __raw1))");
    }

    [Theory]
    [InlineData("int", "global::System.Int32.TryParse")]
    [InlineData("long", "global::System.Int64.TryParse")]
    [InlineData("double", "global::System.Double.TryParse")]
    [InlineData("decimal", "global::System.Decimal.TryParse")]
    [InlineData("bool", "global::System.Boolean.TryParse")]
    [InlineData("System.Guid", "global::System.Guid.TryParse")]
    [InlineData("System.DateTime", "global::System.DateTime.TryParse")]
    [InlineData("System.TimeSpan", "global::System.TimeSpan.TryParse")]
    public void AppLinks_TypedValue_UsesTryParse(string type, string expected)
    {
        var source = AppLinkSource(
            @"[ShellMap<ProductPage>(appLinks: new[] { ""product/{value}"" })]",
            $"[ShellProperty] public {type} Value {{ get; set; }}");

        var result = RunGenerator(source, ("ShinyAppLinkSchemes", "myapp"));
        var maps = GetGeneratedSource(result, "NavigationBuilderExtensions.g.cs");

        maps.ShouldContain(expected);
    }

    [Fact]
    public void AppLinks_NumericValue_ParsesWithInvariantCulture()
    {
        var source = AppLinkSource(
            @"[ShellMap<ProductPage>(appLinks: new[] { ""product/{price}"" })]",
            @"[ShellProperty] public decimal Price { get; set; }");

        var result = RunGenerator(source, ("ShinyAppLinkSchemes", "myapp"));
        var maps = GetGeneratedSource(result, "NavigationBuilderExtensions.g.cs");

        // A URL is not culture-sensitive - a German device must not reject "1.5"
        maps.ShouldContain("global::System.Globalization.CultureInfo.InvariantCulture");
    }

    [Fact]
    public void AppLinks_EnumValue_ParsesCaseInsensitively()
    {
        var source = StubTypes + @"
namespace TestApp
{
    public enum Mode { Compact, Full }
    public class ProductPage : Microsoft.Maui.Controls.Page { }

    [ShellMap<ProductPage>(appLinks: new[] { ""product/{mode}"" })]
    public class ProductViewModel : System.ComponentModel.INotifyPropertyChanged
    {
        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
        [ShellProperty] public Mode Mode { get; set; }
    }
}";
        var result = RunGenerator(source, ("ShinyAppLinkSchemes", "myapp"));
        var maps = GetGeneratedSource(result, "NavigationBuilderExtensions.g.cs");

        maps.ShouldContain("global::System.Enum.TryParse<global::TestApp.Mode>");
        maps.ShouldContain(", true, out var");
    }

    [Fact]
    public void AppLinks_TokenWithoutMatchingProperty_ReportsShiny005()
    {
        var source = AppLinkSource(
            @"[ShellMap<ProductPage>(appLinks: new[] { ""product/{missing}"" })]",
            @"[ShellProperty] public int Id { get; set; }");

        var result = RunGenerator(source, ("ShinyAppLinkSchemes", "myapp"));

        result.Diagnostics.ShouldContain(d => d.Id == "SHINY005");
    }

    [Fact]
    public void AppLinks_UnconvertibleType_ReportsShiny006()
    {
        var source = StubTypes + @"
namespace TestApp
{
    public class Complex { }
    public class ProductPage : Microsoft.Maui.Controls.Page { }

    [ShellMap<ProductPage>(appLinks: new[] { ""product/{thing}"" })]
    public class ProductViewModel : System.ComponentModel.INotifyPropertyChanged
    {
        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
        [ShellProperty] public Complex Thing { get; set; }
    }
}";
        var result = RunGenerator(source, ("ShinyAppLinkSchemes", "myapp"));

        result.Diagnostics.ShouldContain(d => d.Id == "SHINY006");
    }

    [Fact]
    public void AppLinks_SameShapeTemplates_ReportShiny007()
    {
        var source = StubTypes + @"
namespace TestApp
{
    public class ProductPage : Microsoft.Maui.Controls.Page { }
    public class OrderPage : Microsoft.Maui.Controls.Page { }

    [ShellMap<ProductPage>(appLinks: new[] { ""item/{id}"" })]
    public class ProductViewModel : System.ComponentModel.INotifyPropertyChanged
    {
        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
        [ShellProperty] public int Id { get; set; }
    }

    [ShellMap<OrderPage>(appLinks: new[] { ""item/{id}"" })]
    public class OrderViewModel : System.ComponentModel.INotifyPropertyChanged
    {
        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
        [ShellProperty] public int Id { get; set; }
    }
}";
        var result = RunGenerator(source, ("ShinyAppLinkSchemes", "myapp"));

        result.Diagnostics.ShouldContain(d => d.Id == "SHINY007");
    }

    [Fact]
    public void AppLinks_OverlappingButDifferentSpecificity_IsNotAmbiguous()
    {
        var source = StubTypes + @"
namespace TestApp
{
    public class ProductPage : Microsoft.Maui.Controls.Page { }
    public class FeaturedPage : Microsoft.Maui.Controls.Page { }

    [ShellMap<ProductPage>(appLinks: new[] { ""product/{id}"" })]
    public class ProductViewModel : System.ComponentModel.INotifyPropertyChanged
    {
        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
        [ShellProperty] public int Id { get; set; }
    }

    [ShellMap<FeaturedPage>(appLinks: new[] { ""product/featured"" })]
    public class FeaturedViewModel : System.ComponentModel.INotifyPropertyChanged
    {
        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    }
}";
        var result = RunGenerator(source, ("ShinyAppLinkSchemes", "myapp"));

        // Runtime specificity ordering resolves these deterministically
        result.Diagnostics.ShouldNotContain(d => d.Id == "SHINY007");
    }

    [Fact]
    public void AppLinks_NoSchemeOrDomain_ReportsShiny008()
    {
        var source = AppLinkSource(
            @"[ShellMap<ProductPage>(appLinks: new[] { ""product/{id}"" })]",
            @"[ShellProperty] public int Id { get; set; }");

        var result = RunGenerator(source);

        result.Diagnostics.ShouldContain(d => d.Id == "SHINY008");
    }

    [Fact]
    public void AppLinks_SchemeConfigured_DoesNotReportShiny008()
    {
        var source = AppLinkSource(
            @"[ShellMap<ProductPage>(appLinks: new[] { ""product/{id}"" })]",
            @"[ShellProperty] public int Id { get; set; }");

        var result = RunGenerator(source, ("ShinyAppLinkSchemes", "myapp"));

        result.Diagnostics.ShouldNotContain(d => d.Id == "SHINY008");
    }

    [Fact]
    public void AppLinks_RequiredPropertyNotInPath_ReportsShiny009()
    {
        var source = AppLinkSource(
            @"[ShellMap<ProductPage>(appLinks: new[] { ""product"" })]",
            @"[ShellProperty] public int Id { get; set; }");

        var result = RunGenerator(source, ("ShinyAppLinkSchemes", "myapp"));

        result.Diagnostics.ShouldContain(d => d.Id == "SHINY009");
    }

    [Fact]
    public void AppLinkUri_SingleScheme_GeneratesBuilder()
    {
        var source = AppLinkSource(
            @"[ShellMap<ProductPage>(""Product"", appLinks: new[] { ""product/{id}"" })]",
            @"[ShellProperty] public int Id { get; set; }");

        var result = RunGenerator(source, ("ShinyAppLinkSchemes", "myapp"));
        var uris = GetGeneratedSource(result, "AppLinkExtensions.g.cs");

        uris.ShouldContain("CreateProductAppLink");
        uris.ShouldContain(@"__sb.Append(""myapp://"");");
    }

    [Fact]
    public void AppLinkUri_OnlyDomain_UsesHttps()
    {
        var source = AppLinkSource(
            @"[ShellMap<ProductPage>(""Product"", appLinks: new[] { ""product/{id}"" })]",
            @"[ShellProperty] public int Id { get; set; }");

        var result = RunGenerator(source, ("ShinyAppLinkDomains", "shinylib.net"));
        var uris = GetGeneratedSource(result, "AppLinkExtensions.g.cs");

        uris.ShouldContain(@"__sb.Append(""https://shinylib.net/"");");
    }

    [Fact]
    public void AppLinkUri_MultipleSchemes_GeneratesNothing()
    {
        var source = AppLinkSource(
            @"[ShellMap<ProductPage>(""Product"", appLinks: new[] { ""product/{id}"" })]",
            @"[ShellProperty] public int Id { get; set; }");

        var result = RunGenerator(source, ("ShinyAppLinkSchemes", "myapp;other"));

        // No single correct base to build against
        GetGeneratedSourceOrDefault(result, "AppLinkExtensions.g.cs").ShouldBeNull();
    }

    #endregion


    #region App Shortcuts

    static string ShortcutSource(string attribute, string properties = "") => StubTypes + @"
namespace TestApp
{
    public class SearchPage : Microsoft.Maui.Controls.Page { }

    " + attribute + @"
    public class SearchViewModel : System.ComponentModel.INotifyPropertyChanged
    {
        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
        " + properties + @"
    }
}";

    [Fact]
    public void Shortcut_NotDeclared_EmitsNothing()
    {
        var result = RunGenerator(ShortcutSource("[ShellMap<SearchPage>]"));
        var maps = GetGeneratedSource(result, "NavigationBuilderExtensions.g.cs");

        maps.ShouldNotContain("AddAppShortcut");
    }

    [Fact]
    public void Shortcut_Declared_EmitsRegistrationCall()
    {
        var source = ShortcutSource(
            @"[ShellMap<SearchPage>(Shortcut = ""Search"", ShortcutSubtitle = ""Find anything"", ShortcutIcon = ""search"", ShortcutOrder = 2)]");

        var result = RunGenerator(source);
        var maps = GetGeneratedSource(result, "NavigationBuilderExtensions.g.cs");

        // Emits a call to the same public API a consumer uses by hand
        maps.ShouldContain(@"builder.AddAppShortcut<TestApp.SearchViewModel>(""Search"", ""Find anything"", ""search"", 2);");
    }

    [Fact]
    public void Shortcut_OnlyTitle_PassesNullsForTheRest()
    {
        var result = RunGenerator(ShortcutSource(@"[ShellMap<SearchPage>(Shortcut = ""Search"")]"));
        var maps = GetGeneratedSource(result, "NavigationBuilderExtensions.g.cs");

        maps.ShouldContain(@"builder.AddAppShortcut<TestApp.SearchViewModel>(""Search"", null, null, 0);");
    }

    [Fact]
    public void Shortcut_OnRouteWithRequiredProperty_ReportsShiny010()
    {
        var source = ShortcutSource(
            @"[ShellMap<SearchPage>(Shortcut = ""Search"")]",
            @"[ShellProperty] public int Id { get; set; }");

        var result = RunGenerator(source);

        result.Diagnostics.ShouldContain(d => d.Id == "SHINY010");
    }

    [Fact]
    public void Shortcut_OnRouteWithOnlyOptionalProperties_IsAllowed()
    {
        var source = ShortcutSource(
            @"[ShellMap<SearchPage>(Shortcut = ""Search"")]",
            @"[ShellProperty(required: false)] public string? Filter { get; set; }");

        var result = RunGenerator(source);

        result.Diagnostics.ShouldNotContain(d => d.Id == "SHINY010");
    }

    [Fact]
    public void Shortcut_RequiredPropertyOnRouteWithoutShortcut_IsNotReported()
    {
        // SHINY010 is about the shortcut, not the route - a parameterised route is perfectly fine
        var source = ShortcutSource(
            "[ShellMap<SearchPage>]",
            @"[ShellProperty] public int Id { get; set; }");

        var result = RunGenerator(source);

        result.Diagnostics.ShouldNotContain(d => d.Id == "SHINY010");
    }

    [Theory]
    [InlineData(@"ShortcutSubtitle = ""Find anything""")]
    [InlineData(@"ShortcutIcon = ""search""")]
    [InlineData("ShortcutOrder = 3")]
    public void Shortcut_PropertyWithoutTitle_ReportsShiny012(string property)
    {
        var result = RunGenerator(ShortcutSource($"[ShellMap<SearchPage>({property})]"));

        result.Diagnostics.ShouldContain(d => d.Id == "SHINY012");
    }

    [Fact]
    public void Shortcut_TitleWithOtherProperties_DoesNotReportShiny012()
    {
        var result = RunGenerator(ShortcutSource(
            @"[ShellMap<SearchPage>(Shortcut = ""Search"", ShortcutIcon = ""search"")]"));

        result.Diagnostics.ShouldNotContain(d => d.Id == "SHINY012");
    }

    [Fact]
    public void Shortcut_MoreThanFour_ReportsShiny011()
    {
        var sb = new System.Text.StringBuilder(StubTypes);
        sb.AppendLine("namespace TestApp {");
        for (var i = 0; i < 5; i++)
        {
            sb.AppendLine($@"
    public class P{i} : Microsoft.Maui.Controls.Page {{ }}
    [ShellMap<P{i}>(Shortcut = ""S{i}"")]
    public class Vm{i} : System.ComponentModel.INotifyPropertyChanged
    {{
        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    }}");
        }
        sb.AppendLine("}");

        var result = RunGenerator(sb.ToString());

        result.Diagnostics.ShouldContain(d => d.Id == "SHINY011");
    }

    [Fact]
    public void Shortcut_ExactlyFour_DoesNotReportShiny011()
    {
        var sb = new System.Text.StringBuilder(StubTypes);
        sb.AppendLine("namespace TestApp {");
        for (var i = 0; i < 4; i++)
        {
            sb.AppendLine($@"
    public class P{i} : Microsoft.Maui.Controls.Page {{ }}
    [ShellMap<P{i}>(Shortcut = ""S{i}"")]
    public class Vm{i} : System.ComponentModel.INotifyPropertyChanged
    {{
        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    }}");
        }
        sb.AppendLine("}");

        var result = RunGenerator(sb.ToString());

        result.Diagnostics.ShouldNotContain(d => d.Id == "SHINY011");
    }

    [Fact]
    public void Shortcut_EmittedInShortcutOrder()
    {
        var source = StubTypes + @"
namespace TestApp
{
    public class APage : Microsoft.Maui.Controls.Page { }
    public class BPage : Microsoft.Maui.Controls.Page { }

    [ShellMap<APage>(Shortcut = ""Second"", ShortcutOrder = 5)]
    public class AViewModel : System.ComponentModel.INotifyPropertyChanged
    {
        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    }

    [ShellMap<BPage>(Shortcut = ""First"", ShortcutOrder = 1)]
    public class BViewModel : System.ComponentModel.INotifyPropertyChanged
    {
        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    }
}";
        var result = RunGenerator(source);
        var maps = GetGeneratedSource(result, "NavigationBuilderExtensions.g.cs");

        maps.IndexOf(@"""First""").ShouldBeLessThan(maps.IndexOf(@"""Second"""));
    }

    #endregion

    static GeneratorRunResult RunGenerator(string source, params (string Key, string Value)[] buildProperties)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Latest));

        var references = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrWhiteSpace(a.Location))
            .Select(a => MetadataReference.CreateFromFile(a.Location))
            .Cast<MetadataReference>()
            .ToList();

        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            [syntaxTree],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var generator = new ShinyShellGenerator();

        var dict = buildProperties.ToDictionary(
            x => "build_property." + x.Key,
            x => x.Value,
            StringComparer.OrdinalIgnoreCase);

        var provider = new MockAnalyzerConfigOptionsProvider(dict);
        var driver = CSharpGeneratorDriver.Create(generator).WithUpdatedAnalyzerConfigOptions(provider);
        var ran = driver.RunGenerators(compilation);

        return ran.GetRunResult().Results.First();
    }

    static string GetGeneratedSource(GeneratorRunResult result, string hintName)
    {
        var source = GetGeneratedSourceOrDefault(result, hintName);
        source.ShouldNotBeNull($"Expected generated source '{hintName}' was not found. Available: {string.Join(", ", result.GeneratedSources.Select(s => s.HintName))}");
        return source;
    }

    static string? GetGeneratedSourceOrDefault(GeneratorRunResult result, string hintName)
    {
        return result.GeneratedSources
            .FirstOrDefault(s => s.HintName == hintName)
            .SourceText?
            .ToString();
    }

    #endregion
}
