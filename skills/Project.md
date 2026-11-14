---
name: sastreria-nuevo-servicio
description: Convenciones y pasos para crear o ampliar un microservicio .NET del proyecto de sastrería (estructura de carpetas interna, nombres, EF Core con PostgreSQL, Dockerfile, entrada en docker-compose, MassTransit con outbox, health checks, pruebas). Úsala SIEMPRE que se vaya a crear un servicio nuevo, añadir endpoints, entidades, migraciones, consumidores o publicadores de eventos, o contenerizar un servicio, aunque el usuario solo diga "crea el servicio X" o "agrega un endpoint".
---

# Crear o ampliar un servicio

Consulta también `sastreria-arquitectura` (reglas generales) y `sastreria-dominio` (reglas de negocio). Aquí van las convenciones concretas para que todos los servicios se vean igual.

## Nombres (todo en inglés)
- **Todo identificador en inglés**: proyectos, namespaces, clases, métodos, variables, enums, endpoints, eventos, tablas, columnas, variables de entorno, ramas y commits. Usa el vocabulario de `sastreria-dominio` (`WorkOrder`, `Tailor`, `Settlement`...); no mezcles español.
- Servicios: `<Name>Service` (ej. `WorkOrderService`). Base de datos: `<name>_db` (ej. `work_order_db`).
- Clases: `PascalCase`; métodos y propiedades: `PascalCase`; parámetros y variables locales: `camelCase`; interfaces con prefijo `I` (`IWorkOrderRepository`).
- Eventos en **pasado**, sin sufijo `Event`: `WorkOrderCreated`, `PaymentReceived`.
- Comandos/DTOs: `CreateWorkOrderRequest`, `WorkOrderResponse`.
- Endpoints REST en plural y minúsculas con guiones: `POST /api/work-orders`, `GET /api/work-orders/{id}`, `POST /api/settlements/close`.
- Tablas y columnas en `snake_case` y plural para tablas (`work_orders`, `serial_number`).
- Commits con Conventional Commits en inglés: `feat(work-orders): add serial number sequence`.

## Estructura interna del servicio
```
src/Services/WorkOrderService/
├── WorkOrderService.csproj
├── Program.cs
├── Domain/          # entidades, enums, reglas (sin dependencias externas)
├── Application/     # casos de uso, DTOs, validaciones
├── Infrastructure/  # DbContext, migraciones, consumidores, clientes externos
├── Endpoints/       # minimal APIs o controllers
├── Dockerfile
└── appsettings.json
```
Pruebas en `tests/` junto a `src/`, un proyecto por servicio (`WorkOrderService.Tests`). Mantén la lógica de negocio en `Domain`, para poder probarla sin base de datos.

## Pasos para crear un servicio
1. Crear el proyecto: `dotnet new webapi -n <Nombre>Service -o src/Services/<Nombre>Service` y agregarlo a la solución con `dotnet sln add`.
2. Borrar de su `.csproj` lo que ya define `Directory.Build.props` (`TargetFramework`, `Nullable`, `ImplicitUsings`).
3. Agregar paquetes con `dotnet add package`; la versión queda en `Directory.Packages.props`. No escribas versiones dentro del `.csproj`.
4. Configurar EF Core con Npgsql y su **propia base de datos** (`<nombre>_db`).
5. Añadir health check (`/health`) para Docker y Kubernetes.
6. Si publica eventos: MassTransit con RabbitMQ y **outbox de EF Core**. Si los consume: consumidores idempotentes con reintentos y DLQ.
7. Crear el `Dockerfile` (multi-stage: `sdk` para compilar, `aspnet` para correr) y añadir el servicio a `docker-compose.yml` con su PostgreSQL, variables de entorno y `depends_on` con health checks.
8. Escribir pruebas del dominio antes de exponer endpoints.

## Reglas de código
- Validar toda entrada en el borde (endpoints), no confiar en el cliente.
- Un endpoint no contiene lógica de negocio: delega en `Application`/`Domain`.
- Transiciones de estado **solo** a través de métodos de la entidad que validen que el cambio es legal (ej. no pasar a `DeliveredByTailor` desde `Received`).
- Migraciones EF versionadas en git; nunca editar una migración ya aplicada.
- Logs estructurados con Serilog; incluir el `SerialNumber` de la work order o el id de correlación en cada log relevante.
- No exponer entidades de dominio en la API; usar DTOs.

## Antes de terminar un cambio
Verifica que compila (`dotnet build`), que las pruebas pasan (`dotnet test`) y que, si tocaste un contrato de evento, los servicios que lo consumen siguen siendo compatibles.