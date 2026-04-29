using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using OneGood.Core.AI;
using OneGood.Core.Enums;
using OneGood.Infrastructure.Classification;

namespace OneGood.Tests.Unit;

public class AiCauseClassifierTests
{
    private readonly IAiService _aiService = Substitute.For<IAiService>();
    private readonly AiCauseClassifier _classifier;

    public AiCauseClassifierTests()
    {
        var logger = Substitute.For<ILogger<AiCauseClassifier>>();
        _classifier = new AiCauseClassifier(_aiService, logger);
    }

    #region AI Classification

    [Fact]
    public async Task ClassifyAsync_UsesAiResponse_WhenAvailable()
    {
        // Arrange — AI returns "Education"
        _aiService.GetCompletionAsync(Arg.Any<IEnumerable<ChatMessage>>(), Arg.Any<CancellationToken>())
            .Returns("Education");

        // Act
        var result = await _classifier.ClassifyAsync("Bowling machine for cricket",
            "Help young cricketers improve their skills through sport");

        // Assert
        Assert.Equal(CauseCategory.Education, result);
    }

    [Fact]
    public async Task ClassifyAsync_HandlesQuotedResponse()
    {
        _aiService.GetCompletionAsync(Arg.Any<IEnumerable<ChatMessage>>(), Arg.Any<CancellationToken>())
            .Returns("\"AnimalWelfare\"");

        var result = await _classifier.ClassifyAsync("Rettet die Igel");

        Assert.Equal(CauseCategory.AnimalWelfare, result);
    }

    [Fact]
    public async Task ClassifyAsync_HandlesMultiWordFormat()
    {
        _aiService.GetCompletionAsync(Arg.Any<IEnumerable<ChatMessage>>(), Arg.Any<CancellationToken>())
            .Returns("Climate And Nature");

        var result = await _classifier.ClassifyAsync("Klimaschutz Projekt");

        Assert.Equal(CauseCategory.ClimateAndNature, result);
    }

    [Fact]
    public async Task ClassifyAsync_HandlesVerboseResponse()
    {
        // Some LLMs add extra explanation even when asked not to
        _aiService.GetCompletionAsync(Arg.Any<IEnumerable<ChatMessage>>(), Arg.Any<CancellationToken>())
            .Returns("The category is Education.\nThis cause is about youth development through sport.");

        var result = await _classifier.ClassifyAsync("Youth cricket training");

        Assert.Equal(CauseCategory.Education, result);
    }

    #endregion

    #region Fallback Behavior

    [Fact]
    public async Task ClassifyAsync_ReturnsFallback_WhenAiFails()
    {
        // Arrange — AI throws
        _aiService.GetCompletionAsync(Arg.Any<IEnumerable<ChatMessage>>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("AI service unavailable"));

        // Act
        var result = await _classifier.ClassifyAsync("Tierschutz für Igel",
            fallback: CauseCategory.AnimalWelfare);

        // Assert — fallback is used
        Assert.Equal(CauseCategory.AnimalWelfare, result);
    }

    [Fact]
    public async Task ClassifyAsync_ReturnsFallback_WhenAiReturnsEmpty()
    {
        _aiService.GetCompletionAsync(Arg.Any<IEnumerable<ChatMessage>>(), Arg.Any<CancellationToken>())
            .Returns("");

        var result = await _classifier.ClassifyAsync("Klimaschutz für unsere Zukunft");

        Assert.Equal(CauseCategory.HumanRights, result); // Default fallback
    }

    [Fact]
    public async Task ClassifyAsync_ReturnsFallback_WhenAiReturnsGarbage()
    {
        _aiService.GetCompletionAsync(Arg.Any<IEnumerable<ChatMessage>>(), Arg.Any<CancellationToken>())
            .Returns("I'm not sure about that one, it could be anything really");

        var result = await _classifier.ClassifyAsync("Some random text",
            fallback: CauseCategory.Democracy);

        Assert.Equal(CauseCategory.Democracy, result);
    }

    #endregion

    #region Caching

    [Fact]
    public async Task ClassifyAsync_CachesResults_SameContentNotCalledTwice()
    {
        _aiService.GetCompletionAsync(Arg.Any<IEnumerable<ChatMessage>>(), Arg.Any<CancellationToken>())
            .Returns("Education");

        // Act — call twice with the same content
        var result1 = await _classifier.ClassifyAsync("Bowling machine for cricket", "Youth sport project");
        var result2 = await _classifier.ClassifyAsync("Bowling machine for cricket", "Youth sport project");

        // Assert — same result, but AI called only once
        Assert.Equal(CauseCategory.Education, result1);
        Assert.Equal(CauseCategory.Education, result2);
        await _aiService.Received(1).GetCompletionAsync(
            Arg.Any<IEnumerable<ChatMessage>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ClassifyAsync_DifferentContent_CallsAiAgain()
    {
        _aiService.GetCompletionAsync(Arg.Any<IEnumerable<ChatMessage>>(), Arg.Any<CancellationToken>())
            .Returns("Education", "AnimalWelfare");

        var result1 = await _classifier.ClassifyAsync("Youth cricket training");
        var result2 = await _classifier.ClassifyAsync("Rettet die Igel im Tierheim");

        Assert.Equal(CauseCategory.Education, result1);
        Assert.Equal(CauseCategory.AnimalWelfare, result2);
        await _aiService.Received(2).GetCompletionAsync(
            Arg.Any<IEnumerable<ChatMessage>>(), Arg.Any<CancellationToken>());
    }

    #endregion

    #region Edge Cases

    [Fact]
    public async Task ClassifyAsync_NullInput_ReturnsFallback()
    {
        var result = await _classifier.ClassifyAsync(null, null, fallback: CauseCategory.Democracy);

        Assert.Equal(CauseCategory.Democracy, result);
        // AI should not be called for null/empty input
        await _aiService.DidNotReceive().GetCompletionAsync(
            Arg.Any<IEnumerable<ChatMessage>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ClassifyAsync_EmptyInput_ReturnsFallback()
    {
        var result = await _classifier.ClassifyAsync("", "  ");

        Assert.Equal(CauseCategory.HumanRights, result); // Default fallback
        await _aiService.DidNotReceive().GetCompletionAsync(
            Arg.Any<IEnumerable<ChatMessage>>(), Arg.Any<CancellationToken>());
    }

    #endregion
}
