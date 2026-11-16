# ADR-A10: el módulo se llama Access

- **Estado:** aceptada · 16/11/2026

## Decisión

- Módulo `Herensuge.Modules.Access`, esquema `access`, scope `herensuge.access` y API de decisión bajo `/access/v1`.

## Consecuencias

- **Descartado:** `Authorization`, porque se confunde con `Microsoft.AspNetCore.Authorization` en los `using` y en las
  búsquedas.
