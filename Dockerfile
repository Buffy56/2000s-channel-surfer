# syntax=docker/dockerfile:1
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY src/ChannelSurfer/ChannelSurfer.csproj src/ChannelSurfer/
RUN dotnet restore src/ChannelSurfer/ChannelSurfer.csproj
COPY src/ ./src/
RUN dotnet publish src/ChannelSurfer/ChannelSurfer.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app ./
ENV CONFIG_DIR=/config
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
VOLUME /config
ENTRYPOINT ["dotnet", "ChannelSurfer.dll"]
