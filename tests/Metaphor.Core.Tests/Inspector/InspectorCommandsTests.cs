using Metaphor.Core.Inspector;

namespace Metaphor.Core.Tests.Inspector;

/// <summary>The live inspector's commands (#291) against a byte array standing in for memory: a 0x400-byte "module" at
/// <see cref="Base"/>, everything else unreadable, as <c>ReadProcessMemory</c> fails on a page it can't read.</summary>
[Trait("Category", "Unit")]
public sealed class InspectorCommandsTests
{
    private const long Base = 0x140000000;
    private readonly byte[] memory = new byte[0x400];
    private long readableUpTo = 0x400;

    private InspectorCommands Commands() => new(Base, memory.Length, (address, buffer) =>
    {
        if (address < Base || address - Base + buffer.Length > readableUpTo)
        {
            return false;
        }
        memory.AsSpan((int)(address - Base), buffer.Length).CopyTo(buffer);
        return true;
    });

    private void Put(long offset, long value) => BitConverter.GetBytes(value).CopyTo(memory, offset);

    private void Put(long offset, int value) => BitConverter.GetBytes(value).CopyTo(memory, offset);

    private void Put(long offset, string text) => System.Text.Encoding.Latin1.GetBytes(text + "\0").CopyTo(memory, offset);

    [Fact]
    public void BaseGivesTheModulesAddressAndSize() =>
        Assert.Equal("ok 0x140000000 0x400", Commands().Respond("base"));

    [Fact]
    public void ReadGivesTheBytesAsHex()
    {
        memory[0x10] = 0xab;
        memory[0x11] = 0x01;

        Assert.Equal("ok ab0100", Commands().Respond("read 0x140000010 3"));
    }

    [Fact]
    public void ReadTakesAModuleRelativeAddressAndAHexCount()
    {
        memory[0x20] = 0x7f;

        Assert.Equal("ok 7f00", Commands().Respond("read base+0x20 0x2"));
    }

    [Fact]
    public void ReadOfUnreadableMemoryIsAnError() =>
        Assert.Equal("err unreadable", Commands().Respond("read 0x10 4"));

    [Theory]
    [InlineData("read base+0x0 0")]
    [InlineData("read base+0x0 4097")]
    [InlineData("read base+0x0 -1")]
    public void ReadRefusesACountOutsideItsLimit(string line) =>
        Assert.Equal($"err count must be 1 to {InspectorCommands.MaxReadBytes}", Commands().Respond(line));

    [Theory]
    [InlineData("read 140000010 4")]
    [InlineData("read base+16 4")]
    [InlineData("read 0xzz 4")]
    [InlineData("read")]
    [InlineData("read base+0x0 4 5")]
    public void MalformedArgumentsAreAnError(string line) =>
        Assert.StartsWith("err usage: read ", Commands().Respond(line));

    [Fact]
    public void FollowWalksAPointerPath()
    {
        // [[base+0x40]+0x8]+0x4: base+0x40 holds base+0x100, base+0x108 holds base+0x200.
        Put(0x40, Base + 0x100);
        Put(0x108, Base + 0x200);

        Assert.Equal("ok 0x140000204", Commands().Respond("follow base+0x40 0x8 0x4"));
    }

    [Fact]
    public void FollowTakesNegativeOffsets()
    {
        Put(0x40, Base + 0x100);

        Assert.Equal("ok 0x1400000f0", Commands().Respond("follow base+0x40 -0x10"));
    }

    [Fact]
    public void FollowSaysWhichStepWasUnreadable()
    {
        Put(0x40, 0x10);

        Assert.Equal("err unreadable at step 2 (0x18)", Commands().Respond("follow base+0x40 0x8 0x0"));
    }

    [Fact]
    public void FollowRefusesAPathLongerThanItsLimit() =>
        Assert.Equal($"err at most {InspectorCommands.MaxPathSteps} offsets",
            Commands().Respond("follow base+0x0" + string.Concat(Enumerable.Repeat(" 0x0", InspectorCommands.MaxPathSteps + 1))));

