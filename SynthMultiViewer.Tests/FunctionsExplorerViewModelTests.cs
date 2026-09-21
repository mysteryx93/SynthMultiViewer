using System.Reactive.Linq;
using System.Windows.Input;
using HanumanInstitute.MediaSynthUI;
using HanumanInstitute.ScriptAssist;
using HanumanInstitute.ScriptAssist.Tests;
using HanumanInstitute.SynthMultiViewer.ViewModels;
using Moq;
using ReactiveUI.Builder;
using Xunit;

namespace HanumanInstitute.SynthMultiViewer.Tests;

public class FunctionsExplorerViewModelTests : TestsBase
{
    static FunctionsExplorerViewModelTests() =>
        RxAppBuilder.CreateReactiveUIBuilder().WithCoreServices().BuildApp();

    public FunctionsExplorerViewModelTests(ITestOutputHelper output) : base(output)
    {
    }

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
        var languages = Languages(new BrowseGroup("std", [new BrowseFunction("Crop", "Crop()", "core.std.Crop()")]));
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
        var languages = Languages(
            new BrowseGroup("std",
            [
                new BrowseFunction("BlankClip", "BlankClip()", "core.std.BlankClip()"),
                new BrowseFunction("Crop", "Crop()", "core.std.Crop()")
            ]),
            new BrowseGroup("resize", [new BrowseFunction("Bilinear", "Bilinear()", "core.resize.Bilinear()")]));
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
    public async Task Filter_DottedName_MatchesInsertText()
    {
        var languages = Languages(
            new BrowseGroup("vsdenoise",
            [
                new BrowseFunction("MotionMode", "MotionMode: SAD, COHERENCE", "vsdenoise.MotionMode")
            ]),
            new BrowseGroup("std", [new BrowseFunction("Crop", "Crop()", "core.std.Crop()")]));
        var model = new FunctionsExplorerViewModel
        {
            Languages = languages.Object,
            Editor = new EditorViewModel()
        };
        await model.ReloadAsync();

        model.Filter = "vsdenoise.MotionMode";

        var hit = Assert.Single(model.Hits);
        Assert.Equal("MotionMode", hit.Function.Name);
        Assert.Equal("vsdenoise", hit.Group);
    }

