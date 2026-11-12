# Documentación

- [RESUMEN.md](../RESUMEN.md): diseño del Nivel 1, fuente de verdad de la arquitectura.
- [README.md](../README.md): desarrollo local, configuración y tests.
- [Docker/README.md](../Docker/README.md): despliegue.
- `API/Herensuge.Api.json`: contrato OpenAPI de la API. Lo reescribe cada `dotnet build` de `Herensuge.Api` y de él salen
  los tipos de las webs (`pnpm -C Src/Web api:generate`). La CI falla si está desactualizado.
- `Arquitectura/`: diagramas y notas de detalle.
- `Decisiones/`: ADRs (pendientes de redactar a partir de la sección 20 del RESUMEN).
