# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project files first for better layer caching
COPY ["src/Web/RecipeLibrary.Web/RecipeLibrary.Web.csproj", "src/Web/RecipeLibrary.Web/"]
COPY ["src/Application/RecipeLibrary.Application/RecipeLibrary.Application.csproj", "src/Application/RecipeLibrary.Application/"]
COPY ["src/Application/RecipeLibrary.Application.Contracts/RecipeLibrary.Application.Contracts.csproj", "src/Application/RecipeLibrary.Application.Contracts/"]
COPY ["src/Application/RecipeLibrary.Application.Abstractions/RecipeLibrary.Application.Abstractions.csproj", "src/Application/RecipeLibrary.Application.Abstractions/"]
COPY ["src/Infrastructure/RecipeLibrary.Infrastructure/RecipeLibrary.Infrastructure.csproj", "src/Infrastructure/RecipeLibrary.Infrastructure/"]
COPY ["src/Domain/RecipeLibrary.Domain/RecipeLibrary.Domain.csproj", "src/Domain/RecipeLibrary.Domain/"]

RUN dotnet restore "src/Web/RecipeLibrary.Web/RecipeLibrary.Web.csproj"

COPY . .
RUN dotnet publish "src/Web/RecipeLibrary.Web/RecipeLibrary.Web.csproj" -c Debug -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

COPY --from=build /app/publish/ ./
COPY ["src/Web/RecipeLibrary.Web/docker-entrypoint.sh", "./docker-entrypoint.sh"]
RUN sed -i 's/\r$//' ./docker-entrypoint.sh && chmod +x ./docker-entrypoint.sh

ENTRYPOINT ["./docker-entrypoint.sh"]

