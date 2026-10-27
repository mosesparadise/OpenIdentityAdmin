# Pinned API research

Independent Aspire/security review inspected Aspire13.6.1 commit `3751e615ad660621f2a7aa4c390ef48e08a0eed8` and package XML. `WithCommand`, `IInteractionService.PromptInputsAsync`, `InteractionInput` with SecretText, MaxLength and Required, ResourceNotificationService and ResourceCommandService are supported. Command visibility/update state is presentation, not authorization. Fresh callback-local inputs avoid persisted command metadata.

`WithProcessCommand` supports stdin but streams output/errors to resource debug logs, exposes exception-derived failures and has no per-process deadline. Use a small bounded process adapter for the existing stdin boundary. Native DCP graph disposal already removes owned anonymous volumes; no global cleanup wrapper is needed. Keep fixed canonical issuer/BFF addresses, Linux credential guards and ordinary TLS validation.
