# WebServer
.net core Web Server

create test host:
dotnet new console -o Tester

create web server:
dotnet new classlib -o WebServer

Add reference:
open folder WebServer/Tester
dotnet add reference ../WebServer/WebServer.csproj
open folder WebServer

F5 Debug

in program.cs:
var configuration = new Configuration();
click bulb: add Reference using WebServer



