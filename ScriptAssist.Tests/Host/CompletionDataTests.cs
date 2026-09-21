using Avalonia.Controls;
using Avalonia.Media;
using HanumanInstitute.ScriptAssist.AvaloniaEdit;
using Xunit;

namespace HanumanInstitute.ScriptAssist.Tests.Host;

public class CompletionDataTests
{
    [Fact]
    public void Complete_FunctionHint_ShowsSignature()
    {
        var parameters = Enumerable.Repeat("clip:vnode:opt", 20).ToArray();
        var signature = new Symbol("core.rife.RIFE", parameters).Signature;
        var item = new CompletionItem("RIFE", 0, 4, SymbolKind.Function, signature);

        var hint = CompletionData.HintText(item)!;

        Assert.StartsWith("RIFE(", hint, StringComparison.Ordinal);
        Assert.Contains("clip:vnode:opt, clip:vnode:opt", hint, StringComparison.Ordinal);
    }

    [Fact]
    public void Complete_FunctionHint_IncludesReturnType()
    {
        var signature = new Symbol("BlankClip", ["width:int:opt"], ReturnType: "clip:vnode;").Signature;

        var hint = CompletionData.HintText(new("BlankClip", 0, 9, SymbolKind.Function, signature));

        Assert.Equal("BlankClip(width:int:opt) -> clip:vnode", hint);
    }

    [Fact]
    public void Complete_EmptyFunctionHint_ShowsCall()
    {
        var hint = CompletionData.HintText(new("Foo", 0, 3, SymbolKind.Function, "Foo()"));

        Assert.Equal("Foo()", hint);
    }

    [Fact]
    public void Complete_NamespaceHint_ReturnsNull()
    {
        var hint = CompletionData.HintText(new("rife", 0, 4, SymbolKind.Namespace, "core.rife"));

        Assert.Null(hint);
    }

    [Fact]
    public void Complete_BoundPluginHint_ShowsLabel()
    {
        var hint = CompletionData.HintText(new("bm3d", 0, 4, SymbolKind.Namespace, "bm3d: plugin"));

        Assert.Equal("plugin", hint);
    }

    [Theory]
    [InlineData(SymbolKind.Property, "width", 5, "width: int", "int")]
    [InlineData(SymbolKind.Local, "C", 1, "C: clip", "clip")]
    [InlineData(SymbolKind.Property, "fps", 3, "fps: Fraction", "Fraction")]
    public void Complete_TypedHint_ShowsType(SymbolKind kind, string name, int length, string signature, string expected)
    {
        var hint = CompletionData.HintText(new(name, 0, length, kind, signature));

        Assert.Equal(expected, hint);
    }

    [Fact]
    public void Complete_PropertyWithoutType_ReturnsNull()
    {
        var hint = CompletionData.HintText(new("fps", 0, 3, SymbolKind.Property, "fps"));

        Assert.Null(hint);
    }

    [Fact]
    public void Complete_EmptyHint_ClearsDescription()
    {
        var description = new CompletionData(new("fps", 0, 3, SymbolKind.Property, "fps")).Description;

        Assert.Null(description);
    }

    [Fact]
    public void Complete_HintDescription_WrapsAndEllipsizes()
    {
        var parameters = Enumerable.Repeat("clip:vnode:opt", 20).ToArray();
        var item = new CompletionItem("RIFE", 0, 4, SymbolKind.Function,
            new Symbol("core.rife.RIFE", parameters).Signature);
        var hint = CompletionData.HintText(item)!;

        var description = Assert.IsType<TextBlock>(new CompletionData(item).Description);

        Assert.Equal(AssistTipSize.Hint.MaxWidth, description.MaxWidth);
        Assert.Equal(AssistTipSize.Hint.MaxLines, description.MaxLines);
        Assert.Equal(TextWrapping.Wrap, description.TextWrapping);
        Assert.Equal(TextTrimming.CharacterEllipsis, description.TextTrimming);
        Assert.Equal(hint, description.Text);
    }

    [Fact]
    public void Complete_LongHint_TruncatesWithEllipsis()
    {
        var huge = CompletionData.TruncateHint(new('x', AssistTipSize.Hint.MaxCharacters + 100),
            AssistTipSize.Hint.MaxCharacters);

        Assert.True(huge.Length <= AssistTipSize.Hint.MaxCharacters);
        Assert.EndsWith("…", huge);
    }

    [Fact]
    public void Complete_HoverTip_UsesHoverSize()
    {
        var size = AssistTipSize.Hover;
        var text = new string('x', size.MaxCharacters + 80);

        var hover = HoverPresenter.CreateTip(text);

        Assert.Equal(size.MaxWidth, hover.MaxWidth);
        Assert.True(double.IsNaN(hover.Width));
        Assert.Equal(size.MaxLines, hover.MaxLines);
        Assert.Equal(TextWrapping.Wrap, hover.TextWrapping);
        Assert.Equal(TextTrimming.CharacterEllipsis, hover.TextTrimming);
        Assert.True(hover.Text!.Length <= size.MaxCharacters);
        Assert.EndsWith("…", hover.Text);
        Assert.True(size.MaxWidth > AssistTipSize.Hint.MaxWidth);
        Assert.True(size.MaxCharacters > AssistTipSize.Hint.MaxCharacters);
    }

