sudo pkill -9 dotnet 2>/dev/null; \
dotnet build-server shutdown; \
dotnet nuget locals all --clear; \
find . -type d \( -name "bin" -o -name "obj" \) -exec rm -rf {} +; \
dotnet dev-certs https --clean; \
sudo killall trustd 2>/dev/null; \
dotnet dev-certs https --trust; \
dotnet restore