
namespace HanumanInstitute.SynthMultiViewer.Tests;

public class TabAutoNumberTests
{
    [Fact]
    public void Next_Empty_ReturnsOne()
    {
        Assert.Equal(1, TabAutoNumber.Next([], "Script"));
    }

    [Fact]
    public void Next_OnlyHigherNumber_ContinuesAfterMax()
    {
        Assert.Equal(4, TabAutoNumber.Next(["Viewer 3"], "Viewer"));
    }

    [Fact]
    public void Next_Gap_DoesNotFillHole()
    {
        Assert.Equal(4, TabAutoNumber.Next(["Script 1", "Script 3"], "Script"));
    }

    [Fact]
    public void Next_IgnoresOtherPrefixesAndNonExactTitles()
    {
        Assert.Equal(1, TabAutoNumber.Next(["Viewer 1", "script.vpy", "Script 1 copy"], "Script"));
    }

    [Fact]
    public void Next_OpenFileDoesNotConsumeScriptNumbers()
    {
        Assert.Equal(2, TabAutoNumber.Next(["Script 1", "clip.vpy"], "Script"));
    }
}
