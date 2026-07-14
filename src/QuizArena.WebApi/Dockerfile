FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["src/QuizArena.WebApi/QuizArena.WebApi.csproj", "src/QuizArena.WebApi/"]
COPY ["src/QuizArena.Application/QuizArena.Application.csproj", "src/QuizArena.Application/"]
COPY ["src/QuizArena.Domain/QuizArena.Domain.csproj", "src/QuizArena.Domain/"]
COPY ["src/QuizArena.Persistence/QuizArena.Persistence.csproj", "src/QuizArena.Persistence/"]
RUN dotnet restore "src/QuizArena.WebApi/QuizArena.WebApi.csproj"

COPY . .
WORKDIR "/src/src/QuizArena.WebApi"
RUN dotnet build "QuizArena.WebApi.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "QuizArena.WebApi.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "QuizArena.WebApi.dll"]