    [Fact]
    public void RttiNamesTheClassOfAnObject()
    {
        // An object at +0x100 whose vtable is at +0x210; the locator before it at +0x300, its type descriptor at +0x380.
        Put(0x100, Base + 0x210);
        Put(0x208, Base + 0x300);
        Put(0x300, 1);
        Put(0x30c, 0x380);
        Put(0x314, 0x300);
        Put(0x390, ".?AVTitleMenuChoice@ope@ui@app@@");

        Assert.Equal("ok TitleMenuChoice@ope@ui@app", Commands().Respond("rtti base+0x100"));
    }

    [Fact]
    public void RttiOfSomethingWithoutAClassIsAnError() =>
        Assert.Equal("err no class", Commands().Respond("rtti base+0x100"));

    [Fact]
    public void ChangesReportsOnlyWatchesWhoseBytesChanged()
    {
        var commands = Commands();
        Assert.Equal("ok 1", commands.Respond("watch base+0x10 2"));
        Assert.Equal("ok 2", commands.Respond("watch base+0x20 1"));
        Assert.Equal("ok", commands.Respond("changes"));

        memory[0x11] = 0x05;

        Assert.Equal("ok 1=0005", commands.Respond("changes"));
        Assert.Equal("ok", commands.Respond("changes"));
    }

    [Fact]
    public void AWatchThatBecomesUnreadableOrReadableAgainIsAChange()
    {
        var commands = Commands();
        Assert.Equal("ok 1", commands.Respond("watch base+0x10 1"));

        readableUpTo = 0x10;
        Assert.Equal("ok 1=unreadable", commands.Respond("changes"));
        Assert.Equal("ok", commands.Respond("changes"));

        readableUpTo = memory.Length;
        Assert.Equal("ok 1=00", commands.Respond("changes"));
    }

    [Fact]
    public void WatchesListsEachWatch()
    {
        var commands = Commands();
        commands.Respond("watch base+0x10 2");
        commands.Respond("watch 0x10 4");

        Assert.Equal("ok 1=0x140000010+2 2=0x10+4", commands.Respond("watches"));
    }

    [Fact]
    public void UnwatchRemovesAWatch()
    {
        var commands = Commands();
        commands.Respond("watch base+0x10 1");
        Assert.Equal("ok", commands.Respond("unwatch 1"));

        memory[0x10] = 1;

        Assert.Equal("ok", commands.Respond("changes"));
        Assert.Equal("err no watch 1", commands.Respond("unwatch 1"));
    }

    [Fact]
    public void WatchRefusesMoreThanItsLimit()
    {
        var commands = Commands();
        for (var i = 0; i < InspectorCommands.MaxWatches; i++)
        {
            commands.Respond("watch base+0x0 1");
        }

        Assert.Equal($"err at most {InspectorCommands.MaxWatches} watches", commands.Respond("watch base+0x0 1"));
    }

    [Fact]
    public void WatchRefusesACountOverItsLimit() =>
        Assert.Equal($"err count must be 1 to {InspectorCommands.MaxWatchBytes}",
            Commands().Respond($"watch base+0x0 {InspectorCommands.MaxWatchBytes + 1}"));

    [Theory]
    [InlineData("write base+0x0 00")]
    [InlineData("call base+0x0")]
    [InlineData("")]
    [InlineData("READ base+0x0 1")]
    public void AnythingButTheReadCommandsIsRefused(string line) =>
        Assert.Equal("err unknown command; try help", Commands().Respond(line));

    [Fact]
    public void HelpListsEveryCommand()
    {
        var help = Commands().Respond("help");

        Assert.StartsWith("ok ", help);
        Assert.All(InspectorCommands.Names, name => Assert.Contains(name, help));
    }
}
