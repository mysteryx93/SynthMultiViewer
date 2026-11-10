using System.Windows.Input;

namespace HanumanInstitute.SynthMultiViewer.Tests;

public class ScriptViewModelTests
{
    [Fact]
    public void HeaderEditDone_PaddedName_TrimsName()
    {
        var model = new EditorViewModel { IsActive = true };
        ((ICommand)model.BeginHeaderEdit).Execute(null);
        model.DisplayName = "  Clip  ";

        ((ICommand)model.HeaderEditDone).Execute(null);

        Assert.Equal("Clip", model.DisplayName);
        Assert.False(model.IsEditingHeader);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void HeaderEditDone_BlankName_RestoresPreviousName(string name)
    {
        var model = new EditorViewModel { IsActive = true };
        ((ICommand)model.BeginHeaderEdit).Execute(null);
        model.DisplayName = name;

        ((ICommand)model.HeaderEditDone).Execute(null);

        Assert.Equal("Script", model.DisplayName);
        Assert.False(model.IsEditingHeader);
    }

    [Fact]
    public void HeaderEditCancel_EditedName_RestoresPreviousName()
    {
        var model = new EditorViewModel { IsActive = true };
        ((ICommand)model.BeginHeaderEdit).Execute(null);
        model.DisplayName = "Renamed";

        ((ICommand)model.HeaderEditCancel).Execute(null);

        Assert.Equal("Script", model.DisplayName);
        Assert.False(model.IsEditingHeader);
    }
}
