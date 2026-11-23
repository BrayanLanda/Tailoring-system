---
name: sastreria-arquitectura
description: Arquitectura, stack, estructura de carpetas y decisiones técnicas del proyecto de sastrería (microservicios .NET 10, Angular, PostgreSQL, RabbitMQ, Redis, Docker, Kubernetes). Úsala SIEMPRE que se cree o modifique un servicio, un proyecto de la solución, docker-compose, manifiestos de Kubernetes, mensajería/eventos, caché, autenticación, el gateway o el frontend Angular, o cuando se pregunte dónde va algo o qué tecnología usar para algo.
---

# Arquitectura: sastrería

## Enfoque
Se usa **Domain-Driven Design**: cada servicio es un *bounded context* con su propio modelo y lenguaje. Dentro de cada servicio hay cuatro proyectos (Domain, Application, Infrastructure, Api). Detalle en la skill `sastreria-nuevo-servicio`.

## Objetivo del proyecto
El negocio es pequeño y un monolito modular sería más simple. Se eligen **microservicios a propósito**, para practicar un entorno realista: Docker, colas, caché, outbox, observabilidad y Kubernetes. Por eso es correcto aplicar patrones "de producción" aunque parezcan excesivos, pero no agregues piezas que no tengan un uso concreto en el negocio.

## Stack
- .NET 10 (SDK fijado en `global.json`), C#, ASP.NET Core Web API
- Angular (CLI 22.x)
- PostgreSQL, **una base de datos por servicio**, nunca compartida
- RabbitMQ para eventos; `RabbitMQ.Client` con un outbox propio en cada servicio que publique eventos.
- Redis para caché, idempotencia y bloqueos
- Docker Compose para desarrollo; Kubernetes después, cuando Compose funcione
- YARP como API Gateway
- Observabilidad: Serilog + Seq u OpenTelemetry

Entorno de desarrollo: Ubuntu, `~/Documents/itm/sastreria`.

## Estructura del repo
```
sastreria/
├── src/
│   ├── BuildingBlocks/Contracts/     # eventos compartidos (solo records/DTOs)
│   ├── Services/
│   │   ├── WorkOrderService/         # .Domain / .Application / .Infrastructure / .Api
│   │   ├── TailorService/
│   │   ├── SettlementService/
│   │   └── IdentityService/          # identidad, credenciales, roles y tokens
│   └── GatewayService/
├── frontend/web-app/                 # Angular
├── infra/{docker,k8s}/
├── docker-compose.yml
├── global.json
├── Directory.Build.props             # net10.0, Nullable, ImplicitUsings
├── Directory.Packages.props          # versiones NuGet centralizadas
└── Sastreria.slnx
```

## Idioma
**Todo el código va en inglés**: nombres de proyectos, servicios, clases, métodos, enums, endpoints, eventos, tablas/columnas, variables de entorno, ramas y mensajes de commit. Es estándar de la industria. La equivalencia negocio → código está en la skill `sastreria-dominio`. Los textos que ve el usuario final (UI) van en español mediante archivos de traducción, no escritos a mano en el código.

## Servicios y responsabilidades
| Servicio | Responsabilidad | Persistencia |
|---|---|---|
| WorkOrderService | Boletas, serial, garantías, estados de trabajo, asignación de sastre, precio y pagos (abonos) | PostgreSQL |
| TailorService | Perfil, estado y disponibilidad de los sastres | PostgreSQL |
| SettlementService | Periodos, elegibilidad, cálculo del 35 %, cierre | PostgreSQL + Redis |
| IdentityService | Cuentas, credenciales, roles y emisión de tokens | PostgreSQL |
| GatewayService | Entrada única, enrutamiento, auth | — |

