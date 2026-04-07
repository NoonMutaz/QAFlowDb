FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy the csproj from the subfolder
COPY ["WebApplication2/WebApplication2.csproj", "WebApplication2/"]

RUN dotnet restore "WebApplication2/WebApplication2.csproj"

# Copy everything
COPY . .

# Publish the project
RUN dotnet publish "WebApplication2/WebApplication2.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "WebApplication2.dll"]
