
namespace HanumanInstitute.SynthMultiViewer.Tests;

public class VideoPropertiesPlacementTests
{
    [Fact]
    public void AlignToOwnerRight_MatchesOwnerRightEdgeAndVerticalCenter()
    {
        var position = VideoPropertiesPlacement.AlignToOwnerRight(
            new(100, 40), new(800, 600), new(380, 460));

        Assert.Equal(new(520, 110), position);
    }

    [Fact]
    public void AlignToOwnerRight_AccountsForChildChrome()
    {
        var position = VideoPropertiesPlacement.AlignToOwnerRight(
            new(100, 40), new(800, 600), new(400, 480));

        Assert.Equal(new(500, 100), position);
    }
}
