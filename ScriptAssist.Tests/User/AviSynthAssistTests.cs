using System.Diagnostics.CodeAnalysis;
using HanumanInstitute.ScriptAssist.AvaloniaEdit;
using Xunit;

namespace HanumanInstitute.ScriptAssist.Tests.User;

using static AssistHarness;

[SuppressMessage("Usage", "xUnit1051:Calls to methods which accept CancellationToken should use TestContext.Current.CancellationToken")]
public class AviSynthAssistTests
{
    private static readonly IReadOnlyList<Symbol> Mixed =
    [
        new("F", ["int [width]", "string [name]"]),
        new("F", ["clip [source]", "int [height]"])
    ];

    private static readonly IReadOnlyList<Symbol> Crop =
    [
        new("Crop", ["clip", "int [left]", "int [top]"])
    ];

    private static readonly IReadOnlyList<Symbol> Nested =
    [
        new("Crop", ["clip", "int [left]", "int [top]"]),
        new("BlankClip", ["clip", "int [length]", "int [width]"])
    ];

    [Fact]
    public void Insight_MixedOverloads_SkipClipPerSignature()
    {
        const string empty = "F(";
        const string afterInt = "F(10,";

        var atOpen = AvsService().Analyze(empty, empty.Length, Mixed).Insight!;
        var afterValue = AvsService().Analyze(afterInt, afterInt.Length, Mixed).Insight!;

        Assert.Contains("width", OverloadProvider.ActiveParameterText(atOpen, 0), StringComparison.Ordinal);
        Assert.DoesNotContain("name", OverloadProvider.ActiveParameterText(atOpen, 0), StringComparison.Ordinal);
        Assert.Contains("height", OverloadProvider.ActiveParameterText(atOpen, 1), StringComparison.Ordinal);
        Assert.Contains("name", OverloadProvider.ActiveParameterText(afterValue, 0), StringComparison.Ordinal);
        Assert.DoesNotContain("No more parameters", OverloadProvider.ActiveParameterText(afterValue, 0),
            StringComparison.Ordinal);
    }

    [Fact]
    public void Complete_MixedOverloads_OffersNonClipName()
    {
        const string text = "F(";

        var reply = AvsService().Analyze(text, text.Length, Mixed);

        Assert.Contains(reply.Items, x => x.InsertionText == "width=");
        Assert.Contains(reply.Items, x => x.InsertionText == "height=");
        Assert.DoesNotContain(reply.Items, x => x.InsertionText == "source=");
    }

    [Fact]
    public void Complete_MixedOverloadsAfterInt_OffersRemainingName()
    {
        const string text = "F(10,";

        var reply = AvsService().Analyze(text, text.Length, Mixed);

        Assert.Contains(reply.Items, x => x.InsertionText == "name=");
        Assert.DoesNotContain(reply.Items, x => x.InsertionText == "width=");
    }

    [Fact]
    public void Insight_ParenthesizedNumber_IsNotClip()
    {
        const string text = "Crop((10),";

        var insight = AvsService().Analyze(text, text.Length, Crop).Insight!;

        Assert.Contains("top", OverloadProvider.ActiveParameterText(insight), StringComparison.Ordinal);
        Assert.DoesNotContain("[left]", OverloadProvider.ActiveParameterText(insight), StringComparison.Ordinal);
    }

    [Fact]
    public void Complete_ParenthesizedNumber_OffersRemainingName()
    {
        const string text = "Crop((10),";

        var reply = AvsService().Analyze(text, text.Length, Crop);

        Assert.Contains(reply.Items, x => x.InsertionText == "top=");
        Assert.DoesNotContain(reply.Items, x => x.InsertionText == "left=");
    }

    [Fact]
    public void Insight_ParenthesizedClip_MapsFirstArgument()
    {
        const string text = "Crop((last + last),";

        var insight = AvsService().Analyze(text, text.Length, Crop).Insight!;

        Assert.Contains("left", OverloadProvider.ActiveParameterText(insight), StringComparison.Ordinal);
        Assert.DoesNotContain("[top]", OverloadProvider.ActiveParameterText(insight), StringComparison.Ordinal);
    }

