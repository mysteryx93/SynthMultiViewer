using System.Reactive.Linq;
using System.Windows.Input;
using HanumanInstitute.ScriptAssist;
using HanumanInstitute.SynthMultiViewer.ViewModels;
using Moq;
using ReactiveUI.Builder;
using Xunit;

namespace HanumanInstitute.SynthMultiViewer.Tests;

public class FunctionsExplorerViewModelTests
{
    static FunctionsExplorerViewModelTests() =>
        RxAppBuilder.CreateReactiveUIBuilder().WithCoreServices().BuildApp();

    [Fact]
    public void Insert_BeforeCaretWhenEditorAlreadyShifted_MovesOnce()
    {
        var editor = new EditorViewModel { Script = "clip = " };
        editor.CaretOffset = editor.Script.Length;
        var start = editor.CaretOffset;
        editor.Document.Changed += (_, e) =>
        {
            if (e.Offset <= start)
            {
                editor.CaretOffset = start + e.InsertionLength;
            }
        };

        editor.Insert(0, "import x\n");

        Assert.Equal("import x\nclip = ", editor.Script);
        Assert.Equal("import x\nclip = ".Length, editor.CaretOffset);
    }

    [Fact]
    public async Task Insert_AtCaret_PlacesCaretInsideParens()
    {
        var editor = new EditorViewModel { Script = "clip = " };
        editor.CaretOffset = editor.Script.Length;
        var languages = new Mock<IScriptLanguageFactory>();
        languages.Setup(f => f.BrowseAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>(),
                It.IsAny<string?>(), It.IsAny<IReadOnlyList<string>?>()))
            .ReturnsAsync(
            [
                new BrowseGroup("std", [new BrowseFunction("Crop", "Crop()", "core.std.Crop()")])
            ]);
        var model = new FunctionsExplorerViewModel { Languages = languages.Object, Editor = editor };
        await model.ReloadAsync();

        await model.Insert.Execute();

