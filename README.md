# Lance
Language Appliance for Numerical Control code of the sinumerik one cnc control system by siemens

For a description of the Extension see [lance-extension/README.md](lance-extension/README.md).

For a changelog see [lance-extension/CHANGELOG.md](lance-extension/CHANGELOG.md).

## How to install
You can simply install the extension via the Visual Studio Code marketplace by searching for the extension lance.

Or you can build it yourself:

- Install the .NET 10 SDK and Node.js.
- Run `lance-extension\build_release_server.bat` to publish the self-contained Windows x64 language server.
- Run `npm ci` and `npm run compile` in `lance-extension`.
- Package the extension with `npx @vscode/vsce package`.

The generated VSIX contains the .NET runtime required by the language server, so extension users do not need to install .NET separately.
