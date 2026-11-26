# Verificação independente da suíte (pedido do parecer da DSM2): não exige .NET instalado.
#   docker build -t chunklink .            # compila e roda os testes durante o build
#   docker run --rm chunklink depurar      # executa o cenário controlado
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY . .
RUN test -d tcc-vendor || (echo "tcc-vendor/ ausente: use o zip da entrega ou rode ferramentas/empacotar.sh" && exit 1)
RUN dotnet build ChunkLink.sln -c Release -nologo \
 && dotnet test ChunkLink.sln -c Release --no-build -nologo --logger "console;verbosity=normal"
RUN dotnet publish src/ChunkLink.Cli -c Release -o /app --no-build -nologo

FROM mcr.microsoft.com/dotnet/runtime:8.0
WORKDIR /app
COPY --from=build /app .
COPY --from=build /src/dados /app/dados
ENTRYPOINT ["dotnet", "chunklink.dll"]
CMD ["depurar"]
