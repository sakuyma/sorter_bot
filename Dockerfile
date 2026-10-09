# ---- Build stage ----
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy the project file and restore
COPY sorter_bot/sorter_bot/*.csproj ./sorter_bot/sorter_bot/
RUN dotnet restore ./sorter_bot/sorter_bot/*.csproj

# Copy the rest of the source and publish
COPY sorter_bot/ ./sorter_bot/
WORKDIR /src/sorter_bot/sorter_bot
RUN dotnet publish -c Release -o /app/publish

# ---- Runtime stage ----
FROM mcr.microsoft.com/dotnet/runtime:8.0 AS runtime
WORKDIR /app

# Copy the published output
COPY --from=build /app/publish .

# Copy settings (read at runtime)
COPY sorter_bot/sorter_bot/settings/ ./settings/

ENTRYPOINT ["dotnet", "sorter_bot.dll"]
