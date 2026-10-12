FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY src/RogueNetServer/RogueNet.Logic/RogueNet.Logic.csproj src/RogueNetServer/RogueNet.Logic/
COPY src/RogueNetServer/RogueNet.API/RogueNet.API.csproj src/RogueNetServer/RogueNet.API/
RUN dotnet restore src/RogueNetServer/RogueNet.API/RogueNet.API.csproj

COPY src/RogueNetServer/ src/RogueNetServer/
RUN dotnet publish src/RogueNetServer/RogueNet.API/RogueNet.API.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
RUN apt-get update \
    && apt-get install --yes --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
HEALTHCHECK --interval=10s --timeout=3s --start-period=10s --retries=3 \
    CMD curl --fail --silent http://127.0.0.1:8080/health || exit 1
ENTRYPOINT ["dotnet", "RogueNet.API.dll"]
