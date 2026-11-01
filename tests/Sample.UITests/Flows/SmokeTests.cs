using Sample.UITests.DevFlow;
using Sample.UITests.Pages;

namespace Sample.UITests.Flows;

public sealed class SmokeTests(AppFixture app) : UiTest(app)
{
    [Fact]
    public async Task Launches_into_the_standard_shell_on_the_home_list()
    {
        (await this.Ui.Exists("AppShell")).ShouldBeTrue();
        (await this.Ui.Route()).ShouldBe("//HomePage");
        (await this.Ui.Query(automationId: "Push & Pop")).ShouldNotBeEmpty();
    }

    public static TheoryData<string> Rows => new(HomeList.Demos.Select(d => d.Row));

    [Theory]
    [MemberData(nameof(Rows))]
    public async Task Every_home_row_opens_its_page(string row)
    {
        await new HomeList(this.Ui).Open(row);

        var page = HomeList.Demos.Single(d => d.Row == row).Page;
        (await this.Ui.Exists(page)).ShouldBeTrue();
    }
}
