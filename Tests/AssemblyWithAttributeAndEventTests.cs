﻿using Fody;
using Xunit;

public class AssemblyWithAttributeAndEventTests
{
    [Fact]
    public void WithAttributeAndEvent()
    {
        var weavingTask = new ModuleWeaver();
        var exception = Assert.Throws<WeavingException>(() => { weavingTask.ExecuteTestRun("AssemblyWithAttributeAndEvent.dll"); });
        Assert.Equal("The type 'ClassWithAttributeAndEvent' already has a PropertyChanged event. If type has a [AddINotifyPropertyChangedInterfaceAttribute] then the PropertyChanged event can be removed.", exception.Message);
    }
}