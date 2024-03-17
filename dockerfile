# Build stage for .NET app
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app
COPY ./*.csproj ./
RUN dotnet restore
COPY . .
RUN dotnet build --configuration Release
RUN dotnet publish --configuration Release --no-build --output /app/publish

# Final stage/image
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app/publish .

# Install Python and dependencies
RUN apt-get update \
    && apt-get install -y python3.12 python3.12-pip \
    && rm -rf /var/lib/apt/lists/*
COPY requirements.txt ./
RUN pip install --no-cache-dir --upgrade pip \
    && pip install --no-cache-dir -r requirements.txt

# Copy Python script and any other necessary files
COPY . .

# Set environment variables
ENV PYTHON_SCRIPT_PATH=/app
ENV DOTNET_URLS=http://+:5000

EXPOSE 5000

ENTRYPOINT ["dotnet", "Server.dll"]