    [Fact]
    public async Task Filter_GroupName_ListsThatGroup()
    {
        var languages = Languages(
            new BrowseGroup("std", [new BrowseFunction("Crop", "Crop()", "core.std.Crop()")]),
            new BrowseGroup("resize", [new BrowseFunction("Bilinear", "Bilinear()", "core.resize.Bilinear()")]));
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
        var languages = Languages(
            new BrowseGroup("std", [new BrowseFunction("Crop", "Crop()", "core.std.Crop()")]),
            new BrowseGroup("resize", [new BrowseFunction("Bilinear", "Bilinear()", "core.resize.Bilinear()")]));
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
        var languages = Languages(new BrowseGroup("havsfunc",
            [new BrowseFunction("QTGMC", "QTGMC()", "havsfunc.QTGMC()", "havsfunc")]));
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
        var languages = Languages(new BrowseGroup("havsfunc",
            [new BrowseFunction("QTGMC", "QTGMC()", "havsfunc.QTGMC()", "havsfunc")]));
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
        var languages = Languages(new BrowseGroup("havsfunc",
            [new BrowseFunction("QTGMC", "QTGMC()", "havsfunc.QTGMC()", "havsfunc")]));
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
        var languages = Languages(new BrowseGroup("std",
        [
            new BrowseFunction("Crop", "Crop()", "core.std.Crop()"),
            new BrowseFunction("CropAbs", "CropAbs()", "core.std.CropAbs()")
        ]));
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
    public async Task Editor_Changed_BrowsesNewEditor()
    {
        var languages = InitMock<IScriptLanguageFactory>(mock =>
        {
            mock.Setup(f => f.BrowseAsync(It.IsAny<string>(), "first", It.IsAny<CancellationToken>(),
                    It.IsAny<string?>(), It.IsAny<IReadOnlyList<string>?>()))
                .ReturnsAsync(
                [
                    new BrowseGroup("std", [new BrowseFunction("Crop", "Crop()", "core.std.Crop()")])
                ]);
            mock.Setup(f => f.BrowseAsync(It.IsAny<string>(), "second", It.IsAny<CancellationToken>(),
                    It.IsAny<string?>(), It.IsAny<IReadOnlyList<string>?>()))
                .ReturnsAsync(
                [
                    new BrowseGroup("resize", [new BrowseFunction("Bilinear", "Bilinear()", "core.resize.Bilinear()")])
                ]);
        });
        var model = new FunctionsExplorerViewModel { Languages = languages.Object };
        model.Editor = new EditorViewModel { Script = "first" };

        model.Editor = new EditorViewModel { Script = "second" };
        await model.ReloadAsync();

        Assert.Equal("resize", Assert.Single(model.Groups).Name);
        Assert.Equal("Bilinear", Assert.Single(model.Functions).Name);
    }

    [Fact]
    public async Task Editor_ChangedWhileLoading_DisablesInsert()
    {
        var firstDone = new TaskCompletionSource<IReadOnlyList<BrowseGroup>>();
        var second = new TaskCompletionSource<IReadOnlyList<BrowseGroup>>();
        var languages = InitMock<IScriptLanguageFactory>(mock =>
        {
            mock.Setup(f => f.BrowseAsync(It.IsAny<string>(), "first", It.IsAny<CancellationToken>(),
                    It.IsAny<string?>(), It.IsAny<IReadOnlyList<string>?>()))
                .Returns(firstDone.Task);
            mock.Setup(f => f.BrowseAsync(It.IsAny<string>(), "second", It.IsAny<CancellationToken>(),
                    It.IsAny<string?>(), It.IsAny<IReadOnlyList<string>?>()))
                .Returns(second.Task);
        });
        var model = new FunctionsExplorerViewModel { Languages = languages.Object };
        var firstEditor = new EditorViewModel { Script = "first" };
        var secondEditor = new EditorViewModel { Script = "second" };
        model.Editor = firstEditor;
        firstDone.SetResult([new BrowseGroup("std", [new BrowseFunction("Crop", "Crop()", "core.std.Crop()")])]);
        await model.ReloadAsync();

        model.Editor = secondEditor;

        Assert.False(model.CanInsert);
        second.SetResult([new BrowseGroup("resize",
            [new BrowseFunction("Bilinear", "Bilinear()", "core.resize.Bilinear()")])]);
        await model.ReloadAsync();
        Assert.True(model.CanInsert);
        Assert.Equal("Bilinear", model.SelectedFunction?.Name);
    }

    [Fact]
    public async Task OnClosed_PendingBrowse_DiscardsResults()
    {
        var pending = new TaskCompletionSource<IReadOnlyList<BrowseGroup>>();
        var languages = InitMock<IScriptLanguageFactory>(mock =>
            mock.Setup(f => f.BrowseAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>(),
                    It.IsAny<string?>(), It.IsAny<IReadOnlyList<string>?>()))
                .Returns(pending.Task));
        var model = new FunctionsExplorerViewModel
        {
            Languages = languages.Object,
            Editor = new EditorViewModel { Script = "clip = " }
        };
        var reload = model.ReloadAsync();

        model.OnClosed();
        pending.SetResult([new BrowseGroup("std", [new BrowseFunction("Crop", "Crop()", "core.std.Crop()")])]);
        await reload;

        Assert.Empty(model.Groups);
        Assert.False(model.CanInsert);
    }

    [Fact]
    public async Task Insert_UnimportedPackage_OneUndo()
    {
        const string script = "import vapoursynth as vs\nclip = ";
        var editor = new EditorViewModel { Script = script };
        editor.CaretOffset = editor.Script.Length;
        var languages = Languages(new BrowseGroup("havsfunc",
            [new BrowseFunction("QTGMC", "QTGMC()", "havsfunc.QTGMC()", "havsfunc")]));
        var model = new FunctionsExplorerViewModel { Languages = languages.Object, Editor = editor };
        await model.ReloadAsync();
        await model.Insert.Execute();

        editor.Document.UndoStack.Undo();

        Assert.Equal(script, editor.Script);
    }

    [Fact]
    public async Task GoTo_ThisFile_MovesCaret()
    {
        var editor = new EditorViewModel { Script = "def Foo():\n    pass\n" };
        editor.CaretOffset = editor.Script.Length;
        var languages = Languages(new BrowseGroup("This file",
            [new BrowseFunction("Foo", "Foo()", "Foo()", Offset: 0)]));
        var model = new FunctionsExplorerViewModel { Languages = languages.Object, Editor = editor };
        await model.ReloadAsync();

        await model.GoTo.Execute();

        Assert.Equal(0, editor.CaretOffset);
        Assert.True(model.CanGoTo);
        Assert.True(model.CanInsert);
    }

