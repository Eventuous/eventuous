namespace Eventuous.AspNetCore.Web;

[AttributeUsage(AttributeTargets.Class)]
public class HttpCommandAttribute : Attribute {
    public string Route { get; }

    public HttpCommandAttribute(string route) => Route = route;
}