# Ephemeral command state

No database entities or migrations. A per-run command owner captures initializer resource and run ID. State is Ready, Busy or Terminal. Ready becomes Busy atomically before interaction. Before mutation, cancellation/invalid input returns to Ready. Starting any mutation makes the attempt terminal on all outcomes. Concurrent/repeated calls never invoke a second sequence. Prompt values and bootstrap bytes are request-local; disposal clears references/buffers without claiming managed-memory erasure.
