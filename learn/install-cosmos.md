# Installing Cosmos

## Prerequisites

- Windows10/11 or Linux
- [.Net6](https://dotnet.microsoft.com/en-us/download/dotnet/6.0)
- [vcpp2010](https://www.microsoft.com/en-us/download/details.aspx?id=26999&msockid=19eaa39fc49d6d381175b70dc57c6c43)
- [VisualStudio2022 (Windows only)](https://visualstudio.microsoft.com/downloads/)

## Installing the workloads (you do not need VS2022 if you are on Linux)

Cosmos requires a workload for Visual Studio to work. To install it, select the Extensions Development workload in the Visual Studio installer before installing Visual Studio 2022, or if you already have it installed, you can modify the install and add the workload.

![image](https://samma2009.github.io/blog/workloads.PNG)

## Downloading the devkit

If you have Git installed, run this command in an empty folder:

```bash
git clone https://github.com/CosmosOS/Cosmos.git
```

Else, you can go to the [CosmosOS](https://github.com/CosmosOS/Cosmos.git) repo and clone it manually. Remember to put it in an empty folder.

After cloning the repo, you need to rename the Cosmos-master folder to Cosmos.

## Building the devkit

### Windows

Run the install-VS2022.bat file with administrator privileges and make sure to have Visual Studio closed.

### Linux

In the makefile change line 19 from:

```makefile
BUILDMODE=Release
```

to:

```makefile
BUILDMODE=Debug
```

Open a terminal in the Cosmos folder and run the make command. You might want to also install the Cosmos template. To install it, run:

```bash
dotnet new install source/templates/csharp
```

Optionally, you can install [CosmosCLI](https://github.com/PratyushKing/CosmosCLI) for better project management on Linux.
