# Quick Guide: Publishing an Update

## 📋 Pre-Release Checklist

- [ ] All changes are committed
- [ ] Code compiles without errors
- [ ] Tests are passing (if applicable)
- [ ] CHANGELOG.md is updated
- [ ] Version has been incremented

## 🚀 Quick Method (Recommended)

### Single step:
```bash
./release.sh 0.2.3
```

The script handles everything! Just follow the on-screen instructions.

---

## 📝 Manual Method (if the script doesn't work)

### 1. Update version
Edit `KaSe Controller/KaSe Controller.csproj`:
```xml
<Version>0.2.3</Version>
<AssemblyVersion>0.2.3.0</AssemblyVersion>
<FileVersion>0.2.3.0</FileVersion>
```

### 2. Compile
```bash
# Linux
dotnet publish -c Release -r linux-x64 --self-contained true

# Windows
dotnet publish -c Release -r win-x64 --self-contained true
```

### 3. Create archives
```bash
# Linux
cd "KaSe Controller/bin/Release/net10.0/linux-x64/publish"
zip -r KaSe_SOFT_linux-x64.zip .

# Windows
cd "KaSe Controller/bin/Release/net10.0/win-x64/publish"
zip -r KaSe_SOFT_win-x64.zip .
```

### 4. Git
```bash
git add .
git commit -m "Release v0.2.3"
git tag -a v0.2.3 -m "Version 0.2.3"
git push origin master
git push origin v0.2.3
```

### 5. GitHub Release
1. Go to https://github.com/mornepousse/KaSe_SOFT/releases
2. **"Draft a new release"**
3. **Tag**: Select `v0.2.3`
4. **Title**: `KaSe Controller v0.2.3`
5. **Description**: 
   ```markdown
   ## 🎉 New Features
   - Feature 1
   - Feature 2
   
   ## 🐛 Bug Fixes
   - Bug 1
   - Bug 2
   
   ## 📦 Installation
   Download the archive for your OS and extract it.
   ```
6. **Files**: Drag and drop
   - `KaSe_SOFT_linux-x64.zip`
   - `KaSe_SOFT_win-x64.zip`
7. ✅ **"Set as the latest release"**
8. **"Publish release"**

---

## ✅ Verification

After publishing:
- [ ] Release is visible on GitHub
- [ ] Both ZIP files are downloadable
- [ ] Application detects the update (test in Updates tab)

---

## 🆘 Common Issues

### Script doesn't compile
```bash
# Check that .NET 10 is installed
dotnet --version

# Clean and rebuild
dotnet clean
dotnet restore
dotnet build
```

### Archives are too large
This is normal! Self-contained builds include the .NET runtime.
- Linux: ~80-100 MB
- Windows: ~80-100 MB

### Tag already exists
```bash
# Delete local and remote tag
git tag -d v0.2.3
git push origin :refs/tags/v0.2.3
```

### UpdateManager doesn't detect the release
Check:
- ✅ Tag starts with `v`: `v0.2.3`
- ✅ Files contain `linux-x64` or `win-x64` in the name
- ✅ Release is marked as "latest"
- ✅ Release is not in "draft" or "pre-release"

---

## 📚 Semantic Versioning

**Format**: `MAJOR.MINOR.PATCH`

- **MAJOR** (1.0.0): Incompatible changes
- **MINOR** (0.2.0): New compatible features
- **PATCH** (0.2.1): Bug fixes

**Examples**:
- `0.2.1` → `0.2.2`: Bug fix
- `0.2.2` → `0.3.0`: New feature
- `0.3.0` → `1.0.0`: Stable version or major change

---

## 🎯 Release Notes Template

```markdown
## 🎉 New Features
- [Feature] Feature description
- [UI] User interface improvement

## 🐛 Bug Fixes
- Fixed bug that prevented X
- Resolved issue Y

## ⚡ Improvements
- Improved performance for Z
- Memory optimization

## 📦 Installation
1. Download the archive for your operating system
2. Extract the archive
3. Launch KaSeController

## 🔗 Links
- [Full Changelog](https://github.com/mornepousse/KaSe_SOFT/blob/master/CHANGELOG.md)
- [Documentation](https://github.com/mornepousse/KaSe_SOFT/blob/master/README.md)
```

