# Build stage for .NET app
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build-env
WORKDIR /app
COPY . ./
RUN dotnet restore
RUN dotnet publish -c Release -o out

# Final stage/image that includes both .NET runtime and Python
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app

# Copy the published .NET app from the build stage
COPY --from=build-env /app/out .

#Install Python and dependencies
RUN apt-get update \
    && apt-get install -y python3 python3-pip python3-venv \
    && rm -rf /var/lib/apt/lists/*

# Create a virtual environment and activate it
RUN python3 -m venv /app/venv
ENV PATH="/app/venv/bin:$PATH"

# Install Python dependencies within the virtual environment
COPY requirements.txt ./
RUN /app/venv/bin/pip install --no-cache-dir --upgrade pip \
    && /app/venv/bin/pip install --no-cache-dir -r requirements.txt

# Set environment variables
ENV PYTHON_DLL_PATH=/usr/lib/python3.11
ENV PYTHON_SCRIPT_PATH=/app

ENV ASPNETCORE_URLS=http://*:80

# Set the entry point to the .NET executable
ENTRYPOINT ["dotnet", "Server.dll"]