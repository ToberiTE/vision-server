FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build

WORKDIR /app

COPY ./*.csproj ./

RUN dotnet restore

COPY ./ ./

RUN dotnet build --configuration Release

RUN dotnet publish --configuration Release --no-build --output /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime

WORKDIR /app

COPY --from=build /app/publish .

RUN apt-get update && apt-get install -y python3.12 python3.12-dev

RUN curl https://bootstrap.pypa.io/get-pip.py -o get-pip.py

RUN python3.12 get-pip.py

COPY requirements.txt .

RUN python3.12 -m pip install --no-cache-dir -r requirements.txt

ENV PYTHON_DLL_PATH=/usr/bin/python3.12/python3.12.dll

ENV PYTHON_SCRIPT_PATH=/app

ENV DOTNET_URLS=http://+:5000

EXPOSE 5000

ENTRYPOINT ["dotnet", "Server.dll"]