
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
  - [x] InitFromDefault
  - [x] InitFromPreInstantiated
  - [x] Deserialize
- [x] Import (potentially multiple times)
- [x] OnAssociatedWithEntity (raised after AssociateWithEntityData)
- [x] OnDisassociateFromEntity (for latency entity destroying)
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
  - [x] InitFromDefault
  - [x] InitFromPreInstantiated
  - [x] Deserialize
- [x] Import (potentially multiple times)
- [x] OnAssociatedWithExtension (raised after AssociateWithExtensionData)
- [x] OnDisassociateFromExtension (for latency entity destroying)
- [x] WannaBeDestructor