    [Fact]
    public void Complete_ParenthesizedClip_OffersFirstCropName()
    {
        const string text = "Crop((last + last),";

        var reply = AvsService().Analyze(text, text.Length, Crop);

        Assert.Contains(reply.Items, x => x.InsertionText == "left=");
    }

    [Fact]
    public void Insight_NestedInner_TracksBlankClip()
    {
        const string text = "Crop(BlankClip(100,";

        var insight = AvsService().Analyze(text, text.Length, Nested).Insight!;

        Assert.Equal("BlankClip", insight.Overloads[0].Name);
        Assert.Contains("width", OverloadProvider.ActiveParameterText(insight), StringComparison.Ordinal);
        Assert.DoesNotContain("[left]", OverloadProvider.ActiveParameterText(insight), StringComparison.Ordinal);
    }

    [Fact]
    public void Complete_NestedInner_OffersBlankClipNames()
    {
        const string text = "Crop(BlankClip(100,";

        var reply = AvsService().Analyze(text, text.Length, Nested);

        Assert.Contains(reply.Items, x => x.InsertionText == "width=");
        Assert.DoesNotContain(reply.Items, x => x.InsertionText == "left=");
    }

    [Fact]
    public void Insight_NestedAfterClose_MapsInnerAsClip()
    {
        const string text = "Crop(BlankClip(),";

        var insight = AvsService().Analyze(text, text.Length, Nested).Insight!;

        Assert.Equal("Crop", insight.Overloads[0].Name);
        Assert.Contains("left", OverloadProvider.ActiveParameterText(insight), StringComparison.Ordinal);
        Assert.DoesNotContain("[top]", OverloadProvider.ActiveParameterText(insight), StringComparison.Ordinal);
    }

    [Fact]
    public void Complete_NestedAfterClose_OffersCropNames()
    {
        const string text = "Crop(BlankClip(),";

        var reply = AvsService().Analyze(text, text.Length, Nested);

        Assert.Contains(reply.Items, x => x.InsertionText == "left=");
        Assert.DoesNotContain(reply.Items, x => x.InsertionText == "width=");
    }

    [Fact]
    public void Insight_BoundOuterNestedCall_MapsInnerAsLeft()
    {
        const string text = "last.Crop(BlankClip(),";

        var insight = AvsService().Analyze(text, text.Length, Nested).Insight!;

        Assert.Equal("Crop", insight.Overloads[0].Name);
        Assert.True(insight.ImplicitClip);
        Assert.Contains("top", OverloadProvider.ActiveParameterText(insight), StringComparison.Ordinal);
        Assert.DoesNotContain("[left]", OverloadProvider.ActiveParameterText(insight), StringComparison.Ordinal);
    }

    [Fact]
    public void Insight_NestedParenthesizedCall_MapsAsClip()
    {
        const string text = "Crop((BlankClip()),";

        var insight = AvsService().Analyze(text, text.Length, Nested).Insight!;

        Assert.Equal("Crop", insight.Overloads[0].Name);
        Assert.Contains("left", OverloadProvider.ActiveParameterText(insight), StringComparison.Ordinal);
        Assert.DoesNotContain("[top]", OverloadProvider.ActiveParameterText(insight), StringComparison.Ordinal);
    }

    [Fact]
    public void Insight_EqualsInsideString_StaysPositional()
    {
        const string text = "Crop(\"a=b\"";

        var insight = AvsService().Analyze(text, text.Length, Crop).Insight!;

        Assert.Contains("left", OverloadProvider.ActiveParameterText(insight), StringComparison.Ordinal);
        Assert.DoesNotContain("No more parameters", OverloadProvider.ActiveParameterText(insight),
            StringComparison.Ordinal);
    }

