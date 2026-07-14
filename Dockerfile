# Stage 1: Base image for running the app
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
# Mặc định .NET 8/9 sẽ expose port 8080
EXPOSE 8080

# Stage 2: Build image with SDK
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Copy các file csproj để restore dependencies trước (tận dụng Docker cache)
COPY ["ScholarTrend.API/ScholarTrend.API.csproj", "ScholarTrend.API/"]
COPY ["ScholarTrend.Application/ScholarTrend.Application.csproj", "ScholarTrend.Application/"]
COPY ["ScholarTrend.Domain/ScholarTrend.Domain.csproj", "ScholarTrend.Domain/"]
COPY ["ScholarTrend.Infrastructure/ScholarTrend.Infrastructure.csproj", "ScholarTrend.Infrastructure/"]
RUN dotnet restore "./ScholarTrend.API/ScholarTrend.API.csproj"

# Copy toàn bộ source code
COPY . .
WORKDIR "/src/ScholarTrend.API"
RUN dotnet build "./ScholarTrend.API.csproj" -c $BUILD_CONFIGURATION -o /app/build

# Stage 3: Publish
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./ScholarTrend.API.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# Stage 4: Final image
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Để Render có thể binding port động qua biến môi trường $PORT
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "ScholarTrend.API.dll"]
