---
name: sastreria-dominio
description: Reglas de negocio del sistema de sastrería (boletas, garantías, pagos, asignación a sastres y liquidación del 35%). Úsala SIEMPRE que se escriba, revise o diseñe código, entidades, eventos, estados, endpoints, pruebas o pantallas relacionadas con boletas, seriales, garantías, precios, pagos del cliente, sastres o liquidaciones, aunque el usuario no mencione "reglas de negocio". También cuando haya dudas de qué debe pasar en un caso borde (boleta sin precio, garantía, entrega después del cierre).
---

# Dominio: sastrería

Sistema para gestionar boletas de arreglos de ropa y liquidar a los sastres. Hoy el proceso es manual (cuaderno); el sistema lo reemplaza. Cuando una regla no esté aquí, NO la inventes: pregunta al usuario y, si se confirma, agrégala a este archivo.

## Actores
- **Recepción**: recibe la prenda, crea la boleta, define/cobra el precio, entrega la boleta al sastre, registra cuando el sastre devuelve la prenda, ejecuta el cierre de liquidación y paga al sastre.
- **Sastre**: hace el trabajo. Hoy anota sus entregas en un cuaderno; el sistema lo sustituye (recepción registra la entrega).
- **Cliente**: deja y retira la prenda, paga.
- **Admin**: configura catálogos y porcentaje.

## Glosario
- **Boleta**: documento de recepción de una prenda. Tiene **serial** único, cliente, fecha, prenda y trabajos a realizar, valor (puede ser nulo) y estado.
- **Garantía**: prenda que vuelve por un trabajo mal hecho o un reclamo. Ver la sección *Garantías*.
- **Liquidación**: pago al sastre por lo que entregó terminado durante un periodo.

## Vocabulario del código (inglés)
El negocio se explica en español, pero **todo identificador en código, BD, API y eventos va en inglés**. Usa siempre esta equivalencia y no inventes sinónimos:

| Negocio (español) | Código (inglés) |
|---|---|
| Boleta | `WorkOrder` |
| Serial de la boleta | `SerialNumber` |
| Garantía | `Warranty` (una `WorkOrder` con `Type = Warranty`, enlazada por `OriginalWorkOrderId`) |
| Prenda | `Garment` |
| Trabajo / arreglo | `AlterationJob` |
| Cliente | `Customer` (en la work order: `CustomerName` y `CustomerPhone`) |
| Sastre | `Tailor` |
| Recepción / recepcionista | `Receptionist` |
| Pago | `Payment` |
| Precio / valor | `Price` |
| Liquidación | `Settlement` |
| Periodo de liquidación | `SettlementPeriod` |
| Línea de liquidación | `SettlementLine` |
| Cerrar liquidación | `CloseSettlement` |

## Agregados previstos (DDD)
| Servicio | Agregado raíz | Contiene |
|---|---|---|
| WorkOrderService | `WorkOrder` | `AlterationJob`, `Payment` (abonos), estados de trabajo y de pago |
| TailorService | `Tailor` | datos del sastre y su disponibilidad |
| SettlementService | `Settlement` | `SettlementLine`, periodo, porcentaje usado |

Value objects: `Money`, `SerialNumber`, `PhoneNumber`, `Percentage`.

## Dos carriles independientes
Una boleta avanza por dos máquinas de estado separadas. Modelarlas por separado evita estados imposibles, porque el pago del cliente no depende del trabajo del sastre.

**Carril de trabajo**
`Received → Assigned → InProgress → DeliveredByTailor → DeliveredToCustomer`
(enum `WorkStatus`)

**Carril de precio/pago**
`Unpriced → Priced → PartiallyPaid → Paid`
(enum `PaymentStatus`; las garantías usan `NoCharge`, que significa "sin cobro")

Ejemplos válidos: entregada por el sastre y aún sin pagar; pagada al recibir pero todavía en trabajo.

