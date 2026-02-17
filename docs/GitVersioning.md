# Git-Based Automatic Versioning for .NET Projects

This document describes a GitHub Actions workflow pattern for automatic semantic versioning based on git tags, designed for .NET projects but adaptable to other ecosystems.

## How It Works

### Version Generation Logic

1. **Tagged Releases** (e.g., push tag `v0.3.0`):
   - Uses the tag name directly: `0.3.0`
   - Marks as release build (`is_release=true`)
   - Creates GitHub releases with packages

2. **Development Builds** (commits after a tag):
   - Auto-increments patch version from last tag
   - Example: if last tag was `v0.3.0` and there are commits after it:
     - Git describe output: `0.3.0-1-gcc93bf9`
     - Generated version: `0.3.1`
   - Marks as development build (`is_release=false`)

### Version Examples

| Git State | Git Describe Output | Generated Version | Build Type |
|-----------|-------------------|------------------|------------|
| Tag `v0.3.0` exactly | `0.3.0` | `0.3.0` | Release |
| 1 commit after `v0.3.0` | `0.3.0-1-gcc93bf9` | `0.3.1` | Development |
| 5 commits after `v0.3.0` | `0.3.0-5-gab1234f` | `0.3.1` | Development |
| Tag `v0.4.0` exactly | `0.4.0` | `0.4.0` | Release |
| 2 commits after `v0.4.0` | `0.4.0-2-gdef567a` | `0.4.1` | Development |

## Benefits

1. **Predictable Versioning**: Developers know the next version will be `X.Y.(Z+1)`
2. **No Version Conflicts**: Each commit gets the same dev version until tagged
3. **Clean Release Process**: Just tag when ready, workflow handles the rest
4. **SemVer Compliance**: Uses standard semantic versioning
5. **No Manual Version Management**: No need to edit version files

## Workflow Implementation

### PowerShell Version Logic

```powershell
- name: Extract version information
  id: version
  run: |
    if ("${{ github.ref_type }}" -eq "tag") {
      # For tagged releases, use the tag name (remove 'v' prefix if present)
      $version = "${{ github.ref_name }}" -replace '^v', ''
      echo "version=$version" >> $env:GITHUB_OUTPUT
      echo "is_release=true" >> $env:GITHUB_OUTPUT
    } else {
      # For non-tagged builds, auto-increment patch version from last tag
      $gitVersion = git describe --tags --always --dirty
      $cleanVersion = $gitVersion -replace '^v', ''

      # If it has git commit info (e.g., "0.3.0-1-gcc93bf9"), auto-increment patch version
      if ($cleanVersion -match '^(\d+)\.(\d+)\.(\d+)-(\d+)-g([a-f0-9]+)(.*)$') {
        $major = $matches[1]
        $minor = $matches[2]
        $patch = [int]$matches[3] + 1  # Auto-increment patch version
        $version = "$major.$minor.$patch"
      } else {
        # Exact tag match or no tags - use as-is
        $version = $cleanVersion
      }
      echo "version=$version" >> $env:GITHUB_OUTPUT
      echo "is_release=false" >> $env:GITHUB_OUTPUT
    }
    echo "Detected version: $version"
```

### Using the Version

```yaml
- name: Build
  run: |
    dotnet build --no-restore --configuration Release `
      -p:Version="${{ steps.version.outputs.version }}" `
      -p:AssemblyVersion="${{ steps.version.outputs.version }}" `
      -p:FileVersion="${{ steps.version.outputs.version }}" `
      -p:InformationalVersion="${{ steps.version.outputs.version }}"

- name: Create Release
  if: steps.version.outputs.is_release == 'true'
  uses: softprops/action-gh-release@v1
  # ... release configuration
```

## Git Tag Requirements

1. **Tag Format**: Use `v` prefix (e.g., `v1.0.0`, `v0.3.0`)
2. **Semantic Versioning**: Tags should follow SemVer (`MAJOR.MINOR.PATCH`)
3. **Annotated Tags Recommended**: `git tag -a v1.0.0 -m "Release 1.0.0"`

## Developer Workflow

### Creating a Release

```bash
# 1. Ensure all changes are committed and pushed
git push origin main

# 2. Create and push an annotated tag
git tag -a v0.4.0 -m "Release 0.4.0"
git push origin v0.4.0

# 3. GitHub Actions will automatically:
#    - Build with version 0.4.0
#    - Create GitHub release
#    - Upload packages
```

### Development Workflow

```bash
# Normal development - just commit and push
git commit -m "Fix message pump efficiency"
git push origin main

# GitHub Actions will automatically build with version 0.3.1 (if last tag was v0.3.0)
# No manual version management needed
```

## Adaptations for Other Languages

### For Node.js/npm
Replace .NET build commands with:
```bash
npm version ${{ steps.version.outputs.version }} --no-git-tag-version
npm run build
```

### For Python
Update `setup.py` or `pyproject.toml`:
```bash
# For setup.py projects
python setup.py sdist bdist_wheel --version=${{ steps.version.outputs.version }}

# For poetry projects
poetry version ${{ steps.version.outputs.version }}
poetry build
```

### For Go
Use build-time variables:
```bash
go build -ldflags "-X main.version=${{ steps.version.outputs.version }}"
```

## Advantages over Alternatives

| Approach | Pros | Cons |
|----------|------|------|
| **Manual Version Files** | Simple | Requires manual updates, merge conflicts |
| **Calendar Versioning** | Predictable | Not semantic, harder to understand changes |
| **Commit-Count Versioning** | Automatic | Versions don't convey meaning |
| **This Git-Tag Approach** | ✅ Automatic<br>✅ Semantic<br>✅ Predictable<br>✅ Clean | Requires git tag discipline |

## Best Practices

1. **Tag Discipline**: Only tag when ready for a release
2. **Commit Messages**: Use conventional commits for clarity
3. **Branch Protection**: Protect main branch to ensure clean history
4. **Release Notes**: Use GitHub releases to document changes
5. **Version Planning**: Plan major/minor bumps for significant changes

## Troubleshooting

### No Tags in Repository
If `git describe` fails (no tags exist), it will fall back to showing just the commit hash. Consider creating an initial tag:
```bash
git tag -a v0.1.0 -m "Initial version"
git push origin v0.1.0
```

### Dirty Working Tree
The `--dirty` flag appends `-dirty` if there are uncommitted changes. This should not happen in CI but helps during local testing.

### Version Conflicts
Since all commits after a tag get the same incremented version (e.g., `0.3.1`), there are no version conflicts between development builds.