## Reglas de arquitectura
1. **Cada servicio es dueño de sus datos.** Nadie lee la BD de otro servicio. Se comunican por HTTP (consultas) o eventos (hechos).
2. **Los eventos describen hechos pasados** y viven en `Contracts`: `WorkOrderCreated`, `PriceSet`, `PaymentReceived`, `TailorAssigned`, `GarmentDeliveredByTailor`, `GarmentDeliveredToCustomer`, `WarrantyRegistered`, `SettlementClosed`. `Contracts` solo contiene tipos de datos, sin lógica.
3. **Outbox pattern** en todo servicio que guarde datos y publique un evento: se guarda el cambio y el evento en la misma transacción. Así no se pierde un evento si el servicio cae.
4. **RabbitMQ**: el publicador declara el exchange; cada consumidor declara su propia cola durable y la enlaza al exchange para recibir su propia copia de cada evento. Los consumidores son idempotentes: para proyecciones donde el último valor gana, comparan `OccurredOn`; para efectos acumulativos o no conmutativos, registran `EventId` en una tabla de procesados. Tras un número limitado de reintentos, los mensajes fallidos van a una dead-letter queue.
5. **Redis solo donde perder el dato no duele**: caché de catálogos (tipos de prenda, sastres activos), llaves de idempotencia de pagos, bloqueo distribuido al cerrar liquidación. **Nunca** para el serial de la boleta ni para dinero.
6. **SettlementService mantiene su propio modelo** construido a partir de eventos; no consulta a WorkOrderService en el momento del cierre.
7. **Dinero siempre `decimal`** (nunca `double`/`float`), con moneda y redondeo definidos en un solo lugar.
8. **Fechas en UTC** en BD y eventos; conversión a hora local solo en la UI.
9. Los secretos y cadenas de conexión van en variables de entorno / `.env` (fuera de git), nunca en el código.
10. **Responsabilidad de asignación**: `WorkOrderService` es dueño de `WorkOrder.TailorId` y cambia la asignación mediante `WorkOrder.AssignTo(...)`. `TailorService` es dueño del perfil y disponibilidad del sastre; no modifica work orders. Antes de asignar, `WorkOrderService` valida al sastre a través de la frontera del servicio, nunca leyendo su base de datos.
11. **Identidad y autenticación**:
    - `IdentityService` vive en `src/Services/IdentityService`, es dueño de las cuentas, credenciales, roles y tokens; persiste en su propia base PostgreSQL.
    - Para el MVP, soporta correo y contraseña mediante ASP.NET Core Identity, y emite JWT localmente. Google puede añadirse después como proveedor externo opcional; nunca debe ser el único modo de acceso.
    - IdentityService firma JWT con RS256 y una clave privada RSA de al menos 2048 bits, cargada desde archivo secreto (`Jwt:PrivateKeyPath`) o PEM de configuración. WorkOrderService recibe exclusivamente la clave pública (`Jwt:PublicKeyPath`/`Jwt:PublicKeyPem`) y valida emisor, audiencia, expiración, firma y algoritmo RS256. La configuración falla al arrancar si la clave falta o no es válida; WorkOrderService rechaza que se configure una clave privada.
    - No guardar contraseñas en texto plano ni implementar algoritmos propios de hashing o firma de tokens. ASP.NET Core Identity bloquea la cuenta durante 5 minutos tras 5 intentos fallidos; un login correcto reinicia el contador. Para correo inexistente o cuenta bloqueada, se verifica un hash ficticio y se responde con el mismo error genérico para reducir la enumeración por diferencias de tiempo. No permitir que el registro público elija su propio rol privilegiado. La cuenta admin inicial se provisiona de forma controlada.
    - No hay autorregistro público de sastres: un admin provisionado crea primero el perfil `Tailor` y luego invita el correo de esa persona con rol `Tailor`. La invitación es de un solo uso y expira; al aceptarla, la persona configura su contraseña local. El rol admin no se asigna mediante invitaciones de sastre.
    - El Gateway valida tokens y enruta. Cada API también valida los tokens y aplica autorización para protegerse de llamadas que eviten el Gateway.
    - `TailorService` conserva solo datos propios del negocio; no almacena contraseñas ni implementa login. `IdentityService` vincula la cuenta con el ID de perfil `Tailor`, que es un ID externo para IdentityService.
12. **Desarrollo local**: `docker-compose.yml` en la raíz con un PostgreSQL por servicio (`work-order-db` en el puerto 5433, `tailor-db` en el 5434; el siguiente usará el 5435), imagen `postgres:17` y `healthcheck`. Las credenciales van en `.env` (ignorado por git) y `.env.example` documenta las variables. Los puertos externos distintos evitan chocar con un PostgreSQL local en el 5432.
13. **Pruebas por capa**: dominio y Application con pruebas unitarias rápidas (sin base de datos); persistencia con pruebas de integración contra PostgreSQL real (Testcontainers); manejo de errores de la API con pruebas unitarias del `ApiExceptionHandler`. Los flujos entre componentes reales usan Testcontainers para PostgreSQL y RabbitMQ, y `WebApplicationFactory` para las APIs. La configuración leída al inicio de la aplicación se pasa mediante variables de entorno antes de crear la fábrica; no mediante `ConfigureAppConfiguration`. Detalle en `sastreria-nuevo-servicio`.
14. Las opciones que contienen secretos (por ejemplo `RabbitMqOptions`) sobrescriben `ToString()` para no imprimirlos en logs.

