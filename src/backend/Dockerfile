FROM mcr.microsoft.com/dotnet/sdk:8.0@sha256:a9330090b730c2abde8f3f43b3d1e24e4b8cba028de7bab1a7fdcd50cb7a3b7e AS build
WORKDIR /src
COPY . .
RUN dotnet restore src/API/LittleChat.API.csproj
RUN dotnet publish src/API/LittleChat.API.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0@sha256:d4d80bf500f4c8307e5c44bf61eb58aec027da07c4d1c40816846fe5eef3f34d AS final
WORKDIR /app
RUN apt-get update && apt-get install -y --no-install-recommends \
    libheif1 \
    libde265-0 \
    curl \
    && rm -rf /var/lib/apt/lists/*
COPY --from=build /app/publish .
EXPOSE 5000
ENV ASPNETCORE_URLS=http://+:5000
ENTRYPOINT ["dotnet", "LittleChat.API.dll"]
