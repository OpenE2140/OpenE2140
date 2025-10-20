To run OpenE2140, several files are needed from the original game disks. Read instructions on the [How To Install](https://opene2140.net/how-to-install/) page on the OpenE2140 official website.

The following lists per-platform dependencies required to build from source.

Windows
=======

Compiling OpenE2140 requires the following dependencies:
* [Windows PowerShell >= 4.0](https://microsoft.com/powershell) (included by default in recent Windows 10 versions)
* [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) (or via Visual Studio)

To compile OpenE2140, open the `OpenE2140.sln` solution in the main folder, build it from the command-line with `dotnet` or use the Makefile analogue command `make all` scripted in PowerShell syntax.

Run the game with `launch-game.cmd`.

Linux
=====

.NET 8 SDK is required to compile OpenE2140. The [.NET 8 download page](https://dotnet.microsoft.com/download/dotnet/8.0) provides repositories for various package managers and binary releases for several architectures.

To compile OpenE2140, run `make` from the command line. After this one can run the game with `./launch-game.sh`.

The default behaviour on the x86_64 architecture is to download several pre-compiled native libraries using the NuGet packaging manager. If you prefer to use system libraries, compile instead using `make TARGETPLATFORM=unix-generic`.

If you choose to use system libraries, or your system is not x86_64, you will need to install [SDL 2](https://www.libsdl.org/download-2.0.php), [FreeType](https://gnuwin32.sourceforge.net/packages/freetype.htm), [OpenAL](https://openal-soft.org/), and [liblua 5.1](https://luabinaries.sourceforge.net/download.html) before compiling OpenE2140.

These can be installed using your package manager on various distros:

<details>
<summary>Arch Linux</summary>

```
sudo pacman -S openal libgl freetype2 sdl2 lua51
```
</details>

<details>
<summary>Debian/Ubuntu</summary>

```
sudo apt install libfreetype6 libopenal1 liblua5.1-0 libsdl2-2.0-0
```
</details>

<details>
<summary>Fedora</summary>

```
sudo dnf install SDL2 freetype "lua = 5.1" openal-soft
```
</details>

<details>
<summary>Gentoo</summary>

```
sudo emerge -av media-libs/freetype:2 media-libs/libsdl2 media-libs/openal virtual/opengl '=dev-lang/lua-5.1.5*'
```
</details>

<details>
<summary>Mageia</summary>

```
sudo dnf install SDL2 freetype "lib*lua5.1" "lib*freetype2" "lib*sdl2.0_0" openal-soft
```
</details>

<details>
<summary>openSUSE</summary>

```
sudo zypper in openal-soft freetype2 SDL2 lua51
```
</details>

<details>
<summary>Red Hat Enterprise Linux (and rebuilds, e.g. CentOS)</summary>
The EPEL repository is required in order for the following command to run properly.

```
sudo yum install SDL2 freetype "lua = 5.1" openal-soft
```
</details>

macOS
=====

[.NET 8 SDK for macOS](https://dotnet.microsoft.com/download/dotnet/8.0) is required to compile OpenE2140.

To compile OpenE2140, run `make` from the command line. Run with `./launch-game.sh`.
