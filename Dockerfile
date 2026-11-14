FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["src/MarksApi/MarksApi.csproj", "src/MarksApi/"]
RUN dotnet restore "src/MarksApi/MarksApi.csproj"

COPY ["src/MarksApi/", "src/MarksApi/"]
WORKDIR "/src/src/MarksApi"
RUN dotnet publish "MarksApi.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
EXPOSE 5123

ENV ASPNETCORE_URLS=http://+:5123
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "MarksApi.dll"]

