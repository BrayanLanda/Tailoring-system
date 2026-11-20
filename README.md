# Tailoring System

Sistema para administrar boletas de arreglos de ropa, sastres, pagos y liquidaciones. El proyecto reemplaza el control manual en cuadernos y, a la vez, sirve para aprender diseño de dominio, TDD, PostgreSQL y una arquitectura de microservicios en .NET.

> **Estado:** WorkOrderService y TailorService tienen una primera versión funcional. IdentityService está en desarrollo. Gateway, frontend, SettlementService y mensajería todavía no están implementados.

## Contenido

- [Arquitectura](#arquitectura)
- [Estado de implementación](#estado-de-implementación)
- [Reglas principales del negocio](#reglas-principales-del-negocio)
- [Requisitos](#requisitos)
- [Configuración local](#configuración-local)
- [Base de datos y migraciones](#base-de-datos-y-migraciones)
- [Ejecutar las APIs](#ejecutar-las-apis)
- [Endpoints disponibles](#endpoints-disponibles)
- [Pruebas](#pruebas)
- [Seguridad y secretos](#seguridad-y-secretos)
- [Estructura del repositorio](#estructura-del-repositorio)
- [Siguiente trabajo](#siguiente-trabajo)
- [Documentación del dominio y convenciones](#documentación-del-dominio-y-convenciones)

## Arquitectura

El sistema sigue DDD. Cada servicio es dueño de su modelo y su base de datos; ningún servicio consulta directamente la base de otro. La dirección de dependencias dentro de cada servicio es:

```text
Api → Application → Domain
Api → Infrastructure → Application
Infrastructure → Application
```

La solución elige microservicios como objetivo educativo y de práctica. No todos los servicios planeados están construidos todavía.

| Servicio | Responsabilidad | Persistencia | Estado |
|---|---|---|---|
| WorkOrderService | Boletas, seriales, precios, abonos, estados y garantías | PostgreSQL `work_order_db` | Primera versión funcional |
| TailorService | Perfil y estado activo/inactivo del sastre | PostgreSQL `tailor_db` | Registro y consulta por ID funcionales |
| IdentityService | Cuentas, credenciales, roles, invitaciones y tokens | PostgreSQL, pendiente de configurar | Domain y Application de invitaciones |
| SettlementService | Periodos, elegibilidad y liquidación del porcentaje del sastre | PostgreSQL + Redis | Planeado |
| GatewayService | Entrada única, enrutamiento y seguridad | No aplica | Planeado |
| Angular | Interfaz web | No aplica | Planeado |

Las decisiones vigentes de autenticación son cuentas locales con correo y contraseña, IdentityService separado con PostgreSQL, y Google como proveedor opcional futuro. El administrador crea el perfil Tailor y luego invita al correo del sastre. La cuenta y el perfil se vinculan por ID; TailorService no guarda contraseñas. La intención es usar ASP.NET Core Identity y OpenIddict, pero aún no están integrados.

## Estado de implementación

### WorkOrderService

- **Domain:** agregado `WorkOrder`, trabajos descriptivos, pagos, precio, garantías, serial tipado, estados separados de trabajo y pago, y reglas de negocio.
- **Application:** `CreateWorkOrder`, `SetWorkOrderPrice` y `RegisterPayment`.
- **Infrastructure:** EF Core/Npgsql, migraciones, secuencia PostgreSQL para el serial y concurrencia optimista con `Version`.
- **API:** crear boleta, definir precio y registrar pago; manejo central de errores y `/health`.
- **Pendiente:** asignación e hitos de trabajo, entregas, casos de uso de garantías, consultas y publicación de eventos mediante outbox.

### TailorService

- **Domain:** registrar, desactivar y reactivar un sastre. El estado inicial al registrar es activo; no se manejan horarios.
- **Application:** registro y consulta por ID.
- **Infrastructure:** EF Core/Npgsql, migración inicial y concurrencia optimista.
- **API:** registrar y consultar por ID; incluye `/health`.
- **Pendiente:** endpoints y casos de uso para desactivar/reactivar, listado de sastres activos y autorización. El API actual no tiene autenticación: úsalo solo en desarrollo local.

### IdentityService

- **Domain:** invitación de sastre vinculada a su `TailorProfileId`; normalización de correo, hash del token, expiración, aceptación de un solo uso y reemisión que invalida el enlace anterior. No se puede reinvitar una cuenta que ya aceptó.
- **Application:** emitir/reemitir y aceptar invitaciones. La aceptación valida la invitación antes de provisionar la cuenta. Los comandos que contienen contraseña o token ocultan esos valores en `ToString()`.
- **Infrastructure:** EF Core/Npgsql con `IdentityDbContext`, tokens seguros SHA-256 y aprovisionamiento de cuentas locales con ASP.NET Core Identity (`AppUser`, `AppRole`, rol `Tailor` y vínculo a `TailorProfileId`).
- **API:** endpoints `POST /api/tailor-invitations` y `POST /api/tailor-invitations/accept`, manejo centralizado de excepciones con `ApiExceptionHandler` y `/health`.
- **Pendiente:** migraciones para tablas de ASP.NET Core Identity, OpenIddict (OAuth 2.0 / OIDC para tokens de acceso), entrega de correos real, roles adicionales (`Admin`, `Receptionist`) y bootstrap seguro del administrador inicial.

### Componentes todavía planeados

Contracts, RabbitMQ/MassTransit, outbox, SettlementService, Gateway, frontend Angular, Dockerfiles y despliegue Kubernetes no están implementados.

## Reglas principales del negocio

### Actores

- **Recepción:** recibe prendas, crea boletas, define/cobra precios, registra entregas y cierra liquidaciones.
- **Sastre:** realiza los trabajos y devuelve las prendas a recepción.
- **Cliente:** entrega y retira la prenda, y paga.
- **Admin:** administra sastres, catálogos, porcentaje y autorizaciones especiales.

### Boletas: trabajo y pago son carriles separados

El estado del trabajo avanza por `Received → Assigned → InProgress → DeliveredByTailor → DeliveredToCustomer`. Registrar `InProgress` es opcional; una boleta puede pasar de `Assigned` a `DeliveredByTailor`.

En paralelo, el estado del precio/pago avanza por `Unpriced → Priced → PartiallyPaid → Paid`. Las garantías usan `NoCharge`. Por eso una boleta puede estar lista en recepción y seguir pendiente de pago.

- El serial se asigna en PostgreSQL mediante secuencia. Es único, pero puede dejar huecos si una operación falla.
- Una boleta puede crearse sin precio y recibir cero, uno o varios pagos. El precio es total, no se divide entre trabajos.
- Cada trabajo descriptivo tiene texto y cantidad, sin precio propio; toda boleta debe tener al menos uno.
- Un abono no puede superar el saldo pendiente. El precio no cambia una vez recibido un abono.
- Los pagos son idempotentes por `PaymentId`.
- El celular del cliente debe ser colombiano: diez dígitos, comenzar por `3`; se aceptan `+57`/`57` y separadores comunes.
- La entrega al cliente requiere pago completo, excepto las garantías `NoCharge`.

### Garantías

Una garantía siempre es una boleta nueva con serial propio. Se enlaza a la boleta original cuando se encuentra; la original no se reabre. Si no se encuentra, la garantía queda sin enlace para mostrar que no tiene trazabilidad. El nombre y celular guardados son los de quien trae la prenda.

- Una garantía no se cobra al cliente (`NoCharge`) ni se liquida como trabajo regular.
- Debe asignarse al sastre original. Reasignarla a otro exige un motivo que queda registrado.
- No hay pago automático al sastre por atender una garantía. El admin puede autorizar una compensación una sola vez, y solo para un sastre distinto del original.
- El monto de compensación es la base para calcular el porcentaje: con el porcentaje actual del 35 %, autorizar `$8.000` produce `$2.800` para el sastre.

### Liquidaciones

La liquidación paga el porcentaje configurado (actualmente 35 %) sobre boletas de trabajo elegibles y compensaciones autorizadas. Una boleta regular es elegible si está `DeliveredByTailor`, no es garantía y aún no fue liquidada. Una compensación autorizada entra una sola vez.

No hay semanas ni fechas fijas: existe un periodo abierto que recepción cierra manualmente. Lo registrado después del instante de cierre pasa al siguiente periodo. El periodo cerrado es un snapshot inmutable. Una boleta entregada sin precio no se liquida hasta que tenga precio. SettlementService todavía no está implementado.

El sastre solo tiene estado activo/inactivo; no se manejan horarios. Desactivar/reactivar es idempotente. Las reglas completas y decisiones de negocio aún pendientes están en [skills/sastreria-dominio.md](skills/sastreria-dominio.md).

## Requisitos

- .NET SDK **10.0.115** (versión fijada en `global.json`).
- Docker Engine y Docker Compose funcionando.
- Git.
- Acceso a NuGet para restaurar paquetes la primera vez. Una advertencia de red al consultar datos de vulnerabilidades puede aparecer sin impedir la compilación.

## Configuración local

Desde la raíz del repositorio:

```bash
cp .env.example .env
```

Edita `.env` y establece una contraseña local para `WORK_ORDER_DB_PASSWORD`. Puedes establecer una contraseña propia para Tailor con `TAILOR_DB_PASSWORD`; si queda vacía, Compose reutiliza la contraseña de WorkOrder. `.env` está ignorado por Git: no lo agregues al repositorio.

Inicia las bases locales:

```bash
docker compose up -d work-order-db tailor-db
docker compose ps
```

| Servicio de base | Base de datos | Puerto host | Puerto del contenedor |
|---|---|---:|---:|
| `work-order-db` | `work_order_db` | `5433` | `5432` |
| `tailor-db` | `tailor_db` | `5434` | `5432` |

Cada base usa un volumen persistente. `docker compose down` detiene los contenedores sin borrar los datos. **`docker compose down -v` elimina los volúmenes y las bases locales; úsalo solo si quieres borrar esos datos.** Cambiar `POSTGRES_PASSWORD` en `.env` no cambia la contraseña de una base que ya fue inicializada en su volumen.

Las variables de `.env` no se convierten automáticamente en cadenas `ConnectionStrings__...` para `dotnet`. Cárgalas en la terminal donde ejecutarás las herramientas o la API:

```bash
set -a
source .env
set +a

export ConnectionStrings__WorkOrderDb="Host=localhost;Port=5433;Database=work_order_db;Username=${WORK_ORDER_DB_USER:-sastreria};Password=${WORK_ORDER_DB_PASSWORD}"
export ConnectionStrings__TailorDb="Host=localhost;Port=5434;Database=tailor_db;Username=${TAILOR_DB_USER:-sastreria};Password=${TAILOR_DB_PASSWORD:-$WORK_ORDER_DB_PASSWORD}"
```

Repite la carga de variables en cada terminal nueva. Si tu contraseña contiene caracteres especiales de una cadena Npgsql, escápalos según el formato de cadenas de conexión de Npgsql.

## Base de datos y migraciones

Restaura la herramienta local de EF Core:

```bash
dotnet tool restore
```

Aplica las migraciones a las bases locales:

```bash
dotnet ef database update \
  --project src/Services/WorkOrderService/WorkOrderService.Infrastructure/WorkOrderService.Infrastructure.csproj \
  --startup-project src/Services/WorkOrderService/WorkOrderService.Infrastructure/WorkOrderService.Infrastructure.csproj

dotnet ef database update \
  --project src/Services/TailorService/TailorService.Infrastructure/TailorService.Infrastructure.csproj \
  --startup-project src/Services/TailorService/TailorService.Infrastructure/TailorService.Infrastructure.csproj
```

Las herramientas de diseño leen `ConnectionStrings__WorkOrderDb` y `ConnectionStrings__TailorDb`, respectivamente. Las migraciones actuales son:

- WorkOrderService: `InitialCreate`, `AddWorkOrderSerialNumberSequence`, `AddWorkOrderVersion`.
- TailorService: `InitialCreate`.
- IdentityService: aún no tiene Infrastructure ni migraciones.

Al crear una migración, usa un nombre descriptivo y revisa el código generado antes de aplicarla:

```bash
dotnet ef migrations add <MigrationName> \
  --project <ruta-al-proyecto-Infrastructure.csproj> \
  --startup-project <ruta-al-proyecto-Infrastructure.csproj> \
  --output-dir Persistence/Migrations
```

No edites una migración ya aplicada; crea una nueva para corregir el modelo.

## Ejecutar las APIs

Compila la solución:

```bash
dotnet build Sastreria.slnx
```

Después de cargar las variables de conexión en la terminal, ejecuta cada API en su propia terminal:

```bash
dotnet run --project src/Services/WorkOrderService/WorkOrderService.Api/WorkOrderService.Api.csproj
```

```bash
dotnet run --project src/Services/TailorService/TailorService.Api/TailorService.Api.csproj
```

WorkOrderService escucha en `http://localhost:5258`; TailorService, en `http://localhost:5260`. Las APIs también tienen perfil HTTPS de desarrollo. En Development se publica OpenAPI. Los archivos `.http` de cada API contienen ejemplos para Bruno/REST Client:

- [WorkOrderService.Api.http](src/Services/WorkOrderService/WorkOrderService.Api/WorkOrderService.Api.http)
- [TailorService.Api.http](src/Services/TailorService/TailorService.Api/TailorService.Api.http)

## Endpoints disponibles

### WorkOrderService — `http://localhost:5258`

| Método | Ruta | Uso |
|---|---|---|
| `POST` | `/api/work-orders` | Crea una boleta con cliente, prenda y al menos un trabajo; responde con ID y serial. |
| `PUT` | `/api/work-orders/{id}/price` | Define el precio. |
| `POST` | `/api/work-orders/{id}/payments` | Registra un abono con `paymentId` y monto. |
| `GET` | `/health` | Health check. |

Ejemplo de creación:

```http
POST /api/work-orders
Content-Type: application/json

{
  "customerName": "Maria Lopez",
  "customerPhone": "3001234567",
  "garmentDescription": "Black pants",
  "jobs": [
    { "description": "Hem", "quantity": 2 }
  ]
}
```

La respuesta de creación incluye `id` y `serialNumber`. Luego se usa el ID en las rutas de precio y pagos. El contrato de pago exige un `paymentId` GUID para conservar la idempotencia:

```json
{ "paymentId": "<guid-único-del-pago>", "amount": 25000 }
```

La consulta `GET /api/work-orders/{id}` todavía no está implementada.

### TailorService — `http://localhost:5260`

| Método | Ruta | Uso |
|---|---|---|
| `POST` | `/api/tailors` | Crea el perfil del sastre activo; responde `201` con la ruta para consultarlo. |
| `GET` | `/api/tailors/{id}` | Consulta el perfil por ID. |
| `GET` | `/health` | Health check. |

Ejemplo de registro:

```http
POST /api/tailors
Content-Type: application/json

{ "name": "Maria Lopez" }
```

La API Tailor aún no está protegida por IdentityService; no la expongas a Internet ni la uses como un flujo público de autorregistro.

### Errores HTTP

Las APIs usan `ProblemDetails` con manejo central de excepciones:

| Estado | Significado |
|---:|---|
| `400` | Cuerpo o campos de la solicitud inválidos. |
| `404` | Recurso solicitado no encontrado. |
| `409` | Conflicto de concurrencia optimista. |
| `422` | Regla de negocio incumplida. |
| `500` | Error inesperado; no se deben exponer detalles internos. |

## Pruebas

Pruebas unitarias de dominio, Application y manejo de errores de API, excluyendo integración:

```bash
dotnet test Sastreria.slnx --filter "Category!=Integration"
```

Las pruebas de persistencia usan PostgreSQL real mediante Testcontainers, por lo que requieren Docker:

```bash
dotnet test test/WorkOrderService.Infrastructure.IntegrationTests/WorkOrderService.Infrastructure.IntegrationTests.csproj
dotnet test test/TailorService.Infrastructure.IntegrationTests/TailorService.Infrastructure.IntegrationTests.csproj
```

Las pruebas de integración aplican migraciones en un contenedor propio, usan el registro real de dependencias y un scope nuevo por petición simulada. Prueba toda la solución con:

```bash
dotnet test Sastreria.slnx
```

El flujo de trabajo es TDD: escribe una prueba que falle, implementa el mínimo cambio y vuelve a correr las pruebas. Las reglas completas están en [skills/sastreria-nuevo-servicio.md](skills/sastreria-nuevo-servicio.md).

## Seguridad y secretos

- No guardes secretos, cadenas de conexión, contraseñas ni tokens en el código, los ejemplos HTTP o Git. Usa `.env` local (ignorado por Git) o variables de entorno.
- No hay autenticación implementada todavía en las APIs. El endpoint `POST /api/tailors` solo debe usarse en el entorno de desarrollo actual.
- IdentityService no tiene aún endpoints ni almacenamiento real. El token de invitación solo se persiste como hash según el diseño; el generador, el correo y el provisionamiento de cuenta todavía requieren implementación de Infrastructure.
- El rol administrador debe provisionarse de forma controlada. Una invitación de sastre no puede asignar roles privilegiados.
- Al implementar el acceso web, la arquitectura prevista es OIDC Authorization Code con PKCE; no se colocan client secrets en Angular.

## Estructura del repositorio

```text
src/
├── Services/
│   ├── WorkOrderService/
│   │   ├── WorkOrderService.Domain/
│   │   ├── WorkOrderService.Application/
│   │   ├── WorkOrderService.Infrastructure/
│   │   └── WorkOrderService.Api/
│   ├── TailorService/
│   │   ├── TailorService.Domain/
│   │   ├── TailorService.Application/
│   │   ├── TailorService.Infrastructure/
│   │   └── TailorService.Api/
│   └── IdentityService/
│       ├── IdentityService.Domain/
│       └── IdentityService.Application/
├── BuildingBlocks/Contracts/       # planeado; aún no implementado
└── GatewayService/                 # planeado; aún no implementado
test/
├── WorkOrderService.*.Tests/
├── TailorService.*.Tests/
└── IdentityService.*.Tests/
```

`Directory.Build.props` centraliza .NET 10, nullable reference types e implicit usings. `Directory.Packages.props` centraliza las versiones NuGet. Las skills del directorio `skills/` contienen arquitectura, convenciones de servicios y reglas del dominio.

## Siguiente trabajo

1. IdentityService Infrastructure: EF Core/PostgreSQL, concurrencia y persistencia de invitaciones; pruebas Testcontainers.
2. Implementar la generación criptográfica del token, entrega de invitaciones y provisión de cuentas con ASP.NET Core Identity de forma transaccional.
3. IdentityService API y OpenIddict; bootstrap seguro del primer administrador y autorización del endpoint de Tailor.
4. Completar casos de uso de Tailor y WorkOrder.
5. Contracts, mensajería/outbox, SettlementService, Gateway y frontend.

El orden puede cambiar cuando el negocio o una decisión técnica lo justifique. Mantén este README y las skills sincronizados al cerrar cada etapa.

## Documentación del dominio y convenciones

- [Arquitectura y estado de los servicios](skills/sastreria-arquitectura.md)
- [Reglas del negocio y decisiones pendientes](skills/sastreria-dominio.md)
- [Convenciones de implementación y TDD](skills/sastreria-nuevo-servicio.md)
