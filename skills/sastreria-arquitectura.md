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
- RabbitMQ + MassTransit para eventos
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
4. **Los consumidores deben ser idempotentes** (los mensajes pueden llegar repetidos). Configurar reintentos y dead-letter queue.
5. **Redis solo donde perder el dato no duele**: caché de catálogos (tipos de prenda, sastres activos), llaves de idempotencia de pagos, bloqueo distribuido al cerrar liquidación. **Nunca** para el serial de la boleta ni para dinero.
6. **SettlementService mantiene su propio modelo** construido a partir de eventos; no consulta a WorkOrderService en el momento del cierre.
7. **Dinero siempre `decimal`** (nunca `double`/`float`), con moneda y redondeo definidos en un solo lugar.
8. **Fechas en UTC** en BD y eventos; conversión a hora local solo en la UI.
9. Los secretos y cadenas de conexión van en variables de entorno / `.env` (fuera de git), nunca en el código.
10. **Responsabilidad de asignación**: `WorkOrderService` es dueño de `WorkOrder.TailorId` y cambia la asignación mediante `WorkOrder.AssignTo(...)`. `TailorService` es dueño del perfil y disponibilidad del sastre; no modifica work orders. Antes de asignar, `WorkOrderService` valida al sastre a través de la frontera del servicio, nunca leyendo su base de datos.
11. **Identidad y autenticación**:
    - `IdentityService` vive en `src/Services/IdentityService`, es dueño de las cuentas, credenciales, roles y tokens; persiste en su propia base PostgreSQL.
    - Para el MVP, soporta correo y contraseña mediante ASP.NET Core Identity, y emite tokens con OpenIddict usando estándares OAuth 2.0/OIDC. Google puede añadirse después como proveedor externo opcional; nunca debe ser el único modo de acceso.
    - No guardar contraseñas en texto plano ni implementar algoritmos propios de hashing o firma de tokens. No permitir que el registro público elija su propio rol privilegiado. La cuenta admin inicial se provisiona de forma controlada.
    - No hay autorregistro público de sastres: un admin provisionado crea primero el perfil `Tailor` y luego invita el correo de esa persona con rol `Tailor`. La invitación es de un solo uso y expira; al aceptarla, la persona configura su contraseña local. El rol admin no se asigna mediante invitaciones de sastre.
    - El Gateway valida tokens y enruta. Cada API también valida los tokens y aplica autorización para protegerse de llamadas que eviten el Gateway.
    - `TailorService` conserva solo datos propios del negocio; no almacena contraseñas ni implementa login. `IdentityService` vincula la cuenta con el ID de perfil `Tailor`, que es un ID externo para IdentityService.
12. **Desarrollo local**: `docker-compose.yml` en la raíz con un PostgreSQL por servicio (`work-order-db` en el puerto 5433, `tailor-db` en el 5434; el siguiente usará el 5435), imagen `postgres:17` y `healthcheck`. Las credenciales van en `.env` (ignorado por git) y `.env.example` documenta las variables. Los puertos externos distintos evitan chocar con un PostgreSQL local en el 5432.
13. **Pruebas por capa**: dominio y Application con pruebas unitarias rápidas (sin base de datos); persistencia con pruebas de integración contra PostgreSQL real (Testcontainers); manejo de errores de la API con pruebas unitarias del `ApiExceptionHandler`. Detalle en `sastreria-nuevo-servicio`.

## Frontend Angular
- Es una SPA, así que no tiene servidor propio (a diferencia de Next.js): usar Authorization Code con PKCE para OIDC; no guardar client secrets en Angular.
- Habla únicamente con el Gateway, nunca directo con los servicios.

## Orden de construcción
1. Repo y solución base (**hecho**)
2. WorkOrderService (núcleo)
3. Contracts + RabbitMQ + outbox
4. Pagos: ya viven dentro de WorkOrderService (no hay servicio aparte por ahora)
5. TailorService
6. SettlementService (Redis para el bloqueo)
7. IdentityService + Gateway + Angular
8. Docker Compose completo → Kubernetes

Avanzar **paso a paso**: el usuario quiere guía incremental, con comandos listos para copiar y una breve explicación de por qué, confirmando el resultado antes de seguir.

## Estado actual (actualizar al cerrar cada paso)
- **WorkOrderService**: Domain completo (boleta, precio, abonos, garantías con compensación autorizada, trabajos, domain events); Application con `CreateWorkOrder`, `SetWorkOrderPrice` y `RegisterPayment`; Infrastructure con EF Core, migraciones, serial con secuencia y concurrencia optimista; API con esos tres endpoints y manejo central de errores. **Pendiente**: casos de uso de asignar sastre, iniciar trabajo, entregas y garantías; `GET /api/work-orders/{id}` y consultas; publicación de eventos (outbox).
- **TailorService**: Domain (`Tailor` con registrar, desactivar y reactivar, y sus eventos); Application (`RegisterTailor`, `GetTailorById`); Infrastructure (EF Core, migración, concurrencia); API de registro y consulta; pruebas de integración para persistencia y conflicto de concurrencia. **Pendiente**: casos de uso/endpoints de desactivar/reactivar, listar sastres activos y autorización.
- **IdentityService**: Domain y Application de las invitaciones de sastre (emitir, reemitir, aceptar). Infrastructure con persistencia EF Core, tokens SHA-256 e integración con ASP.NET Core Identity (`IAccountProvisioner` con rol `Tailor` y `AppUser.TailorProfileId`). API (`IdentityService.Api`) con endpoints de emitir y aceptar invitaciones, y `ApiExceptionHandler`. **Pendiente**: migraciones de base de datos para tablas de Identity, OpenIddict (emisión de tokens de acceso), bootstrap del primer admin y autenticación/autorización de endpoints.
- **Pendiente por completo**: Contracts, RabbitMQ y outbox; SettlementService; Gateway; Angular; Dockerfiles y Kubernetes.

## Decisiones abiertas
- Provisionamiento seguro de la cuenta admin inicial (secrets/configuración de despliegue; nunca credenciales predeterminadas en código).
- Roles y políticas concretas para recepción, sastre y admin, incluyendo cómo se vincula la identidad con el perfil `Tailor`.
- Cómo valida `WorkOrderService` que un sastre existe y está activo al asignar: llamada HTTP a `TailorService` (con caché en Redis) frente a **eventos (`TailorRegistered`, `TailorDeactivated`, `TailorReactivated`) y una copia local** de los sastres activos. Recomendación: eventos con copia local (asignar sigue funcionando si `TailorService` cae; el riesgo de consistencia eventual es bajo).
- Cuáles domain events se publican como integration events en `Contracts`, y la forma exacta del contrato (sin datos personales ni secretos).
