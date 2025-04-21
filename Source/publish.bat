dotnet publish LuxRunner/Luxrunner.csproj -c Release -r linux-x64 --self-contained true -o ./luxbuild
copy main.py .\luxbuild\main.py
copy .\LuxRunner\*.json .\luxbuild\*.json
tar -cvzf .\release.tar.gz -C luxbuild * 
pause