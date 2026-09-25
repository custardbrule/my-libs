namespace CustardBruleMediatr;

//void return 
public struct Unit
{
    public static readonly Unit Value = new Unit();
}
public interface IRequest<Response> { }
public interface IRequest : IRequest<Unit> { }

public interface IHandler<Request, Response> where Request : IRequest<Response>
{
    Task<Response> Handle(Request request, CancellationToken cancellationToken);
}
public interface IHandler<Request> : IHandler<Request, Unit> where Request : IRequest<Unit> { }

public interface IPipeline<TRequest, TResponse> where TRequest : IRequest<TResponse>
{
    Task Pre(TRequest request, CancellationToken cancellationToken = default);
    Task Post(TRequest request, TResponse response, CancellationToken cancellationToken = default);
}

public interface IPublisher
{
    Task<TResponse> Send<TRequest, TResponse>(TRequest request, CancellationToken cancellationToken = default) where TRequest : IRequest<TResponse>;
    Task<Unit> Send<TRequest>(TRequest request, CancellationToken cancellationToken = default) where TRequest : IRequest<Unit>;
}
