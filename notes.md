
- [ ] Add entity instance pooling instead of instantiating and destroying every time
- [ ] Preferably add staggered entity instantiation, limited to x entities per frame
- [ ] Add dependency on player data, specifically to use persistent ids
- [x] ~~Desynced transform state should periodically fetch the current position and rotation and save that in the entity system's game state~~ nope, since exports can actually include non game state safe data there's no longer a need for this information
- [ ] exporting entities with desynced transforms should include their current transform values
- [x] non export serialization desynced entities should not include transform values
- [ ] add flag to mark an entity as static
- [ ] maybe change the no transform sync flag to a counter. Though when there are multiple systems trying to take control of syncing of an entity's transform, I don't foresee that going well currently so this is low priority if it even makes sense at all
