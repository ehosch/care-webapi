FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["src/Host/Care.WebApi.Host.csproj", "src/Host/"]
COPY ["src/Infrastructure/Care.WebApi.Infrastructure.csproj", "src/Infrastructure/"]
COPY ["src/Core/Application/Care.WebApi.Application.csproj", "src/Core/Application/"]
COPY ["src/Core/Domain/Care.WebApi.Domain.csproj", "src/Core/Domain/"]
COPY ["src/Migrators/Migrators.MySQL/Care.WebApi.Migrators.MySQL.csproj", "src/Migrators/Migrators.MySQL/"]
RUN dotnet restore "src/Host/Care.WebApi.Host.csproj"
COPY . .
WORKDIR "/src/src/Host"
RUN dotnet build "Care.WebApi.Host.csproj" -c Debug -o /app/build

FROM build AS publish
RUN dotnet publish "Care.WebApi.Host.csproj" -c Debug -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Care.WebApi.Host.dll"]
