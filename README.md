# Aetheros oneM2M .NET SDK

## Installation

* Install .NET SDK 6

  **Centos 7**
  ```
  sudo rpm -Uvh https://packages.microsoft.com/config/centos/7/packages-microsoft-prod.rpm
  sudo yum install -y dotnet-sdk-6.0
  ```

  **Ubuntu**
  ```
  sudo apt-get update; \
    sudo apt-get install -y apt-transport-https && \
    sudo apt-get update && \
    sudo apt-get install -y dotnet-sdk-6.0
  ```

* Post-install

  ```
  eval `echo export DOTNET_CLI_TELEMETRY_OPTOUT=1 | tee -a ~/.bashrc`
  dotnet --list-sdks
  ```

* Install `nodejs` (only required for web example)

  ```
  curl -o- https://raw.githubusercontent.com/nvm-sh/nvm/v0.39.0/install.sh | bash
  . $HOME/.nvm/nvm.sh
  nvm install node
  node --version
  npm --version
  ```


## Running the Demo Web App

```
cd Example/Example.Web
vi appsettings.json
dotnet run
```


## Running the command-line tool

```
cd Aetheros.OneM2M.Tool
dotnet run
dotnet run RegisterAE --help
```