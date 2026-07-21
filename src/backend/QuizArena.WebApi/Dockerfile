FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["src/backend/QuizArena.WebApi/QuizArena.WebApi.csproj", "src/backend/QuizArena.WebApi/"]
COPY ["src/backend/QuizArena.Application/QuizArena.Application.csproj", "src/backend/QuizArena.Application/"]
COPY ["src/backend/QuizArena.Domain/QuizArena.Domain.csproj", "src/backend/QuizArena.Domain/"]
COPY ["src/backend/QuizArena.Persistence/QuizArena.Persistence.csproj", "src/backend/QuizArena.Persistence/"]
RUN dotnet restore "src/backend/QuizArena.WebApi/QuizArena.WebApi.csproj"

COPY . .
WORKDIR "/src/src/backend/QuizArena.WebApi"
RUN dotnet build "QuizArena.WebApi.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "QuizArena.WebApi.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "QuizArena.WebApi.dll"]