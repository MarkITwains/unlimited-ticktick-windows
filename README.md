# Unlimited TickTick - Windows

> [!TIP]
> You can also find a MacOS version of this patch in the [Unlimited TickTick - MacOS ](https://github.com/yazdipour/unlimited-ticktick-macos) repository.

> [!TIP]
> If you want to learn how you can do the same for Linux🐧/Electron version of the ticktick [follow instructions by the community.](https://github.com/yazdipour/unlimited-ticktick-windows/issues/12)

### How to use? 

- Upgrade or Install the original TickTick (Chinese Ticktick/dida365 will not work - personally never tried it).
- Close the app from System tray completely.
- Copy the exe file inside installed path (usually `C:\Program Files (x86)\TickTick`). Have a backup from the original exe file just in case.

> [!CAUTION]
> Want the new version or Just got the update notification? First update the app from the app itself, then replace the patched exe file with the new one. This way you can keep enjoying the pro features without losing them after each update.

### Download the patched executable for Windows:

You can also download the final patched executable from [the releases page.](https://github.com/yazdipour/cracked-ticktick-windows/releases)

---

### Cloud Build (GitHub Actions)

If you don't have a local .NET development environment set up, or just prefer to build the patched app in the cloud, you can use GitHub Actions to generate your own patched executable.

1. **Fork this repository** using the fork button on the top right.
2. **Put your original `TickTick.exe` in the repository root and commit it**
    - **⚠️ Use the installed exe from the program files folder, not the installer** (usually `C:\Program Files (x86)\TickTick\TickTick.exe`)
    - **⚠️ The file must be under 100 MB (GitHub's limit for files committed to a repo)**
3. Go to the **Actions** tab on your newly forked repository. If prompted, click the button to enable workflows.
4. On the left sidebar under "All workflows", click on **Build Patched TickTick**.
5. Click the **Run workflow** button and wait for the build to finish.
6. Go to the **Releases** section on the right side of your repository's main page. You will find a new **Draft release** containing your `TickTick_Patched.zip` file ready to download.
7. Download the ZIP, extract the `TickTick_Patched.exe` file, and replace your original `TickTick.exe` located in your TickTick installation folder (usually `C:\Program Files (x86)\TickTick`).

---

### Features

- Calendar view
- Widgets
- Reminders
- Themes
- ⚠️ Unlimited Habits (Might not sync properly)
- ⚠️ **Some features might not work if it is restricted on server side**
## Patch with TTPatcher Script

### Using .NET (Direct)
```bash
# Build and run
dotnet run -- "D:\TickTick.exe"

# Or build then run
dotnet build
dotnet bin/Debug/net8.0/TTPatcher.dll "D:\TickTick.exe"
```

### Using Docker

#### Build
```bash
docker build -t ttpatcher .
```

#### Patch
```bash
docker run --rm -v "/path/to/directory:/data" ttpatcher "/data/TickTick.exe"
```

#### Examples

```bash
# Build image
docker build -t ttpatcher .

# Patch file (Windows)
# This should output to C:/Users/username/Desktop/TickTick_Patched.exe
docker run --rm -v "C:/Users/username/Desktop:/data" ttpatcher "/data/TickTick.exe"

# Patch file (WSL/Linux)
# This should output to /mnt/c/Users/username/Desktop/TickTick_Patched.exe
docker run --rm -v "/mnt/c/Users/username/Desktop:/data" ttpatcher "/data/TickTick.exe" 
```

### Learn how it was made

- Use dnSpy
- Update these:

<details>
<summary>Approach 1: UserModel Property Modification</summary>

```csharp
// in ticktick_WPF.Models.UserModel
proEndDate=>DateTime.MaxValue;
pro=>true;
```
</details>

<details>
<summary>Approach 2: LocalSettings and UserDao Modification</summary>

```csharp
// in ticktick_WPF.Resource.LocalSettings
public bool IsPro
{
  get
  {
    return this.SettingsModel.IsPro;
  }
  set
  {
    this.SettingsModel.IsPro = true; //force it to true
    this.OnPropertyChanged("IsPro");
  }
}
```
</details>

## Disclaimer

This repository is provided for informational and educational purposes only.
