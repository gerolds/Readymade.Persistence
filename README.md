# Readymade.Persistence
A simple and very generic system for game state persistence. Uses a pull-architecture where the runtime state is created on demand when a save event is triggered. This allows distributed AND centralized state serialization. Built on top of a JSON serialized KeyValueStore and a very capable system for restoring static and dynamic objects.

## Orientation

Primary entrypoints:

- Implementation of an actual save system: [PackSystem](/Runtime/Components/PackSystem.cs)
- The backing database: [PackDB](/Runtime/PackDB.cs)
- The interface used by all packable components: [IPackableComponent](/Runtime/IPackableComponent.cs)
- An abstract implementation of that packable interface: [PackableComponent](/Runtime/Components/PackableComponent.cs)
- The component at the root of all packable objects: [PackIdentity](/Runtime/Components/PackIdentity.cs)
