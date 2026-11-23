---
name: sastreria-nuevo-servicio
description: Convenciones y pasos para crear o ampliar un microservicio .NET del proyecto de sastrería (estructura de carpetas interna, nombres, EF Core con PostgreSQL, Dockerfile, entrada en docker-compose, mensajería con outbox, health checks, pruebas). Úsala SIEMPRE que se vaya a crear un servicio nuevo, añadir endpoints, entidades, migraciones, consumidores o publicadores de eventos, o contenerizar un servicio, aunque el usuario solo diga "crea el servicio X" o "agrega un endpoint".
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
├── WorkOrderService.Application.Tests/            # con Fakes/
├── WorkOrderService.Infrastructure.IntegrationTests/  # PostgreSQL real (Testcontainers)
└── WorkOrderService.Api.Tests/                    # manejo de errores de la API
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
5. **Value objects** inmutables, declarados como **`sealed record` (clase), nunca `struct`**: un `struct` siempre puede crearse con `default` y saltarse la validación. Solo los identificadores tipados son `readonly record struct`. Cada value object con reglas tiene sus propias pruebas. Para conceptos sin identidad: `Money`, `SerialNumber`, `PhoneNumber`, `AlterationJob`, `WarrantyCompensation`, `Percentage` (y en Identity: `EmailAddress`, `InvitationTokenHash`). Se validan al crearse. El dinero nunca es un `decimal` suelto en el dominio.
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
    - **Una operación rechazada no cambia nada**: primero se validan todas las reglas y después se modifica el estado (ej. `Reissue` valida antes de reemplazar el token; una reemisión fallida deja la invitación como estaba).
    - **Idempotencia por estado**: repetir una operación sobre algo que ya está en su estado final (ej. `Deactivate` sobre un sastre inactivo) no hace nada y no levanta evento. Es el equivalente, cuando no hay un ID que comparar, a la idempotencia por ID de los abonos.
    - Una regla vive **en un solo lugar**: el agregado. Si el handler la repite, se quita del handler, pero solo después de escribir la prueba que protege ese comportamiento.
    - Los campos de fecha de hitos importantes (ej. `DeliveredByTailorAt`) se guardan en el agregado; la liquidación depende de ellos.
13. **TDD**: cada regla nueva empieza con una prueba que falla (rojo), luego el mínimo código para que pase (verde). Comprobar siempre que la prueba falla antes de escribir el código.
    - **Refactor**: si el comportamiento no cambia, las pruebas deben estar en verde antes y después; son la red de seguridad.
    - **Cambio de firma**: se actualizan primero las pruebas; el rojo es de compilación y el compilador lista todo lo que se rompe.
    - Las pruebas de integración de código ya existente *verifican*, no son rojo-primero. En cambio, la prueba de un bug nuevo (ej. la concurrencia) sí empieza en **rojo real**: falla en ejecución mostrando el bug.
    - Una prueba que pasaría por otro motivo no demuestra nada: se afirma el efecto o mensaje específico (ej. `Assert.Contains("no charge", ex.Message)`).
    - Una prueba solo puede usar variables declaradas dentro de ella; si menciona `workOrder`, debe haber un `var workOrder = ...` arriba.

14. **Application (casos de uso)**:
    - Una carpeta por caso de uso: `Application/WorkOrders/CreateWorkOrder/` con `Command` (record), `Handler` (método `HandleAsync`) y `Result` (record).
    - **Puertos**: `IWorkOrderRepository` (`Domain/Repositories`) e `ISerialNumberGenerator` (`Domain/Services`) viven en `Domain`; `IUnitOfWork` vive en `Application/Abstractions`. Infrastructure los implementa.
    - La hora se pide con `TimeProvider` (nunca `DateTime.UtcNow`) y el handler se la pasa al agregado.
    - El handler convierte los datos primitivos a value objects **antes** de pedir recursos con efecto (ej. el serial), para no gastar números por entradas inválidas.
    - El handler orquesta (cargar, ejecutar el método del agregado, guardar); **no contiene reglas de negocio**.
    - Los efectos externos (enviar un correo, crear una cuenta, generar o hashear tokens) son **puertos** en `Application/Abstractions` (`IInvitationDelivery`, `IAccountProvisioner`, `IInvitationTokenHasher`) con fakes en las pruebas.
    - **Orden**: primero el agregado valida (token, estado, expiración); solo después se ejecutan efectos externos. Nunca crear una cuenta, enviar un correo ni cobrar antes de que el agregado haya aceptado la operación.
    - **No filtrar existencia**: "no existe" y "datos incorrectos" producen exactamente el mismo mensaje cuando la diferencia ayudaría a un atacante (ej. `TailorInvitation.NotValidMessage`).
    - Los errores de entrada que valida otro componente (ej. la política de contraseñas de Identity) no son `DomainException`: usan una excepción propia con la lista de errores (`AccountCreationFailedException`).
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
    - El `IUnitOfWork` traduce a `ConcurrencyConflictException` (409) tanto `DbUpdateConcurrencyException` como las violaciones de índice único de PostgreSQL (`SqlState` 23505): una restricción de la base de datos que se viola por una carrera no es un error del servidor.
    - Los endpoints de administración (emitir invitaciones, cambiar precios de otros, etc.) exigen autenticación y rol; nunca quedan abiertos, ni siquiera de forma provisional.
    - El mapeo de errores se prueba con pruebas unitarias del handler (`DefaultHttpContext`), sin servidor ni base de datos.

