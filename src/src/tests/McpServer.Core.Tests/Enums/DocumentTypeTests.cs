using System;
using FluentAssertions;
using McpServer.Core.Enums;
using Xunit;

namespace McpServer.Core.Tests.Enums;

/// <summary>
/// Unit tests for the DocumentType enum
/// </summary>
public class DocumentTypeTests
{
    [Fact]
    public void DocumentType_Should_Have_Expected_Values()
    {
        // Assert
        Enum.GetValues<DocumentType>().Should().HaveCount(3);
        Enum.IsDefined(typeof(DocumentType), DocumentType.Policy).Should().BeTrue();
        Enum.IsDefined(typeof(DocumentType), DocumentType.Procedure).Should().BeTrue();
        Enum.IsDefined(typeof(DocumentType), DocumentType.ReferenceData).Should().BeTrue();
    }

    [Theory]
    [InlineData(DocumentType.Policy, 0)]
    [InlineData(DocumentType.Procedure, 1)]
    [InlineData(DocumentType.ReferenceData, 2)]
    public void DocumentType_Should_Have_Correct_Numeric_Values(DocumentType type, int expectedValue)
    {
        // Assert
        ((int)type).Should().Be(expectedValue);
    }

    [Theory]
    [InlineData("Policy")]
    [InlineData("Procedure")]
    [InlineData("ReferenceData")]
    public void DocumentType_Should_Parse_From_String(string typeName)
    {
        // Act
        var success = Enum.TryParse<DocumentType>(typeName, out var result);

        // Assert
        success.Should().BeTrue();
        result.ToString().Should().Be(typeName);
    }

    [Theory]
    [InlineData("InvalidType")]
    [InlineData("")]
    public void DocumentType_Should_Not_Parse_Invalid_Values(string typeName)
    {
        // Act
        var success = Enum.TryParse<DocumentType>(typeName, out _);

        // Assert
        success.Should().BeFalse();
    }

    [Fact]
    public void DocumentType_Should_Be_Flags_Compatible()
    {
        // This test verifies that the enum can be used in switch statements
        // and other enum-specific operations
        var type = DocumentType.Policy;
        
        var result = type switch
        {
            DocumentType.Policy => "Policy Document",
            DocumentType.Procedure => "Procedure Document",
            DocumentType.ReferenceData => "Reference Data Document",
            _ => "Unknown"
        };

        result.Should().Be("Policy Document");
    }
}