## Datos del cliente
- Se guardan nombre y **celular** (`PhoneNumber`): se necesita para avisar que la prenda está lista.
- Regla provisional del celular: se guarda solo con dígitos, 10 dígitos empezando por 3 (celular colombiano), aceptando prefijo `+57`/`57` y separadores (espacios, guiones, paréntesis).

## Fechas de entrega
- La work order guarda `DeliveredByTailorAt` y `DeliveredToCustomerAt`. La primera es la **marca de tiempo que usa la liquidación** para decidir en qué periodo cae la boleta.
- Las fechas entran por parámetro; el dominio nunca llama al reloj del sistema.
- El sastre puede entregar desde `Assigned` o `InProgress`: `InProgress` es opcional, porque en la práctica recepción no siempre registra que el sastre empezó.

## Garantías
Problemas reales que el sistema debe mitigar: (1) hoy, si encuentran la boleta original la reutilizan y si no, crean otra que dice "garantía" y sin precio; (2) quien trae la garantía suele ser **otra persona**, y se pierde la trazabilidad de quién dejó la prenda originalmente; (3) la garantía se le da a **cualquier sastre**, lo que genera malestar entre ellos.

Reglas:
- **Siempre se crea una work order nueva de tipo `Warranty`** (serial propio, ciclo propio), enlazada a la original por `OriginalWorkOrderId`. La original **no se reabre**: queda cerrada y ya liquidada, así no se paga dos veces y su historial no se altera. (En papel hoy se reutiliza la misma boleta; en el sistema eso se reemplaza por el enlace.)
- Recepción debe poder **buscar la original** por serial, nombre, celular o descripción de la prenda (consulta pendiente en Application).
- Si no se encuentra la original, se crea la garantía **sin enlace** (`IsLinkedToOriginal = false`). Queda marcada como sin trazabilidad para poder revisarla.
- En una garantía, `CustomerName`/`CustomerPhone` son los de **quien la trae** (a quien se avisa). El dueño original se consulta por el enlace; no se copia ni se pierde.
- **Sin cobro**: `PaymentStatus = NoCharge`. No admite `SetPrice` ni `RegisterPayment`, se entrega sin pago y **nunca cuenta en la liquidación**.
- **Asignación justa**: la garantía debe ir al **sastre original** (`OriginalTailorId`, copiado de la work order original al crearla). Asignarla a otro sastre exige un **motivo** (`ReassignmentReason`) que queda registrado. En una garantía sin enlace no hay sastre original, y no se exige motivo.
- Objetivo: que quien cometió el error responda por él, y que reasignar sea una decisión visible y no arbitraria. Más adelante: reporte de garantías por sastre.

## Reglas de precio y pago
1. El pago puede ocurrir al **dejar** la prenda, al **retirarla**, o cuando se defina el precio.
2. Una boleta puede crearse **sin precio**; el precio se define después según el trabajo.
3. Una boleta puede tener **cero, uno o varios pagos** (los pagos son registros aparte, no un campo de la boleta).
4. **La prenda solo se entrega al cliente cuando está totalmente pagada** (`PaymentStatus = Paid`), **excepto las garantías** (`NoCharge`). `DeliverToCustomer` debe verificarlo.
5. **Existen pagos parciales (abonos)**: el cliente deja una parte y termina de pagar al reclamar la prenda. No siempre ocurre; a veces paga todo de una vez.
6. Mientras el sastre entrega la prenda a recepción, la boleta puede seguir sin pagar (el pago no bloquea ese paso, solo bloquea la entrega al cliente).
7. **Decidido**: los pagos son entidades dentro de `WorkOrder` (no hay `PaymentService` por ahora). Un abono no puede superar el saldo pendiente, y el precio **no se puede cambiar** una vez hay abonos.
8. Los pagos son idempotentes: registrar dos veces el mismo pago (reintento, doble clic) no debe duplicarlo.

