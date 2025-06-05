
Default state entities
- [ ] instantiated at build time
- [ ] one for each entity prototype
- [ ] immutable
- [ ] used to initialize EntityExtensionData before they get associated with a real entity, specifically when creating new default entities (so not through deserialization)
- [ ] used to reset entities when returned to the pool

Entity lifetime
- [x] OnInstantiate
- [x] AssociateWithEntityData (calls ApplyEntityData by default)
- [x] ApplyEntityData (after Import, potentially multiple times)
- [x] DisassociateFromEntityDataAndReset
- [ ] OnDestroyEntity
Or, separate lifetime:
- [x] OnDestroyUnusedPreInstantiatedEntity
  - [x] Remove, probably

EntityData lifetime
- [x] WannaBeConstructor
- [x] One of
  - [x] InitFromDefault (not game state safe, however can init the EntityData structure)
  - [x] InitFromPreInstantiated
  - [x] Deserialize (game state safe)
- [x] OnEntityDataCreated (game state safe)
- [x] Import (potentially multiple times, game state safe)
- [x] OnAssociatedWithEntity (raised after AssociateWithEntityData)
- [x] OnDisassociateFromEntity (for latency entity destroying)
- [x] OnEntityDataDestroyed (game state safe)
- [x] WannaBeDestructor

EntityExtension lifetime
- [x] OnInstantiate
  - [x] Also for pre instantiated ones
- [x] AssociateWithExtensionData (calls ApplyExtensionData by default)
- [x] ApplyExtensionData (potentially multiple times)
- [x] DisassociateFromExtensionDataAndReset
- [ ] OnDestroyExtension
Or, separate lifetime:
- [x] OnDestroyUnusedPreInstantiatedExtension
  - [x] Remove, probably

EntityExtensionData lifetime
- [x] WannaBeConstructor
- [x] One of
  - [x] InitFromDefault (not game state safe, however can init the EntityData structure)
  - [x] InitFromPreInstantiated
  - [x] Deserialize (game state safe)
- [x] OnExtensionDataCreated (game state safe)
- [x] Import (potentially multiple times)
- [x] OnAssociatedWithExtension (raised after AssociateWithExtensionData)
- [x] OnDisassociateFromExtension (for latency entity destroying)
- [x] OnEntityExtensionDataDestroyed (game state safe)
- [x] WannaBeDestructor
