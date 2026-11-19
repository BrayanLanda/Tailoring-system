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

## Estructura interna del servicio (DDD, un proyecto por capa)
Cada servicio es un *bounded context* y se divide en cuatro proyectos, para que el compilador haga cumplir la regla de dependencias:

```
src/Services/WorkOrderService/
├── WorkOrderService.Domain/           # agregados, entidades, value objects, domain events, interfaces de repositorio
├── WorkOrderService.Application/      # casos de uso (commands/queries + handlers), DTOs, puertos
├── WorkOrderService.Infrastructure/   # EF Core, DbContext, repositorios, outbox, consumidores, Redis
└── WorkOrderService.Api/              # Program.cs, endpoints, DI, health checks, Dockerfile
test/
├── WorkOrderService.Domain.Tests/
└── WorkOrderService.Application.Tests/
```

**Regla de dependencias** (las flechas apuntan hacia adentro, nunca al revés):
`Api → Application + Infrastructure`, `Infrastructure → Application`, `Application → Domain`, `Domain → nada`.
`Domain` no referencia ningún paquete de infraestructura (ni EF Core, ni MassTransit, ni ASP.NET).

## Reglas de DDD
1. **Lenguaje ubicuo**: las clases usan el vocabulario en inglés de `sastreria-dominio` (`WorkOrder`, `Tailor`, `Settlement`...). Si el negocio cambia un término, se cambia en el código.
2. **Agregados**: un agregado es un límite de consistencia y de transacción. Una transacción modifica **un solo agregado**. Se accede a sus partes internas solo por la raíz.
   - `WorkOrder` (raíz; contiene sus `AlterationJob` como value objects y sus `Payment` como entidades), `Tailor`, `Settlement` (raíz; contiene `SettlementLine`).
   - **Los pagos viven dentro de `WorkOrder`**: "no se entrega hasta pagar" es un invariante y necesita consistencia inmediata. Solo se separaría un `PaymentService` si aparecen cierre de caja, devoluciones, medios de pago/pasarelas, varias sedes o reportes financieros; en ese caso se alimentaría con eventos de `WorkOrderService`.
   - **Un agregado referencia a otro solo por ID** (`WorkOrderId`, `TailorId`), nunca por objeto.
3. **Modelo rico, no anémico**: las reglas viven en el agregado. Nada de `workOrder.Status = ...` desde fuera; se usan métodos con intención (`workOrder.AssignTo(tailorId)`, `workOrder.MarkDeliveredByTailor()`) que validan la transición y lanzan una excepción de dominio si es ilegal.
4. **Constructores protegidos**: las entidades se crean con *factory methods* estáticos (`WorkOrder.Create(...)`) que dejan el objeto siempre válido. Setters `private`. EF Core usa un constructor privado sin parámetros.
5. **Value objects** inmutables (`record`) para conceptos sin identidad: `Money`, `SerialNumber`, `PhoneNumber`, `AlterationJob`, `WarrantyCompensation`, `Percentage`. Se validan al crearse. El dinero nunca es un `decimal` suelto en el dominio.
6. **Identificadores tipados** (`WorkOrderId`, `TailorId`) en vez de `Guid` sueltos, para no mezclarlos.
7. **Domain events** (ej. `WorkOrderCreated`) los levanta el agregado en una lista interna. Se despachan al guardar. **No son lo mismo** que los *integration events* de `Contracts`: el handler de Application traduce un domain event a un integration event y lo publica por el outbox.
8. **Repositorios**: una interfaz por agregado raíz, definida en `Domain`, implementada en `Infrastructure`. Solo cargan y guardan agregados completos.
9. **Domain services / policies** para reglas que no pertenecen a una sola entidad (ej. `SettlementEligibilityPolicy`: qué work orders entran a una liquidación).
10. **Application**: un caso de uso = un handler (`CreateWorkOrderHandler`). Orquesta: carga el agregado, ejecuta el método de dominio, guarda. Sin lógica de negocio. Empezamos con handlers propios, sin MediatR (ahora tiene licencia comercial); se puede revisar más adelante.
11. Las **consultas (lectura)** pueden saltarse el dominio y leer con proyecciones directas a DTOs, para no cargar agregados completos solo para mostrar listas.

