# syntax=docker/dockerfile:1

# ---------- Stage 1: build ----------
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy only the project file and restore first.
# This layer is cached, so packages are not downloaded again when only the code changes.
COPY RaceDayWeb.csproj ./
RUN dotnet restore RaceDayWeb.csproj

# Copy the rest of the source and publish a release build.
COPY . ./
RUN dotnet publish RaceDayWeb.csproj -c Release -o /app/publish --no-restore /p:UseAppHost=false

# ---------- Stage 2: runtime ----------
# The runtime image is much smaller than the SDK image and contains no build tools.
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# The site listens on port 8080 and reads the API address from configuration.
# Override the address when you start the container: -e Api__BaseUrl=http://...
ENV ASPNETCORE_HTTP_PORTS=8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    Api__BaseUrl=http://host.docker.internal:5280/

EXPOSE 8080

COPY --from=build /app/publish ./

# Run as the built-in non-root user instead of root.
USER $APP_UID

ENTRYPOINT ["dotnet", "RaceDayWeb.dll"]
