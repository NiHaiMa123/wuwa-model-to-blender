# Extraction design

Planned extraction flow:

1. Resolve local config and game profile.
2. Discover PAK/IoStore files.
3. Resolve AES and mappings through providers.
4. Initialize CUE4Parse.
5. Search/resolve an explicit `USkeletalMesh` object path.
6. Walk only the required dependency graph.
7. Export UEFormat, textures and material metadata while preserving UE paths.
8. Emit a versioned manifest and warnings.

Do not extract the entire game as a prerequisite for one character.
