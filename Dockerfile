# Build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY src/SmartMeetingNotes.Api/ ./SmartMeetingNotes.Api/
WORKDIR /src/SmartMeetingNotes.Api
RUN dotnet publish -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app

# Install Python 3 and pip
RUN apt-get update && \
    apt-get install -y python3 python3-pip python3-venv && \
    rm -rf /var/lib/apt/lists/*

# Create Python virtual environment
RUN python3 -m venv /app/venv
ENV PATH="/app/venv/bin:$PATH"

# Install Python dependencies
COPY requirements.txt .
RUN pip install --no-cache-dir -r requirements.txt

# Copy Python modules
COPY transcriber/ ./transcriber/
COPY analyzer/ ./analyzer/
COPY recorder/ ./recorder/

# Copy published .NET app
COPY --from=build /app/publish .

# Create data directories
RUN mkdir -p data/audio data/meetings

# Render uses PORT env variable
ENV ASPNETCORE_URLS=http://+:${PORT:-10000}
ENV ASPNETCORE_ENVIRONMENT=Production

EXPOSE 10000

ENTRYPOINT ["dotnet", "SmartMeetingNotes.Api.dll"]
