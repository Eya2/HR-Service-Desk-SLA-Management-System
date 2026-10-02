# syntax=docker/dockerfile:1
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Restore first so the layer is cached until a project file changes.
COPY global.json Directory.Build.props Directory.Packages.props .editorconfig ./
COPY src/HrServiceDesk.Domain/HrServiceDesk.Domain.csproj src/HrServiceDesk.Domain/
COPY src/HrServiceDesk.Application/HrServiceDesk.Application.csproj src/HrServiceDesk.Application/
COPY src/HrServiceDesk.Infrastructure/HrServiceDesk.Infrastructure.csproj src/HrServiceDesk.Infrastructure/
COPY src/HrServiceDesk.Api/HrServiceDesk.Api.csproj src/HrServiceDesk.Api/
RUN dotnet restore src/HrServiceDesk.Api/HrServiceDesk.Api.csproj

COPY src/ src/
RUN dotnet publish src/HrServiceDesk.Api/HrServiceDesk.Api.csproj \
    -c Release -o /app/publish --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080
COPY --from=build /app/publish .
# Attachment storage (mounted as a volume); owned by the non-root user shipped with the .NET 8 images.
RUN mkdir -p /app/data/attachments && chown -R app:app /app/data
USER app
EXPOSE 8080
ENTRYPOINT ["dotnet", "HrServiceDesk.Api.dll"]
