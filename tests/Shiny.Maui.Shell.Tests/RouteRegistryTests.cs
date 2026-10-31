using System.ComponentModel;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Shiny.Infrastructure;
using Shiny.Maui.Shell.SourceGenerators;
using Shouldly;

namespace Shiny.Maui.Shell.Tests;

/// <summary>
/// <see cref="IRouteRegistry"/> over a real <see cref="ShinyAppBuilder"/>, and the generated
/// <c>AddGeneratedMaps()</c> that feeds it descriptions - compiled against the real library rather
/// than the generator tests' stubs, so a signature drift between the two is caught.
/// </summary>
public class RouteRegistryTests
{
    class HomePage : ContentPage { }
    class DetailPage : ContentPage { }
    class TabPage : ContentPage { }

    class TestViewModel : INotifyPropertyChanged
    {
#pragma warning disable CS0067
        public event PropertyChangedEventHandler? PropertyChanged;
#pragma warning restore CS0067
    }

    class HomeViewModel : TestViewModel { }
    class DetailViewModel : TestViewModel { }
    class TabViewModel : TestViewModel { }


    static ShinyAppBuilder Builder() => new(MauiApp.CreateBuilder(false));


    [Fact]
    public void ListsEveryMappedRouteInOrder()
    {
        var builder = Builder()
            .Add<HomePage, HomeViewModel>("home")
            .Add<DetailPage, DetailViewModel>()
            .Add<TabPage, TabViewModel>("tab", registerRoute: false);

        var routes = new RouteRegistry(builder).Routes;

        routes.Select(x => x.Route).ShouldBe(["home", nameof(DetailPage), "tab"]);
        routes[0].PageType.ShouldBe(typeof(HomePage));
        routes[0].ViewModelType.ShouldBe(typeof(HomeViewModel));
        routes[0].IsRegisteredWithShell.ShouldBeTrue();
        routes[2].IsRegisteredWithShell.ShouldBeFalse();
    }


    [Fact]
    public void HandMappedRoute_HasNoDescriptionAndUnknownParameters()
    {
        var route = new RouteRegistry(Builder().Add<HomePage, HomeViewModel>("home")).Routes.Single();

        route.Description.ShouldBeNull();
        // Null, not empty: nobody described this route, so its parameters are unknown.
        route.Parameters.ShouldBeNull();
    }


    [Fact]
    public void RouteInfo_IsJoinedOntoTheRoute_InEitherOrder()
    {
        var builder = Builder()
            .AddRouteInfo(new GeneratedRouteInfo(
                "detail",
                "Shows a product",
                [new GeneratedRouteParameter("ProductId", "The product", "int", true)]
            ))
            .Add<DetailPage, DetailViewModel>("detail")
            .Add<HomePage, HomeViewModel>("home")
            .AddRouteInfo(new GeneratedRouteInfo("home", "", []));

        var registry = new RouteRegistry(builder);

        var detail = registry.GetRoute("detail")!;
        detail.Description.ShouldBe("Shows a product");
        detail.Parameters!.Single().ShouldBe(new GeneratedRouteParameter("ProductId", "The product", "int", true));

        var home = registry.GetRoute("home")!;
        // The generator writes "" for no description - reported as none.
        home.Description.ShouldBeNull();
        // Described, with no parameters - empty rather than unknown.
        home.Parameters.ShouldNotBeNull();
        home.Parameters.ShouldBeEmpty();
    }


    [Fact]
    public void RouteInfo_ForAnUnmappedRoute_IsNotListed()
    {
        var builder = Builder()
            .Add<HomePage, HomeViewModel>("home")
            .AddRouteInfo(new GeneratedRouteInfo("nowhere", "Not mapped", []));

        new RouteRegistry(builder).Routes.Select(x => x.Route).ShouldBe(["home"]);
    }


    [Fact]
    public void Lookups()
    {
        var registry = new RouteRegistry(Builder()
            .Add<HomePage, HomeViewModel>("home")
            .Add<DetailPage, DetailViewModel>("detail"));

        registry.GetRoute("detail")!.ViewModelType.ShouldBe(typeof(DetailViewModel));
        registry.GetRoute("//detail").ShouldBeNull();
        registry.GetRoute("missing").ShouldBeNull();
        registry.GetRouteForViewModel(typeof(HomeViewModel))!.Route.ShouldBe("home");
        registry.GetRouteForViewModel(typeof(TabViewModel)).ShouldBeNull();
    }


    [Fact]
    public void GeneratedAddGeneratedMaps_DescribesEveryRoute_AndCompilesAgainstTheLibrary()
    {
        const string source = @"
using System.ComponentModel;
using Shiny;

namespace TestApp
{
    public class HomePage : Microsoft.Maui.Controls.ContentPage { }
    public class DetailPage : Microsoft.Maui.Controls.ContentPage { }

    [ShellMap<HomePage>(""home"", registerRoute: false)]
    public class HomeViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
    }

    [ShellMap<DetailPage>(""detail"", description: ""Shows a \""product\"""")]
    public class DetailViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        [ShellProperty(""The product"")]
        public int ProductId { get; set; }

        [ShellProperty(required: false)]
        public string? Note { get; set; }
    }
}";
        var (output, generated) = CompileWithGenerator(source);

        var maps = generated.Single(x => x.HintName == "NavigationBuilderExtensions.g.cs").SourceText.ToString();
        maps.ShouldContain("builder.AddRouteInfo(");
        maps.ShouldContain("\"home\",");
        maps.ShouldContain("\"Shows a \\\"product\\\"\",");
        maps.ShouldContain("new global::Shiny.Infrastructure.GeneratedRouteParameter(\"ProductId\", \"The product\", \"int\", true)");
        maps.ShouldContain("new global::Shiny.Infrastructure.GeneratedRouteParameter(\"Note\", \"\", \"string?\", false)");

        // Scoped to the file AddRouteInfo is emitted into: the Roslyn package the tests use predates
        // C# 13's params collections, so the generated INavigator extensions (which use
        // `params IEnumerable<...>`) do not bind here even though they compile in a real app.
        var errors = output
            .GetDiagnostics()
            .Where(x => x.Severity == DiagnosticSeverity.Error)
            .Where(x => x.Location.SourceTree?.FilePath.EndsWith("NavigationBuilderExtensions.g.cs") == true)
            .Select(x => x.ToString())
            .ToList();
        errors.ShouldBeEmpty();
    }


    /// <summary>
    /// Runs the generator over <paramref name="source"/> referencing the real Shiny.Maui.Shell and
    /// MAUI assemblies - everything the test host itself loads - and returns the resulting
    /// compilation with the generated trees added.
    /// </summary>
    static (Compilation Output, IReadOnlyList<GeneratedSourceResult> Generated) CompileWithGenerator(string source)
    {
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Append(typeof(ShinyAppBuilder).Assembly.Location)
            .Distinct()
            .Select(x => MetadataReference.CreateFromFile(x))
            .ToList();

        var compilation = CSharpCompilation.Create(
            "RouteRegistryTestApp",
            [CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Latest))],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable)
        );

        var driver = CSharpGeneratorDriver
            .Create(new ShinyShellGenerator())
            .WithUpdatedAnalyzerConfigOptions(new MockAnalyzerConfigOptionsProvider(new Dictionary<string, string>()))
            .RunGeneratorsAndUpdateCompilation(compilation, out var output, out _);

        return (output, driver.GetRunResult().Results.Single().GeneratedSources);
    }
}
