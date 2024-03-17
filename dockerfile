FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build

WORKDIR /app

COPY ./*.csproj ./

RUN dotnet restore

COPY . .

RUN dotnet build --configuration Release

RUN dotnet publish --configuration Release --no-build --output /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime

WORKDIR /app

COPY --from=build /app/publish .

FROM python:3

WORKDIR /app

COPY requirements.txt ./

RUN pip install --no-cache-dir --upgrade pip \
  && pip install --no-cache-dir -r requirements.txt

COPY . .

ENV PYTHON_DLL_PATH=/app/python3/python3.dll

ENV PYTHON_SCRIPT_PATH=/app

ENV DOTNET_URLS=http://+:5000

EXPOSE 5000

ENTRYPOINT ["dotnet", "Server.dll"]