    [Fact]
    public async Task GoTo_AlreadyAtOffset_StillReveals()
    {
        var editor = new EditorViewModel { Script = "def Foo():\n    pass\n" };
        editor.CaretOffset = 0;
        var languages = Languages(new BrowseGroup("This file",
            [new BrowseFunction("Foo", "Foo()", "Foo()", Offset: 0)]));
        var model = new FunctionsExplorerViewModel { Languages = languages.Object, Editor = editor };
        await model.ReloadAsync();
        await model.GoTo.Execute();
        var first = editor.RevealRequest;

        await model.GoTo.Execute();

        Assert.Equal(0, editor.CaretOffset);
        Assert.True(editor.RevealRequest > first);
    }

    [Fact]
    public async Task Insert_ThisFile_WritesCall()
    {
        var editor = new EditorViewModel { Script = "def Foo():\n    pass\n" };
        editor.CaretOffset = editor.Script.Length;
        var languages = Languages(new BrowseGroup("This file",
            [new BrowseFunction("Foo", "Foo()", "Foo()", Offset: 0)]));
        var model = new FunctionsExplorerViewModel { Languages = languages.Object, Editor = editor };
        await model.ReloadAsync();

        await model.Insert.Execute();

        Assert.Equal("def Foo():\n    pass\nFoo()", editor.Script);
        Assert.Equal(editor.Script.Length - 1, editor.CaretOffset);
        Assert.True(model.CanInsert);
        Assert.True(model.CanGoTo);
        Assert.Equal(0, model.SelectedFunction?.Offset);
    }

    [Fact]
    public async Task GoTo_AfterEdit_IsDisabled()
    {
        var editor = new EditorViewModel { Script = "def Foo():\n    pass\n" };
        var languages = Languages(new BrowseGroup("This file",
            [new BrowseFunction("Foo", "Foo()", "Foo()", Offset: 0)]));
        var model = new FunctionsExplorerViewModel { Languages = languages.Object, Editor = editor };
        await model.ReloadAsync();

        editor.Script = "x = 1\ndef Foo():\n    pass\n";

        Assert.False(model.CanGoTo);
        Assert.True(model.CanInsert);
    }

    [Fact]
    public async Task GoTo_InsertBeforeHeader_ShiftsOffset()
    {
        var editor = new EditorViewModel { Script = "def Foo():\n    pass\n" };
        var languages = Languages(new BrowseGroup("This file",
            [new BrowseFunction("Foo", "Foo()", "Foo()", Offset: 0)]));
        var model = new FunctionsExplorerViewModel { Languages = languages.Object, Editor = editor };
        await model.ReloadAsync();

        editor.Insert(0, "import vs\n");
        await model.GoTo.Execute();

        Assert.True(model.CanGoTo);
        Assert.Equal("import vs\n".Length, editor.CaretOffset);
        Assert.Equal("import vs\n".Length, model.SelectedFunction?.Offset);
    }

    [Fact]
    public async Task GoTo_EditDuringBrowse_KeepsCapturedVersion()
    {
        var editor = new EditorViewModel { Script = "def Foo():\n    pass\n" };
        var pending = new TaskCompletionSource<IReadOnlyList<BrowseGroup>>();
        var languages = InitMock<IScriptLanguageFactory>(mock =>
            mock.Setup(f => f.BrowseAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>(),
                    It.IsAny<string?>(), It.IsAny<IReadOnlyList<string>?>()))
                .Returns(pending.Task));
        var model = new FunctionsExplorerViewModel { Languages = languages.Object, Editor = editor };
        var reload = model.ReloadAsync();
        editor.Script = "x = 1\ndef Foo():\n    pass\n";
        pending.SetResult(
        [
            new BrowseGroup("This file",
                [new BrowseFunction("Foo", "Foo()", "Foo()", Offset: 0)])
        ]);

        await reload;

