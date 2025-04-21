dotnet publish lux_runner/Luxrunner.csproj -c Release -r linux-x64 --self-contained true -o ./luxbuild
copy main.py .\luxbuild\main.py
copy .\LuxRunner\*.cs .\luxbuild\*.cs
copy .\LuxRunner\*.json .\luxbuild\*.json
copy .\LuxNN\*.py .\luxbuild\*.py
copy .\LuxNN\*.ipynb .\luxbuild\*.ipynb
tar -cvzf .\release.tar.gz -C luxbuild * 
pause