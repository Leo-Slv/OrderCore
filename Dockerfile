FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Directory.Build.props .
COPY OrderCore.Api.csproj .
RUN dotnet restore OrderCore.Api.csproj

COPY . .
RUN dotnet publish OrderCore.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

# The non-root user the .NET images ship with; nothing here needs root
# (8080 is above 1024).
USER $APP_UID

# `docker run <image>` starts the API; `docker run <image> migrate` applies
# the migrations and exits (Docs/operations/deployment.md).
ENTRYPOINT ["dotnet", "OrderCore.Api.dll"]
