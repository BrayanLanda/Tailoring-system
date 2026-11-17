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
│   │   └── SettlementService/
│   ├── IdentityService/              # (a definir su ubicación al llegar a este paso)
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
| WorkOrderService | Boletas, serial, garantías, estados de trabajo, precio y pagos (abonos) | PostgreSQL |
| TailorService | Sastres y asignación de trabajos | PostgreSQL |
| SettlementService | Periodos, elegibilidad, cálculo del 35 %, cierre | PostgreSQL + Redis |
| IdentityService | Usuarios y roles (recepción, sastre, admin) | PostgreSQL |
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

## Frontend Angular
- Es una SPA, así que no tiene servidor propio (a diferencia de Next.js): la autenticación se resuelve con **OIDC + PKCE** o con un patrón BFF. **Decisión pendiente** al llegar a IdentityService.
- Habla únicamente con el Gateway, nunca directo con los servicios.

## Orden de construcción
1. Repo y solución base (**hecho**)
2. WorkOrderService (núcleo)
3. Contracts + RabbitMQ + outbox
4. Pagos: ya viven dentro de WorkOrderService (no hay servicio aparte por ahora)
5. TailorService
6. SettlementService (Redis para el bloqueo)
7. Gateway + Identity + Angular
8. Docker Compose completo → Kubernetes

Avanzar **paso a paso**: el usuario quiere guía incremental, con comandos listos para copiar y una breve explicación de por qué, confirmando el resultado antes de seguir.

## Decisiones abiertas
- Ubicación y tecnología de IdentityService (¿Duende, Keycloak u otra?)
- Estrategia de autenticación del frontend (PKCE vs BFF)