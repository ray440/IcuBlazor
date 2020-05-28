::set SRV=Server.CSBLinked
set SRV=Server.SSBLinked
::set SRV=SSB

cd %SRV%
taskkill /IM %SRV%.exe /F
dotnet run
