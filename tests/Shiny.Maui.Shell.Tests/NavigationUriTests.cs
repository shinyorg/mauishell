using Shiny.Infrastructure;
using Shouldly;

namespace Shiny.Maui.Shell.Tests;

/// <summary>
/// The URI grammar interceptor redirects are written in.
/// </summary>
public class NavigationUriTests
{
    [Theory]
    [InlineData("login", "login")]
    [InlineData("//login", "//login")]
    [InlineData("  //login  ", "//login")]
    // A single leading slash is what everybody writes and what Shell understands least - it is
    // promoted rather than handed to Shell as-is.
    [InlineData("/login", "//login")]
    [InlineData("../detail", "../detail")]
    public void Normalize(string input, string expected)
        => NavigationUri.Normalize(input).ShouldBe(expected);


    [Theory]
    [InlineData("//main/home", NavigationType.SetRoot)]
    [InlineData("detail", NavigationType.Push)]
    [InlineData("..", NavigationType.GoBack)]
    [InlineData("../../detail", NavigationType.GoBack)]
    [InlineData("", NavigationType.Push)]
    public void GetNavigationType(string uri, NavigationType expected)
        => NavigationUri.GetNavigationType(uri).ShouldBe(expected);


    [Theory]
    [InlineData("//main/home", "home")]
    [InlineData("detail", "detail")]
    [InlineData("detail?id=5", "detail")]
    [InlineData("../detail", "detail")]
    [InlineData("//main/home/detail#frag", "detail")]
    public void GetTargetRoute(string uri, string expected)
        => NavigationUri.GetTargetRoute(uri).ShouldBe(expected);


    [Theory]
    [InlineData("..")]
    [InlineData("../..")]
    [InlineData("")]
    public void GetTargetRoute_PureBackNavigationHasNoTarget(string uri)
        => NavigationUri.GetTargetRoute(uri).ShouldBeNull();


    [Theory]
    [InlineData("a/b/c", true)]
    [InlineData("a/b?id=5", true)]
    [InlineData("../a/b", true)]
    [InlineData("//home/a/b", true)]
    [InlineData("a", false)]
    [InlineData("../a", false)]
    [InlineData("../..", false)]
    [InlineData("//home/a", false)]
    [InlineData("a?next=b/c", false)]
    [InlineData("", false)]
    public void PushesSeveralPages(string uri, bool expected)
        => NavigationUri.PushesSeveralPages(uri).ShouldBe(expected);
}