12. **Convenciones de dominio ya establecidas**:
    - `DomainException` (en `Domain/Exceptions`) para toda regla de negocio violada.
    - Value objects: `Of(...)` crea un valor desde cero y normaliza (ej. `PhoneNumber.Of`); `From(...)` reconstruye un dato que ya existe, como el serial asignado por la BD (`SerialNumber.From`).
    - Identificadores tipados como `readonly record struct`; los del propio servicio usan `Guid.CreateVersion7()`. El ID de un agregado ajeno (ej. `TailorId`) **no** tiene `New()`.
    - **El dominio nunca llama al reloj** (`DateTime.UtcNow`): las fechas entran por parámetro, así las pruebas son exactas.
    - Las entidades internas de un agregado (ej. `Payment`) tienen fábrica `internal`: solo la raíz puede crearlas.
    - **Domain events**: `record` que implementa `IDomainEvent`, en `Domain/Events`. El agregado hereda de `AggregateRoot` y los levanta con `Raise(...)` solo cuando la operación tuvo éxito (si falla, no se levanta nada). Llevan IDs y los datos que otros servicios necesitan; la hora solo si el dominio la conoce (el outbox sella el resto). Infrastructure debe ignorar `DomainEvents` al mapear con EF Core.
    - Operaciones que pueden reintentarse (ej. `RegisterPayment`) son idempotentes mediante un ID que llega de fuera (`PaymentId`); repetir el mismo ID no duplica nada.
    - Los enums empiezan en `1`, para que el `0` por defecto nunca sea un estado válido.
    - Los campos de fecha de hitos importantes (ej. `DeliveredByTailorAt`) se guardan en el agregado; la liquidación depende de ellos.
13. **TDD**: cada regla nueva empieza con una prueba que falla (rojo), luego el mínimo código para que pase (verde). Comprobar siempre que la prueba falla antes de escribir el código.

14. **Application (casos de uso)**:
    - Una carpeta por caso de uso: `Application/WorkOrders/CreateWorkOrder/` con `Command` (record), `Handler` (método `HandleAsync`) y `Result` (record).
    - **Puertos**: `IWorkOrderRepository` (`Domain/Repositories`) e `ISerialNumberGenerator` (`Domain/Services`) viven en `Domain`; `IUnitOfWork` vive en `Application/Abstractions`. Infrastructure los implementa.
    - La hora se pide con `TimeProvider` (nunca `DateTime.UtcNow`) y el handler se la pasa al agregado.
    - El handler convierte los datos primitivos a value objects **antes** de pedir recursos con efecto (ej. el serial), para no gastar números por entradas inválidas.
    - El handler orquesta (cargar, ejecutar el método del agregado, guardar); **no contiene reglas de negocio**.
    - Pruebas de handlers con **fakes escritos a mano** en `test/<Service>.Application.Tests/Fakes/` (repositorio en memoria, serial secuencial, unit of work contador, `TimeProvider` fijo). No se usan librerías de mocks.

15. **Pruebas de integración (persistencia)**:
    - Proyecto `test/<Service>.Infrastructure.IntegrationTests`, con **PostgreSQL real en un contenedor** (Testcontainers), nunca una base en memoria: el mapeo de EF Core contra PostgreSQL es justo lo que se quiere verificar.
    - Un contenedor compartido por todas las pruebas (`ICollectionFixture`). Las migraciones se aplican al arrancar el contenedor.
    - Se usa el **registro real de dependencias** (`Add<Service>Infrastructure`): cada "petición" de la prueba es un `scope` nuevo (un `DbContext` nuevo), así se imita el ciclo real de una petición HTTP y no se reutiliza el estado en memoria de EF.
    - Patrón de cada prueba: guardar en una petición, **recargar en otra** y comprobar. Verificar siempre las partes delicadas: value objects, colecciones hijas (abonos, trabajos), tipos "owned", índices únicos y concurrencia.
    - Las clases llevan `[Trait("Category", "Integration")]` para poder excluirlas (`dotnet test --filter "Category!=Integration"`) cuando no hay Docker.

16. **Concurrencia optimista** (Infrastructure, sin tocar el dominio):
    - Cada agregado raíz tiene una columna *shadow* `Version` (`int`, `IsConcurrencyToken`). Cuando dos peticiones modifican el mismo agregado a la vez, la segunda falla en vez de pisar a la primera.
    - El `DbContext` incrementa `Version` en `SaveChangesAsync` cuando el agregado **cambió**: su fila quedó `Modified` **o** tiene domain events pendientes. La segunda condición es necesaria porque insertar un registro hijo (ej. un abono) no modifica la fila de la raíz.
    - Consecuencia de diseño: **toda operación del agregado que cambie estado debe modificar la fila de la raíz o levantar un domain event**.
    - `IUnitOfWork` traduce `DbUpdateConcurrencyException` a `ConcurrencyConflictException` (Application), y la API la responde como **409 Conflict**.
    - Toda regla de concurrencia se prueba con una prueba de integración: dos "peticiones" cargan el mismo agregado, ambas lo modifican, la segunda debe fallar.

