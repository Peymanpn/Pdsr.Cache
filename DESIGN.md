# Design Guidelines

- Do not name methods starting with a `Try` prefix. Particularly in the asynchronous world, ["Try" is ambiguous](https://github.com/StephenCleary/AsyncEx/issues/141#issuecomment-409287646); does it mean "attempt this synchronously" or "attempt this without exceptions"?
  - `TaskCompletionSourceExtensions.TryCompleteFromCompletedTask` is an exception to this rule; it uses the `Try` prefix to match the existing `TaskCompletionSource<T>` API.
- Try to structure APIs to guide users into success rather than failure.
  - E.g., coordination primitives should avoid exposing their current state via properties because those properties would encourage code with race conditions.
- Do not use strong naming. Currently, adding strong naming creates too much of a maintenance burden; if Microsoft releases better strongname tooling, then this guideline can be reconsidered.
