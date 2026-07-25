FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0.301 AS build
WORKDIR /src

# Paths assume build context = repo root (same convention as
# src/backend/QuizArena.WebApi/Dockerfile) and bot projects living at src/bot/*.
# Adjust the "src/bot/" prefix below if your actual folder differs.
COPY ["src/bot/TelegramBot.Bot/TelegramBot.Bot.csproj", "src/bot/TelegramBot.Bot/"]
COPY ["src/bot/TelegramBot.Application/TelegramBot.Application.csproj", "src/bot/TelegramBot.Application/"]
COPY ["src/bot/TelegramBot.Domain/TelegramBot.Domain.csproj", "src/bot/TelegramBot.Domain/"]
COPY ["src/bot/TelegramBot.Infrastructure/TelegramBot.Infrastructure.csproj", "src/bot/TelegramBot.Infrastructure/"]
RUN dotnet restore "src/bot/TelegramBot.Bot/TelegramBot.Bot.csproj"

COPY . .
WORKDIR "/src/src/bot/TelegramBot.Bot"
RUN dotnet build "TelegramBot.Bot.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "TelegramBot.Bot.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "TelegramBot.Bot.dll"]