18. **Secretos**: un `record` imprime todas sus propiedades en `ToString()`, y eso llega a los logs y a los mensajes de las pruebas que fallan. Todo comando, resultado, evento u opciones de configuración que contengan un secreto (contraseña, token en claro, `RabbitMqOptions`) sobrescriben `ToString()` ocultándolo y llevan una prueba. Los secretos nunca viajan en domain events ni en integration events. **Ninguna cadena de conexión, credencial ni clave JWT por defecto en el código**: IdentityService firma con RS256 usando una clave privada RSA cargada desde un archivo secreto; los servicios que validan tokens reciben únicamente la clave pública. Si la clave falta, es inválida o no cumple el tamaño mínimo de 2048 bits, el host falla al arrancar. Una implementación que imprime secretos (ej. entrega de invitaciones por log) solo se registra en `Development`.

## Pasos para crear un servicio
1. Crear los cuatro proyectos (`Api` con `dotnet new webapi`, el resto con `dotnet new classlib`), agregarlos a la solución y configurar las referencias según la regla de dependencias. Los proyectos de prueba se crean copiando el `.csproj` de otro proyecto de pruebas (evita los errores de versiones centralizadas) y agregándole su `ProjectReference`.
2. Borrar de cada `.csproj` lo que ya define `Directory.Build.props` (`TargetFramework`, `Nullable`, `ImplicitUsings`) y quitar `Class1.cs`.
3. Agregar paquetes con `dotnet add package`; la versión queda en `Directory.Packages.props`. No escribas versiones dentro del `.csproj`.
4. **Domain** con TDD: agregado, value objects, domain events y reglas, solo con pruebas unitarias.
5. **Application** con TDD: un caso de uso a la vez, con fakes escritos a mano.
6. **Infrastructure**: PostgreSQL propio en `docker-compose.yml`; EF Core con Npgsql y `snake_case`; mapeo y migración; columna `Version` (concurrencia); repositorio y unit of work. Después las **pruebas de integración** con Testcontainers, incluida la de concurrencia, que empieza en rojo real.
7. **Api**: endpoints con DTOs simples, `ApiExceptionHandler` central, health check (`/health`) y `public partial class Program;` al final de `Program.cs`.
8. Si publica eventos: **outbox** (Infrastructure traduce el domain event a un contrato y lo guarda en la misma transacción que el agregado; un proceso en segundo plano lo publica en RabbitMQ mediante `RabbitMQ.Client` y publisher confirms). El publicador declara el exchange, no las colas de los consumidores. Cada consumidor declara una cola durable propia enlazada al exchange, para recibir una copia de cada evento. Los consumidores son idempotentes: comparan `OccurredOn` para proyecciones donde gana el último valor, o persisten `EventId` para efectos no conmutativos. Tras reintentos limitados, los mensajes van a DLQ. Domain/Application no dependen de RabbitMQ.
9. Los flujos de integración entre varios componentes reales usan Testcontainers para PostgreSQL y RabbitMQ, y `WebApplicationFactory` para las APIs. Los valores de configuración que el host lee al arrancar se establecen en variables de entorno antes de construir la fábrica, no mediante `ConfigureAppConfiguration`.
10. `Dockerfile` multi-stage en el proyecto `Api` y entrada del servicio en `docker-compose.yml` con variables de entorno y `depends_on` con health checks.

## Reglas de código
- Validar toda entrada en el borde (endpoints), no confiar en el cliente.
- Un endpoint no contiene lógica de negocio: delega en un handler de `Application`.
- Transiciones de estado **solo** mediante métodos del agregado (ej. no se puede pasar a `DeliveredByTailor` desde `Received`).
- Migraciones EF versionadas en git; nunca editar una migración ya aplicada.
- Logs estructurados con Serilog; incluir el `SerialNumber` de la work order o el id de correlación en cada log relevante.
- No exponer entidades de dominio en la API; usar DTOs.
- Migraciones con `dotnet ef` instalado como herramienta local (`.config/dotnet-tools.json`). La cadena de conexión de diseño se lee de la variable de entorno `ConnectionStrings__<Name>Db` mediante una `IDesignTimeDbContextFactory`. Siempre se lee el código de la migración generada antes de aplicarla.
- Una cadena de conexión por servicio (`ConnectionStrings:<Name>Db`); los secretos van en user-secrets o variables de entorno.
- Un puerto externo de PostgreSQL distinto por servicio en `docker-compose.yml` (WorkOrder 5433, Tailor 5434, siguientes 5435...).

