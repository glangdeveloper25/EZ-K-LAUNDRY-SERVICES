# Builds the EZ-K Laundry System for online hosting (for example, Render).
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY EZKLaundrySystem.csproj .
RUN dotnet restore
COPY . .
RUN dotnet publish EZKLaundrySystem.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
# Philippine time for DateTime.Now (bookings, time slots, grace period, reports).
RUN apt-get update && apt-get install -y --no-install-recommends tzdata && rm -rf /var/lib/apt/lists/*
ENV TZ=Asia/Manila \
    ASPNETCORE_HTTP_PORTS=10000
EXPOSE 10000
COPY --from=build /app .
ENTRYPOINT ["dotnet", "EZKLaundrySystem.dll"]
