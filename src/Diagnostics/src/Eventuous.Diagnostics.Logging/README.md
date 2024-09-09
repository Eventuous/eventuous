# Eventuous.Diagnostics.Logging

Eventuous has internal event sources that emit log messages. 
It's possible to expose those messages into the logs by using the logging event listener from this package.

## Usage

Normally, you'd not need to use the logging listener directly. 
The `Eventuous.Extensions.DependencyInjection` package contains extensions for `IApplicationBuilder` and `IHost` to connect Eventuous diagnostic events to the logging system of .NET.