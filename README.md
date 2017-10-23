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

SocketError on Ubuntu: permission denied:
sudo apt-get install libcap2-bin
sudo setcap cap_net_bind_service=+ep /usr/share/dotnet/dotnet
Debugging not possible with port 80

Debugging: port 20000
sudo setcap -r /usr/share/dotnet/dotnet