17. **API (errores y contratos)**:
    - Los endpoints **no tienen `try/catch`**. Un `IExceptionHandler` central traduce excepciones a `ProblemDetails`: `*NotFoundException` → **404**, `ConcurrencyConflictException` → **409**, `DomainException` → **422**, cualquier otra → **500** genérico sin detalles internos (el handler no la atiende).
    - **400** (`ValidationProblem`) solo para peticiones mal formadas (campos faltantes o nulos), validadas en el endpoint.
    - Peticiones y respuestas usan tipos simples (`Guid`, `long`, `decimal`, `string`), nunca value objects ni entidades del dominio.
    - Todo `201 Created` devuelve un `Location` que existe (hay un GET para ese recurso).
    - Los mensajes de las excepciones llegan al cliente en `detail`: deben ser legibles y no filtrar tipos internos (ej. usar `id.Value`, no `id`).
    - El mapeo de errores se prueba con pruebas unitarias del handler (`DefaultHttpContext`), sin servidor ni base de datos.

## Pasos para crear un servicio
1. Crear los cuatro proyectos (`Api` con `dotnet new webapi`, el resto con `dotnet new classlib`), agregarlos a la solución y configurar las referencias según la regla de dependencias.
2. Borrar de cada `.csproj` lo que ya define `Directory.Build.props` (`TargetFramework`, `Nullable`, `ImplicitUsings`) y quitar `Class1.cs`.
3. Agregar paquetes con `dotnet add package`; la versión queda en `Directory.Packages.props`. No escribas versiones dentro del `.csproj`.
4. Empezar siempre por el **dominio** (agregado, value objects, reglas) y sus pruebas unitarias, antes de tocar BD o endpoints.
5. Application: casos de uso e interfaces. Infrastructure: EF Core con Npgsql y su **propia base de datos** (`<name>_db`).
6. Api: endpoints, health check (`/health`), inyección de dependencias.
7. Si publica eventos: MassTransit con RabbitMQ y **outbox de EF Core**. Si los consume: consumidores idempotentes con reintentos y DLQ.
8. `Dockerfile` multi-stage en el proyecto `Api` y entrada en `docker-compose.yml` con su PostgreSQL, variables de entorno y `depends_on` con health checks.

## Reglas de código
- Validar toda entrada en el borde (endpoints), no confiar en el cliente.
- Un endpoint no contiene lógica de negocio: delega en un handler de `Application`.
- Transiciones de estado **solo** mediante métodos del agregado (ej. no se puede pasar a `DeliveredByTailor` desde `Received`).
- Migraciones EF versionadas en git; nunca editar una migración ya aplicada.
- Logs estructurados con Serilog; incluir el `SerialNumber` de la work order o el id de correlación en cada log relevante.
- No exponer entidades de dominio en la API; usar DTOs.

## Consideraciones de IdentityService
- IdentityService es un servicio propio en `src/Services/IdentityService` con PostgreSQL, ASP.NET Core Identity para cuentas/contraseñas/roles y OpenIddict para OAuth 2.0/OIDC.
- El inicio de sesión local con correo y contraseña es el flujo base. Google es un proveedor externo opcional que se puede incorporar después; no se requiere para crear una cuenta local.
- IdentityService nunca almacena contraseñas en texto plano ni crea algoritmos criptográficos propios. El rol admin se provisiona de forma controlada; un registro público no puede autoasignarse roles.
- No hay autorregistro público de sastres: el admin crea el perfil `Tailor` e invita el correo con rol `Tailor`. La invitación es de un solo uso y expira; al aceptarla, la persona configura una contraseña local. El flujo no puede asignar el rol admin.
- IdentityService vincula la cuenta con el ID de perfil `Tailor`, que es externo para IdentityService. TailorService solo guarda su perfil de negocio y no guarda credenciales.
- Gateway y APIs validan tokens. Las APIs también aplican autorización, incluso si el tráfico suele pasar por Gateway.
- La cuenta admin inicial se provisiona mediante configuración/secrets de despliegue, sin credenciales predeterminadas en código.
- Hay **una sola invitación por perfil de sastre**. Reemitir (`Reissue`) reemplaza el hash del token, el correo y la vigencia, así que el enlace anterior deja de funcionar y falla con el mismo mensaje genérico de un token incorrecto. No se puede reemitir una invitación ya aceptada. Si el correo falla tras guardar, el admin vuelve a invitar.
- La verificación del token de invitación vive en el agregado: `Accept(presentedHash, acceptedAt)` compara en tiempo constante y se hace antes que cualquier comprobación de estado, con un mensaje genérico, para no revelar si una invitación existe, expiró o ya se usó.
- Solo se persiste el hash del token. El token en claro viaja únicamente por el puerto `IInvitationDelivery`: nunca se guarda, se registra en logs ni se incluye en eventos.
- Infrastructure (pendiente): índice único sobre `tailor_profile_id` para garantizar una sola invitación por perfil, y columna `Version` para concurrencia.

## Antes de terminar un cambio
Verifica que compila (`dotnet build`), que las pruebas pasan (`dotnet test`) y que, si tocaste un contrato de evento, los servicios que lo consumen siguen siendo compatibles.
