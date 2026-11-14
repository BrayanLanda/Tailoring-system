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
- **Garantía**: boleta que vuelve a entrar por un trabajo mal hecho o reclamo. Se registra y se asigna a un sastre como cualquier boleta, pero **nunca cuenta para la liquidación**. Se enlaza a la boleta original.
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
| Cliente | `Customer` |
| Sastre | `Tailor` |
| Recepción / recepcionista | `Receptionist` |
| Pago | `Payment` |
| Precio / valor | `Price` |
| Liquidación | `Settlement` |
| Periodo de liquidación | `SettlementPeriod` |
| Línea de liquidación | `SettlementLine` |
| Cerrar liquidación | `CloseSettlement` |

## Dos carriles independientes
Una boleta avanza por dos máquinas de estado separadas. Modelarlas por separado evita estados imposibles, porque el pago del cliente no depende del trabajo del sastre.

**Carril de trabajo**
`Received → Assigned → InProgress → DeliveredByTailor → DeliveredToCustomer`
(enum `WorkStatus`)

**Carril de precio/pago**
`Unpriced → Priced → PartiallyPaid → Paid`
(enum `PaymentStatus`)

Ejemplos válidos: entregada por el sastre y aún sin pagar; pagada al recibir pero todavía en trabajo.

## Reglas de precio y pago
1. El pago puede ocurrir al **dejar** la prenda, al **retirarla**, o cuando se defina el precio.
2. Una boleta puede crearse **sin precio**; el precio se define después según el trabajo.
3. Una boleta puede tener **cero, uno o varios pagos** (los pagos son registros aparte, no un campo de la boleta).
4. Los pagos son idempotentes: registrar dos veces el mismo pago (reintento, doble clic) no debe duplicarlo.

## Serial
- Único, **nunca se repite ni se pierde**. Se genera en PostgreSQL con una secuencia, NO en Redis (es dato de negocio crítico).

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
- ¿Una boleta entregada sin precio se paga cuando se define el precio (propuesta actual)?
- Redondeo del 35 % (a pesos enteros o decimales) y de qué valor se calcula si hubo descuentos o pagos parciales.
- ¿Una garantía puede asignarse a un sastre distinto del original?
- ¿El sastre puede devolver una prenda sin terminar (reasignación)?