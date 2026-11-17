FROM mcr.microsoft.com/dotnet/sdk:8.0
WORKDIR /app
COPY . .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "run", "--project", "Builder.csproj", "--configuration", "Release"]
