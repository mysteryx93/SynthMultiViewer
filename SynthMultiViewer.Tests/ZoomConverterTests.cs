using System.Globalization;
using Avalonia.Data;

namespace HanumanInstitute.SynthMultiViewer.Tests;

public class ZoomConverterTests
{
    [Fact]
    public void Convert_One_ReturnsOneHundredPercent()
    {
        var converter = new ZoomConverter();

        var text = converter.Convert(1.0, typeof(string), null, CultureInfo.InvariantCulture);

        Assert.Equal("100%", text);
    }

    [Fact]
    public void Convert_Zero_ReturnsScaleToFitLabel()
    {
        var converter = new ZoomConverter();

        var text = converter.Convert(0.0, typeof(string), null, CultureInfo.InvariantCulture);

        Assert.Equal("Scale to Fit", text);
    }

    [Fact]
    public void ConvertBack_ScaleToFitLabel_ReturnsZero()
    {
        var converter = new ZoomConverter();

        var value = converter.ConvertBack("Scale to Fit", typeof(double), null, CultureInfo.InvariantCulture);

        Assert.Equal(0.0, value);
    }

    [Fact]
    public void ConvertBack_Percentage_ReturnsFactor()
    {
        var converter = new ZoomConverter();

        var value = converter.ConvertBack("150%", typeof(double), null, CultureInfo.InvariantCulture);

        Assert.Equal(1.5, value);
    }

    [Fact]
    public void ConvertBack_PlainNumber_ReturnsFactor()
    {
        var converter = new ZoomConverter();

        var value = converter.ConvertBack("50", typeof(double), null, CultureInfo.InvariantCulture);

        Assert.Equal(0.5, value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("abc")]
    [InlineData("%")]
    public void ConvertBack_IncompleteText_LeavesBindingUnchanged(string? text)
    {
        var converter = new ZoomConverter();

        var value = converter.ConvertBack(text, typeof(double), null, CultureInfo.InvariantCulture);

        Assert.Same(BindingOperations.DoNothing, value);
    }
}
