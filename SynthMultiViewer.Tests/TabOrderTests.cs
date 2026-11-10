using System.Collections.ObjectModel;

namespace HanumanInstitute.SynthMultiViewer.Tests;

public class TabOrderTests
{
    [Fact]
    public void TryMove_Right_SwapsWithNeighbor()
    {
        var items = new ObservableCollection<string> { "a", "b", "c" };

        var moved = TabOrder.TryMove(items, "a", 1);

        Assert.True(moved);
        Assert.Equal(["b", "a", "c"], items);
    }

    [Fact]
    public void TryMove_Left_SwapsWithNeighbor()
    {
        var items = new ObservableCollection<string> { "a", "b", "c" };

        var moved = TabOrder.TryMove(items, "c", -1);

        Assert.True(moved);
        Assert.Equal(["a", "c", "b"], items);
    }

    [Fact]
    public void TryMove_PastStart_LeavesOrderUnchanged()
    {
        var items = new ObservableCollection<string> { "a", "b" };

        var moved = TabOrder.TryMove(items, "a", -1);

        Assert.False(moved);
        Assert.Equal(["a", "b"], items);
    }

    [Fact]
    public void TryMove_PastEnd_LeavesOrderUnchanged()
    {
        var items = new ObservableCollection<string> { "a", "b" };

        var moved = TabOrder.TryMove(items, "b", 1);

        Assert.False(moved);
        Assert.Equal(["a", "b"], items);
    }

    [Fact]
    public void TryMove_MissingItem_ReturnsFalse()
    {
        var items = new ObservableCollection<string> { "a" };

        var moved = TabOrder.TryMove(items, "missing", 1);

        Assert.False(moved);
    }

    [Fact]
    public void TryMove_NullItem_ReturnsFalse()
    {
        var items = new ObservableCollection<string> { "a" };

        var moved = TabOrder.TryMove(items, null, 1);

        Assert.False(moved);
    }
}