## Serial
- **El sistema asigna el serial** al crear la work order, para evitar errores humanos de digitación y duplicados (decisión del usuario, aunque hoy el negocio usa talonarios preimpresos).
- Es único y se genera en **PostgreSQL con una secuencia** (`nextval`), NO en Redis. Es dato de negocio crítico.
- En el dominio es el value object `SerialNumber`, un entero positivo. El dominio **no lo genera**: lo recibe ya asignado. Application obtiene el siguiente valor mediante un puerto (`ISerialNumberGenerator`, definido en Domain, implementado en Infrastructure con la secuencia) y se lo pasa a `WorkOrder.Create(...)`.
- Una garantía es una work order nueva: recibe **su propio serial** y se enlaza a la original por `OriginalWorkOrderId`.
- Las secuencias de PostgreSQL garantizan unicidad pero **pueden dejar huecos** (por ejemplo, si una transacción se revierte). Si el negocio exige numeración sin saltos, habría que cambiar de estrategia.

## Liquidación al sastre
- El sastre recibe **35 % del valor** de sus boletas elegibles. El porcentaje es **configurable** (hoy 35) y cada línea de liquidación **guarda el porcentaje usado**.
- **No hay semanas ni fechas fijas.** Hay un **periodo abierto** que se cierra **manualmente** ("Cerrar liquidación"). Lo habitual es cerrar el sábado hacia el mediodía, pero el sistema no depende de eso.
- Mientras el periodo esté abierto, toda boleta elegible entra en él (aunque se registre el mismo sábado antes de cerrar).
- Lo registrado **después** del instante del cierre cae en el periodo siguiente (ejemplo: cierre 12:00, entrega 13:00 → siguiente liquidación).
- Al cerrar se abre un periodo nuevo.

### Una boleta es elegible si cumple las tres
1. `WorkStatus = DeliveredByTailor`.
2. **No** es garantía.
3. **No** ha sido liquidada antes (evita pagar dos veces).

### Casos borde
| Caso | Resultado |
|---|---|
| Entrega después del cierre | Siguiente liquidación |
| Entrega antes del cierre, mismo día | Liquidación actual |
| Dos cierres simultáneos | Solo uno procede (bloqueo distribuido en Redis) |
| Entrega justo durante el cierre | Cuenta si su marca de tiempo es anterior a la del cierre |
| Entregada sin precio al cerrar | No se liquida; queda pendiente hasta tener precio (propuesta, ver pendientes) |
| Garantía entregada | Se registra pero aporta 0 a la liquidación |

- Una liquidación cerrada es **inmutable** (snapshot): líneas, valores, porcentaje y marca de cierre no se editan. Las correcciones se hacen con ajustes en la liquidación siguiente.

## Pendientes de confirmar con el usuario
- Medio de pago (efectivo, transferencia...): ¿hay que registrarlo?
- ¿Cómo se relacionan el número impreso en el talonario físico y el serial del sistema? (¿el sistema imprime la boleta/etiqueta, o se guarda el número del papel como referencia opcional `PaperReference`?). Sin esto, el cliente y el sastre podrían ver un número distinto al del sistema.
- ¿Es aceptable que haya huecos en la numeración del serial?
- ¿Una boleta entregada sin precio se paga cuando se define el precio (propuesta actual)?
- Redondeo del 35 % (a pesos enteros o decimales) y de qué valor se calcula si hubo descuentos o pagos parciales.
- Garantías: si otro sastre atiende una garantía que no causó, ¿se le compensa de alguna forma, o su trabajo sigue sin pago?
- Garantías: ¿solo se pueden abrir sobre boletas ya entregadas al cliente? (lo verificaría Application, no el dominio).
- Garantías: ¿qué pasa si al revisarla resulta que NO está cubierta? (¿se convierte en trabajo con precio?).
- ¿Una garantía sobre una garantía se enlaza a la original de la cadena?
- ¿El sastre puede devolver una prenda sin terminar (reasignación)?