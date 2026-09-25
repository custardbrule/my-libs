using System.Runtime.CompilerServices;
using Microsoft.Extensions.DependencyInjection;

namespace CustardBruleMediatr;
public class Publisher : IPublisher
{
    private readonly IServiceProvider _serviceProvider;

    public Publisher(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public async Task<TResponse> Send<TRequest, TResponse>(TRequest request, CancellationToken cancellationToken = default) where TRequest : IRequest<TResponse>
    {
        if (request is null)
            throw new ArgumentNullException(nameof(request));

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var token = cts.Token;

        var handler = GetHandler<TRequest, TResponse>();
        var pipelines = GetPipelines<TRequest, TResponse>().ToArray();

        await ExecutePrePipelines(pipelines, request, token);
        var response = await handler.Handle(request, token);
        await ExecutePostPipelines(pipelines, request, response, token);

        return response;
    }

    public Task<Unit> Send<TRequest>(TRequest request, CancellationToken cancellationToken = default) where TRequest : IRequest<Unit>
        => Send<TRequest, Unit>(request, cancellationToken);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private IHandler<TRequest, TResponse> GetHandler<TRequest, TResponse>() where TRequest : IRequest<TResponse>
        => _serviceProvider.GetRequiredService<IHandler<TRequest, TResponse>>();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private IEnumerable<IPipeline<TRequest, TResponse>> GetPipelines<TRequest, TResponse>() where TRequest : IRequest<TResponse>
        => _serviceProvider.GetServices<IPipeline<TRequest, TResponse>>();

    private async Task ExecutePrePipelines<TRequest, TResponse>(
        IPipeline<TRequest, TResponse>[] pipelines,
        TRequest request,
        CancellationToken cancellationToken)
        where TRequest : IRequest<TResponse>
    {
        foreach (var pipeline in pipelines)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await pipeline.Pre(request, cancellationToken);
        }
    }

    private async Task ExecutePostPipelines<TRequest, TResponse>(
        IPipeline<TRequest, TResponse>[] pipelines,
        TRequest request,
        TResponse response,
        CancellationToken cancellationToken)
        where TRequest : IRequest<TResponse>
    {
        foreach (var pipeline in pipelines)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await pipeline.Post(request, response, cancellationToken);
        }
    }
}
