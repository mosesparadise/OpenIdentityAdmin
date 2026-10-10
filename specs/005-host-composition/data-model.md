# Data and lifetime model

No persistent entity, schema, migration or protocol DTO changes.

Auth startup context owns command arguments/mode and loaded IssuerCredentials, and remembers pilot enablement. It is an ordinary class with no secret serialization. Lifetime: context created → existing database registration → protection material load → pilot validation/registration → application built → protection verified → initialize once or configure/run HTTP → application disposed → credentials disposed. Failure before application construction still disposes loaded material. Failed material registration already cleans up inside the existing protection adapter.

Migrator's configuration carries only its existing optional LocalRuntimeCredentials into the one-shot upgrade. Aspire typed graph results carry database, connection expression and project resource references; connection/password values stay in Aspire parameter/reference objects until the existing environment resolution boundary.