        Assert.False(model.CanGoTo);
        Assert.True(model.CanInsert);
    }

    [Fact]
    public async Task ReloadAsync_WhileLoaded_DisablesInsert()
    {
        var pending = new TaskCompletionSource<IReadOnlyList<BrowseGroup>>();
        var languages = Languages(new BrowseGroup("std", [new BrowseFunction("Crop", "Crop()", "core.std.Crop()")]));
        var model = new FunctionsExplorerViewModel
        {
            Languages = languages.Object,
            Editor = new EditorViewModel()
        };
        await model.ReloadAsync();
        languages.Setup(f => f.BrowseAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>(),
                It.IsAny<string?>(), It.IsAny<IReadOnlyList<string>?>()))
            .Returns(pending.Task);

        var reload = model.ReloadAsync();

        Assert.False(model.CanInsert);
        Assert.False(model.CanGoTo);
        pending.SetResult([new BrowseGroup("std", [new BrowseFunction("Crop", "Crop()", "core.std.Crop()")])]);
        await reload;
        Assert.True(model.CanInsert);
    }

    [Fact]
    public async Task Editor_KindChanged_Reloads()
    {
        var editor = new EditorViewModel { Script = "clip = ", Kind = ScriptKind.VapourSynth };
        var languages = InitMock<IScriptLanguageFactory>(mock =>
        {
            mock.Setup(f => f.BrowseAsync(ScriptLanguageFactory.VapourSynth, It.IsAny<string>(),
                    It.IsAny<CancellationToken>(), It.IsAny<string?>(), It.IsAny<IReadOnlyList<string>?>()))
                .ReturnsAsync(
                [
                    new BrowseGroup("std", [new BrowseFunction("Crop", "Crop()", "core.std.Crop()")])
                ]);
            mock.Setup(f => f.BrowseAsync(ScriptLanguageFactory.AviSynth, It.IsAny<string>(),
                    It.IsAny<CancellationToken>(), It.IsAny<string?>(), It.IsAny<IReadOnlyList<string>?>()))
                .ReturnsAsync(
                [
                    new BrowseGroup("Internal", [new BrowseFunction("Crop", "Crop()", "Crop()")])
                ]);
        });
        var model = new FunctionsExplorerViewModel { Languages = languages.Object, Editor = editor };
        await model.ReloadAsync();

        editor.Kind = ScriptKind.AviSynth;
        await model.ReloadAsync();

        Assert.Equal("Internal", Assert.Single(model.Groups).Name);
    }

    [Fact]
    public async Task Editor_FileNameChanged_ReloadsWithPath()
    {
        var editor = new EditorViewModel { Script = "clip = " };
        var languages = Languages(new BrowseGroup("std", [new BrowseFunction("Crop", "Crop()", "core.std.Crop()")]));
        var model = new FunctionsExplorerViewModel { Languages = languages.Object, Editor = editor };
        await model.ReloadAsync();

        editor.FileName = "/tmp/script.vpy";
        await model.ReloadAsync();

        languages.Verify(f => f.BrowseAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>(),
            "/tmp/script.vpy", It.IsAny<IReadOnlyList<string>?>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task Filter_SameName_KeepsInsertText()
    {
        var languages = Languages(
            new BrowseGroup("pkg.a", [new BrowseFunction("Foo", "Foo()", "pkg.a.Foo()")]),
            new BrowseGroup("pkg.b", [new BrowseFunction("Foo", "Foo()", "pkg.b.Foo()")]));
        var model = new FunctionsExplorerViewModel
        {
            Languages = languages.Object,
            Editor = new EditorViewModel()
        };
        await model.ReloadAsync();
        model.SelectedGroup = model.Groups.Single(group => group.Name == "pkg.b");

        model.Filter = "Foo";

        Assert.Equal("pkg.b.Foo()", model.SelectedFunction?.InsertText);
        Assert.Equal("pkg.b", model.SelectedHit?.Group);
    }

    [Fact]
    public async Task ReloadAsync_IOException_KeepsListAndSetsError()
    {
        var languages = Languages(new BrowseGroup("std", [new BrowseFunction("Crop", "Crop()", "core.std.Crop()")]));
        var model = new FunctionsExplorerViewModel
        {
            Languages = languages.Object,
            Editor = new EditorViewModel()
        };
        await model.ReloadAsync();
        languages.Setup(f => f.BrowseAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>(),
                It.IsAny<string?>(), It.IsAny<IReadOnlyList<string>?>()))
            .ThrowsAsync(new System.IO.IOException("disk"));

        await model.ReloadAsync();

        Assert.Equal("disk", model.Error);
        Assert.Equal("std", Assert.Single(model.Groups).Name);
        Assert.False(model.CanInsert);
    }

    private Mock<IScriptLanguageFactory> Languages(params BrowseGroup[] groups) =>
        InitMock<IScriptLanguageFactory>(mock =>
            mock.Setup(f => f.BrowseAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>(),
                    It.IsAny<string?>(), It.IsAny<IReadOnlyList<string>?>()))
                .ReturnsAsync(groups));
}