    [Fact]
    public void Complete_TypeHover_DoesNotForceHintWidth()
    {
        var hover = HoverPresenter.CreateTip("VideoNode");

        Assert.Equal("VideoNode", hover.Text);
        Assert.Equal(AssistTipSize.Hover.MaxWidth, hover.MaxWidth);
        Assert.True(double.IsNaN(hover.Width));
    }

    [Fact]
    public void Complete_HoverPopup_OverridesFluentMaxWidth()
    {
        var size = AssistTipSize.Hover;

        var popup = HoverPresenter.CreatePopup("BlankClip(width:int:opt)");

        Assert.Equal(size.MaxWidth, popup.MaxWidth);
        var content = Assert.IsType<TextBlock>(popup.Content);
        Assert.Equal(size.MaxWidth, content.MaxWidth);
    }

    [Fact]
    public void Complete_CustomTipSize_AppliesToBlock()
    {
        var size = new AssistTipSize { MaxWidth = 320, MaxLines = 3, MaxCharacters = 12 };

        var block = CompletionData.HintBlock("abcdefghijklmnopqrstuvwxyz", size);

        Assert.Equal(320, block.MaxWidth);
        Assert.Equal(3, block.MaxLines);
        Assert.Equal("abcdefghijk…", block.Text);
    }

    [Fact]
    public void Update_SameOverloads_KeepsSelectedIndex()
    {
        var first = new CallInsight([new Symbol("A", ["x"]), new Symbol("B", ["y"])], 0, false);
        var provider = new OverloadProvider(first);
        provider.SelectedIndex = 1;

        var names = new List<string?>();
        provider.PropertyChanged += (_, e) => names.Add(e.PropertyName);
        provider.Update(new([new Symbol("A", ["x"]), new Symbol("B", ["y"])], 0, false));

        Assert.Equal(1, provider.SelectedIndex);
        Assert.Equal("Parameter 1: y", provider.CurrentContent);
        Assert.DoesNotContain(nameof(OverloadProvider.CurrentHeader), names);
    }

    [Fact]
    public void Update_SameSignature_ReusesHeader()
    {
        var overload = new Symbol("Crop", ["clip", "int [left]", "int [top]"]);
        var provider = new OverloadProvider(new CallInsight([overload], 0, false));
        var header = provider.CurrentHeader;

        provider.Update(new CallInsight([overload], 2, false));

        Assert.Same(header, provider.CurrentHeader);
        Assert.Equal("Parameter 3: int [top]", provider.CurrentContent);
    }

    [Fact]
    public void Complete_ZeroMaxCharacters_Truncates()
    {
        var item = new CompletionItem("Foo", 0, 3, SymbolKind.Function, "Foo(bar)");
        var size = new AssistTipSize { MaxCharacters = 0 };

        var hint = CompletionData.HintText(item, size);

        Assert.Equal("…", hint);
    }

    [Fact]
    public void Update_DifferentOverloads_ResetsSelection()
    {
        var first = new CallInsight([new Symbol("A", ["x"]), new Symbol("B", ["y"])], 0, false);
        var provider = new OverloadProvider(first);
        provider.SelectedIndex = 1;

        provider.Update(new([new Symbol("C", ["z"])], 0, false));

        Assert.Equal(0, provider.SelectedIndex);
        var header = Assert.IsType<TextBlock>(provider.CurrentHeader);
        Assert.Equal("C(z)", header.Text);
    }

    [Fact]
    public void Update_AutomaticSelection_FollowsNamedArgument()
    {
        var left = new Symbol("Overloaded", ["left"]);
        var right = new Symbol("Overloaded", ["right"]);
        var provider = new OverloadProvider(new CallInsight([left, right], 0, false));

        provider.Update(new CallInsight([left, right], 0, false)
        {
            Keyword = "right",
            OverloadSlots = [-1, 0]
        });

        Assert.Equal(1, provider.SelectedIndex);
        Assert.Equal("Parameter 1: right", provider.CurrentContent);
    }

    [Fact]
    public void Update_ExplicitSelection_StaysOnChosenOverload()
    {
        var left = new Symbol("Overloaded", ["left"]);
        var right = new Symbol("Overloaded", ["right"]);
        var provider = new OverloadProvider(new CallInsight([left, right], 0, false));
        provider.SelectedIndex = 0;

        provider.Update(new CallInsight([left, right], 0, false)
        {
            Keyword = "right",
            OverloadSlots = [-1, 0]
        });

        Assert.Equal(0, provider.SelectedIndex);
        Assert.Equal("No more parameters", provider.CurrentContent);
    }

    [Fact]
    public void Update_LongSignature_WrapsHeader()
    {
        var parameters = Enumerable.Repeat("clip:vnode:opt", 20).ToArray();
        var provider = new OverloadProvider(new CallInsight(
            [new Symbol("core.std.BlankClip", parameters)], 0, false));

        var header = Assert.IsType<TextBlock>(provider.CurrentHeader);

        Assert.Equal(AssistTipSize.Hover.MaxWidth, header.MaxWidth);
        Assert.Equal(TextWrapping.Wrap, header.TextWrapping);
    }
}
