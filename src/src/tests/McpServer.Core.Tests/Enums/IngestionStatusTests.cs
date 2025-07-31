using System;
using FluentAssertions;
using McpServer.Core.Enums;
using Xunit;

namespace McpServer.Core.Tests.Enums;

/// <summary>
/// Unit tests for the IngestionStatus enum
/// </summary>
public class IngestionStatusTests
{
    [Fact]
    public void IngestionStatus_Should_Have_Expected_Values()
    {
        // Assert
        Enum.GetValues<IngestionStatus>().Should().HaveCount(6);
        Enum.IsDefined(typeof(IngestionStatus), IngestionStatus.Pending).Should().BeTrue();
        Enum.IsDefined(typeof(IngestionStatus), IngestionStatus.InProgress).Should().BeTrue();
        Enum.IsDefined(typeof(IngestionStatus), IngestionStatus.Completed).Should().BeTrue();
        Enum.IsDefined(typeof(IngestionStatus), IngestionStatus.CompletedWithErrors).Should().BeTrue();
        Enum.IsDefined(typeof(IngestionStatus), IngestionStatus.Failed).Should().BeTrue();
        Enum.IsDefined(typeof(IngestionStatus), IngestionStatus.Cancelled).Should().BeTrue();
    }

    [Theory]
    [InlineData(IngestionStatus.Pending, 0)]
    [InlineData(IngestionStatus.InProgress, 1)]
    [InlineData(IngestionStatus.Completed, 2)]
    [InlineData(IngestionStatus.CompletedWithErrors, 3)]
    [InlineData(IngestionStatus.Failed, 4)]
    [InlineData(IngestionStatus.Cancelled, 5)]
    public void IngestionStatus_Should_Have_Correct_Numeric_Values(IngestionStatus status, int expectedValue)
    {
        // Assert
        ((int)status).Should().Be(expectedValue);
    }

    [Theory]
    [InlineData("Pending")]
    [InlineData("InProgress")]
    [InlineData("Completed")]
    [InlineData("CompletedWithErrors")]
    [InlineData("Failed")]
    [InlineData("Cancelled")]
    public void IngestionStatus_Should_Parse_From_String(string statusName)
    {
        // Act
        var success = Enum.TryParse<IngestionStatus>(statusName, out var result);

        // Assert
        success.Should().BeTrue();
        result.ToString().Should().Be(statusName);
    }

    [Theory]
    [InlineData("InvalidStatus")]
    [InlineData("Running")]
    [InlineData("")]
    public void IngestionStatus_Should_Not_Parse_Invalid_Values(string statusName)
    {
        // Act
        var success = Enum.TryParse<IngestionStatus>(statusName, out _);

        // Assert
        success.Should().BeFalse();
    }

    [Fact]
    public void IngestionStatus_Should_Support_State_Transitions()
    {
        // This test verifies logical state transitions
        var status = IngestionStatus.Pending;
        
        // Pending can transition to InProgress
        status = IngestionStatus.InProgress;
        status.Should().Be(IngestionStatus.InProgress);
        
        // InProgress can transition to Completed
        status = IngestionStatus.Completed;
        status.Should().Be(IngestionStatus.Completed);
        
        // Or InProgress can transition to Failed
        status = IngestionStatus.InProgress;
        status = IngestionStatus.Failed;
        status.Should().Be(IngestionStatus.Failed);
    }

    [Theory]
    [InlineData(IngestionStatus.Pending, true)]
    [InlineData(IngestionStatus.InProgress, true)]
    [InlineData(IngestionStatus.Completed, false)]
    [InlineData(IngestionStatus.Failed, false)]
    public void IngestionStatus_Should_Identify_Active_States(IngestionStatus status, bool expectedIsActive)
    {
        // Act
        var isActive = status is IngestionStatus.Pending or IngestionStatus.InProgress;

        // Assert
        isActive.Should().Be(expectedIsActive);
    }

    [Theory]
    [InlineData(IngestionStatus.Completed, true)]
    [InlineData(IngestionStatus.CompletedWithErrors, true)]
    [InlineData(IngestionStatus.Failed, true)]
    [InlineData(IngestionStatus.Cancelled, true)]
    [InlineData(IngestionStatus.Pending, false)]
    [InlineData(IngestionStatus.InProgress, false)]
    public void IngestionStatus_Should_Identify_Terminal_States(IngestionStatus status, bool expectedIsTerminal)
    {
        // Act
        var isTerminal = status is IngestionStatus.Completed or IngestionStatus.CompletedWithErrors or IngestionStatus.Failed or IngestionStatus.Cancelled;

        // Assert
        isTerminal.Should().Be(expectedIsTerminal);
    }
}