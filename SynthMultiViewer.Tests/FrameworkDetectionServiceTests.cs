using HanumanInstitute.ScriptAssist;
using HanumanInstitute.ScriptAssist.AviSynth;
using HanumanInstitute.ScriptAssist.Tests;
using HanumanInstitute.ScriptAssist.VapourSynth;
using HanumanInstitute.SynthMultiViewer.Services;
using Moq;
using Xunit;

namespace HanumanInstitute.SynthMultiViewer.Tests;

public class FrameworkDetectionServiceTests : TestsBase
{
    public FrameworkDetectionServiceTests(ITestOutputHelper output) : base(output)
    {
    }

    [Fact]
    public void Apply_WritesFactoryEnablementFromSettings()
    {
        var vs = InitMock<IVapourSynthNativeCatalog>(n => n.Setup(x => x.Read()).Returns([]));
        var avs = InitMock<IAviSynthNativeCatalog>(n => n.Setup(x => x.Read()).Returns([]));
        var folders = InitMock<IScriptDirectory>(d =>
        {
            d.Setup(x => x.Roots()).Returns([]);
            d.Setup(x => x.Files(It.IsAny<string>(), It.IsAny<IReadOnlyList<string>>())).Returns([]);
            d.Setup(x => x.TryRead(It.IsAny<string>())).Returns((string?)null);
        });
        var factory = new ScriptLanguageFactory(vs.Object, avs.Object, folders.Object);
        var settings = new TestSupport.MemorySettingsProvider
        {
            Value = { EnhanceEditorWithAutoComplete = false }
        };
        var detection = new FrameworkDetectionService(settings, factory);
        var disabled = factory.IsEnabled;
        settings.Value.EnhanceEditorWithAutoComplete = true;

        detection.Apply(settings.Value);

        Assert.False(disabled);
        Assert.True(factory.IsEnabled);
    }
}
