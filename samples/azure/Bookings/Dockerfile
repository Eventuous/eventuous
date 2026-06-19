FROM mcr.microsoft.com/dotnet/aspnet:6.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore "Bookings/Bookings.csproj"
WORKDIR "/src/Bookings"
RUN dotnet build "Bookings.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Bookings.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Bookings.dll"]
