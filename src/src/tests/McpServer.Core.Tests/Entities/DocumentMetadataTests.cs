using System;
using FluentAssertions;
using McpServer.Core.Entities;
using McpServer.Core.Enums;
using Xunit;

namespace McpServer.Core.Tests.Entities;

/// <summary>
/// Unit tests for the DocumentMetadata entity
/// </summary>
public class DocumentMetadataTests
{
    [Fact]
    public void DocumentMetadata_Should_Initialize_All_Properties()
    {
        // Arrange
        var title = "Test Policy Document";
        var department = "Reference Data";
        var documentType = DocumentType.Policy;
        var effectiveDate = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var version = "2.0";

        // Act
        var metadata = new DocumentMetadata
        {
            Title = title,
            Department = department,
            DocumentType = documentType,
            EffectiveDate = effectiveDate,
            Version = version
        };

        // Assert
        metadata.Title.Should().Be(title);
        metadata.Department.Should().Be(department);
        metadata.DocumentType.Should().Be(documentType);
        metadata.EffectiveDate.Should().Be(effectiveDate);
        metadata.Version.Should().Be(version);
    }

    [Fact]
    public void DocumentMetadata_Should_Handle_Default_Values()
    {
        // Arrange & Act
        var metadata = new DocumentMetadata();

        // Assert
        metadata.Title.Should().BeEmpty();
        metadata.Department.Should().BeEmpty();
        metadata.DocumentType.Should().Be(DocumentType.ReferenceData); // Default value
        metadata.EffectiveDate.Should().BeNull();
        metadata.Version.Should().BeEmpty();
    }

    [Fact]
    public void DocumentMetadata_Should_Handle_Empty_Strings()
    {
        // Arrange & Act
        var metadata = new DocumentMetadata
        {
            Title = string.Empty,
            Department = string.Empty,
            Version = string.Empty
        };

        // Assert
        metadata.Title.Should().BeEmpty();
        metadata.Department.Should().BeEmpty();
        metadata.Version.Should().BeEmpty();
    }

    [Theory]
    [InlineData("1.0")]
    [InlineData("2.5.1")]
    [InlineData("v3.0-alpha")]
    [InlineData("2024.01.15")]
    public void DocumentMetadata_Should_Accept_Various_Version_Formats(string version)
    {
        // Arrange & Act
        var metadata = new DocumentMetadata { Version = version };

        // Assert
        metadata.Version.Should().Be(version);
    }
}