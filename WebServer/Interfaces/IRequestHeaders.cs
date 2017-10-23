namespace WebServer
{
    public interface IRequestHeaders : IHeaders
    {
        Method Method { get; }
        string Url { get; }
        string Host { get; }
        string Http { get; }
        ContentEncoding ContentEncoding { get; }
    }
}