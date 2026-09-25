using Microsoft.Extensions.DependencyInjection;

namespace CustardBruleMediatr.Tests;

public class TestRequest : IRequest<string>
{
    public string Value { get; set; } = string.Empty;
}

public class TestHandler : IHandler<TestRequest, string>
{
    public Task<string> Handle(TestRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult($"handled:{request.Value}");
    }
}

public class RecordingPipeline : IPipeline<TestRequest, string>
{
    public static List<string> Calls { get; } = new();

    public Task Pre(TestRequest request, CancellationToken cancellationToken = default)
    {
        Calls.Add($"pre:{request.Value}");
        return Task.CompletedTask;
    }

    public Task Post(TestRequest request, string response, CancellationToken cancellationToken = default)
    {
        Calls.Add($"post:{response}");
        return Task.CompletedTask;
    }
}

[TestFixture]
public class PublisherTests
{
    [SetUp]
    public void Setup()
    {
        RecordingPipeline.Calls.Clear();
    }

    [Test]
    public async Task Send_InvokesHandler_AndPipelines()
    {
        var services = new ServiceCollection();
        services.AddCQRSCore();
        services.AddSingleton<IHandler<TestRequest, string>, TestHandler>();
        services.AddSingleton<IPipeline<TestRequest, string>, RecordingPipeline>();

        using var provider = services.BuildServiceProvider();
        var publisher = provider.GetRequiredService<IPublisher>();

        var result = await publisher.Send<TestRequest, string>(new TestRequest { Value = "abc" });

        Assert.That(result, Is.EqualTo("handled:abc"));
        Assert.That(RecordingPipeline.Calls, Is.EqualTo(new[] { "pre:abc", "post:handled:abc" }));
    }

    [Test]
    public void Send_WhenRequestIsNull_ThrowsArgumentNullException()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IPublisher, Publisher>();
        services.AddSingleton<IHandler<TestRequest, string>, TestHandler>();

        using var provider = services.BuildServiceProvider();
        var publisher = provider.GetRequiredService<IPublisher>();

        Assert.ThrowsAsync<ArgumentNullException>(async () => await publisher.Send<TestRequest, string>(null!));
    }

    [Test]
    public async Task AddHandler_RegistersImplementationByInterface()
    {
        var services = new ServiceCollection();
        services.AddCQRSCore();
        services.AddHandler<TestHandler>(ServiceLifetime.Singleton);

        using var provider = services.BuildServiceProvider();
        var handler = provider.GetRequiredService<IHandler<TestRequest, string>>();

        var result = await handler.Handle(new TestRequest { Value = "registered" }, CancellationToken.None);

        Assert.That(result, Is.EqualTo("handled:registered"));
    }
}