Aetheros oneM2M .NET SDK
========================

Installation
------------

**Centos 7**

- Install .NET SDK 3.1:

```
sudo rpm -Uvh https://packages.microsoft.com/config/centos/7/packages-microsoft-prod.rpm
sudo yum install dotnet-sdk-3.1
eval `echo export DOTNET_CLI_TELEMETRY_OPTOUT=1 | tee -a ~/.bashrc`
dotnet --list-sdks
```

- Install `nodejs`:

```
curl -o- https://raw.githubusercontent.com/nvm-sh/nvm/v0.35.3/install.sh | bash
. $HOME/.nvm/nvm.sh
nvm install node
node --version
npm --version
```



Running the Demo Web App
------------------------

```
cd Example/Example.Web
vi appsettings.json
dotnet run --verbosity minimal
```