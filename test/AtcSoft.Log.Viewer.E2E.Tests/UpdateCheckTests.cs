namespace AtcSoft.Log.Viewer.E2E;

/// <summary>
/// The update check at start, with a newer release the test writes, so GitHub is never asked:
/// the notice shows, it opens the dialog, and the dialog offers the download.
/// </summary>
/// <remarks>
/// The download itself isn't pressed: it would open the browser.
/// </remarks>
[Collection(E2ETestGroup.Name)]
public sealed class UpdateCheckTests
{
    private const string NoticeId = "NewVersionNotice";

    [Fact]
    public void A_Newer_Release_Shows_The_Notice_And_The_Dialog_Offers_The_Download()
    {
        E2ESettings.SkipUnlessEnabled();

        // Arrange
        var releaseFile = Path.Combine(E2ESettings.NewFolder("update-check-release"), "latest.json");
        File.WriteAllText(releaseFile, NewerReleaseJson());

        using var run = new ViewerRun("update-check", releaseFile: releaseFile);
        var app = run.App;

        // Act
        var notice = app.FindById(run.Main, NoticeId, TimeSpan.FromSeconds(20));
        var dialog = app.OpenWindow(() => DesktopApp.Press(notice), "the new version notice");
        var download = app.Find(dialog, "Download latest", ControlType.Button);

        // Assert
        WaitUntilEnabled(download, TimeSpan.FromSeconds(20)).Should().BeTrue("the dialog finds the newer release and offers its installer");
        app.ShouldNotHaveCrashed();

        app.CloseWindow(dialog);
        app.CloseApp(run.Main).Should().BeTrue("the viewer must close when its window is closed");
    }

    private static bool WaitUntilEnabled(
        AutomationElement element,
        TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (element.IsEnabled)
            {
                return true;
            }

            Thread.Sleep(250);
        }

        return element.IsEnabled;
    }

    private static string NewerReleaseJson()
        => new System.Text.Json.Nodes.JsonObject
        {
            ["tag_name"] = "v99.0.0",
            ["html_url"] = "https://github.com/atc-net-apps/atc-soft-log-viewer/releases/tag/v99.0.0",
            ["assets"] = new System.Text.Json.Nodes.JsonArray(
                new System.Text.Json.Nodes.JsonObject
                {
                    ["name"] = UpdateCheck.InstallerAssetName,
                    ["browser_download_url"] = "https://github.com/atc-net-apps/atc-soft-log-viewer/releases/download/v99.0.0/" + UpdateCheck.InstallerAssetName,
                }),
        }.ToJsonString();
}