using System.Windows.Input;
using Avalonia.Media;
using ReactiveUI.Builder;

namespace HanumanInstitute.SynthMultiViewer.Tests;

public class TabColorViewModelTests
{
    static TabColorViewModelTests() =>
        RxAppBuilder.CreateReactiveUIBuilder().WithCoreServices().BuildApp();

    [Fact]
    public void Ok_CustomColorChosen_ReturnsOpaqueOverride()
    {
        var model = new TabColorViewModel();
        model.Load(TabColors.VapourSynth, true, TabColors.VapourSynth);
        model.Color = Color.FromArgb(0x80, 0xFF, 0x69, 0xB4);

        ((ICommand)model.Ok).Execute(null);

        Assert.True(model.DialogResult);
        Assert.Equal(Colors.HotPink, model.Result);
    }

    [Fact]
    public void Ok_UnchangedDefault_ReturnsNullOverride()
    {
        var model = new TabColorViewModel();
        model.Load(TabColors.AviSynth, true, TabColors.AviSynth);

        ((ICommand)model.Ok).Execute(null);

        Assert.True(model.DialogResult);
        Assert.Null(model.Result);
    }

    [Fact]
    public void RestoreDefault_CustomColorLoaded_ClearsOverride()
    {
        var model = new TabColorViewModel();
        model.Load(Colors.HotPink, false, TabColors.VapourSynth);

        ((ICommand)model.RestoreDefault).Execute(null);
        ((ICommand)model.Ok).Execute(null);

        Assert.Equal(TabColors.VapourSynth, model.Color);
        Assert.True(model.UseDefault);
        Assert.Null(model.Result);
    }

    [Fact]
    public void Close_ColorChanged_LeavesDialogUnconfirmed()
    {
        var model = new TabColorViewModel();
        model.Load(TabColors.VapourSynth, true, TabColors.VapourSynth);
        model.Color = Colors.HotPink;

        ((ICommand)model.Close).Execute(null);

        Assert.Null(model.DialogResult);
        Assert.Equal(Colors.HotPink, model.Result);
    }
}
