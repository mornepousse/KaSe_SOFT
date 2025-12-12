# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Upcoming
- macOS support
- Customizable themes

## [0.2.2-alpha] - 2025-12-09

### Added
- Integrated automatic update system using Octokit
- Multi-layout support: QWERTY, AZERTY, QWERTZ
- CDC Terminal to display keyboard logs in real-time
- Layout selector in the main interface
- Automatic display refresh when layout changes
- Publish profiles for Linux and Windows
- Complete documentation in README

### Changed
- Improved KeyConverter with conversion tables per layout
- Refactored settings management code
- Optimized SerialPortManager

### Fixed
- Startup freeze issue (SettingsManager.LoadAsync)
- Key conversion bug (Q↔A swap in layouts)
- AVLN compilation errors in MainWindow.axaml
- Executable generation with Rider

## [0.2.1] - 2025-11-XX

### Added
- Complete configuration import/export
- Individual layer import/export
- Layer name management

### Fixed
- Serial connection stability
- Layout name validation

## [0.2.0] - 2025-11-XX

### Added
- Avalonia graphical interface
- Keyboard layout visualization
- Keymap and layer editing
- CDC ACM communication with keyboard
- Macro management

### Technical
- Migration to .NET 10
- Native Linux support
- Windows support

## [0.1.0] - 2025-XX-XX

### Added
- Initial project version
- Basic features

---

## Legend of Change Types

- **Added** : for new features
- **Changed** : for changes in existing functionality
- **Deprecated** : for soon-to-be removed features
- **Removed** : for removed features
- **Fixed** : for bug fixes
- **Security** : in case of vulnerabilities