        Assert.Equal("clip = core.std.Crop()", editor.Script);
        Assert.Equal(editor.Script.Length - 1, editor.CaretOffset);
    }

    [Fact]
    public void Insert_ViewerTab_DoesNotRun()
    {
        var model = new FunctionsExplorerViewModel
        {
            SelectedFunction = new BrowseFunction("Crop", "Crop()", "core.std.Crop()")
        };

        var canInsert = ((ICommand)model.Insert).CanExecute(null);

        Assert.Null(model.Editor);
        Assert.False(model.CanInsert);
        Assert.False(canInsert);
    }

    [Fact]
    public async Task Filter_Text_SearchesAllGroups()
    {
        var languages = new Mock<IScriptLanguageFactory>();
        languages.Setup(f => f.BrowseAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>(),
                It.IsAny<string?>(), It.IsAny<IReadOnlyList<string>?>()))
            .ReturnsAsync(
            [
                new BrowseGroup("std",
                [
                    new BrowseFunction("BlankClip", "BlankClip()", "core.std.BlankClip()"),
                    new BrowseFunction("Crop", "Crop()", "core.std.Crop()")
                ]),
                new BrowseGroup("resize", [new BrowseFunction("Bilinear", "Bilinear()", "core.resize.Bilinear()")])
            ]);
        var model = new FunctionsExplorerViewModel
        {
            Languages = languages.Object,
            Editor = new EditorViewModel()
        };
        await model.ReloadAsync();

        model.Filter = "Bilin";

        Assert.True(model.IsSearching);
        var hit = Assert.Single(model.Hits);
        Assert.Equal("Bilinear", hit.Function.Name);
        Assert.Equal("resize", hit.Group);
        Assert.Equal("Bilinear", model.SelectedFunction?.Name);
    }

    [Fact]
    public async Task Filter_GroupName_ListsThatGroup()
    {
        var languages = new Mock<IScriptLanguageFactory>();
        languages.Setup(f => f.BrowseAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>(),
                It.IsAny<string?>(), It.IsAny<IReadOnlyList<string>?>()))
            .ReturnsAsync(
            [
                new BrowseGroup("std", [new BrowseFunction("Crop", "Crop()", "core.std.Crop()")]),
                new BrowseGroup("resize", [new BrowseFunction("Bilinear", "Bilinear()", "core.resize.Bilinear()")])
            ]);
        var model = new FunctionsExplorerViewModel
        {
            Languages = languages.Object,
            Editor = new EditorViewModel()
        };
        await model.ReloadAsync();

        model.Filter = "std";

        Assert.Equal("Crop", Assert.Single(model.Hits).Function.Name);
        Assert.Equal("std", model.Hits[0].Group);
    }

    [Fact]
    public async Task Filter_Cleared_ReturnsToGroupList()
    {
        var languages = new Mock<IScriptLanguageFactory>();
        languages.Setup(f => f.BrowseAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>(),
                It.IsAny<string?>(), It.IsAny<IReadOnlyList<string>?>()))
            .ReturnsAsync(
            [
                new BrowseGroup("std", [new BrowseFunction("Crop", "Crop()", "core.std.Crop()")]),
                new BrowseGroup("resize", [new BrowseFunction("Bilinear", "Bilinear()", "core.resize.Bilinear()")])
            ]);
        var model = new FunctionsExplorerViewModel
        {
            Languages = languages.Object,
            Editor = new EditorViewModel()
        };
        await model.ReloadAsync();
        model.Filter = "Bilin";

        model.Filter = "";

        Assert.False(model.IsSearching);
        Assert.Empty(model.Hits);
        Assert.Equal("Crop", Assert.Single(model.Functions).Name);
    }

    [Fact]
    public async Task Insert_UnimportedPackage_AddsImportAndQualifiedCall()
    {
        const string script = "import vapoursynth as vs\nclip = ";
        var editor = new EditorViewModel { Script = script };
        editor.CaretOffset = editor.Script.Length;
        var languages = new Mock<IScriptLanguageFactory>();
        languages.Setup(f => f.BrowseAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>(),
                It.IsAny<string?>(), It.IsAny<IReadOnlyList<string>?>()))
            .ReturnsAsync(
            [
                new BrowseGroup("havsfunc",
                    [new BrowseFunction("QTGMC", "QTGMC()", "havsfunc.QTGMC()", "havsfunc")])
            ]);
        var model = new FunctionsExplorerViewModel { Languages = languages.Object, Editor = editor };
        await model.ReloadAsync();

        await model.Insert.Execute();

        Assert.Equal("import vapoursynth as vs\nimport havsfunc\nclip = havsfunc.QTGMC()", editor.Script);
        Assert.Equal(editor.Script.Length - 1, editor.CaretOffset);
    }

    [Fact]
    public async Task Insert_ExistingImport_DoesNotDuplicate()
    {
        const string script = "import havsfunc\nclip = ";
        var editor = new EditorViewModel { Script = script };
        editor.CaretOffset = editor.Script.Length;
        var languages = new Mock<IScriptLanguageFactory>();
        languages.Setup(f => f.BrowseAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>(),
                It.IsAny<string?>(), It.IsAny<IReadOnlyList<string>?>()))
            .ReturnsAsync(
            [
                new BrowseGroup("havsfunc",
                    [new BrowseFunction("QTGMC", "QTGMC()", "havsfunc.QTGMC()", "havsfunc")])
            ]);
        var model = new FunctionsExplorerViewModel { Languages = languages.Object, Editor = editor };
        await model.ReloadAsync();

        await model.Insert.Execute();

        Assert.Equal("import havsfunc\nclip = havsfunc.QTGMC()", editor.Script);
    }

    [Fact]
    public async Task Insert_FromImport_AddsModuleImport()
    {
        const string script = "from havsfunc import QTGMC\nclip = ";
        var editor = new EditorViewModel { Script = script };
        editor.CaretOffset = editor.Script.Length;
        var languages = new Mock<IScriptLanguageFactory>();
        languages.Setup(f => f.BrowseAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>(),
                It.IsAny<string?>(), It.IsAny<IReadOnlyList<string>?>()))
            .ReturnsAsync(
            [
                new BrowseGroup("havsfunc",
                    [new BrowseFunction("QTGMC", "QTGMC()", "havsfunc.QTGMC()", "havsfunc")])
            ]);
        var model = new FunctionsExplorerViewModel { Languages = languages.Object, Editor = editor };
        await model.ReloadAsync();

        await model.Insert.Execute();

        Assert.Equal("from havsfunc import QTGMC\nimport havsfunc\nclip = havsfunc.QTGMC()", editor.Script);
    }

    [Fact]
    public void ClearFilter_HasText_Clears()
    {
        var model = new FunctionsExplorerViewModel { Filter = "Crop" };

        ((ICommand)model.ClearFilter).Execute(null);

        Assert.Equal("", model.Filter);
        Assert.False(model.IsSearching);
    }

    [Fact]
    public async Task MoveSelection_DownWhileSearching_SelectsNextHit()
    {
        var languages = new Mock<IScriptLanguageFactory>();
        languages.Setup(f => f.BrowseAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>(),
                It.IsAny<string?>(), It.IsAny<IReadOnlyList<string>?>()))
            .ReturnsAsync(
            [
                new BrowseGroup("std",
                [
                    new BrowseFunction("Crop", "Crop()", "core.std.Crop()"),
                    new BrowseFunction("CropAbs", "CropAbs()", "core.std.CropAbs()")
                ])
            ]);
        var model = new FunctionsExplorerViewModel
        {
            Languages = languages.Object,
            Editor = new EditorViewModel()
        };
        await model.ReloadAsync();
        model.Filter = "Crop";

        model.MoveSelection(1);

        Assert.Equal("CropAbs", model.SelectedHit?.Function.Name);
        Assert.Equal("CropAbs", model.SelectedFunction?.Name);
    }

    [Fact]
    public void Editor_Changed_BrowsesNewEditor()
    {
        var languages = new Mock<IScriptLanguageFactory>();
        languages.Setup(f => f.BrowseAsync(It.IsAny<string>(), "first", It.IsAny<CancellationToken>(),
                It.IsAny<string?>(), It.IsAny<IReadOnlyList<string>?>()))
            .ReturnsAsync(
            [
                new BrowseGroup("std", [new BrowseFunction("Crop", "Crop()", "core.std.Crop()")])
            ]);
        languages.Setup(f => f.BrowseAsync(It.IsAny<string>(), "second", It.IsAny<CancellationToken>(),
                It.IsAny<string?>(), It.IsAny<IReadOnlyList<string>?>()))
            .ReturnsAsync(
            [
                new BrowseGroup("resize", [new BrowseFunction("Bilinear", "Bilinear()", "core.resize.Bilinear()")])
            ]);
        var model = new FunctionsExplorerViewModel { Languages = languages.Object };
        model.Editor = new EditorViewModel { Script = "first" };

        model.Editor = new EditorViewModel { Script = "second" };

        Assert.Equal("resize", Assert.Single(model.Groups).Name);
        Assert.Equal("Bilinear", Assert.Single(model.Functions).Name);
    }
}
