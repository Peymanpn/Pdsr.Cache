# Contributing to Pdsr Cache Helper Library

Thanks for taking the time to contribute! :+1:

If you have encountered a bug (or just have a question on how to use the library), feel free to open an issue immediately. If reporting a bug, please include the version of cache library you are using, your runtime platform and version, and code to reproduce the problem.

If you have an idea for code, or would like to contribute some code, please open an issue for discussion. Finally, if the idea has been discussed and sounds like a good fit for the library, feel free to open a pull request and if all went well, the PR will be merged.

Our code of conduct is: be nice. Just be nice. :blush:

Thanks again for contributing! :+1:

## Releasing

Packages are published to NuGet only when a version tag is pushed.

1. Set `<Version>` in `src/Directory.Build.props` (e.g. `4.0.0` or `4.0.0-beta.3`) in a PR and merge it.
2. Tag the merged commit with the same version and push the tag:
   ```
   git tag v4.0.0
   git push origin v4.0.0
   ```
3. CI builds, tests and publishes. It stops without publishing if the tag doesn't match `<Version>`.

A version with a suffix such as `-beta.3` is published as a NuGet prerelease.