## Consideraciones de IdentityService
- IdentityService es un servicio propio en `src/Services/IdentityService` con PostgreSQL y ASP.NET Core Identity para cuentas, contraseñas y roles. La autenticación local emite JWT; OIDC externo queda para una etapa posterior.
- El inicio de sesión local con correo y contraseña es el flujo base. Google es un proveedor externo opcional que se puede incorporar después; no se requiere para crear una cuenta local.
- IdentityService nunca almacena contraseñas en texto plano ni crea algoritmos criptográficos propios. El rol admin se provisiona de forma controlada; un registro público no puede autoasignarse roles.
- No hay autorregistro público de sastres: el admin crea el perfil `Tailor` e invita el correo con rol `Tailor`. La invitación es de un solo uso y expira; al aceptarla, la persona configura una contraseña local. El flujo no puede asignar el rol admin.
- IdentityService vincula la cuenta con el ID de perfil `Tailor`, que es externo para IdentityService. TailorService solo guarda su perfil de negocio y no guarda credenciales.
- Gateway y APIs validan tokens. Las APIs también aplican autorización, incluso si el tráfico suele pasar por Gateway.
- La cuenta admin inicial se provisiona mediante configuración/secrets de despliegue, sin credenciales predeterminadas en código.
- Hay **una sola invitación por perfil de sastre**. Reemitir (`Reissue`) reemplaza el hash del token, el correo y la vigencia, así que el enlace anterior deja de funcionar y falla con el mismo mensaje genérico de un token incorrecto. No se puede reemitir una invitación ya aceptada. Si el correo falla tras guardar, el admin vuelve a invitar.
- La verificación del token de invitación vive en el agregado: `Accept(presentedHash, acceptedAt)` compara en tiempo constante y se hace antes que cualquier comprobación de estado, con un mensaje genérico, para no revelar si una invitación existe, expiró o ya se usó.
- Solo se persiste el hash del token. El token en claro viaja únicamente por el puerto `IInvitationDelivery`: nunca se guarda, se registra en logs ni se incluye en eventos.
- **Estado**: invitaciones, autenticación local, persistencia, roles y admin inicial, y endpoints API están implementados. El login inválido responde 401 genérico; cinco fallos bloquean la cuenta cinco minutos. Para correo inexistente o cuenta bloqueada se verifica un hash ficticio para reducir diferencias de tiempo. Las pruebas HTTP con `WebApplicationFactory` y PostgreSQL Testcontainers verifican el recorrido admin → invitación → aceptación → login de sastre y sus claims; las pruebas de Infrastructure cubren lockout y verificación del hash ficticio.
- Aceptar una invitación (`AcceptTailorInvitationHandler`): el orden es `Accept` (valida token, estado y expiración) → crear la cuenta → guardar. Nunca crear la cuenta antes de `Accept`. "La invitación no existe" y "el token es incorrecto" devuelven el mismo mensaje (`TailorInvitation.NotValidMessage`).
- El hash del token usa un algoritmo estándar de la plataforma (SHA-256 sobre un token aleatorio de alta entropía generado con `RandomNumberGenerator`) y se compara en tiempo constante (`CryptographicOperations.FixedTimeEquals`). Sin implementaciones propias.
- Infrastructure (pendiente): índice único sobre `tailor_profile_id` (una sola invitación por perfil), columna `Version` para concurrencia y un índice único sobre el correo y sobre el vínculo cuenta–perfil de sastre, para que dos aceptaciones simultáneas no creen dos cuentas.
- Infrastructure (pendiente): la creación de la cuenta (ASP.NET Core Identity) y la aceptación de la invitación se confirman en **una sola transacción** (mismo `DbContext`, sin autoguardado de `UserManager`), para no dejar una cuenta sin su invitación aceptada.
- Verificar con pruebas de integración que una contraseña débil deja la invitación **todavía válida** para reintentar (con el fake en memoria no se puede comprobar).

- **Seguridad pendiente (bloqueante antes de exponer el servicio)**: `IInvitationDelivery` real (correo) fuera de Development, con el arranque fallando si no hay ninguna registrada; alta de cuenta atómica (usuario, rol e invitación en una sola transacción, con una prueba de integración que inyecte un fallo en el segundo guardado); límite de tasa en el endpoint de aceptación.
- Un correo pertenece a un solo sastre: el índice único sobre `tailor_invitations.email` lo impone en base de datos.

## Antes de terminar un cambio
Verifica que compila (`dotnet build`), que las pruebas pasan (`dotnet test`) y que, si tocaste un contrato de evento, los servicios que lo consumen siguen siendo compatibles.
