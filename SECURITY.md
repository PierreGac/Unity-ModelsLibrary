# Security Policy

Models Library (`com.models-library`) is an Editor-only Unity package. The supported release is the version in `package.json`. That version is currently **1.0.12**. Older versions are not supported.

## Reporting a vulnerability

Report a suspected vulnerability through GitHub private security advisories:

https://github.com/PierreGac/Unity-ModelsLibrary/security/advisories/new

Do not open a public issue for an unfixed vulnerability. Include the package version, the Unity Editor version, and the steps that show the problem.

The maintainer will confirm the report and say whether it is accepted. Please wait for a fix, or for a decline, before publishing the details.

How-to questions and ordinary bugs belong on the public issue tracker:

https://github.com/PierreGac/Unity-ModelsLibrary/issues

## Scope

Reports are in scope when they concern this package: path handling, import of repository files, settings, or the Editor tools that read and write a model repository.

The package does not operate a hosted model server. A repository you host, including an experimental HTTP server, is your system. Editor roles are not authorization.
