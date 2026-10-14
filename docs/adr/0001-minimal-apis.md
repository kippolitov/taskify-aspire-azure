# Minimal APIs replace controllers

The API's endpoints are Minimal APIs, one static class per resource, instead of MVC controllers. Request records carry DataAnnotations that `AddValidation()` enforces before a handler runs; handlers return `TypedResults`; and services return a `Result<T>` that a single `ToHttpResult()` mapping turns into a response. Validation, the Problem Details error format and the OpenAPI response shapes therefore come from the framework, instead of being written by hand in every action as they were in the controllers (D1 in `docs/hardening-modernization-plan.md`).

## Consequences

- The request records live in `Taskify.Shared`, outside the assembly that calls `AddValidation()`. `Taskify.Shared` registers itself with `AddTaskifyContractValidation()`, because the validation source generator documents that it only covers the calling assembly (research R6). On .NET SDK 10.0.301 the API's own registration already covers the records, because they appear as endpoint parameters; the extra registration keeps validation working if a shared type is ever validated outside an endpoint signature. A 400 test for every operation that takes input guards against validation silently not running.
- Error responses that `ProblemHttpResult` cannot describe are declared per endpoint with `.ProducesProblem(...)` and `.ProducesValidationProblem()`. A contract test fails if an operation lists no error response.
- A body the API cannot read returns 400 in every environment (`ThrowOnBadRequest = false`); `UseStatusCodePages()` writes it as Problem Details.
