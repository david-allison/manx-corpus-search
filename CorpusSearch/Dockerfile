FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
# Install Node.js and npm
RUN rm /bin/sh && ln -s /bin/bash /bin/sh
RUN curl -o- https://raw.githubusercontent.com/nvm-sh/nvm/v0.39.7/install.sh | bash
RUN source ~/.nvm/nvm.sh && nvm install node && nvm use node && node -v

WORKDIR /src
COPY ["CorpusSearch.csproj", "CorpusSearch/"]
WORKDIR "/src/CorpusSearch"
RUN dotnet restore "CorpusSearch.csproj"
#Note: this is to only perform a dotnet restore if the csproj has changed
COPY . .
RUN dotnet build "CorpusSearch.csproj" -c $BUILD_CONFIGURATION -o /app/build

WORKDIR /src
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN source ~/.nvm/nvm.sh && dotnet publish "CorpusSearch/CorpusSearch.csproj" -c $BUILD_CONFIGURATION -f net8.0 -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "CorpusSearch.dll"]
