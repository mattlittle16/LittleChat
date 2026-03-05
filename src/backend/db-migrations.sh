dotnet ef migrations add Notifications \
      --context LittleChatDbContext \
      --project src/Modules/Messaging/Infrastructure/Messaging.Infrastructure.csproj \
      --startup-project src/API/LittleChat.API.csproj