    [Fact]
    public void Insight_ScalarArithmetic_SkipsImplicitClip()
    {
        const string added = "Crop(10 + 20,";
        const string width = "Crop(Width() + 2,";

        var afterAdd = AvsService().Analyze(added, added.Length, Crop).Insight!;
        var afterWidth = AvsService().Analyze(width, width.Length, Crop).Insight!;

        Assert.Contains("top", OverloadProvider.ActiveParameterText(afterAdd), StringComparison.Ordinal);
        Assert.DoesNotContain("[left]", OverloadProvider.ActiveParameterText(afterAdd), StringComparison.Ordinal);
        Assert.Contains("top", OverloadProvider.ActiveParameterText(afterWidth), StringComparison.Ordinal);
        Assert.DoesNotContain("[left]", OverloadProvider.ActiveParameterText(afterWidth), StringComparison.Ordinal);
    }

    [Fact]
    public void Hover_StringAssignment_ShowsStringType()
    {
        const string text = "x=\"text\"\nx";

        var hover = AvsService().Analyze(text, text.Length, Crop).Hover;

        Assert.Equal("string", hover?.Text);
    }

    [Fact]
    public void Hover_StringWithQuestion_ShowsStringType()
    {
        const string text = "x=\"why?last:1\"\nx";

        var hover = AvsService().Analyze(text, text.Length, Crop).Hover;

        Assert.Equal("string", hover?.Text);
    }

    [Fact]
    public void Hover_ParenthesizedQuote_ShowsStringType()
    {
        const string text = "x=(\"(\")\nx";

        var hover = AvsService().Analyze(text, text.Length, Crop).Hover;

        Assert.Equal("string", hover?.Text);
    }

    [Fact]
    public void Insight_ParenthesizedString_MapsFirstArgument()
    {
        const string text = "Crop((\"text\"),";

        var insight = AvsService().Analyze(text, text.Length, Crop).Insight!;

        Assert.Contains("top", OverloadProvider.ActiveParameterText(insight), StringComparison.Ordinal);
        Assert.DoesNotContain("[left]", OverloadProvider.ActiveParameterText(insight), StringComparison.Ordinal);
    }

    [Fact]
    public void Insight_ConcatenatedString_MapsFirstArgument()
    {
        const string text = "Crop(\"a\" + \"b\",";

        var insight = AvsService().Analyze(text, text.Length, Crop).Insight!;

        Assert.Contains("top", OverloadProvider.ActiveParameterText(insight), StringComparison.Ordinal);
        Assert.DoesNotContain("[left]", OverloadProvider.ActiveParameterText(insight), StringComparison.Ordinal);
    }

    [Fact]
    public void Hover_FloatPlusInt_ShowsFloat()
    {
        const string text = "x=1.5 + 2\nx";

        var hover = AvsService().Analyze(text, text.Length, Crop).Hover;

        Assert.Equal("float", hover?.Text);
    }

    [Fact]
    public void Hover_IntPlusFloat_ShowsFloat()
    {
        const string text = "x=2 + 1.5\nx";

        var hover = AvsService().Analyze(text, text.Length, Crop).Hover;

        Assert.Equal("float", hover?.Text);
    }

    [Fact]
    public void Complete_IntReceiver_OmitsClipMembers()
    {
        const string text = "x=1\nx.";

        var reply = AvsService().Analyze(text, text.Length, Crop);

        Assert.DoesNotContain(reply.Items, x => x.InsertionText == "Crop");
    }

    [Fact]
    public void Hover_IntReceiverMember_IsSilent()
    {
        const string text = "x=1\nx.Crop";

        var hover = AvsService().Analyze(text, text.Length, Crop).Hover;

        Assert.Null(hover);
    }

    [Fact]
    public void Insight_IntReceiverCall_IsSilent()
    {
        const string text = "x=1\nx.Crop(";

        var insight = AvsService().Analyze(text, text.Length, Crop).Insight;

        Assert.Null(insight);
    }
}
