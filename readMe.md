# Building the Audio Server

To build the C# audio server as a single executable:
```bash
dotnet publish -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true
```

The compiled executable will be located in `bin/Release/net[version]/win-x64/publish/`

Once built, place the executable in the `client` folder of the FlowThing app.