## Frontend Angular
- Es una SPA, así que no tiene servidor propio (a diferencia de Next.js): usar Authorization Code con PKCE para OIDC; no guardar client secrets en Angular.
- Habla únicamente con el Gateway, nunca directo con los servicios.

## Orden de construcción
1. Repo y solución base (**hecho**)
2. WorkOrderService (núcleo)
3. Contracts + RabbitMQ + outbox (**en curso**: publicador de Tailor y consumer inicial de WorkOrder; falta probar el ciclo completo outbox→broker→consumer)
4. Pagos: ya viven dentro de WorkOrderService (no hay servicio aparte por ahora)
5. TailorService
6. SettlementService (Redis para el bloqueo)
7. IdentityService + Gateway + Angular
8. Docker Compose completo → Kubernetes

Avanzar **paso a paso**: el usuario quiere guía incremental, con comandos listos para copiar y una breve explicación de por qué, confirmando el resultado antes de seguir.

## Estado actual (actualizar al cerrar cada paso)
- **WorkOrderService**: Domain completo (boleta, precio, abonos, garantías con compensación autorizada, trabajos, asignación y domain events); Application con `CreateWorkOrder`, `SetWorkOrderPrice`, `RegisterPayment` y `AssignTailor` (valida que el sastre exista y esté activo en la proyección local). Infrastructure persiste `assignable_tailors`; `TailorProjectionUpdater` ignora duplicados y eventos con `OccurredOn` anterior o igual al último aplicado. `TailorEventConsumer` consume su cola durable, confirma después de persistir y aplica reintentos limitados con DLQ. La API valida JWT RS256 con la clave pública; `POST /api/work-orders/{id}/assignments` exige rol `Admin`. Pruebas con Testcontainers cubren el flujo Tailor outbox → RabbitMQ → proyección WorkOrder y la asignación HTTP protegida. **Pendiente**: inicio/entregas/garantías, `GET /api/work-orders/{id}` y outbox propio.
- **TailorService**: Domain (`Tailor` con registrar, desactivar y reactivar, y sus eventos); Application (`RegisterTailor`, `GetTailorById`); Infrastructure (EF Core, migración, concurrencia); API de registro y consulta; pruebas de integración para persistencia y conflicto de concurrencia. **Pendiente**: casos de uso/endpoints de desactivar/reactivar, listar sastres activos y autorización.
- **IdentityService**: invitaciones, autenticación local, EF Core, ASP.NET Core Identity y seed de roles/admin. API de login, emisión protegida de invitaciones y aceptación. Firma JWT RS256 con clave privada RSA de IdentityService. Pruebas HTTP con PostgreSQL Testcontainers y `WebApplicationFactory` verifican login inválido (401), algoritmo RS256 y el flujo de invitación hasta los claims `role` y `tailor_profile_id` del sastre.
- **Seguridad pendiente de IdentityService (bloqueante antes de exponerlo fuera de localhost)**: la entrega de invitaciones solo existe como implementación de desarrollo; el alta de cuenta (usuario + rol + invitación aceptada) debe ser atómica; limitar la tasa de peticiones del endpoint de aceptación. La verificación ficticia mitiga diferencias de trabajo de hash, pero no garantiza tiempos de respuesta idénticos.
- **Mensajería**: contratos versionados para Tailor; TailorService persiste eventos en su outbox y publica al exchange durable con publisher confirms; el publicador no declara colas. WorkOrderService declara su cola durable, confirma tras persistir y aplica retry/DLQ. El flujo Tailor outbox→RabbitMQ→WorkOrder consumer está probado con PostgreSQL y RabbitMQ reales (Testcontainers). Pendiente agregar exclusión/lease al outbox para varias instancias.
- **Pendiente por completo**: SettlementService; Gateway; Angular; Dockerfiles y Kubernetes.

## Decisiones abiertas
- Correo real para entregar invitaciones en producción y límite de tasa en aceptación.
- Roles y políticas concretas para recepción, sastre y admin, incluyendo cómo se vincula la identidad con el perfil `Tailor`.
- **Mensajería y outbox**: decisión tomada: outbox propio en Infrastructure y `RabbitMQ.Client` para publicar. Domain/Application no dependen de RabbitMQ; TailorService marca como procesado solo después del publisher confirm. Cada consumidor administra una cola durable propia. WorkOrderService mantiene la copia local de sastres activos mediante eventos.
- Incorporar contratos y mapeos equivalentes para los eventos que WorkOrderService necesite publicar, cuidando no incluir datos personales ni secretos.
