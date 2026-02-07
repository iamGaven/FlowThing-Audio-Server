# Building the Audio Server

To build the C# audio server as a single executable:
```bash
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:DebugType=None -o ./publish/windows```
```
The compiled executable will be located in `./publish/windows`

Once built:
- If you have already built the FlowThing app, place the `audio.exe` file in the `client` folder
- If you have not already built the FlowThing app, place the `audio.exe` in the `public` folder, then build the FlowThing app