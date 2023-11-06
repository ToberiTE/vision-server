FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build

WORKDIR /app

COPY ./*.csproj ./

RUN dotnet restore

COPY ./ ./

RUN dotnet build --configuration Release

RUN dotnet publish --configuration Release --no-build --output /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:7.0 AS runtime

WORKDIR /app

COPY --from=build /app/publish .

ENV DOTNET_URLS=http://+:5000

EXPOSE 5000

ENTRYPOINT ["dotnet", "Server.dll"]